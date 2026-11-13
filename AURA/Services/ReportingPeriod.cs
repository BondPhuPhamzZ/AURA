using System.Globalization;

namespace AURA.Services;

/// <summary>Single definition of the selectable dashboard/report month range.</summary>
public static class ReportingPeriod
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    /// <summary>Bounds for an HTML <c>input type="month"</c>, rendered from the same constants.</summary>
    public static string MinMonthValue { get; } = FormatMonthValue(MinYear, 1);

    public static string MaxMonthValue { get; } = FormatMonthValue(MaxYear, 12);

    public static bool IsValidYear(int? year) => year is >= MinYear and <= MaxYear;

    public static bool IsValidMonth(int? month) => month is >= 1 and <= 12;

    /// <summary>Keeps each valid part and falls back to the current Vietnam month for the other.</summary>
    public static (int Year, int Month) ResolveOrCurrent(int? year, int? month, DateTime utcNow)
    {
        var localNow = VietnamTime.FromUtc(utcNow);
        return (IsValidYear(year) ? year!.Value : localNow.Year,
            IsValidMonth(month) ? month!.Value : localNow.Month);
    }

    /// <summary>Parses the <c>yyyy-MM</c> value posted by a month input.</summary>
    public static bool TryParseMonthValue(string? value, out int year, out int month)
    {
        year = 0;
        month = 0;
        var parts = value?.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts is not { Length: 2 } ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedYear) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedMonth) ||
            !IsValidYear(parsedYear) || !IsValidMonth(parsedMonth))
            return false;

        year = parsedYear;
        month = parsedMonth;
        return true;
    }

    public static string FormatMonthValue(int year, int month) =>
        string.Create(CultureInfo.InvariantCulture, $"{year:0000}-{month:00}");
}
