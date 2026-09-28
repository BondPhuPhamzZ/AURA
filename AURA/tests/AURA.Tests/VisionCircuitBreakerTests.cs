using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class VisionCircuitBreakerTests
{
    [Fact]
    public void OpensAtThresholdAndAllowsProbeAfterCooldown()
    {
        var circuit = new VisionCircuitBreaker();
        var now = new DateTime(2026, 9, 28, 1, 0, 0, DateTimeKind.Utc);

        circuit.RecordFailure(now, threshold: 2, TimeSpan.FromSeconds(60));
        Assert.False(circuit.ShouldSkipPrimary(now));

        circuit.RecordFailure(now, threshold: 2, TimeSpan.FromSeconds(60));
        Assert.True(circuit.ShouldSkipPrimary(now.AddSeconds(30)));
        Assert.False(circuit.ShouldSkipPrimary(now.AddSeconds(61)));
    }

    [Fact]
    public void SuccessClosesCircuit()
    {
        var circuit = new VisionCircuitBreaker();
        var now = DateTime.UtcNow;
        circuit.RecordFailure(now, threshold: 1, TimeSpan.FromMinutes(1));
        Assert.True(circuit.ShouldSkipPrimary(now));

        circuit.RecordSuccess();
        Assert.False(circuit.ShouldSkipPrimary(now));
    }
}
