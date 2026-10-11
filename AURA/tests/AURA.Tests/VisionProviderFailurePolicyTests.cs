using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class VisionProviderFailurePolicyTests
{
    [Theory]
    [InlineData("AI_RATE_LIMIT")]
    [InlineData("AI_PROVIDER_COOLDOWN")]
    [InlineData("AI_TIMEOUT")]
    [InlineData("AI_CREDITS_REQUIRED")]
    public void Provider_wide_failures_stop_worker_drain(string code) =>
        Assert.True(VisionProviderFailurePolicy.IsBlocking(code));

    [Theory]
    [InlineData("AI_SCHEMA_MISMATCH")]
    [InlineData("RECEIPT_FILE_MISSING")]
    public void Receipt_specific_failures_do_not_stop_worker_drain(string code) =>
        Assert.False(VisionProviderFailurePolicy.IsBlocking(code));
}
