using System.Threading.Tasks;

namespace AURA.Interfaces
{
    public interface IAuditLogger
    {
        Task LogActionAsync(string requestId, string action, string details);
        Task<List<AURA.Models.AuditLog>> GetRecentAsync(int limit = 100);
        Task<List<AURA.Models.AuditLog>> GetByRequestIdAsync(string requestId);
    }
}
