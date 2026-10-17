using AURA.Options;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class MailboxSandboxGuardTests
{
    private readonly MailboxSandboxOptions _options = new()
    {
        Enabled = true,
        ApiKey = "0123456789abcdefghijklmn",
        AllowedFolder = "AURA_INVOICE",
        AllowedSenders = ["demo.sender@example.test"]
    };

    [Fact]
    public void Api_key_requires_an_exact_constant_time_compatible_match()
    {
        Assert.True(MailboxSandboxGuard.IsAuthorized("0123456789abcdefghijklmn", _options.ApiKey));
        Assert.False(MailboxSandboxGuard.IsAuthorized("wrong", _options.ApiKey));
        Assert.False(MailboxSandboxGuard.IsAuthorized(null, _options.ApiKey));
    }

    [Fact]
    public void Sender_and_folder_are_allow_listed_case_insensitively()
    {
        Assert.True(MailboxSandboxGuard.IsSenderAllowed("DEMO.SENDER@example.test", _options));
        Assert.True(MailboxSandboxGuard.IsFolderAllowed("aura_invoice", _options));
        Assert.False(MailboxSandboxGuard.IsSenderAllowed("attacker@example.test", _options));
        Assert.False(MailboxSandboxGuard.IsFolderAllowed("INBOX", _options));
    }

    [Fact]
    public void Source_key_is_stable_for_provider_mailbox_message_and_attachment()
    {
        var first = MailboxSandboxGuard.BuildSourceKey("sandbox", "mailbox", "message-1", "attachment-1");
        var second = MailboxSandboxGuard.BuildSourceKey(" SANDBOX ", "MAILBOX", "MESSAGE-1", "ATTACHMENT-1");

        Assert.Equal(first, second);
        Assert.Equal("SANDBOX:MAILBOX:MESSAGE-1:ATTACHMENT-1", first);
    }
}
