using AURA.Data;

namespace AURA.Services;

public sealed class ReceiptQueueMonitor(IServiceScopeFactory scopeFactory,
    ILogger<ReceiptQueueMonitor> logger) : BackgroundService
{
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
                    "Receipt queue snapshot: pending={PendingCount}; processing={ProcessingCount}; failed={FailedCount}; oldest pending age seconds={OldestPendingAgeSeconds}.",
                    snapshot.PendingCount, snapshot.ProcessingCount, snapshot.FailedCount,
                    snapshot.OldestPendingAgeSeconds);
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
