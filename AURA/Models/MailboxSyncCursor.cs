using System.ComponentModel.DataAnnotations;

namespace AURA.Models;

public sealed class MailboxSyncCursor
{
    [Key, StringLength(32)]
    public string MailboxConnectionId { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Cursor { get; set; }

    public DateTime? LastSuccessfulSyncAt { get; set; }
    public int ConsecutiveFailureCount { get; set; }

    [StringLength(100)]
    public string? LastErrorCode { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
