using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using AURA.Data;
using AURA.Interfaces;
using AURA.Models;
using AURA.Services;

namespace AURA.ViewComponents
{
    public class EscalationQueueViewComponent : ViewComponent
    {
        private readonly IReimbursementRepository _repo;
        private readonly ILogger<EscalationQueueViewComponent> _logger;

        public EscalationQueueViewComponent(IReimbursementRepository repo, ILogger<EscalationQueueViewComponent> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                var items = (await _repo.GetAllRequestsAsync())
                    .Where(x => x.IsForwardedToManager && PolicyDecisionEngine.IsEscalation(x.Status));
                return View(items);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Cannot load escalation queue.");
                ViewData["LoadError"] = "Không thể tải hàng đợi quản lý. Hãy kiểm tra kết nối cơ sở dữ liệu.";
                return View(Array.Empty<ReimbursementRequest>());
            }
        }
    }
}
