using System.Threading.Tasks;

namespace AURA.Interfaces
{
    public interface IAuditLogger
    {
        Task LogActionAsync(string requestId, string action, string details);
    }
}
