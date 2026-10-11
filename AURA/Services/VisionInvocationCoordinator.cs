using AURA.Options;
using Microsoft.Extensions.Options;

namespace AURA.Services;

/// <summary>
/// Coordinates every vision invocation in this application process. Upload jobs and the
/// Verify Harness share this gate so they cannot accidentally create parallel provider calls.
/// Provider cooldowns prevent a queue of pending jobs from turning one upstream 429 into a
/// burst of additional requests.
/// </summary>
public sealed class VisionInvocationCoordinator
{
    private readonly SemaphoreSlim _capacity;
    private readonly object _sync = new();
    private readonly Dictionary<string, DateTime> _cooldownUntilUtc =
        new(StringComparer.OrdinalIgnoreCase);

    public VisionInvocationCoordinator(IOptions<VisionOptions> options)
    {
        var maxConcurrency = options.Value.MaxConcurrentRequests;
        _capacity = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public async ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _capacity.WaitAsync(cancellationToken);
        return new Lease(_capacity);
    }

    public TimeSpan? GetRemainingCooldown(string provider, DateTime utcNow)
    {
        lock (_sync)
        {
            if (!_cooldownUntilUtc.TryGetValue(provider, out var cooldownUntil)) return null;
            if (cooldownUntil <= utcNow)
            {
                _cooldownUntilUtc.Remove(provider);
                return null;
            }

            return cooldownUntil - utcNow;
        }
    }

    public void RecordCooldown(string provider, DateTime utcNow, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return;
        lock (_sync)
        {
            var candidate = utcNow.Add(duration);
            if (!_cooldownUntilUtc.TryGetValue(provider, out var existing) || candidate > existing)
                _cooldownUntilUtc[provider] = candidate;
        }
    }

    public void ClearCooldown(string provider)
    {
        lock (_sync) _cooldownUntilUtc.Remove(provider);
    }

    private sealed class Lease(SemaphoreSlim capacity) : IDisposable
    {
        private SemaphoreSlim? _capacity = capacity;

        public void Dispose() => Interlocked.Exchange(ref _capacity, null)?.Release();
    }
}
