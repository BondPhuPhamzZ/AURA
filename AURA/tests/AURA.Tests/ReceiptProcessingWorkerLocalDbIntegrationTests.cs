using System.Reflection;
using System.Security.Cryptography;
using AURA.Data;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using AURA.Services;
using AURA.Tests.Fakes;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptProcessingWorkerLocalDbIntegrationTests
{
    [Fact]
    public async Task Database_utc_clock_reads_authoritative_sql_server_time()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, _) =>
        {
            await using var database = new AppDbContext(options);
            var before = DateTime.UtcNow.AddSeconds(-2);
            var databaseNow = await DatabaseUtcClock.ReadAsync(database, CancellationToken.None);
            var after = DateTime.UtcNow.AddSeconds(2);
            Assert.InRange(databaseNow, before, after);
        });
    }

    [Fact]
    public async Task Sql_ai_gate_serializes_two_independent_coordinators()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, _) =>
        {
            await using var db = new AppDbContext(options);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = db.Database.GetConnectionString()
                }).Build();
            var visionOptions = Microsoft.Extensions.Options.Options.Create(new VisionOptions
            {
                MaxConcurrentRequests = 1,
                UseSqlDistributedGate = true
            });
            var first = new VisionInvocationCoordinator(visionOptions, configuration);
            var second = new VisionInvocationCoordinator(visionOptions, configuration);

            await using var firstLease = await first.EnterAsync(CancellationToken.None);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var secondAttempt = second.EnterAsync(cancellation.Token).AsTask();
            await Task.Delay(300);
            Assert.False(secondAttempt.IsCompleted);
            await firstLease.DisposeAsync();
            await using var secondLease = await secondAttempt.WaitAsync(TimeSpan.FromSeconds(4));
        });
    }

    [Fact]
    public async Task Queue_snapshot_reports_aggregate_counts_age_and_recorded_bytes()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var pendingId = await SeedAsync(options, root);
            var processingId = await SeedAsync(options, root);
            var failedId = await SeedAsync(options, root);
            var now = DateTime.UtcNow;
            await using var db = new AppDbContext(options);
            await db.ReimbursementRequests.Where(x => x.Id == pendingId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.QueuedAt, now.AddMinutes(-7)));
            await db.ReimbursementRequests.Where(x => x.Id == processingId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.ProcessingState,
                    ProcessingStates.Processing));
            await db.ReimbursementRequests.Where(x => x.Id == failedId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.ProcessingState,
                    ProcessingStates.Failed));

            var snapshot = await ReceiptQueueSnapshot.ReadAsync(db, now, CancellationToken.None);

            Assert.Equal(1, snapshot.PendingCount);
            Assert.Equal(1, snapshot.ProcessingCount);
            Assert.Equal(1, snapshot.FailedCount);
            Assert.InRange(snapshot.OldestPendingAgeSeconds!.Value, 419, 421);
            Assert.Equal(9, snapshot.RecordedReceiptBytes);
        });
    }

    [Fact]
    public async Task Shared_intake_capacity_is_enforced_across_independent_SQL_repositories()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, _) =>
        {
            await using var firstDb = new AppDbContext(options);
            await using var secondDb = new AppDbContext(options);
            var firstRepository = new ReimbursementRepository(firstDb);
            var secondRepository = new ReimbursementRepository(secondDb);

            var results = await Task.WhenAll(
                firstRepository.TryAddRequestWithAuditAsync(CreateCapacityRequest("capacity-a"),
                    "AI_QUEUED", "synthetic", 1, CancellationToken.None),
                secondRepository.TryAddRequestWithAuditAsync(CreateCapacityRequest("capacity-b"),
                    "AI_QUEUED", "synthetic", 1, CancellationToken.None));

            Assert.Single(results, result => result == ReceiptIntakePersistenceResult.Added);
            Assert.Single(results, result => result == ReceiptIntakePersistenceResult.CapacityReached);
            await using var verifyDb = new AppDbContext(options);
            Assert.Equal(1, await verifyDb.ReimbursementRequests.CountAsync(request =>
                request.ProcessingState == ProcessingStates.Pending));
            Assert.Single(await verifyDb.AuditLogs.ToListAsync());
        });
    }

    [Fact]
    public async Task Second_host_can_process_receipt_from_absolute_private_shared_directory()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var id = await SeedAsync(options, root);
            var secondHostRoot = Path.Combine(root, "host-b");
            var sharedDirectory = Path.Combine(root, "private-shared");
            Directory.CreateDirectory(secondHostRoot);
            Directory.CreateDirectory(sharedDirectory);
            var sharedFile = Path.Combine(sharedDirectory, "synthetic.jpg");
            File.Copy(Path.Combine(root, "receipts", "synthetic.jpg"), sharedFile);

            await ProcessOnceAsync(options, secondHostRoot,
                new PathAssertingExtractor(sharedFile), sharedDirectory);

            await AssertStateAsync(options, id, ProcessingStates.Completed, 1, null);
        });
    }

    [Fact]
    public async Task Intake_on_one_host_is_processed_by_another_host_using_shared_object_storage()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            byte[] png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0];
            var sharedStorageClient = new FakeGoogleStorageClient();
            var hostAStore = new GoogleCloudReceiptFileStore(sharedStorageClient,
                "integration-test-bucket", "private/receipts", 1024);
            var hostBStore = new GoogleCloudReceiptFileStore(sharedStorageClient,
                "integration-test-bucket", "private/receipts", 1024);
            const string sourceKey = "gcs-integration:folder:file:version-1";
            ReimbursementRequest accepted;

            await using (var database = new AppDbContext(options))
            {
                var intake = new ReceiptIntakeService(hostAStore, new ReimbursementRepository(database),
                    Microsoft.Extensions.Options.Options.Create(new ReceiptStorageOptions
                    {
                        Provider = "GoogleCloudStorage",
                        MaxFileSizeMb = 1
                    }),
                    Microsoft.Extensions.Options.Options.Create(new DecisionPolicyOptions()),
                    Microsoft.Extensions.Options.Options.Create(new ReceiptProcessingOptions()),
                    new ReceiptProcessingQueue());

                accepted = await intake.AcceptAsync(new ReceiptIntakeInput(png, "synthetic.png",
                    "image/png", 0, "SYNTHETIC", "Synthetic test", "Test", sourceKey));
            }

            Assert.Equal(Convert.ToHexString(SHA256.HashData(png)), accepted.FileSha256);
            Assert.Equal((long)png.Length, accepted.FileSizeBytes);
            Assert.Equal("private/receipts/" + accepted.StoredFileName, sharedStorageClient.LastObjectName);

            await ProcessOnceAsync(options, Path.Combine(root, "host-b"),
                new StubExtractor(() => new ReceiptExtractionDto()), fileStore: hostBStore);

            await AssertStateAsync(options, accepted.Id, ProcessingStates.Completed, 1, null);
            Assert.True(sharedStorageClient.DownloadCalled);
            Assert.True(sharedStorageClient.LastDownloadWasGenerationPinned);
        });
    }

    [Fact]
    public async Task Sql_ai_gate_cancellation_does_not_leave_a_waiting_permit()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, _) =>
        {
            await using var db = new AppDbContext(options);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = db.Database.GetConnectionString()
                }).Build();
            var visionOptions = Microsoft.Extensions.Options.Options.Create(new VisionOptions
            {
                MaxConcurrentRequests = 1,
                UseSqlDistributedGate = true
            });
            var first = new VisionInvocationCoordinator(visionOptions, configuration);
            var second = new VisionInvocationCoordinator(visionOptions, configuration);
            await using var firstLease = await first.EnterAsync(CancellationToken.None);
            using var canceled = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                second.EnterAsync(canceled.Token).AsTask());
            await firstLease.DisposeAsync();
            await using var recoveredLease = await second.EnterAsync(CancellationToken.None)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(4));
        });
    }

    [Fact]
    public async Task Sql_ai_gate_signals_loss_when_owning_session_is_killed()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, _) =>
        {
            await using var db = new AppDbContext(options);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = db.Database.GetConnectionString()
                }).Build();
            var settings = Microsoft.Extensions.Options.Options.Create(new VisionOptions
            {
                MaxConcurrentRequests = 1, UseSqlDistributedGate = true
            });
            var coordinator = new VisionInvocationCoordinator(settings, configuration);
            var successorCoordinator = new VisionInvocationCoordinator(settings, configuration);
            var lease = await coordinator.EnterAsync(CancellationToken.None);
            Assert.True(lease.FencingToken > 0);
            var sessionId = lease.SqlSessionId;
            Assert.NotNull(sessionId);

            await using (var admin = new SqlConnection(db.Database.GetConnectionString()))
            {
                await admin.OpenAsync();
                await using var kill = admin.CreateCommand();
                kill.CommandText = $"KILL {sessionId.Value};";
                await kill.ExecuteNonQueryAsync();
            }

            try
            {
                var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = lease.LostToken.Register(() => lost.TrySetResult());
                await lost.Task.WaitAsync(TimeSpan.FromSeconds(12));
                var successor = await successorCoordinator.EnterAsync(CancellationToken.None);
                try
                {
                    Assert.True(successor.FencingToken > lease.FencingToken);
                    await successor.AssertOwnershipAsync(CancellationToken.None);
                    await Assert.ThrowsAsync<VisionExtractionException>(() =>
                        lease.AssertOwnershipAsync(CancellationToken.None));
                }
                finally
                {
                    await successor.DisposeAsync();
                }
            }
            finally
            {
                await Assert.ThrowsAsync<VisionExtractionException>(() => lease.DisposeAsync().AsTask());
            }
        });
    }

    [Fact]
    public async Task Sql_ai_gate_uses_database_clock_for_shared_provider_cooldown()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, _) =>
        {
            await using var db = new AppDbContext(options);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = db.Database.GetConnectionString()
                }).Build();
            var settings = Microsoft.Extensions.Options.Options.Create(new VisionOptions
            {
                MaxConcurrentRequests = 1, UseSqlDistributedGate = true
            });
            var first = new VisionInvocationCoordinator(settings, configuration);
            var second = new VisionInvocationCoordinator(settings, configuration);
            // Deliberately model hosts whose clocks differ by years. Shared SQL cooldown
            // must use the database clock and must not inherit either caller's timestamp.
            await first.RecordCooldownAsync("OpenRouter", DateTime.MinValue,
                TimeSpan.FromSeconds(120),
                CancellationToken.None);

            var remaining = await second.GetRemainingCooldownAsync("OpenRouter",
                DateTime.MaxValue, CancellationToken.None);
            Assert.InRange(remaining!.Value.TotalSeconds, 118, 120);

            // A future-skewed host must not extend the cooldown by using its own clock.
            await second.RecordCooldownAsync("OpenRouter", DateTime.MaxValue,
                TimeSpan.FromSeconds(20), CancellationToken.None);
            var notExtended = await first.GetRemainingCooldownAsync(
                "OpenRouter", DateTime.MinValue, CancellationToken.None);
            Assert.InRange(notExtended!.Value.TotalSeconds, 117, 120);

            await first.ClearCooldownAsync("OpenRouter", CancellationToken.None);
            Assert.Null(await second.GetRemainingCooldownAsync("OpenRouter", DateTime.UtcNow,
                CancellationToken.None));
        });
    }

    [Fact]
    public async Task Migration_chain_creates_shared_provider_cooldown_table()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        var name = "AuraMigrateIT_" + Guid.NewGuid().ToString("N");
        var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
        var exists = false;
        try
        {
            await using var database = new AppDbContext(options);
            await database.Database.MigrateAsync();
            exists = true;
            Assert.Empty(await database.Database.GetPendingMigrationsAsync());
            Assert.Contains("20261023181204_AddSharedVisionProviderCooldown",
                await database.Database.GetAppliedMigrationsAsync());
            Assert.Contains("20261023182852_AddVisionInvocationFence",
                await database.Database.GetAppliedMigrationsAsync());
            Assert.Equal(0L, (await database.VisionInvocationFences.SingleAsync(x => x.Id == 1)).Token);
            database.VisionProviderCooldowns.Add(new VisionProviderCooldown
            {
                Provider = "OpenRouter", CooldownUntilUtc = DateTime.UtcNow.AddMinutes(2)
            });
            await database.SaveChangesAsync();
            Assert.NotNull(await database.VisionProviderCooldowns.FindAsync("OpenRouter"));
        }
        finally
        {
            if (exists)
            {
                await using var cleanup = new AppDbContext(options);
                await cleanup.Database.EnsureDeletedAsync();
            }
        }
    }

    [Fact]
    public async Task Tampered_stored_bytes_fail_before_AI_is_called()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var id = await SeedAsync(options, root);
            await File.WriteAllBytesAsync(Path.Combine(root, "receipts", "synthetic.jpg"), [1, 2, 4]);
            await ProcessOnceAsync(options, root, new StubExtractor(() =>
                throw new InvalidOperationException("AI must not receive tampered bytes.")));
            await using var db = new AppDbContext(options);
            var request = await db.ReimbursementRequests.SingleAsync(x => x.Id == id);
            Assert.Equal(ProcessingStates.Failed, request.ProcessingState);
            Assert.Equal("ESCALATE_SYSTEM_ERROR", request.Status);
            Assert.Equal("RECEIPT_FILE_INTEGRITY", request.ProviderErrorCode);
            Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.RequestId == id));
        });
    }

    [Fact]
    public async Task Synthetic_database_and_receipt_backup_restore_consistent_pending_job_and_image()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var id = await SeedAsync(options, root);
            var liveReceiptPath = Path.Combine(root, "receipts", "synthetic.jpg");
            var receiptBackupDirectory = Path.Combine(root, "receipt-object-backup");
            Directory.CreateDirectory(receiptBackupDirectory);
            var receiptBackupPath = Path.Combine(receiptBackupDirectory, "synthetic.jpg");
            File.Copy(liveReceiptPath, receiptBackupPath);
            await using var source = new AppDbContext(options);
            source.AuditLogs.Add(new AuditLog
            {
                RequestId = id,
                Action = "SYNTHETIC_BACKUP_MARKER"
            });
            await source.SaveChangesAsync();
            var sourceName = source.Database.GetDbConnection().Database;
            Assert.Matches("^[A-Za-z0-9_]+$", sourceName);
            var restoredName = sourceName + "_Restored";
            var backupPath = Path.Combine(root, "synthetic-only.bak");
            var dataPath = Path.Combine(root, "restored.mdf");
            var logPath = Path.Combine(root, "restored_log.ldf");
            var masterConnection = new SqlConnectionStringBuilder(source.Database.GetConnectionString())
            {
                InitialCatalog = "master"
            }.ConnectionString;

            await using var master = new SqlConnection(masterConnection);
            await master.OpenAsync();
            try
            {
                await using (var backup = master.CreateCommand())
                {
                    backup.CommandText = $"BACKUP DATABASE [{sourceName}] TO DISK = @path " +
                        "WITH COPY_ONLY, INIT, CHECKSUM;";
                    backup.CommandTimeout = 60;
                    backup.Parameters.AddWithValue("@path", backupPath);
                    await backup.ExecuteNonQueryAsync();
                }

                string? dataLogical = null;
                string? logLogical = null;
                await using (var files = master.CreateCommand())
                {
                    files.CommandText = "RESTORE FILELISTONLY FROM DISK = @path;";
                    files.Parameters.AddWithValue("@path", backupPath);
                    await using var reader = await files.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var logical = reader.GetString(reader.GetOrdinal("LogicalName"));
                        var type = reader.GetString(reader.GetOrdinal("Type"));
                        if (type == "D") dataLogical = logical;
                        if (type == "L") logLogical = logical;
                    }
                }
                Assert.NotNull(dataLogical);
                Assert.NotNull(logLogical);

                await File.WriteAllBytesAsync(liveReceiptPath, [1, 2, 4]);
                await source.ReimbursementRequests.Where(x => x.Id == id)
                    .ExecuteUpdateAsync(update => update.SetProperty(x => x.ProcessingState,
                        ProcessingStates.Completed));

                await using (var restore = master.CreateCommand())
                {
                    restore.CommandText = $"RESTORE DATABASE [{restoredName}] FROM DISK = @backup " +
                        "WITH MOVE @dataLogical TO @dataPath, MOVE @logLogical TO @logPath, RECOVERY;";
                    restore.CommandTimeout = 60;
                    restore.Parameters.AddWithValue("@backup", backupPath);
                    restore.Parameters.AddWithValue("@dataLogical", dataLogical);
                    restore.Parameters.AddWithValue("@logLogical", logLogical);
                    restore.Parameters.AddWithValue("@dataPath", dataPath);
                    restore.Parameters.AddWithValue("@logPath", logPath);
                    await restore.ExecuteNonQueryAsync();
                }

                var restoredConnection = new SqlConnectionStringBuilder(masterConnection)
                {
                    InitialCatalog = restoredName
                }.ConnectionString;
                await using var restored = new AppDbContext(
                    new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(restoredConnection).Options);
                var restoredJob = await restored.ReimbursementRequests.SingleAsync(x => x.Id == id);
                Assert.Equal(ProcessingStates.Pending, restoredJob.ProcessingState);
                Assert.Equal(3, restoredJob.FileSizeBytes);
                var restoredReceiptPath = Path.Combine(root, "restored-receipts", restoredJob.StoredFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(restoredReceiptPath)!);
                File.Copy(receiptBackupPath, restoredReceiptPath);
                var restoredReceiptBytes = await File.ReadAllBytesAsync(restoredReceiptPath);
                Assert.Equal(restoredJob.FileSizeBytes, restoredReceiptBytes.LongLength);
                Assert.Equal(restoredJob.FileSha256,
                    Convert.ToHexString(SHA256.HashData(restoredReceiptBytes)));
                Assert.Equal(1, await restored.ReimbursementRequests.CountAsync());
                Assert.Equal(1, await restored.AuditLogs.CountAsync(x =>
                    x.RequestId == id && x.Action == "SYNTHETIC_BACKUP_MARKER"));
            }
            finally
            {
                await using var cleanup = master.CreateCommand();
                cleanup.CommandText = $"IF DB_ID(@name) IS NOT NULL BEGIN " +
                    $"ALTER DATABASE [{restoredName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                    $"DROP DATABASE [{restoredName}]; END";
                cleanup.Parameters.AddWithValue("@name", restoredName);
                await cleanup.ExecuteNonQueryAsync();
            }
        });
    }

    [Fact]
    public async Task Concurrent_duplicate_manager_approval_writes_one_state_change_and_audit()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var id = await SeedAsync(options, root);
            await using (var seed = new AppDbContext(options))
            {
                var request = await seed.ReimbursementRequests.SingleAsync(item => item.Id == id);
                request.Status = "ESCALATE_POLICY";
                request.IsForwardedToManager = true;
                request.ManagerQuestion = "Synthetic manager question";
                await seed.SaveChangesAsync();
            }

            await using var firstDatabase = new AppDbContext(options);
            await using var secondDatabase = new AppDbContext(options);
            var firstCopy = await firstDatabase.ReimbursementRequests.SingleAsync(item => item.Id == id);
            var secondCopy = await secondDatabase.ReimbursementRequests.SingleAsync(item => item.Id == id);
            var approval = EscalationWorkflow.Answer("ESCALATE_POLICY", true);
            firstCopy.Status = approval.Status;
            firstCopy.ManagerAnswer = true;
            firstCopy.ManagerDecisionAt = DateTime.UtcNow;
            secondCopy.Status = approval.Status;
            secondCopy.ManagerAnswer = true;
            secondCopy.ManagerDecisionAt = DateTime.UtcNow;

            static async Task<string> PersistApprovalAsync(AppDbContext database,
                ReimbursementRequest request)
            {
                try
                {
                    await new ReimbursementRepository(database).UpdateRequestWithAuditAsync(
                        request, "MANAGER_YES", "synthetic duplicate approval");
                    return "SAVED";
                }
                catch (DbUpdateConcurrencyException)
                {
                    return "CONFLICT";
                }
            }

            var results = await Task.WhenAll(
                PersistApprovalAsync(firstDatabase, firstCopy),
                PersistApprovalAsync(secondDatabase, secondCopy));

            Assert.Equal(1, results.Count(result => result == "SAVED"));
            Assert.Equal(1, results.Count(result => result == "CONFLICT"));
            await using var verify = new AppDbContext(options);
            var persisted = await verify.ReimbursementRequests.SingleAsync(item => item.Id == id);
            Assert.Equal(approval.Status, persisted.Status);
            Assert.True(persisted.ManagerAnswer);
            Assert.Equal(1, await verify.AuditLogs.CountAsync(item => item.RequestId == id &&
                item.Action == "MANAGER_YES"));
        });
    }

    [Fact]
    public async Task Rate_limited_job_survives_worker_restart_and_completes_once()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var id = await SeedAsync(options, root);
            await ProcessOnceAsync(options, root, new StubExtractor(() =>
                throw new VisionExtractionException("AI_RATE_LIMIT", "Transient 429",
                    retryAfter: TimeSpan.FromMilliseconds(50))));
            await AssertStateAsync(options, id, ProcessingStates.Pending, 1, "PENDING");

            // A new service provider and worker model an application restart.
            await ProcessOnceAsync(options, root, new StubExtractor(() => new ReceiptExtractionDto()));
            await AssertStateAsync(options, id, ProcessingStates.Completed, 2, null);

            await ProcessOnceAsync(options, root, new StubExtractor(() =>
                throw new InvalidOperationException("A completed job must not be processed twice.")));
            await using var db = new AppDbContext(options);
            Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.RequestId == id));
        });
    }

    [Fact]
    public async Task Repeated_rate_limit_ends_in_fail_safe_after_three_claims()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var id = await SeedAsync(options, root);
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                await ProcessOnceAsync(options, root, new StubExtractor(() =>
                    throw new VisionExtractionException("AI_RATE_LIMIT", "Transient 429",
                        retryAfter: TimeSpan.FromMilliseconds(50))));
                await AssertStateAsync(options, id,
                    attempt < 3 ? ProcessingStates.Pending : ProcessingStates.Failed,
                    attempt, attempt < 3 ? "PENDING" : "ESCALATE_SYSTEM_ERROR");
            }
            await using var db = new AppDbContext(options);
            Assert.Equal(3, await db.AuditLogs.CountAsync(a => a.RequestId == id));
        });
    }

    [Fact]
    public async Task Reclaimed_lease_prevents_stale_worker_result_and_audit()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var id = await SeedAsync(options, root);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = ProcessOnceAsync(options, root, new BlockingExtractor(entered, release));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            try
            {
                await using (var db = new AppDbContext(options))
                {
                    var databaseNow = await DatabaseUtcClock.ReadAsync(db, CancellationToken.None);
                    await db.ReimbursementRequests.Where(x => x.Id == id)
                        .ExecuteUpdateAsync(update => update.SetProperty(x => x.ProcessingLeaseUntil,
                            databaseNow.AddSeconds(-1)));
                }
                await ProcessOnceAsync(options, root, new StubExtractor(() => new ReceiptExtractionDto()));
            }
            finally
            {
                release.TrySetResult(true);
            }

            var lost = await Assert.ThrowsAsync<InvalidOperationException>(() => first);
            Assert.Contains("lease ownership was lost", lost.Message, StringComparison.Ordinal);
            await AssertStateAsync(options, id, ProcessingStates.Completed, 2, null);
            await using var finalDb = new AppDbContext(options);
            Assert.Equal(1, await finalDb.AuditLogs.CountAsync(a => a.RequestId == id));
        });
    }

    [Fact]
    public async Task Long_extraction_renews_its_lease_without_invalidating_terminal_write()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var id = await SeedAsync(options, root);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var processing = ProcessOnceAsync(options, root, new BlockingExtractor(entered, release),
                leaseSeconds: 30);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            DateTime? originalExpiry;
            await using (var db = new AppDbContext(options))
            {
                var databaseNow = await DatabaseUtcClock.ReadAsync(db, CancellationToken.None);
                originalExpiry = await db.ReimbursementRequests.Where(x => x.Id == id)
                    .Select(x => x.ProcessingLeaseUntil).SingleAsync();
                Assert.InRange((originalExpiry!.Value - databaseNow).TotalSeconds, 25, 30);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(12));
                await using var db = new AppDbContext(options);
                var current = await db.ReimbursementRequests.AsNoTracking().SingleAsync(x => x.Id == id);
                Assert.True(current.ProcessingLeaseUntil > originalExpiry);
                Assert.Equal(1, current.ProcessingAttemptCount);
                var databaseNow = await DatabaseUtcClock.ReadAsync(db, CancellationToken.None);
                Assert.True(current.ProcessingLeaseUntil > databaseNow);
            }
            finally
            {
                release.TrySetResult(true);
            }

            await processing.WaitAsync(TimeSpan.FromSeconds(10));
            await AssertStateAsync(options, id, ProcessingStates.Completed, 1, null);
            await using var finalDb = new AppDbContext(options);
            Assert.Equal(1, await finalDb.AuditLogs.CountAsync(a => a.RequestId == id));
        });
    }

    [Fact]
    public async Task Lease_heartbeat_cancels_extraction_after_ownership_changes()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var id = await SeedAsync(options, root);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var neverRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var processing = ProcessOnceAsync(options, root,
                new BlockingExtractor(entered, neverRelease), leaseSeconds: 30);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            await using (var db = new AppDbContext(options))
                await db.ReimbursementRequests.Where(x => x.Id == id)
                    .ExecuteUpdateAsync(update => update.SetProperty(x => x.ProcessingAttemptCount,
                        x => x.ProcessingAttemptCount + 1));

            var lost = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                processing.WaitAsync(TimeSpan.FromSeconds(20)));
            Assert.Contains("lease ownership was lost", lost.Message, StringComparison.Ordinal);
            await using var finalDb = new AppDbContext(options);
            Assert.Equal(2, await finalDb.ReimbursementRequests.Where(x => x.Id == id)
                .Select(x => x.ProcessingAttemptCount).SingleAsync());
            Assert.Empty(await finalDb.AuditLogs.Where(x => x.RequestId == id).ToListAsync());
        });
    }

    private static async Task WithIsolatedDatabase(
        Func<DbContextOptions<AppDbContext>, string, Task> test)
    {
        var name = "AuraWorkerRetryIT_" + Guid.NewGuid().ToString("N");
        var root = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(Path.Combine(root, "receipts"));
        var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
        var created = false;
        try
        {
            await using (var db = new AppDbContext(options))
                created = await db.Database.EnsureCreatedAsync();
            Assert.True(created);
            await test(options, root);
        }
        finally
        {
            if (created)
            {
                await using var db = new AppDbContext(options);
                await db.Database.EnsureDeletedAsync();
            }
            if (root.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(root) == name && Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<string> SeedAsync(DbContextOptions<AppDbContext> options, string root)
    {
        const string storedFileName = "synthetic.jpg";
        byte[] bytes = [1, 2, 3];
        await File.WriteAllBytesAsync(Path.Combine(root, "receipts", storedFileName), bytes);
        var item = new ReimbursementRequest
        {
            Id = Guid.NewGuid().ToString("N"),
            OriginalFileName = storedFileName,
            StoredFileName = storedFileName,
            ImageUrl = "private",
            ContentType = "image/jpeg",
            FileSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            FileSizeBytes = bytes.Length,
            Status = ProcessingStates.Pending,
            ProcessingState = ProcessingStates.Pending
        };
        await using var db = new AppDbContext(options);
        db.ReimbursementRequests.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private static ReimbursementRequest CreateCapacityRequest(string id) => new()
    {
        Id = id,
        OriginalFileName = id + ".jpg",
        StoredFileName = id + ".jpg",
        ImageUrl = "private",
        ContentType = "image/jpeg",
        FileSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id))),
        FileSizeBytes = 1,
        Status = ProcessingStates.Pending,
        ProcessingState = ProcessingStates.Pending,
        QueuedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };

    private static async Task ProcessOnceAsync(DbContextOptions<AppDbContext> options,
        string root, IVisionExtractor extractor, string storageDirectory = "receipts",
        int leaseSeconds = 600, IReceiptFileStore? fileStore = null)
    {
        using var services = new ServiceCollection()
            .AddScoped(_ => new AppDbContext(options))
            .AddScoped(_ => new VisionExecutionContext())
            .AddScoped(_ => extractor)
            .AddSingleton<IReceiptFileStore>(_ => fileStore ?? new LocalReceiptFileStore(
                new TestEnvironment(root), Microsoft.Extensions.Options.Options.Create(
                    new ReceiptStorageOptions { Directory = storageDirectory })))
            .BuildServiceProvider();
        var worker = new ReceiptProcessingWorker(
            services.GetRequiredService<IServiceScopeFactory>(), new ReceiptProcessingQueue(),
            Microsoft.Extensions.Options.Options.Create(new ReceiptProcessingOptions
            {
                MaxRateLimitAttempts = 3, LeaseSeconds = leaseSeconds
            }),
            Microsoft.Extensions.Options.Options.Create(new ReceiptStorageOptions
            {
                Directory = storageDirectory
            }),
            services.GetRequiredService<AURA.Interfaces.IReceiptFileStore>(), NullLogger<ReceiptProcessingWorker>.Instance);
        var method = typeof(ReceiptProcessingWorker).GetMethod("ProcessNextAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task)method.Invoke(worker, [CancellationToken.None])!;
        await task;
    }

    private static async Task AssertStateAsync(DbContextOptions<AppDbContext> options,
        string id, string state, int attempts, string? status)
    {
        await using var db = new AppDbContext(options);
        var item = await db.ReimbursementRequests.SingleAsync(x => x.Id == id);
        Assert.Equal(state, item.ProcessingState);
        Assert.Equal(attempts, item.ProcessingAttemptCount);
        if (status is not null) Assert.Equal(status, item.Status);
        Assert.Null(item.ProcessingLeaseUntil);
        Assert.Equal(state != ProcessingStates.Pending, item.ProcessingCompletedAt.HasValue);
    }

    private sealed class StubExtractor(Func<ReceiptExtractionDto> response) : IVisionExtractor
    {
        public Task<ReceiptExtractionDto> ExtractFactsAsync(string physicalImagePath,
            CancellationToken cancellationToken = default) => Task.FromResult(response());
        public Task<ReceiptExtractionDto> ExtractFactsAsync(ReadOnlyMemory<byte> imageBytes, string imageFileName,
            CancellationToken cancellationToken = default) => Task.FromResult(response());
    }

    private sealed class PathAssertingExtractor(string expectedPath) : IVisionExtractor
    {
        public Task<ReceiptExtractionDto> ExtractFactsAsync(string physicalImagePath,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(Path.GetFullPath(expectedPath), Path.GetFullPath(physicalImagePath));
            return Task.FromResult(new ReceiptExtractionDto());
        }
        public Task<ReceiptExtractionDto> ExtractFactsAsync(ReadOnlyMemory<byte> imageBytes, string imageFileName,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(Path.GetFileName(expectedPath), imageFileName);
            Assert.Equal(File.ReadAllBytes(expectedPath), imageBytes.ToArray());
            return Task.FromResult(new ReceiptExtractionDto());
        }
    }

    private sealed class BlockingExtractor(TaskCompletionSource<bool> entered,
        TaskCompletionSource<bool> release) : IVisionExtractor
    {
        public async Task<ReceiptExtractionDto> ExtractFactsAsync(string physicalImagePath,
            CancellationToken cancellationToken = default)
        {
            entered.SetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return new ReceiptExtractionDto();
        }
        public async Task<ReceiptExtractionDto> ExtractFactsAsync(ReadOnlyMemory<byte> imageBytes,
            string imageFileName, CancellationToken cancellationToken = default)
        {
            entered.SetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            return new ReceiptExtractionDto();
        }
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AURA.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
