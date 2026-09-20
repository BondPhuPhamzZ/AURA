using System.Threading.Tasks;
using AURA.Models;

namespace AURA.Interfaces
{
    public interface IVisualValidator
    {
        Task<ReimbursementRequest> ValidateReceiptAsync(ReimbursementRequest request);
    }
}
