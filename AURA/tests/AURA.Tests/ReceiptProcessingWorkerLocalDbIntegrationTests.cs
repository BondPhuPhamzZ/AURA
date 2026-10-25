using System.Reflection;
using System.Security.Cryptography;
using AURA.Controllers;
using AURA.Data;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using AURA.Services;
using AURA.Tests.Fakes;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
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
    public async Task Pre_inference_privacy_block_is_a_durable_human_review_case_with_zero_provider_HTTP_calls()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var id = await SeedAsync(options, root);
            using var handler = new RejectUnexpectedHttpHandler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://remote.example.test/") };
            var environment = new TestEnvironment(root);
            var gate = new ExternalImagePrivacyGate(Microsoft.Extensions.Options.Options.Create(
                new ExternalImagePrivacyOptions { Mode = ExternalImagePrivacyOptions.BlockExternal }), environment);
            var extractor = new OpenRouterVisionExtractorService(client,
                Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions { ApiKey = "synthetic-test-key" }),
                environment, NullLogger<OpenRouterVisionExtractorService>.Instance, gate);
            await ProcessOnceAsync(options, root, extractor);
            await AssertStateAsync(options, id, ProcessingStates.Completed, 1, "ESCALATE_PRIVACY");
            await ProcessOnceAsync(options, root, extractor);
            Assert.Equal(0, handler.Count);
            await using var db = new AppDbContext(options);
            var request = await db.ReimbursementRequests.SingleAsync(item => item.Id == id);
            Assert.Null(request.ExtractedFactsJson);
            Assert.Null(request.RetentionExpiresAt);
            Assert.Equal(ExternalImagePrivacyGate.ExternalBlockedCode, request.ProviderErrorCode);
            var audit = await db.AuditLogs.SingleAsync(item => item.RequestId == id);
            Assert.Equal("AI_PROCESSED_ESCALATE_PRIVACY", audit.Action);
            Assert.Contains("PreInferencePrivacyBlocked=True", audit.Details, StringComparison.Ordinal);
            Assert.Contains("PrivacyRisk=NOT_EVALUATED", audit.Details, StringComparison.Ordinal);
            // Human acceptance closes the case locally; it does not approve a
            // hash or automatically send the blocked image to a provider.
            request.IsForwardedToManager = true;
            await db.SaveChangesAsync();
            Assert.IsType<JsonResult>(await CreateReviewerController(db, retentionEnabled: true)
                .EscalateAction(id, "YES"));
            await db.Entry(request).ReloadAsync();
            Assert.Equal("MANUAL_REVIEW_ACCEPTED", request.Status);
            Assert.Equal(request.ManagerDecisionAt!.Value.AddDays(7), request.RetentionExpiresAt);
            Assert.Equal(0, handler.Count);
        });
    }

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
    public async Task Retention_purge_deletes_only_expired_finalized_requests_and_their_audit_and_image()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var store = new LocalReceiptFileStore(new TestEnvironment(root),
                Microsoft.Extensions.Options.Options.Create(new ReceiptStorageOptions
                {
                    Directory = "receipts"
                }));
            var ids = new[] { "expired-auto", "expired-manager", "pending", "unresolved", "recent", "expired-metadata" };
            var requests = new[]
            {
                CreateRetentionRequest(ids[0], "AUTO_APPROVE", ProcessingStates.Completed,
                    managerAnswer: null, finalizedAt: DateTime.UtcNow.AddDays(-8), expired: true),
                CreateRetentionRequest(ids[1], "RETURNED_BY_MANAGER", ProcessingStates.Failed,
                    managerAnswer: false, finalizedAt: DateTime.UtcNow.AddDays(-8), expired: true),
                CreateRetentionRequest(ids[2], "PENDING", ProcessingStates.Pending,
                    managerAnswer: null, finalizedAt: null, expired: true),
                CreateRetentionRequest(ids[3], "ESCALATE_FACT", ProcessingStates.Completed,
                    managerAnswer: null, finalizedAt: null, expired: true),
                CreateRetentionRequest(ids[4], "AUTO_APPROVE", ProcessingStates.Completed,
                    managerAnswer: null, finalizedAt: DateTime.UtcNow, expired: false),
                CreateRetentionRequest(ids[5], "AUTO_APPROVE", ProcessingStates.Completed,
                    managerAnswer: null, finalizedAt: DateTime.UtcNow.AddDays(-8), expired: true)
            };
            requests[5].StoredFileName = string.Empty;
            requests[5].ExtractedFactsJson = "{\"synthetic\":true}";

            foreach (var request in requests)
            {
                if (string.IsNullOrWhiteSpace(request.StoredFileName)) continue;
                byte[] bytes = [1, 2, 3, 4];
                request.FileSizeBytes = bytes.Length;
                request.FileSha256 = Convert.ToHexString(SHA256.HashData(bytes));
                await store.PutIfAbsentAsync(request.StoredFileName, bytes, "image/jpeg",
                    request.FileSha256, CancellationToken.None);
            }

            await using (var db = new AppDbContext(options))
            {
                db.ReimbursementRequests.AddRange(requests);
                db.AuditLogs.AddRange(requests.Select(request => new AuditLog
                {
                    RequestId = request.Id,
                    Action = "SYNTHETIC_TEST",
                    Details = "synthetic",
                    Timestamp = DateTime.UtcNow
                }));
                await db.SaveChangesAsync();
            }

            await using (var db = new AppDbContext(options))
            {
                var purger = new ReceiptRetentionPurger(db, store,
                    NullLogger<ReceiptRetentionPurger>.Instance);
                Assert.Equal(3, await purger.PurgeExpiredBatchAsync(25, CancellationToken.None));
                Assert.Equal(0, await purger.PurgeExpiredBatchAsync(25, CancellationToken.None));
            }

            await using var verify = new AppDbContext(options);
            Assert.Equal(new[] { ids[2], ids[4], ids[3] }, await verify.ReimbursementRequests
                .OrderBy(request => request.Id).Select(request => request.Id).ToArrayAsync());
            Assert.Equal(3, await verify.AuditLogs.CountAsync());
            foreach (var request in requests)
            {
                if (string.IsNullOrWhiteSpace(request.StoredFileName)) continue;
                await using var image = await store.OpenReadAsync(request.StoredFileName,
                    CancellationToken.None);
                Assert.Equal(request.Id is "pending" or "unresolved" or "recent", image is not null);
            }
        });
    }

    [Fact]
    public async Task Retention_purge_retries_idempotently_if_process_stops_after_image_delete()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var localStore = new LocalReceiptFileStore(new TestEnvironment(root),
                Microsoft.Extensions.Options.Options.Create(new ReceiptStorageOptions
                {
                    Directory = "receipts"
                }));
            var request = CreateRetentionRequest("expired-retry", "AUTO_APPROVE",
                ProcessingStates.Completed, managerAnswer: null,
                finalizedAt: DateTime.UtcNow.AddDays(-9), expired: true);
            byte[] bytes = [5, 6, 7];
            request.FileSizeBytes = bytes.Length;
            request.FileSha256 = Convert.ToHexString(SHA256.HashData(bytes));
            await localStore.PutIfAbsentAsync(request.StoredFileName, bytes, "image/jpeg",
                request.FileSha256, CancellationToken.None);
            await using (var seed = new AppDbContext(options))
            {
                seed.ReimbursementRequests.Add(request);
                seed.AuditLogs.Add(new AuditLog
                {
                    RequestId = request.Id,
                    Action = "SYNTHETIC_TEST",
                    Details = "synthetic",
                    Timestamp = DateTime.UtcNow
                });
                await seed.SaveChangesAsync();
            }

            var flakyStore = new DeleteThenFailOnceReceiptFileStore(localStore);
            await using (var firstAttempt = new AppDbContext(options))
            {
                var purger = new ReceiptRetentionPurger(firstAttempt, flakyStore,
                    NullLogger<ReceiptRetentionPurger>.Instance);
                await Assert.ThrowsAsync<IOException>(() =>
                    purger.PurgeExpiredBatchAsync(25, CancellationToken.None));
            }

            await using (var afterRollback = new AppDbContext(options))
            {
                Assert.True(await afterRollback.ReimbursementRequests.AnyAsync(item => item.Id == request.Id));
                Assert.True(await afterRollback.AuditLogs.AnyAsync(item => item.RequestId == request.Id));
                Assert.Null(await localStore.OpenReadAsync(request.StoredFileName, CancellationToken.None));
            }

            await using (var retry = new AppDbContext(options))
            {
                var purger = new ReceiptRetentionPurger(retry, flakyStore,
                    NullLogger<ReceiptRetentionPurger>.Instance);
                Assert.Equal(1, await purger.PurgeExpiredBatchAsync(25, CancellationToken.None));
                Assert.False(await retry.ReimbursementRequests.AnyAsync(item => item.Id == request.Id));
                Assert.False(await retry.AuditLogs.AnyAsync(item => item.RequestId == request.Id));
            }
        });
    }

    [Fact]
    public async Task Retention_startup_drains_multiple_batches_before_returning_and_preserves_open_work()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var store = new LocalReceiptFileStore(new TestEnvironment(root),
                Microsoft.Extensions.Options.Options.Create(new ReceiptStorageOptions { Directory = "receipts" }));
            var requests = Enumerable.Range(0, 5).Select(index => CreateRetentionRequest(
                $"expired-batch-{index}", "AUTO_APPROVE", ProcessingStates.Completed,
                null, DateTime.UtcNow.AddDays(-8), expired: true)).ToList();
            requests.Add(CreateRetentionRequest("open-batch", "ESCALATE_FACT", ProcessingStates.Completed,
                null, null, expired: true));
            foreach (var request in requests)
            {
                byte[] bytes = [1, 2, 3];
                request.FileSizeBytes = bytes.Length;
                request.FileSha256 = Convert.ToHexString(SHA256.HashData(bytes));
                await store.PutIfAbsentAsync(request.StoredFileName, bytes, "image/jpeg",
                    request.FileSha256, CancellationToken.None);
            }
            await using (var seed = new AppDbContext(options))
            {
                seed.ReimbursementRequests.AddRange(requests);
                seed.AuditLogs.AddRange(requests.Select(request => new AuditLog
                {
                    RequestId = request.Id, Action = "SYNTHETIC_TEST", Timestamp = DateTime.UtcNow
                }));
                await seed.SaveChangesAsync();
            }
            await using var services = new ServiceCollection()
                .AddScoped(_ => new AppDbContext(options))
                .AddSingleton<IReceiptFileStore>(store)
                .AddSingleton<Microsoft.Extensions.Logging.ILogger<ReceiptRetentionPurger>>(
                    NullLogger<ReceiptRetentionPurger>.Instance)
                .AddScoped<ReceiptRetentionPurger>()
                .BuildServiceProvider();
            using var worker = new ReceiptRetentionCleanupWorker(
                services.GetRequiredService<IServiceScopeFactory>(),
                Microsoft.Extensions.Options.Options.Create(new ReceiptRetentionOptions
                {
                    Enabled = true, BatchSize = 2
                }), NullLogger<ReceiptRetentionCleanupWorker>.Instance);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                await worker.StartAsync(timeout.Token);
                await using var verify = new AppDbContext(options);
                Assert.Equal("open-batch", (await verify.ReimbursementRequests.SingleAsync()).Id);
                Assert.Equal("open-batch", (await verify.AuditLogs.SingleAsync()).RequestId);
                foreach (var request in requests)
                {
                    await using var image = await store.OpenReadAsync(request.StoredFileName, timeout.Token);
                    Assert.Equal(request.Id == "open-batch", image is not null);
                }
            }
            finally
            {
                await worker.StopAsync(CancellationToken.None);
            }
        });
    }

    [Fact]
    public async Task Retention_selection_locks_final_decision_until_image_and_SQL_deletion_commit()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var store = new LocalReceiptFileStore(new TestEnvironment(root),
                Microsoft.Extensions.Options.Options.Create(new ReceiptStorageOptions { Directory = "receipts" }));
            var request = CreateRetentionRequest("expired-race", "MANUAL_REVIEW_ACCEPTED",
                ProcessingStates.Completed, true, DateTime.UtcNow.AddDays(-8), expired: true);
            byte[] bytes = [1, 2, 3];
            request.FileSha256 = Convert.ToHexString(SHA256.HashData(bytes));
            request.FileSizeBytes = bytes.Length;
            await store.PutIfAbsentAsync(request.StoredFileName, bytes, "image/jpeg",
                request.FileSha256, CancellationToken.None);
            await using (var seed = new AppDbContext(options))
            {
                seed.ReimbursementRequests.Add(request);
                await seed.SaveChangesAsync();
            }
            // Simulate an action loaded before expiry but reaching SQL during the purge.
            await using var staleAction = new AppDbContext(options);
            var stale = await staleAction.ReimbursementRequests.SingleAsync();
            stale.Status = "ESCALATE_FACT";
            stale.ManagerAnswer = null;
            stale.ManagerDecisionAt = null;
            stale.RetentionExpiresAt = null;
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var blockingStore = new BlockingDeleteReceiptFileStore(store, entered, release);
            await using var purgeDatabase = new AppDbContext(options);
            var purger = new ReceiptRetentionPurger(purgeDatabase, blockingStore,
                NullLogger<ReceiptRetentionPurger>.Instance);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var purge = purger.PurgeExpiredBatchAsync(2, timeout.Token);
            Task<int>? staleSave = null;
            try
            {
                await entered.Task.WaitAsync(timeout.Token);
                await using (var competingDatabase = new AppDbContext(options))
                {
                    var competingPurger = new ReceiptRetentionPurger(competingDatabase, store,
                        NullLogger<ReceiptRetentionPurger>.Instance);
                    var lockFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                        competingPurger.PurgeExpiredBatchAsync(2, timeout.Token));
                    Assert.Contains("cleanup lock", lockFailure.Message, StringComparison.Ordinal);
                }
                staleSave = staleAction.SaveChangesAsync(timeout.Token);
                await Task.Delay(250, timeout.Token);
                Assert.False(staleSave.IsCompleted);
            }
            finally
            {
                release.TrySetResult(true);
                Assert.Equal(1, await purge);
                if (staleSave is not null)
                    await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleSave);
            }
            await using var verify = new AppDbContext(options);
            Assert.Empty(await verify.ReimbursementRequests.ToListAsync());
            Assert.Null(await store.OpenReadAsync(request.StoredFileName, timeout.Token));
        });
    }

    [Fact]
    public async Task Verify_records_get_retention_deadlines_only_for_final_auto_approvals_without_owned_images()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var environment = new TestEnvironment(root);
            var imageRoot = Path.Combine(environment.WebRootPath, "test_data", "images");
            Directory.CreateDirectory(imageRoot);
            var fixtures = Enumerable.Range(0, 5).Select(index => new ExpectedResult
            {
                Id = $"SYNTHETIC-{index}", ImageName = $"synthetic-{index}.jpg",
                ExpectedStatus = index < 3 ? "AUTO_APPROVE" : index == 3 ? "ESCALATE_FACT" : "ESCALATE_PRIVACY",
                ClaimedAmount = 150_000
            }).ToList();
            foreach (var fixture in fixtures)
                await File.WriteAllBytesAsync(Path.Combine(imageRoot, fixture.ImageName), [1, 2, 3]);
            await File.WriteAllTextAsync(Path.Combine(environment.WebRootPath, "test_data", "expected-results.json"),
                System.Text.Json.JsonSerializer.Serialize(fixtures));
            var invoiceDate = DateTime.UtcNow.Date;
            while (invoiceDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                invoiceDate = invoiceDate.AddDays(-1);
            var calls = 0;
            var extractor = new StubExtractor(() => ++calls == 5
                ? throw new VisionExtractionException(ExternalImagePrivacyGate.ImageNotApprovedCode, "Synthetic privacy block.")
                : calls > 3 ? new ReceiptExtractionDto() : new()
            {
                EvidenceContractVersion = ReceiptExtractionDto.CurrentEvidenceContractVersion,
                DocumentType = "VAT_INVOICE", MerchantName = "Synthetic Taxi", TaxId = "0312345678",
                InvoiceNumber = "SYNTHETIC-ONLY", InvoiceDate = invoiceDate.ToString("yyyy-MM-dd"),
                InvoiceDateEvidence = invoiceDate.ToString("dd/MM/yyyy"), InvoiceTime = "09:00",
                Currency = "VND", TotalAmount = 150_000, TotalAmountSource = "PRINTED_FINAL_TOTAL",
                TotalAmountEvidence = "TOTAL 150.000 VND", Confidence = 0.98,
                LineItems = [new ReceiptLineItem { Description = "Business taxi trip", Amount = 150_000 }]
            });
            await using var database = new AppDbContext(options);
            var controller = new VerifyController(extractor, environment, new ReimbursementRepository(database),
                NullLogger<VerifyController>.Instance, new WorkflowOperationGate(), new VisionExecutionContext(),
                Microsoft.Extensions.Options.Options.Create(new VerifyOptions { Enabled = true, InterCaseDelayMs = 0 }));
            Assert.IsType<JsonResult>(await controller.RunHarness(CancellationToken.None));
            var records = await database.ReimbursementRequests.AsNoTracking().ToListAsync();
            Assert.Equal(5, records.Count);
            Assert.Equal(3, records.Count(request => request.Status == "AUTO_APPROVE"));
            Assert.Single(records, request => request.Status == "ESCALATE_FACT");
            var blocked = Assert.Single(records, request => request.Status == "ESCALATE_PRIVACY");
            Assert.Equal(ExternalImagePrivacyGate.ImageNotApprovedCode, blocked.ProviderErrorCode);
            Assert.Null(blocked.ExtractedFactsJson);
            foreach (var request in records)
            {
                Assert.Empty(request.StoredFileName);
                Assert.Equal(request.Status == "AUTO_APPROVE" ? request.ProcessingCompletedAt!.Value.AddDays(7)
                    : (DateTime?)null, request.RetentionExpiresAt);
            }
        });
    }

    [Fact]
    public async Task Enabled_retention_rejects_manager_undo_after_the_record_deadline()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, _) =>
        {
            var request = new ReimbursementRequest
            {
                Id = "retention-undo-expired",
                OriginalFileName = "synthetic.jpg",
                StoredFileName = "retention-undo-expired.jpg",
                ImageUrl = "private",
                ContentType = "image/jpeg",
                FileSha256 = new string('0', 64),
                FileSizeBytes = 1,
                Status = "APPROVED_POLICY_EXCEPTION",
                ProcessingState = ProcessingStates.Completed,
                IsForwardedToManager = true,
                ManagerAnswer = true,
                ManagerDecisionAt = DateTime.UtcNow.AddDays(-8),
                RetentionExpiresAt = DateTime.UtcNow.AddDays(-1)
            };

            await using var db = new AppDbContext(options);
            db.ReimbursementRequests.Add(request);
            db.AuditLogs.AddRange(
                new AuditLog
                {
                    RequestId = request.Id,
                    Action = "AI_PROCESSED_ESCALATE_POLICY",
                    Details = "synthetic",
                    Timestamp = DateTime.UtcNow.AddDays(-9)
                },
                new AuditLog
                {
                    RequestId = request.Id,
                    Action = "MANAGER_YES",
                    Details = "synthetic",
                    Timestamp = DateTime.UtcNow.AddDays(-8)
                });
            await db.SaveChangesAsync();

            var result = await CreateReviewerController(db, retentionEnabled: true)
                .EscalateAction(request.Id, "UNDO");

            Assert.IsType<ConflictObjectResult>(result);
            Assert.Contains("hết thời hạn lưu", ((ConflictObjectResult)result).Value?.ToString(),
                StringComparison.OrdinalIgnoreCase);
            Assert.Equal("APPROVED_POLICY_EXCEPTION",
                (await db.ReimbursementRequests.AsNoTracking().SingleAsync(item => item.Id == request.Id)).Status);
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
    public async Task Synthetic_database_and_receipt_backup_restore_preserves_pending_job_and_original_retention_deadline()
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
            var expired = CreateRetentionRequest("expired-restore", "AUTO_APPROVE",
                ProcessingStates.Completed, null, DateTime.UtcNow.AddDays(-8), expired: true);
            byte[] expiredBytes = [4, 5, 6];
            expired.FileSizeBytes = expiredBytes.Length;
            expired.FileSha256 = Convert.ToHexString(SHA256.HashData(expiredBytes));
            await File.WriteAllBytesAsync(Path.Combine(root, "receipts", expired.StoredFileName), expiredBytes);
            File.Copy(Path.Combine(root, "receipts", expired.StoredFileName),
                Path.Combine(receiptBackupDirectory, expired.StoredFileName));
            source.ReimbursementRequests.Add(expired);
            source.AuditLogs.Add(new AuditLog
            {
                RequestId = expired.Id, Action = "SYNTHETIC_EXPIRED_MARKER"
            });
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

                await using (var verifyBackup = master.CreateCommand())
                {
                    verifyBackup.CommandText = "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;";
                    verifyBackup.Parameters.AddWithValue("@path", backupPath);
                    await verifyBackup.ExecuteNonQueryAsync();
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
                Assert.Equal(2, await restored.ReimbursementRequests.CountAsync());
                Assert.Equal(1, await restored.AuditLogs.CountAsync(x =>
                    x.RequestId == id && x.Action == "SYNTHETIC_BACKUP_MARKER"));
                var restoredExpired = await restored.ReimbursementRequests.SingleAsync(x => x.Id == expired.Id);
                Assert.Equal(expired.RetentionExpiresAt, restoredExpired.RetentionExpiresAt);
                File.Copy(Path.Combine(receiptBackupDirectory, expired.StoredFileName),
                    Path.Combine(root, "restored-receipts", expired.StoredFileName));
                var restoredStore = new LocalReceiptFileStore(new TestEnvironment(root),
                    Microsoft.Extensions.Options.Options.Create(new ReceiptStorageOptions
                    {
                        Directory = "restored-receipts"
                    }));
                var purger = new ReceiptRetentionPurger(restored, restoredStore,
                    NullLogger<ReceiptRetentionPurger>.Instance);
                Assert.Equal(1, await purger.PurgeExpiredBatchAsync(25, CancellationToken.None));
                Assert.Equal(id, (await restored.ReimbursementRequests.AsNoTracking().SingleAsync()).Id);
                Assert.False(await restored.AuditLogs.AnyAsync(x => x.RequestId == expired.Id));
                Assert.False(File.Exists(Path.Combine(root, "restored-receipts", expired.StoredFileName)));
                Assert.True(File.Exists(restoredReceiptPath));
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
    public async Task Concurrent_duplicate_manager_undo_writes_one_state_change_and_audit()
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
                request.ManagerAnswer = true;
                request.ManagerDecisionAt = DateTime.UtcNow;
                await seed.SaveChangesAsync();
                seed.AuditLogs.Add(new AuditLog
                {
                    RequestId = id,
                    Action = "MANAGER_YES",
                    Details = "Synthetic prior decision",
                    Timestamp = DateTime.UtcNow
                });
                await seed.SaveChangesAsync();
            }

            await using var firstDatabase = new AppDbContext(options);
            await using var secondDatabase = new AppDbContext(options);
            var firstCopy = await firstDatabase.ReimbursementRequests.SingleAsync(item => item.Id == id);
            var secondCopy = await secondDatabase.ReimbursementRequests.SingleAsync(item => item.Id == id);
            foreach (var copy in new[] { firstCopy, secondCopy })
            {
                copy.Status = "ESCALATE_POLICY";
                copy.ManagerAnswer = null;
                copy.ManagerDecisionAt = null;
            }

            static async Task<string> PersistUndoAsync(AppDbContext database,
                ReimbursementRequest request)
            {
                try
                {
                    await new ReimbursementRepository(database).UpdateRequestWithAuditAsync(
                        request, "MANAGER_UNDO", "synthetic duplicate undo");
                    return "SAVED";
                }
                catch (DbUpdateConcurrencyException)
                {
                    return "CONFLICT";
                }
            }

            var results = await Task.WhenAll(
                PersistUndoAsync(firstDatabase, firstCopy),
                PersistUndoAsync(secondDatabase, secondCopy));

            Assert.Equal(1, results.Count(result => result == "SAVED"));
            Assert.Equal(1, results.Count(result => result == "CONFLICT"));
            await using var verify = new AppDbContext(options);
            var persisted = await verify.ReimbursementRequests.SingleAsync(item => item.Id == id);
            Assert.Equal("ESCALATE_POLICY", persisted.Status);
            Assert.Null(persisted.ManagerAnswer);
            Assert.Equal(1, await verify.AuditLogs.CountAsync(item => item.RequestId == id &&
                item.Action == "MANAGER_UNDO"));
        });
    }

    [Fact]
    public async Task Concurrent_bulk_forward_writes_one_handoff_audit_per_request()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var id = await SeedAsync(options, root);
            await using (var seed = new AppDbContext(options))
            {
                var request = await seed.ReimbursementRequests.SingleAsync(item => item.Id == id);
                request.Status = "ESCALATE_POLICY";
                request.ManagerQuestion = "Synthetic bulk handoff question";
                await seed.SaveChangesAsync();
            }

            await using var firstDatabase = new AppDbContext(options);
            await using var secondDatabase = new AppDbContext(options);
            var firstCopy = (await new ReimbursementRepository(firstDatabase).GetAllRequestsAsync())
                .Single(item => item.Id == id);
            var secondCopy = (await new ReimbursementRepository(secondDatabase).GetAllRequestsAsync())
                .Single(item => item.Id == id);
            firstCopy.IsForwardedToManager = true;
            firstCopy.ForwardedAt = DateTime.UtcNow;
            secondCopy.IsForwardedToManager = true;
            secondCopy.ForwardedAt = DateTime.UtcNow;

            static async Task<string> PersistBulkForwardAsync(AppDbContext database,
                ReimbursementRequest request)
            {
                try
                {
                    await new ReimbursementRepository(database).UpdateRequestsWithAuditAsync(
                        [(request, $"Mode=BULK; Question={request.ManagerQuestion}; Status={request.Status}")],
                        "EMPLOYEE_FORWARDED_TO_MANAGER");
                    return "SAVED";
                }
                catch (DbUpdateConcurrencyException)
                {
                    return "CONFLICT";
                }
            }

            var results = await Task.WhenAll(
                PersistBulkForwardAsync(firstDatabase, firstCopy),
                PersistBulkForwardAsync(secondDatabase, secondCopy));

            Assert.Equal(1, results.Count(result => result == "SAVED"));
            Assert.Equal(1, results.Count(result => result == "CONFLICT"));
            await using var verify = new AppDbContext(options);
            var persisted = await verify.ReimbursementRequests.SingleAsync(item => item.Id == id);
            Assert.True(persisted.IsForwardedToManager);
            Assert.NotNull(persisted.ForwardedAt);
            Assert.Equal(1, await verify.AuditLogs.CountAsync(item => item.RequestId == id &&
                item.Action == "EMPLOYEE_FORWARDED_TO_MANAGER"));
        });
    }

    [Fact]
    public async Task Manager_decision_retry_after_committed_response_loss_is_replayed_without_duplicate_audit()
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

            await using (var firstDatabase = new AppDbContext(options))
            {
                var firstResult = await CreateReviewerController(firstDatabase)
                    .EscalateAction(id, "YES");
                Assert.IsType<JsonResult>(firstResult);
            }

            await using (var retryDatabase = new AppDbContext(options))
            {
                var retryResult = await CreateReviewerController(retryDatabase)
                    .EscalateAction(id, "YES");
                var payload = ReadJsonResult(retryResult);
                Assert.True(payload.GetProperty("alreadyApplied").GetBoolean());
                Assert.Equal("APPROVED_POLICY_EXCEPTION", payload.GetProperty("status").GetString());
            }

            await using var verify = new AppDbContext(options);
            Assert.Equal(1, await verify.AuditLogs.CountAsync(item => item.RequestId == id &&
                item.Action == "MANAGER_YES"));
        });
    }

    [Fact]
    public async Task Manager_undo_retry_after_committed_response_loss_is_replayed_without_duplicate_audit()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, root) =>
        {
            var id = await SeedAsync(options, root);
            await using (var seed = new AppDbContext(options))
            {
                var request = await seed.ReimbursementRequests.SingleAsync(item => item.Id == id);
                request.Status = "APPROVED_POLICY_EXCEPTION";
                request.IsForwardedToManager = true;
                request.ManagerQuestion = "Synthetic manager question";
                request.ManagerAnswer = true;
                request.ManagerDecisionAt = DateTime.UtcNow;
                await seed.SaveChangesAsync();
                seed.AuditLogs.AddRange(
                    new AuditLog { RequestId = id, Action = "AI_PROCESSED_ESCALATE_POLICY",
                        Details = "Synthetic original escalation", Timestamp = DateTime.UtcNow.AddMinutes(-2) },
                    new AuditLog { RequestId = id, Action = "MANAGER_YES",
                        Details = "Synthetic manager decision", Timestamp = DateTime.UtcNow.AddMinutes(-1) });
                await seed.SaveChangesAsync();
            }

            await using (var firstDatabase = new AppDbContext(options))
            {
                var firstResult = await CreateReviewerController(firstDatabase)
                    .EscalateAction(id, "UNDO");
                Assert.IsType<JsonResult>(firstResult);
            }

            await using (var retryDatabase = new AppDbContext(options))
            {
                var retryResult = await CreateReviewerController(retryDatabase)
                    .EscalateAction(id, "UNDO");
                var payload = ReadJsonResult(retryResult);
                Assert.True(payload.GetProperty("alreadyApplied").GetBoolean());
                Assert.Equal("ESCALATE_POLICY", payload.GetProperty("status").GetString());
            }

            await using var verify = new AppDbContext(options);
            Assert.Equal(1, await verify.AuditLogs.CountAsync(item => item.RequestId == id &&
                item.Action == "MANAGER_UNDO"));
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

    private static ReimbursementRequest CreateRetentionRequest(string id, string status,
        string processingState, bool? managerAnswer, DateTime? finalizedAt, bool expired)
    {
        var now = DateTime.UtcNow;
        return new ReimbursementRequest
        {
            Id = id,
            OriginalFileName = id + ".jpg",
            StoredFileName = id + ".jpg",
            ImageUrl = "private",
            ContentType = "image/jpeg",
            FileSha256 = new string('0', 64),
            FileSizeBytes = 0,
            Status = status,
            ProcessingState = processingState,
            ProcessingCompletedAt = processingState is ProcessingStates.Completed or ProcessingStates.Failed
                ? finalizedAt : null,
            ManagerAnswer = managerAnswer,
            ManagerDecisionAt = managerAnswer is null ? null : finalizedAt,
            RetentionExpiresAt = expired ? now.AddDays(-1) : now.AddDays(1)
        };
    }

    private sealed class BlockingDeleteReceiptFileStore(IReceiptFileStore inner,
        TaskCompletionSource<bool> entered, TaskCompletionSource<bool> release) : IReceiptFileStore
    {
        public Task<bool> PutIfAbsentAsync(string storedFileName, ReadOnlyMemory<byte> bytes,
            string contentType, string sha256, CancellationToken cancellationToken) =>
            inner.PutIfAbsentAsync(storedFileName, bytes, contentType, sha256, cancellationToken);

        public Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken cancellationToken) =>
            inner.OpenReadAsync(storedFileName, cancellationToken);

        public async Task DeleteAsync(string storedFileName, CancellationToken cancellationToken)
        {
            entered.TrySetResult(true);
            await release.Task.WaitAsync(cancellationToken);
            await inner.DeleteAsync(storedFileName, cancellationToken);
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
            inner.IsAvailableAsync(cancellationToken);
    }

    private sealed class RejectUnexpectedHttpHandler : HttpMessageHandler
    {
        public int Count { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            throw new InvalidOperationException("A blocked synthetic image must not reach HTTP.");
        }
    }

    private sealed class DeleteThenFailOnceReceiptFileStore(IReceiptFileStore inner) : IReceiptFileStore
    {
        private bool _shouldFail = true;

        public Task<bool> PutIfAbsentAsync(string storedFileName, ReadOnlyMemory<byte> bytes,
            string contentType, string sha256, CancellationToken cancellationToken) =>
            inner.PutIfAbsentAsync(storedFileName, bytes, contentType, sha256, cancellationToken);

        public Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken cancellationToken) =>
            inner.OpenReadAsync(storedFileName, cancellationToken);

        public async Task DeleteAsync(string storedFileName, CancellationToken cancellationToken)
        {
            await inner.DeleteAsync(storedFileName, cancellationToken);
            if (!_shouldFail) return;
            _shouldFail = false;
            throw new IOException("Synthetic failure after idempotent image deletion.");
        }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
            inner.IsAvailableAsync(cancellationToken);
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

    private static ReviewerController CreateReviewerController(AppDbContext database,
        bool retentionEnabled = false)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        return new ReviewerController(new ReimbursementRepository(database), new AuditLogger(database),
            Microsoft.Extensions.Options.Options.Create(new ReceiptRetentionOptions
            {
                Enabled = retentionEnabled
            }))
        {
            ControllerContext = new ControllerContext(actionContext),
            Url = new TestUrlHelper(actionContext),
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
    }

    private static System.Text.Json.JsonElement ReadJsonResult(IActionResult result)
    {
        var json = Assert.IsType<JsonResult>(result);
        return System.Text.Json.JsonSerializer.SerializeToElement(json.Value);
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestUrlHelper(ActionContext actionContext) : IUrlHelper
    {
        public ActionContext ActionContext { get; } = actionContext;
        public string? Action(UrlActionContext actionContext) => null;
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => url?.StartsWith('/') == true;
        public string? Link(string? routeName, object? values) => null;
        public string? RouteUrl(UrlRouteContext routeContext) => null;
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
