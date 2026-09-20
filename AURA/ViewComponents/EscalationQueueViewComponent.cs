using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AURA.Data;

namespace AURA.ViewComponents
{
    public class EscalationQueueViewComponent : ViewComponent
    {
        private readonly IReimbursementRepository _repo;

        public EscalationQueueViewComponent(IReimbursementRepository repo)
        {
            _repo = repo;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var items = _repo.GetAllRequests();
            return View(items);
        }
    }
}
