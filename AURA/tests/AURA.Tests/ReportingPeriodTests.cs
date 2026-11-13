using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class ReportingPeriodTests
{
    [Theory]
    [InlineData("2026-09", true, 2026, 9)]
    [InlineData(" 2000-01 ", true, 2000, 1)]
    [InlineData("2100-12", true, 2100, 12)]
    [InlineData("1999-12", false, 0, 0)]
    [InlineData("2101-01", false, 0, 0)]
    [InlineData("2026-00", false, 0, 0)]
    [InlineData("2026-13", false, 0, 0)]
    [InlineData("+2026-09", false, 0, 0)]
    [InlineData("not-a-period", false, 0, 0)]
    [InlineData(null, false, 0, 0)]
    public void Month_values_are_parsed_within_the_shared_bounds(string? value, bool expected,
        int expectedYear, int expectedMonth)
    {
        Assert.Equal(expected, ReportingPeriod.TryParseMonthValue(value, out var year, out var month));
        Assert.Equal(expectedYear, year);
        Assert.Equal(expectedMonth, month);
    }

    [Fact]
    public void Html_month_bounds_come_from_the_same_constants()
    {
        Assert.Equal("2000-01", ReportingPeriod.MinMonthValue);
        Assert.Equal("2100-12", ReportingPeriod.MaxMonthValue);
        Assert.Equal("2026-03", ReportingPeriod.FormatMonthValue(2026, 3));
    }

    [Fact]
    public void Each_invalid_part_falls_back_to_the_current_vietnam_month()
    {
        // 2026-12-31 18:00 UTC is already 2027-01-01 in Vietnam.
        var utcNow = new DateTime(2026, 12, 31, 18, 0, 0, DateTimeKind.Utc);

        Assert.Equal((2026, 5), ReportingPeriod.ResolveOrCurrent(2026, 5, utcNow));
        Assert.Equal((2026, 1), ReportingPeriod.ResolveOrCurrent(2026, 13, utcNow));
        Assert.Equal((2027, 5), ReportingPeriod.ResolveOrCurrent(1999, 5, utcNow));
        Assert.Equal((2027, 1), ReportingPeriod.ResolveOrCurrent(null, null, utcNow));
    }
}
