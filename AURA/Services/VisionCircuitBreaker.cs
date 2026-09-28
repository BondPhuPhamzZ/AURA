namespace AURA.Services;

public sealed class VisionCircuitBreaker
{
    private readonly object _sync = new();
    private int _consecutiveFailures;
    private DateTime? _openUntilUtc;

    public bool ShouldSkipPrimary(DateTime utcNow)
    {
        lock (_sync)
        {
            if (_openUntilUtc is null) return false;
            if (_openUntilUtc > utcNow) return true;

            // Cooldown elapsed: allow one primary probe. A success closes the circuit;
            // a new eligible failure opens it again through RecordFailure.
            _openUntilUtc = null;
            _consecutiveFailures = 0;
            return false;
        }
    }

    public void RecordSuccess()
    {
        lock (_sync)
        {
            _consecutiveFailures = 0;
            _openUntilUtc = null;
        }
    }

    public void RecordFailure(DateTime utcNow, int threshold, TimeSpan breakDuration)
    {
        lock (_sync)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= threshold) _openUntilUtc = utcNow.Add(breakDuration);
        }
    }
}
