namespace AURA.Services;

/// <summary>
/// Converts persisted UTC timestamps to the product's business timezone.
/// Hosting servers may use a different OS timezone, so UI code must not rely
/// on <see cref="DateTime.ToLocalTime()"/>.
/// </summary>
public static class VietnamTime
{
    private static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

    public static DateTime FromUtc(DateTime timestamp)
    {
        var utcTimestamp = timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utcTimestamp, TimeZone);
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the other platform-specific identifier.
            }
            catch (InvalidTimeZoneException)
            {
                // Fall back to a fixed UTC+7 zone below.
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "AURA Vietnam Standard Time",
            TimeSpan.FromHours(7),
            "Vietnam Standard Time",
            "Vietnam Standard Time");
    }
}
