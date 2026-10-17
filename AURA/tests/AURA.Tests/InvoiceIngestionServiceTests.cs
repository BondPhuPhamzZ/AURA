using AURA.Data;
using AURA.Models;
using AURA.Options;
using AURA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

namespace AURA.Tests;

public sealed class InvoiceIngestionServiceTests
{
    [Fact]
    public async Task Sandbox_replay_returns_the_existing_request_without_creating_a_second_job()
    {
        await using var fixture = await IngestionFixture.CreateAsync();
        var command = fixture.ValidMailboxCommand("message-1", "attachment-1");

        var first = await fixture.Service.IngestAsync(command);
        var replay = await fixture.Service.IngestAsync(command);

        Assert.Equal(InvoiceIngestionOutcomes.Accepted, first.Outcome);
        Assert.Equal(InvoiceIngestionOutcomes.Duplicate, replay.Outcome);
        Assert.Equal(first.RequestId, replay.RequestId);
        Assert.Equal(1, await fixture.Db.ReimbursementRequests.CountAsync());
        Assert.Equal(1, await fixture.Db.EmailMessages.CountAsync());
        Assert.Equal(1, await fixture.Db.EmailAttachments.CountAsync());
        Assert.Equal(1, await fixture.Db.IngestionOutboxEvents.CountAsync());
        Assert.Equal(1, await fixture.Db.AuditLogs.CountAsync());
        Assert.Equal("message-1", (await fixture.Db.MailboxSyncCursors.SingleAsync()).Cursor);
        Assert.Equal(InvoiceSourceTypes.SandboxMailbox,
            (await fixture.Db.ReimbursementRequests.SingleAsync()).SourceType);
    }

    [Fact]
    public async Task Invalid_mail_attachment_is_quarantined_without_creating_an_ai_job()
    {
        await using var fixture = await IngestionFixture.CreateAsync();
        var command = fixture.ValidMailboxCommand("message-bad", "attachment-bad") with
        {
            Bytes = [0x01, 0x02, 0x03]
        };

        var result = await fixture.Service.IngestAsync(command);

        Assert.Equal(InvoiceIngestionOutcomes.Quarantined, result.Outcome);
        Assert.Equal("SIGNATURE_MISMATCH", result.ErrorCode);
        Assert.Empty(await fixture.Db.ReimbursementRequests.ToListAsync());
        Assert.Empty(await fixture.Db.IngestionOutboxEvents.ToListAsync());
        var attachment = await fixture.Db.EmailAttachments.SingleAsync();
        Assert.Equal(AttachmentQuarantineStates.Quarantined, attachment.QuarantineStatus);
        Assert.Null(attachment.ReimbursementRequestId);
    }

    [Fact]
    public async Task Manual_and_mailbox_sources_share_the_same_durable_intake_boundary()
    {
        await using var fixture = await IngestionFixture.CreateAsync();

        var manual = await fixture.Service.IngestAsync(new InvoiceIngestionCommand(
            InvoiceSourceTypes.ManualUpload, "manual.jpg", "image/jpeg", fixture.ValidJpeg));
        var mailbox = await fixture.Service.IngestAsync(fixture.ValidMailboxCommand("message-2", "attachment-2"));

        Assert.Equal(InvoiceIngestionOutcomes.Accepted, manual.Outcome);
        Assert.Equal(InvoiceIngestionOutcomes.Accepted, mailbox.Outcome);
        var sources = await fixture.Db.ReimbursementRequests.OrderBy(item => item.SourceType)
            .Select(item => item.SourceType).ToArrayAsync();
        Assert.Equal([InvoiceSourceTypes.ManualUpload, InvoiceSourceTypes.SandboxMailbox], sources);
        Assert.Equal(2, await fixture.Db.IngestionOutboxEvents.CountAsync());
    }

    [Fact]
    public async Task Requiring_an_unavailable_production_scanner_fails_closed_into_quarantine()
    {
        await using var fixture = await IngestionFixture.CreateAsync(requireMalwareScanner: true);

        var result = await fixture.Service.IngestAsync(
            fixture.ValidMailboxCommand("message-scan", "attachment-scan"));

        Assert.Equal(InvoiceIngestionOutcomes.Quarantined, result.Outcome);
        Assert.Equal("MALWARE_SCANNER_UNAVAILABLE", result.ErrorCode);
        Assert.Empty(await fixture.Db.ReimbursementRequests.ToListAsync());
        Assert.Empty(await fixture.Db.IngestionOutboxEvents.ToListAsync());
    }

    private sealed class IngestionFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly string _root;

        private IngestionFixture(SqliteConnection connection, AppDbContext db,
            InvoiceIngestionService service, string root)
        {
            _connection = connection;
            Db = db;
            Service = service;
            _root = root;
        }

        public AppDbContext Db { get; }
        public InvoiceIngestionService Service { get; }
        public byte[] ValidJpeg { get; } = [0xFF, 0xD8, 0xFF, 0x01, 0x02];

        public static async Task<IngestionFixture> CreateAsync(bool requireMalwareScanner = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();

            var root = Path.Combine(Path.GetTempPath(), "aura-mailbox-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var storage = Microsoft.Extensions.Options.Options.Create(new ReceiptStorageOptions
            {
                Directory = "receipts",
                MaxFileSizeMb = 5
            });
            var policy = Microsoft.Extensions.Options.Options.Create(
                new DecisionPolicyOptions { EscalateDuplicateReceipts = false });
            var mailbox = Microsoft.Extensions.Options.Options.Create(new MailboxSandboxOptions
            {
                Enabled = true,
                Provider = "SANDBOX",
                MailboxId = "mailbox-test",
                AllowedFolder = "AURA_INVOICE",
                AllowedSenders = ["demo.sender@example.test"],
                ApiKey = "0123456789abcdefghijklmn",
                RequireMalwareScanner = requireMalwareScanner
            });
            var environment = new TestEnvironment { ContentRootPath = root };
            var service = new InvoiceIngestionService(db, storage, policy, mailbox, environment);
            return new IngestionFixture(connection, db, service, root);
        }

        public InvoiceIngestionCommand ValidMailboxCommand(string messageId, string attachmentId) => new(
            InvoiceSourceTypes.SandboxMailbox,
            "receipt.jpg",
            "image/jpeg",
            ValidJpeg,
            SubmitterCode: "MAIL-SANDBOX",
            SubmitterDisplayName: "Synthetic sender",
            SubmitterDepartment: "Mailbox sandbox",
            SourceReference: MailboxSandboxGuard.BuildSourceKey("SANDBOX", "mailbox-test", messageId, attachmentId),
            ExternalMessageId: messageId,
            ExternalThreadId: "thread-1",
            ExternalAttachmentId: attachmentId,
            Sender: "demo.sender@example.test",
            Subject: "Synthetic receipt",
            SourceFolder: "AURA_INVOICE",
            ReceivedAt: DateTimeOffset.UtcNow);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
            var resolved = Path.GetFullPath(_root);
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            if (resolved.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved))
                Directory.Delete(resolved, recursive: true);
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AURA.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
