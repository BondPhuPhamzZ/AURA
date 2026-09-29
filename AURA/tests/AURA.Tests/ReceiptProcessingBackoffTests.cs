using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptProcessingBackoffTests
{
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    [InlineData(5, 60)]
    [InlineData(20, 60)]
    public void CalculateUsesExponentialDelayWithMaximum(int failureCount, int expectedSeconds)
    {
        var delay = ReceiptProcessingBackoff.Calculate(
            failureCount,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(60));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public void CalculateReturnsZeroWhenThereIsNoFailure()
    {
        var delay = ReceiptProcessingBackoff.Calculate(
            0,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(60));

        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void CalculateRejectsMaximumBelowBaseDelay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReceiptProcessingBackoff.Calculate(
            1,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(5)));
    }
}
