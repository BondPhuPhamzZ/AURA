using System.ComponentModel.DataAnnotations;

namespace AURA.Models;

public sealed class EmailMessage
{
    [Key, StringLength(32)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required, StringLength(32)]
    public string MailboxConnectionId { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string ExternalMessageId { get; set; } = string.Empty;

    [StringLength(300)]
    public string? ExternalThreadId { get; set; }

    [Required, StringLength(320)]
    public string Sender { get; set; } = string.Empty;

    [StringLength(500)]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string SourceFolder { get; set; } = string.Empty;

    [Required, StringLength(32)]
    public string Status { get; set; } = MailboxMessageStates.Discovered;

    public DateTime ReceivedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class MailboxMessageStates
{
    public const string Discovered = "DISCOVERED";
    public const string Queued = "QUEUED";
    public const string Quarantined = "QUARANTINED";
}
