using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class VerifyExecutionPolicyTests
{
    [Theory]
    [InlineData(true, "Development", true)]
    [InlineData(true, "Production", false)]
    [InlineData(true, "Staging", false)]
    [InlineData(false, "Development", false)]
    [InlineData(true, null, false)]
    public void Anonymous_harness_is_only_available_when_explicitly_enabled_for_development(
        bool configured, string? environment, bool expected)
    {
        Assert.Equal(expected, VerifyExecutionPolicy.IsAllowed(configured, environment));
    }
}
