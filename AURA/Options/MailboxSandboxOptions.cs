using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

public sealed class MailboxSandboxOptions
{
    public const string SectionName = "MailboxSandbox";

    public bool Enabled { get; set; }

    [Required, StringLength(50)]
    public string Provider { get; set; } = "SANDBOX";

    [Required, StringLength(200)]
    public string MailboxId { get; set; } = "aura-mail-sandbox";

    [Required, StringLength(200)]
    public string AllowedFolder { get; set; } = "AURA_INVOICE";

    public string[] AllowedSenders { get; set; } = [];

    // Required only when Enabled=true. Keep the value in environment/secret storage.
    public string ApiKey { get; set; } = string.Empty;

    public bool RequireMalwareScanner { get; set; }

    [Range(1, 100)]
    public int MaxAttachmentsPerMessage { get; set; } = 10;
}
