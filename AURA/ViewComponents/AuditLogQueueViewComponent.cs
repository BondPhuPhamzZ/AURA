using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AURA.Interfaces;
using System.Linq;
using AURA.ViewModels;

namespace AURA.ViewComponents
{
    public class AuditLogQueueViewComponent : ViewComponent
    {
        private readonly IReimbursementRepository _repo;
        private readonly IAuditLogger _audit;

        public AuditLogQueueViewComponent(IReimbursementRepository repo, IAuditLogger audit)
        {
            _repo = repo;
            _audit = audit;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var requests = (await _repo.GetAllRequestsAsync()).ToDictionary(x => x.Id);
            var logs = await _audit.GetRecentAsync();
            var items = logs.Select(log => new AuditEntryViewModel
            {
                Log = log,
                Request = requests.GetValueOrDefault(log.RequestId)
            });
            return View(items);
        }
    }
}
