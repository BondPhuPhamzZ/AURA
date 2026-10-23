using AURA.Options;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed class ReceiptTemporaryFileCleanupWorker(IWebHostEnvironment environment,
    IOptions<ReceiptStorageOptions> storageOptions, ILogger<ReceiptTemporaryFileCleanupWorker> logger)
    : BackgroundService
{
    private readonly ReceiptStorageOptions _storage = storageOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_storage.TempFileCleanupIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            if (string.Equals(_storage.Provider, "LocalFileSystem", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var root = ReceiptStoragePath.ResolveRoot(environment, _storage.Directory);
                    var result = ReceiptTemporaryFileCleanup.Clean(root, DateTime.UtcNow,
                        TimeSpan.FromHours(_storage.StaleTempFileAgeHours));
                    if (result.DeletedCount > 0 || result.SkippedCount > 0)
                        logger.LogInformation(
                            "Receipt staging cleanup: deleted={DeletedCount}; skipped={SkippedCount}.",
                            result.DeletedCount, result.SkippedCount);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogWarning("Receipt staging cleanup unavailable ({ExceptionType}).",
                        exception.GetType().Name);
                }
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
