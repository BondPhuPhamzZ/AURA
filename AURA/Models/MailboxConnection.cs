using System.ComponentModel.DataAnnotations;

namespace AURA.Models;

public sealed class MailboxConnection
{
    [Key, StringLength(32)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required, StringLength(50)]
    public string Provider { get; set; } = "SANDBOX";

    [Required, StringLength(200)]
    public string ExternalMailboxId { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string AllowedFolder { get; set; } = string.Empty;

    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncAt { get; set; }
}
