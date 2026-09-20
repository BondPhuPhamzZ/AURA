using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AURA.Data;
using AURA.Interfaces;

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
            var items = await _repo.GetAllRequestsAsync();
            return View(items);
        }
    }
}
