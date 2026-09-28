using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class VisionFallbackPolicyTests
{
    [Theory]
    [InlineData("AI_TIMEOUT")]
    [InlineData("AI_RATE_LIMIT")]
    [InlineData("AI_TEMPORARILY_UNAVAILABLE")]
    [InlineData("AI_LOCAL_UNAVAILABLE")]
    public void InfrastructureFailuresAreEligible(string code)
    {
        Assert.True(VisionFallbackPolicy.IsEligible(code));
    }

    [Theory]
    [InlineData("AI_INVALID_RESPONSE")]
    [InlineData("AI_SCHEMA_MISMATCH")]
    [InlineData("AI_REQUEST_INVALID")]
    [InlineData("RECEIPT_FILE_MISSING")]
    public void DataAndContractFailuresDoNotTriggerFallback(string code)
    {
        Assert.False(VisionFallbackPolicy.IsEligible(code));
    }
}
