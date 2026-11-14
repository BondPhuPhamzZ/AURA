using AURA.Data;
using AURA.Models;
using AURA.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AURA.Tests;

/// <summary>
/// Screen queries must filter in SQL Server, not after loading every receipt. Runs only with
/// AURA_RUN_LOCALDB_INTEGRATION=1 and uses a throw-away LocalDB database.
/// </summary>
public sealed class ReimbursementRepositoryQueryLocalDbTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Queue_queries_return_only_their_rows_in_reviewer_order()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, sql) =>
        {
            await SeedAsync(options,
                Request("forwarded-new", "ESCALATE_FACT", forwardedAt: Now.AddHours(-1)),
                Request("forwarded-old", "ESCALATE_POLICY", forwardedAt: Now.AddHours(-30)),
                Request("forwarded-legacy", "ESCALATE_AUTHORITY", forwarded: true),
                Request("decided", "MANUAL_REVIEW_ACCEPTED", forwardedAt: Now.AddHours(-2)),
                Request("handoff", "ESCALATE_SYSTEM_ERROR"),
                Request("auto", "AUTO_APPROVE"),
                // "_" is a LIKE wildcard; this status must not match the ESCALATE_ prefix.
                Request("wildcard", "ESCALATEX", forwardedAt: Now.AddHours(-3)),
                Request("drive-recent", "AUTO_APPROVE", drive: true, createdAt: Now.AddHours(-2)),
                Request("drive-old", "AUTO_APPROVE", drive: true, createdAt: Now.AddDays(-3)));

            await using var db = new AppDbContext(options);
            var repository = new ReimbursementRepository(db);

            Assert.Equal(new[] { "forwarded-old", "forwarded-new", "forwarded-legacy" },
                (await repository.GetManagerQueueAsync()).Select(x => x.Id));
            Assert.Equal(new[] { "handoff" },
                (await repository.GetPendingHandoffsAsync()).Select(x => x.Id));
            Assert.Equal(new[] { "drive-recent" },
                (await repository.GetRecentDriveRequestsAsync(Now.AddDays(-1), 20)).Select(x => x.Id));
            Assert.DoesNotContain(sql, command => command.Contains("OPENJSON", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public async Task Audit_and_period_queries_use_sql_ranges()
    {
        if (Environment.GetEnvironmentVariable("AURA_RUN_LOCALDB_INTEGRATION") != "1") return;
        await WithIsolatedDatabase(async (options, sql) =>
        {
            // November 2026 in Vietnam is [31/10 17:00 UTC, 30/11 17:00 UTC).
            var (novemberStartUtc, decemberStartUtc) = ReportingPeriod.MonthUtcRange(2026, 11);
            await SeedAsync(options,
                Request("october-edge", "AUTO_APPROVE", createdAt: novemberStartUtc.AddMinutes(-30)),
                Request("november-start", "AUTO_APPROVE", createdAt: novemberStartUtc),
                Request("november-late", "AUTO_APPROVE", createdAt: decemberStartUtc.AddTicks(-1)),
                Request("december", "AUTO_APPROVE", createdAt: decemberStartUtc));
            await using (var seed = new AppDbContext(options))
            {
                seed.AuditLogs.AddRange(
                    new AuditLog { RequestId = "october-edge", Action = "AI_QUEUED", Timestamp = Now.AddDays(-20) },
                    new AuditLog { RequestId = "november-start", Action = "AI_QUEUED", Timestamp = Now.AddMinutes(-5) },
                    new AuditLog { RequestId = "december", Action = "AI_QUEUED", Timestamp = Now });
                await seed.SaveChangesAsync();
            }

            await using var db = new AppDbContext(options);
            var repository = new ReimbursementRepository(db);
            Assert.Equal(new[] { "november-late", "november-start" },
                (await repository.GetRequestsCreatedBetweenAsync(novemberStartUtc, decemberStartUtc))
                    .Select(x => x.Id));
            Assert.Equal(new[] { "december", "november-start" },
                (await repository.GetRequestsWithAuditSinceAsync(Now.AddMinutes(-10)))
                    .Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal));
            Assert.DoesNotContain(sql, command => command.Contains("OPENJSON", StringComparison.OrdinalIgnoreCase));
        });
    }

    private static ReimbursementRequest Request(string id, string status, DateTime? forwardedAt = null,
        bool forwarded = false, bool drive = false, DateTime? createdAt = null) => new()
    {
        Id = id,
        OriginalFileName = id + ".jpg",
        StoredFileName = id + ".jpg",
        ImageUrl = "private",
        ContentType = "image/jpeg",
        FileSha256 = new string('0', 64),
        FileSizeBytes = 3,
        Status = status,
        ProcessingState = ProcessingStates.Completed,
        IsForwardedToManager = forwarded || forwardedAt is not null,
        ForwardedAt = forwardedAt,
        SubmitterCode = drive ? DriveSandboxWorker.SubmitterCode : "EMP-001",
        CreatedAt = createdAt ?? Now.AddDays(-1),
        QueuedAt = createdAt ?? Now.AddDays(-1)
    };

    private static async Task SeedAsync(DbContextOptions<AppDbContext> options, params ReimbursementRequest[] items)
    {
        await using var db = new AppDbContext(options);
        db.ReimbursementRequests.AddRange(items);
        await db.SaveChangesAsync();
    }

    private static async Task WithIsolatedDatabase(Func<DbContextOptions<AppDbContext>, List<string>, Task> test)
    {
        var name = "AuraRepositoryQueryIT_" + Guid.NewGuid().ToString("N");
        var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;TrustServerCertificate=True";
        var sql = new List<string>();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection)
            .LogTo(message => { lock (sql) sql.Add(message); },
                new[] { Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted })
            .Options;
        var created = false;
        try
        {
            await using (var db = new AppDbContext(options))
                created = await db.Database.EnsureCreatedAsync();
            Assert.True(created);
            await test(options, sql);
        }
        finally
        {
            if (created)
            {
                await using var db = new AppDbContext(options);
                await db.Database.EnsureDeletedAsync();
            }
        }
    }
}
