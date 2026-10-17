using System.Security.Cryptography;
using System.Text;
using AURA.Options;

namespace AURA.Services;

public static class MailboxSandboxGuard
{
    public static bool IsAuthorized(string? suppliedKey, string configuredKey)
    {
        if (string.IsNullOrWhiteSpace(suppliedKey) || string.IsNullOrWhiteSpace(configuredKey)) return false;
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        return suppliedBytes.Length == configuredBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, configuredBytes);
    }

    public static bool IsSenderAllowed(string? sender, MailboxSandboxOptions options) =>
        !string.IsNullOrWhiteSpace(sender) && options.AllowedSenders.Any(allowed =>
            string.Equals(allowed.Trim(), sender.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsFolderAllowed(string? folder, MailboxSandboxOptions options) =>
        !string.IsNullOrWhiteSpace(folder) &&
        string.Equals(folder.Trim(), options.AllowedFolder.Trim(), StringComparison.OrdinalIgnoreCase);

    public static string BuildSourceKey(string provider, string mailboxId, string messageId, string attachmentId) =>
        string.Join(":", new[] { provider, mailboxId, messageId, attachmentId }
            .Select(value => value.Trim().ToUpperInvariant()));
}
