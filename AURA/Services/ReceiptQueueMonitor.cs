using AURA.Data;
using AURA.Options;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed class ReceiptQueueMonitor(IServiceScopeFactory scopeFactory,
    ILogger<ReceiptQueueMonitor> logger, IOptions<ReceiptProcessingOptions> options) : BackgroundService
{
    private readonly int _maxOutstandingReceipts = options.Value.MaxOutstandingReceipts;
    private readonly int _queueWarningAgeSeconds = options.Value.QueueWarningAgeSeconds;
    private bool _capacityWarningActive;
    private bool _ageWarningActive;
    private long? _lastFailedCount;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var snapshot = await ReceiptQueueSnapshot.ReadAsync(database, DateTime.UtcNow, stoppingToken);
                logger.LogInformation(
                    "Receipt queue snapshot: pending={PendingCount}; processing={ProcessingCount}; failed={FailedCount}; oldest pending age seconds={OldestPendingAgeSeconds}; recorded receipt bytes={RecordedReceiptBytes}.",
                    snapshot.PendingCount, snapshot.ProcessingCount, snapshot.FailedCount,
                    snapshot.OldestPendingAgeSeconds, snapshot.RecordedReceiptBytes);

                var outstanding = snapshot.OutstandingCount;
                var capacityThreshold = Math.Max(1,
                    (long)Math.Ceiling(_maxOutstandingReceipts * 0.8));
                var capacityWarning = snapshot.IsNearCapacity(_maxOutstandingReceipts);
                if (capacityWarning && !_capacityWarningActive)
                    logger.LogWarning(
                        "Receipt queue capacity warning: outstanding={OutstandingCount}; limit={CapacityLimit}; warning threshold={WarningThreshold}.",
                        outstanding, _maxOutstandingReceipts, capacityThreshold);
                else if (!capacityWarning && _capacityWarningActive)
                    logger.LogInformation("Receipt queue capacity warning cleared: outstanding={OutstandingCount}.",
                        outstanding);
                _capacityWarningActive = capacityWarning;

                var ageWarning = snapshot.HasStalePending(_queueWarningAgeSeconds);
                if (ageWarning && !_ageWarningActive)
                    logger.LogWarning(
                        "Receipt queue age warning: oldest pending age seconds={OldestPendingAgeSeconds}; threshold seconds={WarningThreshold}.",
                        snapshot.OldestPendingAgeSeconds, _queueWarningAgeSeconds);
                else if (!ageWarning && _ageWarningActive)
                    logger.LogInformation("Receipt queue age warning cleared.");
                _ageWarningActive = ageWarning;

                if (_lastFailedCount is long lastFailed && snapshot.FailedCount > lastFailed)
                    logger.LogWarning("New failed receipts observed: count={NewFailedCount}; cumulative failed={FailedCount}.",
                        snapshot.FailedCount - lastFailed, snapshot.FailedCount);
                _lastFailedCount = snapshot.FailedCount;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Receipt queue snapshot unavailable ({ExceptionType}).",
                    exception.GetType().Name);
            }
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
