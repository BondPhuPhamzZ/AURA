using AURA.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
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
    private readonly bool _useSqlDistributedGate;
    private readonly string? _connectionString;
    private readonly object _sync = new();
    private readonly Dictionary<string, DateTime> _cooldownUntilUtc =
        new(StringComparer.OrdinalIgnoreCase);

    public VisionInvocationCoordinator(IOptions<VisionOptions> options, IConfiguration configuration)
    {
        var maxConcurrency = options.Value.MaxConcurrentRequests;
        _capacity = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _useSqlDistributedGate = options.Value.UseSqlDistributedGate;
        if (_useSqlDistributedGate)
        {
            if (maxConcurrency != 1)
                throw new InvalidOperationException("SQL distributed AI gate requires exactly one permit.");
            var configured = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(configured))
                throw new InvalidOperationException("SQL distributed AI gate requires DefaultConnection.");
            _connectionString = new SqlConnectionStringBuilder(configured) { Pooling = false }.ConnectionString;
        }
    }

    public async ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _capacity.WaitAsync(cancellationToken);
        if (!_useSqlDistributedGate) return new Lease(_capacity, null);

        SqlConnection? connection = null;
        try
        {
            connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            while (true)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock " +
                    "@Resource = N'AURA_VISION_GLOBAL_PERMIT', @LockMode = 'Exclusive', " +
                    "@LockOwner = 'Session', @LockTimeout = 0; SELECT @result;";
                command.CommandTimeout = 5;
                var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
                if (result >= 0) return new Lease(_capacity, connection);
                if (result != -1)
                    throw new InvalidOperationException($"SQL distributed AI gate failed with code {result}.");
                await Task.Delay(200, cancellationToken);
            }
        }
        catch
        {
            try
            {
                if (connection is not null) await connection.DisposeAsync();
            }
            finally
            {
                _capacity.Release();
            }
            throw;
        }
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

    private sealed class Lease(SemaphoreSlim capacity, SqlConnection? connection) : IDisposable
    {
        private SemaphoreSlim? _capacity = capacity;
        private SqlConnection? _connection = connection;

        public void Dispose()
        {
            var heldCapacity = Interlocked.Exchange(ref _capacity, null);
            if (heldCapacity is null) return;
            try
            {
                // Pooling is disabled so closing this physical session releases its application lock.
                Interlocked.Exchange(ref _connection, null)?.Dispose();
            }
            finally
            {
                heldCapacity.Release();
            }
        }
    }
}
