using System.Collections.Generic;
using System.Threading.Tasks;
using AURA.Models;

namespace AURA.Interfaces
{
    public interface IReimbursementRepository
    {
        Task<List<ReimbursementRequest>> GetAllRequestsAsync();
        Task AddRequestAsync(ReimbursementRequest request);
        Task<ReimbursementRequest?> GetRequestByIdAsync(string id);
        Task UpdateRequestAsync(ReimbursementRequest request);
        Task UpdateRequestWithAuditAsync(ReimbursementRequest request, string action, string details);
        Task<bool> ExistsByFileHashAsync(string sha256);
    }
}
