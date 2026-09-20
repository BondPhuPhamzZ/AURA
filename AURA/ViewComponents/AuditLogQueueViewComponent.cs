using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AURA.Interfaces;
using System.Linq;

namespace AURA.ViewComponents
{
    public class AuditLogQueueViewComponent : ViewComponent
    {
        private readonly IReimbursementRepository _repo;

        public AuditLogQueueViewComponent(IReimbursementRepository repo)
        {
            _repo = repo;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var items = await _repo.GetAllRequestsAsync();
            var sortedItems = items.OrderByDescending(r => r.CreatedAt);
            return View(sortedItems);
        }
    }
}
