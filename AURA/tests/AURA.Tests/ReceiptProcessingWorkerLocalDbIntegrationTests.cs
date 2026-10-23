using System.Reflection;
using AURA.Data;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using AURA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptProcessingWorkerLocalDbIntegrationTests
{
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
        await File.WriteAllBytesAsync(Path.Combine(root, "receipts", storedFileName), [1, 2, 3]);
        var item = new ReimbursementRequest
        {
            Id = Guid.NewGuid().ToString("N"),
            OriginalFileName = storedFileName,
            StoredFileName = storedFileName,
            ImageUrl = "private",
            ContentType = "image/jpeg",
            FileSha256 = new string('a', 64),
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
