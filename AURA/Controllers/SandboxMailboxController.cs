using AURA.Interfaces;
using AURA.Options;
using AURA.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AURA.Controllers;

[ApiController]
[Route("api/mailbox-sandbox")]
public sealed class SandboxMailboxController : ControllerBase
{
    private readonly MailboxSandboxOptions _options;
    private readonly IInvoiceIngestionService _ingestion;

    public SandboxMailboxController(IOptions<MailboxSandboxOptions> options,
        IInvoiceIngestionService ingestion)
    {
        _options = options.Value;
        _ingestion = ingestion;
    }

    [HttpGet("status")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Status() => Ok(new
    {
        enabled = _options.Enabled,
        provider = _options.Provider,
        mailboxId = _options.MailboxId,
        allowedFolder = _options.AllowedFolder,
        allowedSenderCount = _options.AllowedSenders.Length,
        maxAttachmentsPerMessage = _options.MaxAttachmentsPerMessage,
        requireMalwareScanner = _options.RequireMalwareScanner,
        mode = "synthetic-sandbox-only",
        gmailConnected = false,
        productionReady = false
    });

    [HttpPost("deliver")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> Deliver([FromForm] SandboxMailboxDelivery delivery,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return NotFound(new { error = "Mailbox sandbox đang tắt." });
        if (!MailboxSandboxGuard.IsAuthorized(Request.Headers["X-AURA-Sandbox-Key"], _options.ApiKey))
            return Unauthorized(new { error = "Sandbox API key không hợp lệ." });
        if (delivery.Attachment is null || delivery.Attachment.Length == 0)
            return BadRequest(new { error = "Thiếu attachment." });
        if (string.IsNullOrWhiteSpace(delivery.MessageId) || delivery.MessageId.Length > 300 ||
            string.IsNullOrWhiteSpace(delivery.AttachmentId) || delivery.AttachmentId.Length > 300)
        {
            return BadRequest(new { error = "Message ID và attachment ID là bắt buộc, tối đa 300 ký tự." });
        }

        var bytes = new byte[checked((int)delivery.Attachment.Length)];
        await using (var input = delivery.Attachment.OpenReadStream())
        {
            await input.ReadExactlyAsync(bytes, cancellationToken);
        }

        try
        {
            var sourceReference = MailboxSandboxGuard.BuildSourceKey(_options.Provider,
                _options.MailboxId, delivery.MessageId, delivery.AttachmentId);
            var result = await _ingestion.IngestAsync(new InvoiceIngestionCommand(
                InvoiceSourceTypes.SandboxMailbox,
                delivery.Attachment.FileName,
                delivery.Attachment.ContentType,
                bytes,
                SubmitterCode: "MAIL-SANDBOX",
                SubmitterDisplayName: delivery.Sender,
                SubmitterDepartment: "Mailbox sandbox",
                SourceReference: sourceReference,
                ExternalMessageId: delivery.MessageId,
                ExternalThreadId: delivery.ThreadId,
                ExternalAttachmentId: delivery.AttachmentId,
                Sender: delivery.Sender,
                Subject: delivery.Subject,
                SourceFolder: delivery.Folder,
                ReceivedAt: delivery.ReceivedAt), cancellationToken);

            if (result.Outcome == InvoiceIngestionOutcomes.Quarantined)
                return BadRequest(ToResponse(result));
            if (result.Outcome == InvoiceIngestionOutcomes.Duplicate)
                return Ok(ToResponse(result));
            if (result.Outcome != InvoiceIngestionOutcomes.Accepted)
                return BadRequest(ToResponse(result));

            return Accepted(result.RequestId is null ? null : $"/Applicant/Status/{result.RequestId}",
                ToResponse(result));
        }
        catch (MailboxIngestionRejectedException exception)
        {
            return BadRequest(new { errorCode = exception.Code, error = exception.Message });
        }
    }

    private static object ToResponse(InvoiceIngestionResult result) => new
    {
        outcome = result.Outcome,
        requestId = result.RequestId,
        result.ErrorCode,
        message = result.Message,
        result.DuplicateDetected,
        statusUrl = result.RequestId is null ? null : $"/Applicant/Status/{result.RequestId}"
    };
}

public sealed class SandboxMailboxDelivery
{
    public string MessageId { get; set; } = string.Empty;
    public string? ThreadId { get; set; }
    public string AttachmentId { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public DateTimeOffset? ReceivedAt { get; set; }
    public IFormFile? Attachment { get; set; }
}
