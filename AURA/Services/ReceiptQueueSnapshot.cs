using AURA.Data;
using AURA.Models;
using Microsoft.EntityFrameworkCore;

namespace AURA.Services;

public sealed record ReceiptQueueSnapshot(long PendingCount, long ProcessingCount,
    long FailedCount, long? OldestPendingAgeSeconds)
{
    public static async Task<ReceiptQueueSnapshot> ReadAsync(AppDbContext database,
        DateTime utcNow, CancellationToken cancellationToken)
    {
        var pending = await database.ReimbursementRequests.LongCountAsync(
            request => request.ProcessingState == ProcessingStates.Pending, cancellationToken);
        var processing = await database.ReimbursementRequests.LongCountAsync(
            request => request.ProcessingState == ProcessingStates.Processing, cancellationToken);
        var failed = await database.ReimbursementRequests.LongCountAsync(
            request => request.ProcessingState == ProcessingStates.Failed, cancellationToken);
        var oldestPendingAt = await database.ReimbursementRequests
            .Where(request => request.ProcessingState == ProcessingStates.Pending)
            .Select(request => (DateTime?)request.QueuedAt)
            .MinAsync(cancellationToken);
        var age = oldestPendingAt is null
            ? null
            : (long?)Math.Max(0, (long)(utcNow - oldestPendingAt.Value).TotalSeconds);
        return new ReceiptQueueSnapshot(pending, processing, failed, age);
    }
}
