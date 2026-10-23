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
            _connectionString = new SqlConnectionStringBuilder(configured)
            {
                Pooling = false,
                ConnectRetryCount = 0
            }.ConnectionString;
        }
    }

    public async ValueTask<VisionInvocationLease> EnterAsync(CancellationToken cancellationToken)
    {
        await _capacity.WaitAsync(cancellationToken);
        if (!_useSqlDistributedGate) return new VisionInvocationLease(_capacity, null);

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
                if (result >= 0)
                {
                    await using var fenceCommand = connection.CreateCommand();
                    fenceCommand.CommandText = """
                        UPDATE dbo.VisionInvocationFences
                        SET Token = Token + 1
                        OUTPUT INSERTED.Token
                        WHERE Id = 1;
                        """;
                    fenceCommand.CommandTimeout = 5;
                    var token = await fenceCommand.ExecuteScalarAsync(cancellationToken);
                    if (token is null or DBNull)
                        throw new InvalidOperationException("SQL AI gate fencing row is missing.");
                    return new VisionInvocationLease(_capacity, connection, Convert.ToInt64(token));
                }
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

    public async Task<TimeSpan?> GetRemainingCooldownAsync(string provider, DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!_useSqlDistributedGate) return GetRemainingCooldown(provider, utcNow);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DATEDIFF_BIG(millisecond, SYSUTCDATETIME(), CooldownUntilUtc)
            FROM dbo.VisionProviderCooldowns
            WHERE Provider = @provider;
            """;
        command.Parameters.AddWithValue("@provider", provider);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not long remainingMilliseconds || remainingMilliseconds <= 0) return null;
        return TimeSpan.FromMilliseconds(Math.Min((double)remainingMilliseconds,
            TimeSpan.MaxValue.TotalMilliseconds));
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

    public async Task RecordCooldownAsync(string provider, DateTime utcNow, TimeSpan duration,
        CancellationToken cancellationToken)
    {
        if (!_useSqlDistributedGate)
        {
            RecordCooldown(provider, utcNow, duration);
            return;
        }
        if (duration <= TimeSpan.Zero) return;
        var durationMilliseconds = duration.Ticks / TimeSpan.TicksPerMillisecond;
        if (duration.Ticks % TimeSpan.TicksPerMillisecond != 0) durationMilliseconds++;
        var durationDays = checked((int)(durationMilliseconds / (24L * 60 * 60 * 1000)));
        var remainderMilliseconds = (int)(durationMilliseconds % (24L * 60 * 60 * 1000));
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            DECLARE @candidateUtc datetime2(7) = DATEADD(millisecond, @remainderMilliseconds,
                DATEADD(day, @durationDays, SYSUTCDATETIME()));
            UPDATE dbo.VisionProviderCooldowns WITH (UPDLOCK, HOLDLOCK)
                SET CooldownUntilUtc = CASE WHEN CooldownUntilUtc < @candidateUtc THEN @candidateUtc ELSE CooldownUntilUtc END
                WHERE Provider = @provider;
            IF @@ROWCOUNT = 0
                INSERT dbo.VisionProviderCooldowns (Provider, CooldownUntilUtc) VALUES (@provider, @candidateUtc);
            COMMIT TRANSACTION;
            """;
        command.Parameters.AddWithValue("@provider", provider);
        command.Parameters.AddWithValue("@durationDays", durationDays);
        command.Parameters.AddWithValue("@remainderMilliseconds", remainderMilliseconds);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public void ClearCooldown(string provider)
    {
        lock (_sync) _cooldownUntilUtc.Remove(provider);
    }

    public async Task ClearCooldownAsync(string provider, CancellationToken cancellationToken)
    {
        if (!_useSqlDistributedGate)
        {
            ClearCooldown(provider);
            return;
        }
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE dbo.VisionProviderCooldowns WHERE Provider = @provider;";
        command.Parameters.AddWithValue("@provider", provider);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public sealed class VisionInvocationLease : IAsyncDisposable
    {
        private const string LockResource = "AURA_VISION_GLOBAL_PERMIT";
        private SemaphoreSlim? _capacity;
        private SqlConnection? _connection;
        private readonly CancellationTokenSource _monitorStop = new();
        private readonly CancellationTokenSource _lost = new();
        private readonly SemaphoreSlim _sqlCommandGate = new(1, 1);
        private readonly Task _monitorTask;
        private readonly long _fencingToken;

        internal VisionInvocationLease(SemaphoreSlim capacity, SqlConnection? connection,
            long fencingToken = 0)
        {
            _capacity = capacity;
            _connection = connection;
            _fencingToken = fencingToken;
            _monitorTask = connection is null ? Task.CompletedTask : MonitorLockAsync(connection);
        }

        public CancellationToken LostToken => _lost.Token;

        internal long FencingToken => _fencingToken;

        internal int? SqlSessionId => _connection?.ServerProcessId;

        public async Task AssertOwnershipAsync(CancellationToken cancellationToken)
        {
            var connection = _connection;
            if (connection is null) return;
            if (_lost.IsCancellationRequested) throw CreateLostException();

            await _sqlCommandGate.WaitAsync(cancellationToken);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT CASE
                        WHEN APPLOCK_MODE('public', N'AURA_VISION_GLOBAL_PERMIT', 'Session') = 'Exclusive'
                         AND EXISTS (SELECT 1 FROM dbo.VisionInvocationFences
                                     WHERE Id = 1 AND Token = @token)
                        THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END;
                    """;
                command.CommandTimeout = 5;
                command.Parameters.AddWithValue("@token", _fencingToken);
                var value = await command.ExecuteScalarAsync(cancellationToken);
                if (value is bool isOwner && isOwner) return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                _lost.Cancel();
                throw CreateLostException();
            }
            finally
            {
                _sqlCommandGate.Release();
            }

            _lost.Cancel();
            throw CreateLostException();
        }

        private static VisionExtractionException CreateLostException() => new("AI_GATE_LOST",
            "Quyền gọi AI dùng chung đã mất hoặc token fencing đã cũ; kết quả không được chấp nhận và hồ sơ sẽ được xử lý lại.");

        public async ValueTask DisposeAsync()
        {
            var heldCapacity = Interlocked.Exchange(ref _capacity, null);
            if (heldCapacity is null) return;
            _monitorStop.Cancel();
            try
            {
                await _monitorTask;
                if (_lost.IsCancellationRequested)
                    throw CreateLostException();
            }
            finally
            {
                // Pooling is disabled so closing this physical session releases its application lock.
                var connection = Interlocked.Exchange(ref _connection, null);
                try
                {
                    if (connection is not null) await connection.DisposeAsync();
                }
                finally
                {
                    heldCapacity.Release();
                    _monitorStop.Dispose();
                    _lost.Dispose();
                    _sqlCommandGate.Dispose();
                }
            }
        }

        private async Task MonitorLockAsync(SqlConnection connection)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            try
            {
                while (await timer.WaitForNextTickAsync(_monitorStop.Token))
                {
                    if (_monitorStop.IsCancellationRequested) break;
                    await _sqlCommandGate.WaitAsync(_monitorStop.Token);
                    try
                    {
                        await using var command = connection.CreateCommand();
                        command.CommandText = """
                            SELECT CASE
                                WHEN APPLOCK_MODE('public', @resource, 'Session') = 'Exclusive'
                                 AND EXISTS (SELECT 1 FROM dbo.VisionInvocationFences
                                             WHERE Id = 1 AND Token = @token)
                                THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END;
                            """;
                        command.CommandTimeout = 5;
                        command.Parameters.AddWithValue("@resource", LockResource);
                        command.Parameters.AddWithValue("@token", _fencingToken);
                        var ownsPermit = await command.ExecuteScalarAsync(CancellationToken.None);
                        if (ownsPermit is not bool isOwner || !isOwner)
                        {
                            _lost.Cancel();
                            break;
                        }
                    }
                    finally
                    {
                        _sqlCommandGate.Release();
                    }
                }
            }
            catch (OperationCanceledException) when (_monitorStop.IsCancellationRequested)
            {
                // Normal disposal after the provider invocation.
            }
            catch
            {
                // A broken SQL connection can release the application lock. The caller's
                // linked cancellation token must stop using the provider result.
                _lost.Cancel();
            }
        }
    }
}
