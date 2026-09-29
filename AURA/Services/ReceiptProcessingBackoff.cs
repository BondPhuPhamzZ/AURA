namespace AURA.Services;

public static class ReceiptProcessingBackoff
{
    public static TimeSpan Calculate(int consecutiveFailureCount, TimeSpan baseDelay, TimeSpan maximumDelay)
    {
        if (consecutiveFailureCount <= 0) return TimeSpan.Zero;
        if (baseDelay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(baseDelay));
        if (maximumDelay < baseDelay) throw new ArgumentOutOfRangeException(nameof(maximumDelay));

        var exponent = Math.Min(consecutiveFailureCount - 1, 30);
        var multiplier = 1L << exponent;
        var delayTicks = baseDelay.Ticks > maximumDelay.Ticks / multiplier
            ? maximumDelay.Ticks
            : baseDelay.Ticks * multiplier;

        return TimeSpan.FromTicks(Math.Min(delayTicks, maximumDelay.Ticks));
    }
}
