using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptQueueSnapshotTests
{
    [Theory]
    [InlineData(6, 1, 10, false)]
    [InlineData(8, 0, 10, true)]
    [InlineData(7, 1, 10, true)]
    [InlineData(0, 0, 1, false)]
    public void Near_capacity_warning_uses_pending_plus_processing_and_eighty_percent(
        long pending, long processing, int maximum, bool expected)
    {
        var snapshot = new ReceiptQueueSnapshot(pending, processing, 0, null, 0);

        Assert.Equal(expected, snapshot.IsNearCapacity(maximum));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(299L, false)]
    [InlineData(300L, true)]
    public void Stale_pending_warning_uses_configured_age_threshold(long? age, bool expected)
    {
        var snapshot = new ReceiptQueueSnapshot(1, 0, 0, age, 0);

        Assert.Equal(expected, snapshot.HasStalePending(300));
    }
}
