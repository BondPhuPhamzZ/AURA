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
