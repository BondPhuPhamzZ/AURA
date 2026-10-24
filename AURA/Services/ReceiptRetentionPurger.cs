using System.Data;
using AURA.Data;
using AURA.Interfaces;
using AURA.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AURA.Services;

/// <summary>Deletes expired finalized requests, their audit rows and image objects idempotently.</summary>
public sealed class ReceiptRetentionPurger(
    AppDbContext database,
    IReceiptFileStore fileStore,
    ILogger<ReceiptRetentionPurger> logger)
{
    public async Task<int> PurgeExpiredBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            DECLARE @lockResult int;
            EXEC @lockResult = sys.sp_getapplock
                @Resource = N'AURA:ReceiptRetentionPurge',
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 0;
            SELECT @lockResult;
            """;
        var lockResult = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (lockResult < 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return 0;
        }

        var now = await DatabaseUtcClock.ReadAsync(database, cancellationToken);
        var finalizedStatuses = new[]
        {
            "MANUAL_REVIEW_ACCEPTED", "RETURNED_FOR_MORE_EVIDENCE",
            "APPROVED_POLICY_EXCEPTION", "REJECTED_POLICY_EXCEPTION",
            "FORWARDED_TO_AUTHORITY", "RETURNED_BY_MANAGER", "RETURNED_FOR_RETRY"
        };
        var expired = await database.ReimbursementRequests
            .Where(request => request.RetentionExpiresAt != null && request.RetentionExpiresAt <= now &&
                (request.ProcessingState == ProcessingStates.Completed ||
                 request.ProcessingState == ProcessingStates.Failed) &&
                ((request.ProcessingState == ProcessingStates.Completed && request.Status == "AUTO_APPROVE") ||
                 (request.ManagerAnswer != null && request.ManagerDecisionAt != null &&
                  finalizedStatuses.Contains(request.Status))))
            .OrderBy(request => request.RetentionExpiresAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var request in expired)
        {
            // Object deletion is idempotent. If the process stops before SQL commits,
            // the expired row remains and the next sweep safely retries this deletion.
            await fileStore.DeleteAsync(request.StoredFileName, cancellationToken);
            await database.AuditLogs
                .Where(log => log.RequestId == request.Id)
                .ExecuteDeleteAsync(cancellationToken);
            database.ReimbursementRequests.Remove(request);
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogInformation("Retention sweep skipped a request changed concurrently.");
            return 0;
        }

        return expired.Count;
    }
}
