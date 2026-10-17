using System.ComponentModel.DataAnnotations;

namespace AURA.Models;

public sealed class EmailAttachment
{
    [Key, StringLength(32)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required, StringLength(32)]
    public string EmailMessageId { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string ExternalAttachmentId { get; set; } = string.Empty;

    [Required, StringLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [StringLength(64)]
    public string? FileSha256 { get; set; }

    [Required, StringLength(32)]
    public string QuarantineStatus { get; set; } = AttachmentQuarantineStates.Pending;

    [StringLength(100)]
    public string? QuarantineReasonCode { get; set; }

    [StringLength(1000)]
    public string? QuarantineReason { get; set; }

    [StringLength(32)]
    public string? ReimbursementRequestId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class AttachmentQuarantineStates
{
    public const string Pending = "PENDING";
    public const string Released = "RELEASED";
    public const string Quarantined = "QUARANTINED";
}
