using System.Reflection;
using System.Security.Cryptography;
using AURA.Data;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using AURA.Services;
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

            using var firstLease = await first.EnterAsync(CancellationToken.None);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var secondAttempt = second.EnterAsync(cancellation.Token).AsTask();
            await Task.Delay(300);
            Assert.False(secondAttempt.IsCompleted);
            firstLease.Dispose();
            using var secondLease = await secondAttempt.WaitAsync(TimeSpan.FromSeconds(4));
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
            using var firstLease = await first.EnterAsync(CancellationToken.None);
            using var canceled = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                second.EnterAsync(canceled.Token).AsTask());
            firstLease.Dispose();
            using var recoveredLease = await second.EnterAsync(CancellationToken.None)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(4));
        });
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
    public async Task Synthetic_database_backup_restores_pending_job_at_backup_point()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var id = await SeedAsync(options, root);
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
                    await db.ReimbursementRequests.Where(x => x.Id == id)
                        .ExecuteUpdateAsync(update => update.SetProperty(x => x.ProcessingLeaseUntil,
                            DateTime.UtcNow.AddSeconds(-1)));
                }
                await ProcessOnceAsync(options, root, new StubExtractor(() => new ReceiptExtractionDto()));
            }
            finally
            {
                release.TrySetResult(true);
            }

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => first);
            await AssertStateAsync(options, id, ProcessingStates.Completed, 2, null);
            await using var finalDb = new AppDbContext(options);
            Assert.Equal(1, await finalDb.AuditLogs.CountAsync(a => a.RequestId == id));
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

    private static async Task ProcessOnceAsync(DbContextOptions<AppDbContext> options,
        string root, IVisionExtractor extractor)
    {
        using var services = new ServiceCollection()
            .AddScoped(_ => new AppDbContext(options))
            .AddScoped(_ => new VisionExecutionContext())
            .AddScoped(_ => extractor)
            .BuildServiceProvider();
        var worker = new ReceiptProcessingWorker(
            services.GetRequiredService<IServiceScopeFactory>(), new ReceiptProcessingQueue(),
            Microsoft.Extensions.Options.Options.Create(new ReceiptProcessingOptions { MaxRateLimitAttempts = 3 }),
            Microsoft.Extensions.Options.Options.Create(new ReceiptStorageOptions { Directory = "receipts" }),
            new TestEnvironment(root), NullLogger<ReceiptProcessingWorker>.Instance);
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
