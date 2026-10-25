using AURA.Options;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed class ReceiptRetentionCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ReceiptRetentionOptions> configuredOptions,
    ILogger<ReceiptRetentionCleanupWorker> logger) : BackgroundService
{
    private readonly ReceiptRetentionOptions _options = configuredOptions.Value;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Drain every batch before accepting requests, including backlog accumulated
        // while the local machine was off. Each batch gets a fresh DbContext.
        await RunSweepAsync(cancellationToken, failStartup: true);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.SweepIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
                await RunSweepAsync(stoppingToken, failStartup: false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunSweepAsync(CancellationToken cancellationToken, bool failStartup)
    {
        if (!_options.Enabled) return;

        try
        {
            int deleted;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var scope = scopeFactory.CreateAsyncScope();
                var purger = scope.ServiceProvider.GetRequiredService<ReceiptRetentionPurger>();
                deleted = await purger.PurgeExpiredBatchAsync(_options.BatchSize, cancellationToken);
                if (deleted > 0)
                    logger.LogInformation("Receipt retention sweep deleted {DeletedCount} expired request(s).", deleted);
            } while (deleted == _options.BatchSize);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (failStartup)
            {
                logger.LogError("Receipt retention startup cleanup failed ({ExceptionType}); startup is blocked.",
                    exception.GetType().Name);
                throw new InvalidOperationException(
                    "Receipt retention startup cleanup failed; the application will not start while retention is enabled.");
            }

            logger.LogWarning("Receipt retention sweep failed ({ExceptionType}); it will retry later.",
                exception.GetType().Name);
        }
    }
}
