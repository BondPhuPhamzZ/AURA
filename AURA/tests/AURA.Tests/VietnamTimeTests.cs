using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class VietnamTimeTests
{
    [Fact]
    public void FromUtc_converts_utc_timestamp_to_vietnam_time()
    {
        var timestamp = new DateTime(2026, 10, 6, 7, 48, 0, DateTimeKind.Utc);

        var result = VietnamTime.FromUtc(timestamp);

        Assert.Equal(new DateTime(2026, 10, 6, 14, 48, 0), result);
        Assert.Equal(DateTimeKind.Unspecified, result.Kind);
    }

    [Fact]
    public void FromUtc_treats_database_unspecified_timestamp_as_utc()
    {
        var timestamp = new DateTime(2026, 10, 6, 7, 48, 0, DateTimeKind.Unspecified);

        var result = VietnamTime.FromUtc(timestamp);

        Assert.Equal(new DateTime(2026, 10, 6, 14, 48, 0), result);
    }
}
