using AURA.Data;
using Microsoft.EntityFrameworkCore;

namespace AURA.Services;

public sealed class IngestionOutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReceiptProcessingQueue _queue;
    private readonly ILogger<IngestionOutboxDispatcher> _logger;

    public IngestionOutboxDispatcher(IServiceScopeFactory scopeFactory, ReceiptProcessingQueue queue,
        ILogger<IngestionOutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Ingestion outbox dispatch failed; SQL jobs remain durable.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task DispatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var events = await db.IngestionOutboxEvents
            .Where(item => item.ProcessedAt == null && item.EventType == IngestionEventTypes.ReceiptQueued)
            .OrderBy(item => item.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (events.Count == 0) return;
        var now = DateTime.UtcNow;
        foreach (var item in events)
        {
            _queue.Signal(item.AggregateId);
            item.AttemptCount++;
            item.ProcessedAt = now;
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
