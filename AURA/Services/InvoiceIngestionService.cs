using System.Security.Cryptography;
using System.Text.Json;
using AURA.Data;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed class InvoiceIngestionService : IInvoiceIngestionService
{
    private readonly AppDbContext _db;
    private readonly ReceiptStorageOptions _storageOptions;
    private readonly DecisionPolicyOptions _decisionOptions;
    private readonly MailboxSandboxOptions _mailboxOptions;
    private readonly IWebHostEnvironment _environment;

    public InvoiceIngestionService(AppDbContext db,
        IOptions<ReceiptStorageOptions> storageOptions,
        IOptions<DecisionPolicyOptions> decisionOptions,
        IOptions<MailboxSandboxOptions> mailboxOptions,
        IWebHostEnvironment environment)
    {
        _db = db;
        _storageOptions = storageOptions.Value;
        _decisionOptions = decisionOptions.Value;
        _mailboxOptions = mailboxOptions.Value;
        _environment = environment;
    }

    public async Task<InvoiceIngestionResult> IngestAsync(InvoiceIngestionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Source is not InvoiceSourceTypes.ManualUpload and not InvoiceSourceTypes.SandboxMailbox)
        {
            throw new ArgumentOutOfRangeException(nameof(command), command.Source,
                "Nguồn tiếp nhận hóa đơn không được hỗ trợ.");
        }

        var validation = ReceiptIntakeValidator.Validate(command.OriginalFileName, command.ContentType,
            command.Bytes, command.ClaimedAmount, _storageOptions);

        if (command.Source == InvoiceSourceTypes.SandboxMailbox)
        {
            ValidateMailboxCommand(command);
            var existing = await FindExistingMailboxAttachmentAsync(command, cancellationToken);
            if (existing is not null)
            {
                return new InvoiceIngestionResult(InvoiceIngestionOutcomes.Duplicate,
                    existing.ReimbursementRequestId, existing.QuarantineReasonCode,
                    "Attachment đã được tiếp nhận trước đó; không tạo thêm AI job.", true);
            }

            var attachmentCount = await CountMessageAttachmentsAsync(command, cancellationToken);
            if (attachmentCount >= _mailboxOptions.MaxAttachmentsPerMessage)
                throw new MailboxIngestionRejectedException("ATTACHMENT_LIMIT_EXCEEDED",
                    $"Mỗi email sandbox chỉ nhận tối đa {_mailboxOptions.MaxAttachmentsPerMessage} attachment.");

            if (!validation.IsValid)
                return await PersistQuarantineAsync(command, validation, cancellationToken);

            if (_mailboxOptions.RequireMalwareScanner)
            {
                return await PersistQuarantineAsync(command,
                    new ReceiptIntakeValidationResult(false, "MALWARE_SCANNER_UNAVAILABLE",
                        "Sandbox không có malware scanner production; attachment được giữ ở quarantine.", null, null),
                    cancellationToken);
            }
        }
        else if (!validation.IsValid)
        {
            return new InvoiceIngestionResult(InvoiceIngestionOutcomes.Rejected, null,
                validation.ErrorCode, validation.Error ?? "File không hợp lệ.", false);
        }

        var requestId = Guid.NewGuid().ToString("N");
        var storedFileName = requestId + validation.NormalizedExtension;
        var storageRoot = GetStorageRoot();
        Directory.CreateDirectory(storageRoot);
        var physicalPath = Path.Combine(storageRoot, storedFileName);
        await File.WriteAllBytesAsync(physicalPath, command.Bytes, cancellationToken);

        try
        {
            var sha256 = Convert.ToHexString(SHA256.HashData(command.Bytes));
            var duplicateByContent = await _db.ReimbursementRequests
                .AnyAsync(item => item.FileSha256 == sha256, cancellationToken);
            var now = DateTime.UtcNow;
            var request = new ReimbursementRequest
            {
                Id = requestId,
                ClaimedAmount = command.ClaimedAmount,
                OriginalFileName = NormalizeFileName(command.OriginalFileName),
                StoredFileName = storedFileName,
                ContentType = validation.NormalizedContentType!,
                FileSizeBytes = command.Bytes.LongLength,
                FileSha256 = sha256,
                ImageUrl = $"/Applicant/Receipt/{requestId}",
                CreatedAt = now,
                QueuedAt = now,
                ProcessingState = ProcessingStates.Pending,
                Status = ProcessingStates.Pending,
                DuplicateDetected = duplicateByContent,
                DuplicatePolicyEnabled = _decisionOptions.EscalateDuplicateReceipts,
                SubmitterCode = NormalizeIdentity(command.SubmitterCode, "DEMO-EMPLOYEE", 100),
                SubmitterDisplayName = NormalizeIdentity(command.SubmitterDisplayName, "Nhân viên demo", 200),
                SubmitterDepartment = NormalizeIdentity(command.SubmitterDepartment, "Demo", 200),
                SourceType = command.Source,
                SourceReference = command.SourceReference
            };

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            _db.ReimbursementRequests.Add(request);
            _db.AuditLogs.Add(new AuditLog
            {
                RequestId = request.Id,
                Action = "AI_QUEUED",
                Timestamp = now,
                Details = BuildAuditDetails(request, command)
            });
            _db.IngestionOutboxEvents.Add(new IngestionOutboxEvent
            {
                EventType = IngestionEventTypes.ReceiptQueued,
                AggregateId = request.Id,
                PayloadJson = JsonSerializer.Serialize(new { requestId = request.Id, source = command.Source }),
                OccurredAt = now
            });

            if (command.Source == InvoiceSourceTypes.SandboxMailbox)
            {
                var message = await GetOrCreateMessageAsync(command, MailboxMessageStates.Queued, now,
                    cancellationToken);
                _db.EmailAttachments.Add(new EmailAttachment
                {
                    EmailMessageId = message.Id,
                    ExternalAttachmentId = command.ExternalAttachmentId!,
                    OriginalFileName = request.OriginalFileName,
                    ContentType = request.ContentType,
                    FileSizeBytes = request.FileSizeBytes,
                    FileSha256 = sha256,
                    QuarantineStatus = AttachmentQuarantineStates.Released,
                    ReimbursementRequestId = request.Id,
                    CreatedAt = now
                });
                await UpdateCursorAsync(command, now, cancellationToken);
            }

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException) when (command.Source == InvoiceSourceTypes.SandboxMailbox)
            {
                await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                File.Delete(physicalPath);
                var existing = await FindExistingMailboxAttachmentAsync(command, cancellationToken);
                if (existing is not null)
                {
                    return new InvoiceIngestionResult(InvoiceIngestionOutcomes.Duplicate,
                        existing.ReimbursementRequestId, existing.QuarantineReasonCode,
                        "Attachment đã được tiếp nhận đồng thời; không tạo thêm AI job.", true);
                }
                throw;
            }

            return new InvoiceIngestionResult(InvoiceIngestionOutcomes.Accepted, request.Id, null,
                "Hồ sơ đã được ghi bền vào SQL queue.", duplicateByContent);
        }
        catch
        {
            if (File.Exists(physicalPath)) File.Delete(physicalPath);
            throw;
        }
    }

    private async Task<InvoiceIngestionResult> PersistQuarantineAsync(InvoiceIngestionCommand command,
        ReceiptIntakeValidationResult validation, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var message = await GetOrCreateMessageAsync(command, MailboxMessageStates.Quarantined, now,
            cancellationToken);
        _db.EmailAttachments.Add(new EmailAttachment
        {
            EmailMessageId = message.Id,
            ExternalAttachmentId = command.ExternalAttachmentId!,
            OriginalFileName = NormalizeFileName(command.OriginalFileName),
            ContentType = Normalize(command.ContentType, "application/octet-stream", 100),
            FileSizeBytes = command.Bytes.LongLength,
            FileSha256 = Convert.ToHexString(SHA256.HashData(command.Bytes)),
            QuarantineStatus = AttachmentQuarantineStates.Quarantined,
            QuarantineReasonCode = validation.ErrorCode,
            QuarantineReason = validation.Error,
            CreatedAt = now
        });
        await UpdateCursorAsync(command, now, cancellationToken);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            var existing = await FindExistingMailboxAttachmentAsync(command, cancellationToken);
            if (existing is not null)
            {
                return new InvoiceIngestionResult(InvoiceIngestionOutcomes.Duplicate,
                    existing.ReimbursementRequestId, existing.QuarantineReasonCode,
                    "Attachment quarantine đã được ghi nhận trước đó.", true);
            }
            throw;
        }

        return new InvoiceIngestionResult(InvoiceIngestionOutcomes.Quarantined, null,
            validation.ErrorCode, validation.Error ?? "Attachment bị giữ ở quarantine.", false);
    }

    private async Task<EmailMessage> GetOrCreateMessageAsync(InvoiceIngestionCommand command, string status,
        DateTime now, CancellationToken cancellationToken)
    {
        var connection = await GetOrCreateConnectionAsync(now, cancellationToken);
        var message = await _db.EmailMessages.FirstOrDefaultAsync(item =>
            item.MailboxConnectionId == connection.Id &&
            item.ExternalMessageId == command.ExternalMessageId, cancellationToken);
        if (message is not null)
        {
            if (status == MailboxMessageStates.Queued) message.Status = status;
            return message;
        }

        message = new EmailMessage
        {
            MailboxConnectionId = connection.Id,
            ExternalMessageId = command.ExternalMessageId!,
            ExternalThreadId = command.ExternalThreadId,
            Sender = Normalize(command.Sender, "unknown@example.invalid", 320),
            Subject = Normalize(command.Subject, string.Empty, 500),
            SourceFolder = Normalize(command.SourceFolder, _mailboxOptions.AllowedFolder, 200),
            Status = status,
            ReceivedAt = command.ReceivedAt?.UtcDateTime ?? now,
            CreatedAt = now
        };
        _db.EmailMessages.Add(message);
        return message;
    }

    private async Task<MailboxConnection> GetOrCreateConnectionAsync(DateTime now,
        CancellationToken cancellationToken)
    {
        var connection = _db.MailboxConnections.Local.FirstOrDefault(item =>
            item.Provider == _mailboxOptions.Provider &&
            item.ExternalMailboxId == _mailboxOptions.MailboxId);
        connection ??= await _db.MailboxConnections.FirstOrDefaultAsync(item =>
            item.Provider == _mailboxOptions.Provider &&
            item.ExternalMailboxId == _mailboxOptions.MailboxId, cancellationToken);
        if (connection is not null)
        {
            connection.Enabled = _mailboxOptions.Enabled;
            connection.LastSyncAt = now;
            return connection;
        }

        connection = new MailboxConnection
        {
            Provider = _mailboxOptions.Provider,
            ExternalMailboxId = _mailboxOptions.MailboxId,
            AllowedFolder = _mailboxOptions.AllowedFolder,
            Enabled = _mailboxOptions.Enabled,
            CreatedAt = now,
            LastSyncAt = now
        };
        _db.MailboxConnections.Add(connection);
        return connection;
    }

    private async Task UpdateCursorAsync(InvoiceIngestionCommand command, DateTime now,
        CancellationToken cancellationToken)
    {
        var connection = await GetOrCreateConnectionAsync(now, cancellationToken);
        var cursor = await _db.MailboxSyncCursors.FindAsync([connection.Id], cancellationToken);
        if (cursor is null)
        {
            cursor = new MailboxSyncCursor { MailboxConnectionId = connection.Id };
            _db.MailboxSyncCursors.Add(cursor);
        }

        cursor.Cursor = Normalize(command.ExternalMessageId, string.Empty, 500);
        cursor.LastSuccessfulSyncAt = now;
        cursor.ConsecutiveFailureCount = 0;
        cursor.LastErrorCode = null;
        cursor.UpdatedAt = now;
    }

    private async Task<EmailAttachment?> FindExistingMailboxAttachmentAsync(InvoiceIngestionCommand command,
        CancellationToken cancellationToken)
    {
        return await (from attachment in _db.EmailAttachments.AsNoTracking()
                      join message in _db.EmailMessages.AsNoTracking()
                          on attachment.EmailMessageId equals message.Id
                      join connection in _db.MailboxConnections.AsNoTracking()
                          on message.MailboxConnectionId equals connection.Id
                      where connection.Provider == _mailboxOptions.Provider &&
                            connection.ExternalMailboxId == _mailboxOptions.MailboxId &&
                            message.ExternalMessageId == command.ExternalMessageId &&
                            attachment.ExternalAttachmentId == command.ExternalAttachmentId
                      select attachment).FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<int> CountMessageAttachmentsAsync(InvoiceIngestionCommand command,
        CancellationToken cancellationToken)
    {
        return await (from attachment in _db.EmailAttachments.AsNoTracking()
                      join message in _db.EmailMessages.AsNoTracking()
                          on attachment.EmailMessageId equals message.Id
                      join connection in _db.MailboxConnections.AsNoTracking()
                          on message.MailboxConnectionId equals connection.Id
                      where connection.Provider == _mailboxOptions.Provider &&
                            connection.ExternalMailboxId == _mailboxOptions.MailboxId &&
                            message.ExternalMessageId == command.ExternalMessageId
                      select attachment.Id).CountAsync(cancellationToken);
    }

    private void ValidateMailboxCommand(InvoiceIngestionCommand command)
    {
        if (!_mailboxOptions.Enabled)
            throw new InvalidOperationException("Mailbox sandbox đang tắt.");
        if (!MailboxSandboxGuard.IsSenderAllowed(command.Sender, _mailboxOptions))
            throw new MailboxIngestionRejectedException("SENDER_NOT_ALLOWED",
                "Người gửi không nằm trong allow-list của mailbox sandbox.");
        if (!MailboxSandboxGuard.IsFolderAllowed(command.SourceFolder, _mailboxOptions))
            throw new MailboxIngestionRejectedException("FOLDER_NOT_ALLOWED",
                "Thư không nằm trong folder được phép.");
        if (string.IsNullOrWhiteSpace(command.ExternalMessageId) ||
            string.IsNullOrWhiteSpace(command.ExternalAttachmentId))
            throw new MailboxIngestionRejectedException("SOURCE_ID_REQUIRED",
                "Thiếu message ID hoặc attachment ID để chống trùng.");
        if (command.ExternalMessageId.Length > 300 || command.ExternalAttachmentId.Length > 300 ||
            command.ExternalThreadId?.Length > 300)
            throw new MailboxIngestionRejectedException("SOURCE_ID_TOO_LONG",
                "Message ID, thread ID và attachment ID không được vượt quá 300 ký tự.");
    }

    private string GetStorageRoot()
    {
        if (string.IsNullOrWhiteSpace(_storageOptions.Directory))
            throw new InvalidOperationException("ReceiptStorage:Directory không được để trống.");
        return Path.GetFullPath(Path.IsPathRooted(_storageOptions.Directory)
            ? _storageOptions.Directory
            : Path.Combine(_environment.ContentRootPath, _storageOptions.Directory));
    }

    private static string BuildAuditDetails(ReimbursementRequest request, InvoiceIngestionCommand command) =>
        $"Source={command.Source}; SourceReference={command.SourceReference}; File={request.OriginalFileName}; " +
        $"Submitter={request.SubmitterCode}; Department={request.SubmitterDepartment}; " +
        $"AmountSource={(request.ClaimedAmount > 0 ? AmountSources.UserProvided : "AI_PENDING")}; " +
        $"SHA256={request.FileSha256}; DuplicateDetected={request.DuplicateDetected}; " +
        $"DuplicatePolicyEnabled={request.DuplicatePolicyEnabled}";

    private static string NormalizeFileName(string? value)
    {
        var fileName = Path.GetFileName(value ?? "receipt");
        return Normalize(fileName, "receipt", 255);
    }

    private static string NormalizeIdentity(string? value, string fallback, int maxLength) =>
        Normalize(value, fallback, maxLength);

    private static string Normalize(string? value, string fallback, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}

public sealed record InvoiceIngestionCommand(
    string Source,
    string OriginalFileName,
    string ContentType,
    byte[] Bytes,
    decimal ClaimedAmount = 0,
    string? SubmitterCode = null,
    string? SubmitterDisplayName = null,
    string? SubmitterDepartment = null,
    string? SourceReference = null,
    string? ExternalMessageId = null,
    string? ExternalThreadId = null,
    string? ExternalAttachmentId = null,
    string? Sender = null,
    string? Subject = null,
    string? SourceFolder = null,
    DateTimeOffset? ReceivedAt = null);

public sealed record InvoiceIngestionResult(
    string Outcome,
    string? RequestId,
    string? ErrorCode,
    string Message,
    bool DuplicateDetected);

public static class InvoiceSourceTypes
{
    public const string ManualUpload = "MANUAL_UPLOAD";
    public const string SandboxMailbox = "SANDBOX_MAILBOX";
}

public static class InvoiceIngestionOutcomes
{
    public const string Accepted = "ACCEPTED";
    public const string Duplicate = "DUPLICATE";
    public const string Quarantined = "QUARANTINED";
    public const string Rejected = "REJECTED";
}

public static class IngestionEventTypes
{
    public const string ReceiptQueued = "RECEIPT_QUEUED";
}

public sealed class MailboxIngestionRejectedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
