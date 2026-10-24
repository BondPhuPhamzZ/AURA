using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AURA.Data;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using AURA.Services;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Xunit;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace AURA.Tests;

public sealed class DriveSandboxIntakeTests
{
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0];

    [Theory]
    [InlineData("receipt.png", "image/jpeg")]
    [InlineData("receipt.pdf", "application/pdf")]
    [InlineData("../receipt.exe", "image/png")]
    public void File_gate_rejects_extension_and_mime_mismatch(string fileName, string mime) =>
        Assert.NotNull(ReceiptAttachmentPolicy.Validate(fileName, mime, Png, 5));

    [Fact]
    public void File_gate_rejects_spoofed_png_bytes() =>
        Assert.NotNull(ReceiptAttachmentPolicy.Validate("receipt.png", "image/png", [1, 2, 3], 5));

    [Fact]
    public void File_gate_rejects_oversized_attachment() =>
        Assert.NotNull(ReceiptAttachmentPolicy.Validate("receipt.png", "image/png",
            new byte[5 * 1024 * 1024 + 1], 5));

    [Fact]
    public async Task Same_Drive_file_version_creates_only_one_SQL_job_and_private_file()
    {
        var root = NewTestRoot();
        try
        {
            var repository = new FakeRepository();
            var service = CreateIntake(root, repository);
            var input = new ReceiptIntakeInput(Png, "receipt.png", "image/png", 0,
                "DRIVE-SANDBOX", "Synthetic", "Test", "drive:folder:file:1");
            var first = await service.AcceptAsync(input);
            var second = await service.AcceptAsync(input);

            Assert.Equal(first.Id, second.Id);
            Assert.Single(repository.Requests);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "receipts")));
            Assert.Contains("Source=DRIVE_SANDBOX", repository.LastAudit);
            Assert.DoesNotContain("File=", repository.LastAudit);
            Assert.DoesNotContain("Submitter=", repository.LastAudit);
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Same_manual_idempotency_key_returns_existing_job_and_file()
    {
        var root = NewTestRoot();
        try
        {
            var repository = new FakeRepository();
            var service = CreateIntake(root, repository);
            var sourceKey = ReceiptIntakeService.CreateManualIdempotencySourceKey(
                "2b4d3b55-32d8-486c-a214-c7bf077bf981");
            var input = new ReceiptIntakeInput(Png, "receipt.png", "image/png", 0,
                "TEST", "Synthetic", "Finance", sourceKey);

            var first = await service.AcceptAsync(input);
            var retried = await service.AcceptAsync(input);

            Assert.Equal(first.Id, retried.Id);
            Assert.Single(repository.Requests);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "receipts")));
            Assert.Contains("Source=MANUAL_IDEMPOTENCY", repository.LastAudit);
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Manual_idempotency_key_cannot_be_reused_for_different_image_bytes()
    {
        var root = NewTestRoot();
        try
        {
            var repository = new FakeRepository();
            var service = CreateIntake(root, repository);
            var sourceKey = ReceiptIntakeService.CreateManualIdempotencySourceKey(
                "2b4d3b55-32d8-486c-a214-c7bf077bf981");
            var input = new ReceiptIntakeInput(Png, "receipt.png", "image/png", 0,
                "TEST", "Synthetic", "Finance", sourceKey);
            await service.AcceptAsync(input);

            await Assert.ThrowsAsync<ReceiptIntakeException>(() => service.AcceptAsync(input with
                { Bytes = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2] }));

            Assert.Single(repository.Requests);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "receipts")));
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Concurrent_manual_retry_resolves_to_one_durable_job()
    {
        var root = NewTestRoot();
        try
        {
            var repository = new FakeRepository { CommitThenReportUniqueConflict = true };
            var service = CreateIntake(root, repository);
            var sourceKey = ReceiptIntakeService.CreateManualIdempotencySourceKey(
                "2b4d3b55-32d8-486c-a214-c7bf077bf981");
            var input = new ReceiptIntakeInput(Png, "receipt.png", "image/png", 0,
                "TEST", "Synthetic", "Finance", sourceKey);

            var accepted = await service.AcceptAsync(input);

            Assert.Equal(ReceiptIntakeService.SourceRequestId(sourceKey!), accepted.Id);
            Assert.Single(repository.Requests);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "receipts")));
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public void Manual_idempotency_key_requires_a_canonical_uuid()
    {
        Assert.Null(ReceiptIntakeService.CreateManualIdempotencySourceKey(null));
        Assert.Equal("manual:2b4d3b5532d8486ca214c7bf077bf981",
            ReceiptIntakeService.CreateManualIdempotencySourceKey("2b4d3b55-32d8-486c-a214-c7bf077bf981"));
        Assert.Throws<ReceiptIntakeException>(() =>
            ReceiptIntakeService.CreateManualIdempotencySourceKey("not-a-uuid"));
    }

    [Fact]
    public async Task Full_durable_queue_rejects_new_manual_upload_and_removes_its_staged_object()
    {
        var root = NewTestRoot();
        try
        {
            var repository = new FakeRepository();
            repository.Requests.Add(new ReimbursementRequest
            {
                Id = "already-pending",
                ProcessingState = ProcessingStates.Pending
            });
            var service = CreateIntake(root, repository, maxOutstandingReceipts: 1);

            await Assert.ThrowsAsync<ReceiptQueueCapacityException>(() => service.AcceptAsync(
                new ReceiptIntakeInput(Png, "new.png", "image/png", 0, "TEST", "Test", "Test")));

            Assert.Single(repository.Requests);
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "receipts"), "*.png"));
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Full_queue_preserves_source_key_object_for_retry_after_capacity_recovers()
    {
        var root = NewTestRoot();
        try
        {
            var repository = new FakeRepository();
            repository.Requests.Add(new ReimbursementRequest
            {
                Id = "already-processing",
                ProcessingState = ProcessingStates.Processing
            });
            var service = CreateIntake(root, repository, maxOutstandingReceipts: 1);
            var input = new ReceiptIntakeInput(Png, "source.png", "image/png", 0,
                "DRIVE-SANDBOX", "Synthetic", "Test", "drive:test-folder:test-file:hash");

            await Assert.ThrowsAsync<ReceiptQueueCapacityException>(() => service.AcceptAsync(input));
            var path = Path.Combine(root, "receipts", ReceiptIntakeService.SourceRequestId(input.SourceKey!) + ".png");
            Assert.True(File.Exists(path));
            Assert.Equal(Png, await File.ReadAllBytesAsync(path));
            Assert.Single(repository.Requests);

            repository.Requests[0].ProcessingState = ProcessingStates.Completed;
            var accepted = await service.AcceptAsync(input);

            Assert.Equal(ReceiptIntakeService.SourceRequestId(input.SourceKey!), accepted.Id);
            Assert.Equal(2, repository.Requests.Count);
            Assert.Equal(Png, await File.ReadAllBytesAsync(path));
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Retry_reuses_complete_file_after_ambiguous_SQL_failure()
    {
        var root = NewTestRoot();
        try
        {
            var repository = new FakeRepository { FailNextAdd = true };
            var service = CreateIntake(root, repository);
            var input = new ReceiptIntakeInput(Png, "receipt.png", "image/png", 0,
                "DRIVE-SANDBOX", "Synthetic", "Test", "drive:folder:file:orphan");

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync(input));
            var directory = Path.Combine(root, "receipts");
            var finalPath = Path.Combine(directory, ReceiptIntakeService.SourceRequestId(input.SourceKey!) + ".png");
            Assert.True(File.Exists(finalPath));
            Assert.Equal(Png, await File.ReadAllBytesAsync(finalPath));
            Assert.Single(Directory.GetFiles(directory));

            var recovered = await service.AcceptAsync(input);

            Assert.Equal(ReceiptIntakeService.SourceRequestId(input.SourceKey!), recovered.Id);
            Assert.Single(repository.Requests);
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Source_object_is_preserved_when_preinsert_database_read_fails()
    {
        var root = NewTestRoot();
        try
        {
            var repository = new FakeRepository { FailNextHashLookup = true };
            var service = CreateIntake(root, repository);
            var input = new ReceiptIntakeInput(Png, "receipt.png", "image/png", 0,
                "DRIVE-SANDBOX", "Synthetic", "Test", "drive:folder:file:preinsert-retry");
            var path = Path.Combine(root, "receipts",
                ReceiptIntakeService.SourceRequestId(input.SourceKey!) + ".png");

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync(input));
            Assert.True(File.Exists(path));
            Assert.Equal(Png, await File.ReadAllBytesAsync(path));

            var recovered = await service.AcceptAsync(input);
            Assert.Equal(ReceiptIntakeService.SourceRequestId(input.SourceKey!), recovered.Id);
            Assert.Single(repository.Requests);
            Assert.Equal(Png, await File.ReadAllBytesAsync(path));
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Retry_rejects_conflicting_or_partial_orphan_without_overwrite()
    {
        var root = NewTestRoot();
        try
        {
            var repository = new FakeRepository();
            var service = CreateIntake(root, repository);
            var input = new ReceiptIntakeInput(Png, "receipt.png", "image/png", 0,
                "DRIVE-SANDBOX", "Synthetic", "Test", "drive:folder:file:partial");
            var directory = Path.Combine(root, "receipts");
            Directory.CreateDirectory(directory);
            var finalPath = Path.Combine(directory, ReceiptIntakeService.SourceRequestId(input.SourceKey!) + ".png");
            await File.WriteAllBytesAsync(finalPath, Png[..8]);

            await Assert.ThrowsAsync<ReceiptIntakeException>(() => service.AcceptAsync(input));

            Assert.Equal(Png[..8], await File.ReadAllBytesAsync(finalPath));
            Assert.Empty(repository.Requests);
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Source_key_cannot_be_reused_for_different_bytes()
    {
        var root = NewTestRoot();
        try
        {
            var repository = new FakeRepository();
            var service = CreateIntake(root, repository);
            var input = new ReceiptIntakeInput(Png, "receipt.png", "image/png", 0,
                "DRIVE-SANDBOX", "Synthetic", "Test", "drive:folder:file:stable");
            await service.AcceptAsync(input);

            var changed = input with { Bytes = [137, 80, 78, 71, 13, 10, 26, 10, 0, 1] };
            await Assert.ThrowsAsync<ReceiptIntakeException>(() => service.AcceptAsync(changed));

            Assert.Single(repository.Requests);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "receipts")));
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Concurrent_source_insert_resolves_to_already_committed_job()
    {
        var root = NewTestRoot();
        try
        {
            var repository = new FakeRepository { CommitThenReportUniqueConflict = true };
            var service = CreateIntake(root, repository);
            var input = new ReceiptIntakeInput(Png, "receipt.png", "image/png", 0,
                "DRIVE-SANDBOX", "Synthetic", "Test", "drive:folder:file:race");

            var accepted = await service.AcceptAsync(input);

            Assert.Equal(ReceiptIntakeService.SourceRequestId(input.SourceKey!), accepted.Id);
            Assert.Single(repository.Requests);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "receipts")));
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Failed_source_insert_does_not_treat_tracked_entity_as_durable()
    {
        var root = NewTestRoot();
        try
        {
            var repository = new FakeRepository { TrackUnpersistedThenFailNextAdd = true };
            var service = CreateIntake(root, repository);
            var input = new ReceiptIntakeInput(Png, "receipt.png", "image/png", 0,
                "DRIVE-SANDBOX", "Synthetic", "Test", "drive:folder:file:rollback-unit");

            await Assert.ThrowsAsync<DbUpdateException>(() => service.AcceptAsync(input));

            Assert.Empty(repository.Requests);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "receipts")));
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Rolled_back_source_insert_is_not_mistaken_for_a_persisted_job()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        var root = NewTestRoot();
        var databaseName = "AuraIntakeRollbackIT_" + Guid.NewGuid().ToString("N");
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        var created = false;
        try
        {
            await using (var setup = new AppDbContext(options))
                created = await setup.Database.EnsureCreatedAsync();
            Assert.True(created);

            await using var database = new AppDbContext(options);
            await database.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER dbo.TR_AuditLogs_RejectIntakeTest ON dbo.AuditLogs
                AFTER INSERT AS
                BEGIN
                    THROW 51000, 'Synthetic audit write failure', 1;
                END
                """);
            var input = new ReceiptIntakeInput(Png, "receipt.png", "image/png", 0,
                "DRIVE-SANDBOX", "Synthetic", "Test", "drive:folder:file:rollback");
            var intake = CreateIntake(root, new ReimbursementRepository(database));

            await Assert.ThrowsAsync<DbUpdateException>(() => intake.AcceptAsync(input));

            Assert.Empty(await database.ReimbursementRequests.AsNoTracking().ToListAsync());
            Assert.Empty(await database.AuditLogs.AsNoTracking().ToListAsync());
            Assert.Single(Directory.GetFiles(Path.Combine(root, "receipts")));

            await database.Database.ExecuteSqlRawAsync(
                "DROP TRIGGER dbo.TR_AuditLogs_RejectIntakeTest;");
            await using var retryDatabase = new AppDbContext(options);
            var retryIntake = CreateIntake(root, new ReimbursementRepository(retryDatabase));
            var retried = await retryIntake.AcceptAsync(input);
            Assert.Equal(ReceiptIntakeService.SourceRequestId(input.SourceKey!), retried.Id);
            Assert.Single(await retryDatabase.ReimbursementRequests.AsNoTracking().ToListAsync());
            Assert.Single(await retryDatabase.AuditLogs.AsNoTracking().ToListAsync());
        }
        finally
        {
            if (created)
            {
                await using var cleanup = new SqlConnection(new SqlConnectionStringBuilder(connectionString)
                    { InitialCatalog = "master" }.ConnectionString);
                await cleanup.OpenAsync();
                await using var command = cleanup.CreateCommand();
                command.CommandText = $"IF DB_ID(@name) IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
                command.Parameters.AddWithValue("@name", databaseName);
                await command.ExecuteNonQueryAsync();
            }
            DeleteTestRoot(root);
        }
    }

    [Fact]
    public async Task Two_sql_intake_instances_accept_the_same_source_once()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        var root = NewTestRoot();
        var databaseName = "AuraIntakeRaceIT_" + Guid.NewGuid().ToString("N");
        var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        var created = false;
        try
        {
            await using (var setup = new AppDbContext(options))
                created = await setup.Database.EnsureCreatedAsync();
            Assert.True(created);

            await using var firstDatabase = new AppDbContext(options);
            await using var secondDatabase = new AppDbContext(options);
            var readBarrier = new PreflightBarrier();
            var preflight = new ConcurrentPreflightRepository(readBarrier,
                new ReimbursementRepository(firstDatabase));
            var secondPreflight = new ConcurrentPreflightRepository(readBarrier,
                new ReimbursementRepository(secondDatabase));
            var firstIntake = CreateIntake(root, preflight);
            var secondIntake = CreateIntake(root, secondPreflight);
            var input = new ReceiptIntakeInput(Png, "receipt.png", "image/png", 0,
                "DRIVE-SANDBOX", "Synthetic", "Test", "drive:folder:file:concurrent-sql");

            var accepted = await Task.WhenAll(firstIntake.AcceptAsync(input),
                secondIntake.AcceptAsync(input)).WaitAsync(TimeSpan.FromSeconds(20));

            Assert.Equal(accepted[0].Id, accepted[1].Id);
            Assert.Equal(ReceiptIntakeService.SourceRequestId(input.SourceKey!), accepted[0].Id);
            Assert.Single(await firstDatabase.ReimbursementRequests.AsNoTracking().ToListAsync());
            Assert.Single(await firstDatabase.AuditLogs.AsNoTracking().ToListAsync());
            Assert.Single(Directory.GetFiles(Path.Combine(root, "receipts")));
        }
        finally
        {
            if (created)
            {
                await using var cleanup = new SqlConnection(new SqlConnectionStringBuilder(connectionString)
                    { InitialCatalog = "master" }.ConnectionString);
                await cleanup.OpenAsync();
                await using var command = cleanup.CreateCommand();
                command.CommandText = $"IF DB_ID(@name) IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END";
                command.Parameters.AddWithValue("@name", databaseName);
                await command.ExecuteNonQueryAsync();
            }
            DeleteTestRoot(root);
        }
    }

    [Theory]
    [InlineData("Production", false)]
    [InlineData("Development", true)]
    public async Task Connector_never_calls_Google_when_production_or_scanner_required(
        string environmentName, bool scannerRequired)
    {
        var handler = new CountingHandler();
        var options = OptionsFactory.Create(new DriveSandboxOptions
        {
            Enabled = true,
            FolderId = "folder123",
            ServiceAccountJsonPath = "not-used",
            RequireMalwareScanner = scannerRequired
        });
        using var http = new HttpClient(handler);
        using var provider = new ServiceCollection().BuildServiceProvider();
        var worker = new DriveSandboxWorker(options, OptionsFactory.Create(new ReceiptStorageOptions()),
            new TestEnvironment(Path.GetTempPath()) { EnvironmentName = environmentName },
            provider.GetRequiredService<IServiceScopeFactory>(), new DriveSandboxClient(http, options),
            LocalConfig(), NullLogger<DriveSandboxWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);
        Assert.Equal(0, handler.Count);
    }

    [Theory]
    [InlineData("http://0.0.0.0:5097")]
    [InlineData("https://example.org")]
    [InlineData("")]
    public void Connector_requires_explicit_loopback_binding(string urls)
    {
        var options = new DriveSandboxOptions { Enabled = true, RequireMalwareScanner = false };
        Assert.False(DriveSandboxRuntimePolicy.IsAllowed(options, "Development", urls));
    }

    [Fact]
    public async Task Local_fake_Drive_API_ingests_one_synthetic_file_without_network()
    {
        var root = NewTestRoot();
        try
        {
            using var rsa = RSA.Create(2048);
            var keyPath = WriteFakeServiceAccount(root, rsa);
            var repository = new FakeRepository();
            var environment = new TestEnvironment(root);
            var options = OptionsFactory.Create(new DriveSandboxOptions
            {
                Enabled = true,
                FolderId = "folder123",
                ServiceAccountJsonPath = keyPath,
                RequireMalwareScanner = false,
                PollIntervalSeconds = 30
            });
            var storage = OptionsFactory.Create(new ReceiptStorageOptions
                { Directory = "receipts", MaxFileSizeMb = 5 });
            using var provider = BuildProvider(root, repository);
            var handler = new FakeDriveHandler(rsa);
            using var http = new HttpClient(handler);
            var worker = new DriveSandboxWorker(options, storage, environment,
                provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            await repository.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await worker.StopAsync(CancellationToken.None);

            Assert.Single(repository.Requests);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "receipts")));
            Assert.Contains("Source=DRIVE_SANDBOX", repository.LastAudit);
            Assert.Equal(1, handler.DownloadCount);
            Assert.True(handler.JwtSignatureVerified);
        }
        finally { DeleteTestRoot(root); }
    }

    [Theory]
    [InlineData("metadata-bump", 1)]
    [InlineData("content-edit", 2)]
    public async Task Drive_content_identity_ignores_metadata_only_changes_but_accepts_new_bytes(
        string mode, int expectedJobs)
    {
        var root = NewTestRoot();
        try
        {
            using var rsa = RSA.Create(2048);
            var repository = new FakeRepository();
            var options = OptionsFactory.Create(new DriveSandboxOptions
            {
                Enabled = true, FolderId = "folder123",
                ServiceAccountJsonPath = WriteFakeServiceAccount(root, rsa),
                RequireMalwareScanner = false, PollIntervalSeconds = 30
            });
            using var provider = BuildProvider(root, repository);
            var handler = new FakeDriveHandler(rsa, mode);
            using var http = new HttpClient(handler);
            var storage = OptionsFactory.Create(new ReceiptStorageOptions
                { Directory = "receipts", MaxFileSizeMb = 5 });
            var environment = new TestEnvironment(root);

            var first = new DriveSandboxWorker(options, storage, environment,
                provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);
            await first.StartAsync(CancellationToken.None);
            await repository.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await first.StopAsync(CancellationToken.None);

            var second = new DriveSandboxWorker(options, storage, environment,
                provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);
            await second.StartAsync(CancellationToken.None);
            for (var attempt = 0; attempt < 50 &&
                (handler.ListRequestCount < 2 || repository.Requests.Count < expectedJobs); attempt++)
                await Task.Delay(100);
            await Task.Delay(200);
            await second.StopAsync(CancellationToken.None);

            Assert.Equal(2, handler.ListRequestCount);
            Assert.Equal(expectedJobs, repository.Requests.Count);
            Assert.Equal(expectedJobs, handler.DownloadCount);
        }
        finally { DeleteTestRoot(root); }
    }

    [Theory]
    [InlineData("rate-limited-once", 2, 1)]
    [InlineData("forbidden", 1, 0)]
    public async Task Drive_client_retries_transient_error_but_not_permission_denial(
        string mode, int expectedFolderRequests, int expectedJobs)
    {
        var root = NewTestRoot();
        try
        {
            using var rsa = RSA.Create(2048);
            var options = OptionsFactory.Create(new DriveSandboxOptions
            {
                Enabled = true, FolderId = "folder123",
                ServiceAccountJsonPath = WriteFakeServiceAccount(root, rsa),
                RequireMalwareScanner = false, PollIntervalSeconds = 30
            });
            var repository = new FakeRepository();
            using var provider = BuildProvider(root, repository);
            var handler = new FakeDriveHandler(rsa, mode);
            using var http = new HttpClient(handler);
            var worker = new DriveSandboxWorker(options,
                OptionsFactory.Create(new ReceiptStorageOptions
                    { Directory = "receipts", MaxFileSizeMb = 5 }),
                new TestEnvironment(root), provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            if (expectedJobs == 1)
                await repository.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            else
                await Task.Delay(300);
            await worker.StopAsync(CancellationToken.None);

            Assert.Equal(expectedFolderRequests, handler.FolderRequestCount);
            Assert.Equal(expectedJobs, repository.Requests.Count);
        }
        finally { DeleteTestRoot(root); }
    }

    [Theory]
    [InlineData("wrong-parent", 0)]
    [InlineData("bad-md5", 1)]
    [InlineData("changed-version", 1)]
    public async Task Rejects_wrong_folder_or_changed_file_before_SQL_intake(
        string mode, int expectedDownloads)
    {
        var root = NewTestRoot();
        try
        {
            using var rsa = RSA.Create(2048);
            var keyPath = WriteFakeServiceAccount(root, rsa);
            var repository = new FakeRepository();
            var options = OptionsFactory.Create(new DriveSandboxOptions
            {
                Enabled = true, FolderId = "folder123", ServiceAccountJsonPath = keyPath,
                RequireMalwareScanner = false, PollIntervalSeconds = 30
            });
            using var provider = BuildProvider(root, repository);
            var handler = new FakeDriveHandler(rsa, mode);
            using var http = new HttpClient(handler);
            var worker = new DriveSandboxWorker(options,
                OptionsFactory.Create(new ReceiptStorageOptions
                    { Directory = "receipts", MaxFileSizeMb = 5 }),
                new TestEnvironment(root), provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            var rejections = Path.Combine(root, "App_Data", "DriveSandbox", "rejections.jsonl");
            for (var attempt = 0; attempt < 50 && !File.Exists(rejections); attempt++)
                await Task.Delay(100);
            await worker.StopAsync(CancellationToken.None);

            Assert.True(File.Exists(rejections));
            Assert.Empty(repository.Requests);
            Assert.Equal(expectedDownloads, handler.DownloadCount);
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Repeated_poll_does_not_duplicate_persistent_rejection_log_entry()
    {
        var root = NewTestRoot();
        try
        {
            using var rsa = RSA.Create(2048);
            var keyPath = WriteFakeServiceAccount(root, rsa);
            var repository = new FakeRepository();
            var options = OptionsFactory.Create(new DriveSandboxOptions
            {
                Enabled = true, FolderId = "folder123", ServiceAccountJsonPath = keyPath,
                RequireMalwareScanner = false, PollIntervalSeconds = 1
            });
            using var provider = BuildProvider(root, repository);
            var handler = new FakeDriveHandler(rsa, "unsupported-file");
            using var http = new HttpClient(handler);
            var worker = new DriveSandboxWorker(options,
                OptionsFactory.Create(new ReceiptStorageOptions
                    { Directory = "receipts", MaxFileSizeMb = 5 }),
                new TestEnvironment(root), provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromMilliseconds(2300));
            await worker.StopAsync(CancellationToken.None);

            var rejections = Path.Combine(root, "App_Data", "DriveSandbox", "rejections.jsonl");
            Assert.True(File.Exists(rejections));
            Assert.Single(await File.ReadAllLinesAsync(rejections));

            var restartedWorker = new DriveSandboxWorker(options,
                OptionsFactory.Create(new ReceiptStorageOptions
                    { Directory = "receipts", MaxFileSizeMb = 5 }),
                new TestEnvironment(root), provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);
            await restartedWorker.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromMilliseconds(1100));
            await restartedWorker.StopAsync(CancellationToken.None);

            Assert.Single(await File.ReadAllLinesAsync(rejections));
            Assert.Empty(repository.Requests);
            Assert.Equal(0, handler.DownloadCount);
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Drive_scan_over_configured_file_limit_fails_closed_without_partial_intake()
    {
        var root = NewTestRoot();
        try
        {
            using var rsa = RSA.Create(2048);
            var keyPath = WriteFakeServiceAccount(root, rsa);
            var repository = new FakeRepository();
            var options = OptionsFactory.Create(new DriveSandboxOptions
            {
                Enabled = true, FolderId = "folder123", ServiceAccountJsonPath = keyPath,
                RequireMalwareScanner = false, MaxFilesPerScan = 100, PollIntervalSeconds = 30
            });
            using var provider = BuildProvider(root, repository);
            var handler = new FakeDriveHandler(rsa, listedEntryCount: 101);
            using var http = new HttpClient(handler);
            var worker = new DriveSandboxWorker(options,
                OptionsFactory.Create(new ReceiptStorageOptions
                    { Directory = "receipts", MaxFileSizeMb = 5 }),
                new TestEnvironment(root), provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            await handler.ListCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            await worker.StopAsync(CancellationToken.None);

            Assert.Empty(repository.Requests);
            Assert.Equal(0, handler.DownloadCount);
            Assert.False(Directory.Exists(Path.Combine(root, "receipts")));
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Download_never_buffers_beyond_limit_even_if_server_omits_content_length()
    {
        using var http = new HttpClient(new OversizeHandler());
        var client = new DriveSandboxClient(http, OptionsFactory.Create(new DriveSandboxOptions()));
        var error = await Assert.ThrowsAsync<DriveSandboxException>(() =>
            client.DownloadAsync("fake", "file123", 10, CancellationToken.None));
        Assert.Equal("DRIVE_FILE_TOO_LARGE", error.Message);
    }

    private static ServiceProvider BuildProvider(string root, FakeRepository repository)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IReimbursementRepository>(repository);
        services.AddSingleton(new ReceiptProcessingQueue());
        services.AddSingleton(provider => CreateIntake(root,
            (FakeRepository)provider.GetRequiredService<IReimbursementRepository>(),
            provider.GetRequiredService<ReceiptProcessingQueue>()));
        return services.BuildServiceProvider();
    }

    private static IConfiguration LocalConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
            { ["urls"] = "http://127.0.0.1:5097" }).Build();

    private static ReceiptIntakeService CreateIntake(string root, IReimbursementRepository repository,
        ReceiptProcessingQueue? queue = null, int maxOutstandingReceipts = 1000) =>
        new(new LocalReceiptFileStore(new TestEnvironment(root),
                OptionsFactory.Create(new ReceiptStorageOptions { Directory = "receipts", MaxFileSizeMb = 5 })), repository,
            OptionsFactory.Create(new ReceiptStorageOptions { Directory = "receipts", MaxFileSizeMb = 5 }),
            OptionsFactory.Create(new DecisionPolicyOptions()),
            OptionsFactory.Create(new ReceiptProcessingOptions { MaxOutstandingReceipts = maxOutstandingReceipts }),
            queue ?? new ReceiptProcessingQueue());

    private static string NewTestRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "aura-drive-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTestRoot(string root)
    {
        if (Path.GetDirectoryName(root)?.TrimEnd(Path.DirectorySeparatorChar) !=
            Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) ||
            !Path.GetFileName(root).StartsWith("aura-drive-test-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to delete outside test temp root.");
        Directory.Delete(root, recursive: true);
    }

    private static string WriteFakeServiceAccount(string root, RSA rsa)
    {
        var path = Path.Combine(root, "fake-service-account.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            type = "service_account",
            client_email = "aura-test@project.iam.gserviceaccount.com",
            private_key = rsa.ExportPkcs8PrivateKeyPem(),
            token_uri = "https://oauth2.googleapis.com/token"
        }));
        return path;
    }

    private sealed class FakeDriveHandler(RSA publicKey, string mode = "valid",
        int listedEntryCount = 1) : HttpMessageHandler
    {
        private readonly string _md5 = Convert.ToHexString(MD5.HashData(Png)).ToLowerInvariant();
        private readonly byte[] _editedPng = [137, 80, 78, 71, 13, 10, 26, 10, 0, 1];
        public int DownloadCount { get; private set; }
        public int FolderRequestCount { get; private set; }
        public int ListRequestCount { get; private set; }
        public bool JwtSignatureVerified { get; private set; }
        public TaskCompletionSource ListCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/token")
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                var assertion = Uri.UnescapeDataString(body.Split("assertion=")[1]).Replace('+', ' ');
                var parts = assertion.Split('.');
                var signed = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
                var signature = Convert.FromBase64String(Pad(parts[2].Replace('-', '+').Replace('_', '/')));
                JwtSignatureVerified = publicKey.VerifyData(signed, signature,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var claims = JsonDocument.Parse(Convert.FromBase64String(
                    Pad(parts[1].Replace('-', '+').Replace('_', '/'))));
                Assert.Equal("https://www.googleapis.com/auth/drive.readonly",
                    claims.RootElement.GetProperty("scope").GetString());
                return Json("""{"access_token":"fake","expires_in":3600}""");
            }
            if (path == "/drive/v3/files/folder123")
            {
                FolderRequestCount++;
                if (mode == "rate-limited-once" && FolderRequestCount == 1)
                    return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                if (mode == "forbidden")
                    return new HttpResponseMessage(HttpStatusCode.Forbidden);
                return Json("""{"id":"folder123","mimeType":"application/vnd.google-apps.folder","trashed":false}""");
            }
            if (path == "/drive/v3/files" && request.RequestUri.Query.Contains("q=", StringComparison.Ordinal))
            {
                ListRequestCount++;
                var response = Json(JsonSerializer.Serialize(new
                {
                    files = Enumerable.Range(0, listedEntryCount).Select(_ => FileMetadata()).ToArray()
                }));
                ListCompleted.TrySetResult();
                return response;
            }
            if (path == "/drive/v3/files/file123" && request.RequestUri.Query.Contains("alt=media",
                    StringComparison.Ordinal))
            {
                DownloadCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new ByteArrayContent(CurrentBytes()) };
            }
            if (path == "/drive/v3/files/file123")
                return Json(JsonSerializer.Serialize(FileMetadata(afterDownload: true)));
            throw new InvalidOperationException("Unexpected fake Drive endpoint.");
        }

        private byte[] CurrentBytes() => mode == "content-edit" && ListRequestCount >= 2 ? _editedPng : Png;

        private object FileMetadata(bool afterDownload = false) => new
        {
            id = "file123",
            name = mode == "unsupported-file" ? "receipt.txt" : "receipt.png",
            mimeType = mode == "unsupported-file" ? "text/plain" : "image/png",
            size = CurrentBytes().Length.ToString(),
            md5Checksum = mode == "bad-md5" ? "00000000000000000000000000000000" :
                mode == "content-edit" ? Convert.ToHexString(MD5.HashData(CurrentBytes())).ToLowerInvariant() : _md5,
            version = afterDownload && mode == "changed-version" ? "2" :
                (mode is "metadata-bump" or "content-edit") && ListRequestCount >= 2 ? "2" : "1",
            parents = new[] { mode == "wrong-parent" ? "other-folder" : "folder123" },
            trashed = false,
            capabilities = new { canDownload = true }
        };

        private static HttpResponseMessage Json(string value) =>
            new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };

        private static string Pad(string value) => value + new string('=', (4 - value.Length % 4) % 4);
    }

    private sealed class OversizeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(new byte[20]))
            });
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Count { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Count++;
            throw new InvalidOperationException("Drive sandbox must not call Google.");
        }
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AURA.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }

    private sealed class FakeRepository : IReimbursementRepository
    {
        public List<ReimbursementRequest> Requests { get; } = [];
        public bool FailNextAdd { get; set; }
        public bool TrackUnpersistedThenFailNextAdd { get; set; }
        public bool FailNextHashLookup { get; set; }
        public bool CommitThenReportUniqueConflict { get; set; }
        private ReimbursementRequest? TrackedRequest { get; set; }
        public string LastAudit { get; private set; } = string.Empty;
        public TaskCompletionSource Accepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ReimbursementRequest?> GetRequestByIdAsync(string id) =>
            Task.FromResult(Requests.SingleOrDefault(item => item.Id == id) ??
                (TrackedRequest?.Id == id ? TrackedRequest : null));
        public Task<ReimbursementRequest?> GetPersistedRequestByIdAsync(string id) =>
            Task.FromResult(Requests.SingleOrDefault(item => item.Id == id));
        public Task<bool> ExistsByFileHashAsync(string sha256)
        {
            if (FailNextHashLookup)
            {
                FailNextHashLookup = false;
                throw new InvalidOperationException("Simulated database read failure before insert.");
            }
            return Task.FromResult(Requests.Any(item => item.FileSha256 == sha256));
        }
        public Task AddRequestWithAuditAsync(ReimbursementRequest request, string action, string details)
        {
            if (TrackUnpersistedThenFailNextAdd)
            {
                TrackUnpersistedThenFailNextAdd = false;
                TrackedRequest = request;
                throw new DbUpdateException("Simulated rollback with entity retained in change tracker.");
            }
            if (FailNextAdd)
            {
                FailNextAdd = false;
                throw new InvalidOperationException("Simulated uncertain SQL write result.");
            }
            Requests.Add(request);
            LastAudit = details;
            Accepted.TrySetResult();
            if (CommitThenReportUniqueConflict)
            {
                CommitThenReportUniqueConflict = false;
                throw new DbUpdateException("Simulated concurrent primary-key winner.");
            }
            return Task.CompletedTask;
        }
        public async Task<ReceiptIntakePersistenceResult> TryAddRequestWithAuditAsync(
            ReimbursementRequest request, string action, string details, int maxOutstandingReceipts,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var existing = Requests.SingleOrDefault(item => item.Id == request.Id);
            if (existing is not null)
                return ReceiptIntakePersistenceResult.AlreadyExists;
            var outstanding = Requests.Count(item => item.ProcessingState is
                ProcessingStates.Pending or ProcessingStates.Processing);
            if (outstanding >= maxOutstandingReceipts)
                return ReceiptIntakePersistenceResult.CapacityReached;
            await AddRequestWithAuditAsync(request, action, details);
            return ReceiptIntakePersistenceResult.Added;
        }
        public Task<List<ReimbursementRequest>> GetAllRequestsAsync() => throw new NotSupportedException();
        public Task UpdateRequestAsync(ReimbursementRequest request) => throw new NotSupportedException();
        public Task UpdateRequestWithAuditAsync(ReimbursementRequest request, string action, string details) =>
            throw new NotSupportedException();
        public Task UpdateRequestsWithAuditAsync(IEnumerable<(ReimbursementRequest Request, string Details)> requests,
            string action) => throw new NotSupportedException();
    }

    private sealed class PreflightBarrier
    {
        public TaskCompletionSource BothPreflights { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int PreflightCount;
    }

    private sealed class ConcurrentPreflightRepository(PreflightBarrier barrier,
        IReimbursementRepository inner) : IReimbursementRepository
    {
        public async Task<ReimbursementRequest?> GetRequestByIdAsync(string id)
        {
            var request = await inner.GetRequestByIdAsync(id);
            if (Interlocked.Increment(ref barrier.PreflightCount) == 2)
                barrier.BothPreflights.TrySetResult();
            await barrier.BothPreflights.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return request;
        }

        public Task<ReimbursementRequest?> GetPersistedRequestByIdAsync(string id) =>
            inner.GetPersistedRequestByIdAsync(id);
        public Task<List<ReimbursementRequest>> GetAllRequestsAsync() => inner.GetAllRequestsAsync();
        public Task AddRequestWithAuditAsync(ReimbursementRequest request, string action, string details) =>
            inner.AddRequestWithAuditAsync(request, action, details);
        public Task<ReceiptIntakePersistenceResult> TryAddRequestWithAuditAsync(ReimbursementRequest request,
            string action, string details, int maxOutstandingReceipts, CancellationToken cancellationToken) =>
            inner.TryAddRequestWithAuditAsync(request, action, details, maxOutstandingReceipts, cancellationToken);
        public Task UpdateRequestAsync(ReimbursementRequest request) => inner.UpdateRequestAsync(request);
        public Task UpdateRequestWithAuditAsync(ReimbursementRequest request, string action, string details) =>
            inner.UpdateRequestWithAuditAsync(request, action, details);
        public Task UpdateRequestsWithAuditAsync(IEnumerable<(ReimbursementRequest Request, string Details)> requests,
            string action) => inner.UpdateRequestsWithAuditAsync(requests, action);
        public Task<bool> ExistsByFileHashAsync(string sha256) => inner.ExistsByFileHashAsync(sha256);
    }
}
