using System.Collections.Generic;
using System.Threading.Tasks;
using AURA.Models;

namespace AURA.Interfaces
{
    public enum ReceiptIntakePersistenceResult
    {
        Added,
        AlreadyExists,
        CapacityReached
    }

    public interface IReimbursementRepository
    {
        Task<List<ReimbursementRequest>> GetAllRequestsAsync();
        // Screen-specific reads filter in SQL so polling does not load every receipt and its facts JSON.
        /// <summary>Escalations already forwarded and still waiting for the manager, oldest handoff first.</summary>
        Task<List<ReimbursementRequest>> GetManagerQueueAsync();
        /// <summary>Escalations the employee has not forwarded yet.</summary>
        Task<List<ReimbursementRequest>> GetPendingHandoffsAsync();
        /// <summary>Latest unforwarded Drive sandbox jobs created at or after <paramref name="sinceUtc"/>.</summary>
        Task<List<ReimbursementRequest>> GetRecentDriveRequestsAsync(DateTime sinceUtc, int limit);
        /// <summary>Requests referenced by any audit event at or after <paramref name="sinceUtc"/>.</summary>
        Task<List<ReimbursementRequest>> GetRequestsWithAuditSinceAsync(DateTime sinceUtc);
        /// <summary>Requests created in the half-open UTC range [fromUtc, toUtc).</summary>
        Task<List<ReimbursementRequest>> GetRequestsCreatedBetweenAsync(DateTime fromUtc, DateTime toUtc);
        Task AddRequestWithAuditAsync(ReimbursementRequest request, string action, string details);
        Task<ReceiptIntakePersistenceResult> TryAddRequestWithAuditAsync(ReimbursementRequest request,
            string action, string details, int maxOutstandingReceipts, CancellationToken cancellationToken);
        Task<ReimbursementRequest?> GetRequestByIdAsync(string id);
        Task<ReimbursementRequest?> GetPersistedRequestByIdAsync(string id);
        Task UpdateRequestAsync(ReimbursementRequest request);
        Task UpdateRequestWithAuditAsync(ReimbursementRequest request, string action, string details);
        Task UpdateRequestsWithAuditAsync(IEnumerable<(ReimbursementRequest Request, string Details)> requests,
            string action);
        Task<bool> ExistsByFileHashAsync(string sha256);
    }
}
