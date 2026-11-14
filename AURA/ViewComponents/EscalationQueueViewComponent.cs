using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AURA.Interfaces;
using AURA.Options;
using AURA.Services;
using AURA.ViewModels;

namespace AURA.ViewComponents
{
    public class EscalationQueueViewComponent : ViewComponent
    {
        private readonly IReimbursementRepository _repo;
        private readonly ILogger<EscalationQueueViewComponent> _logger;
        private readonly int _targetHours;

        public EscalationQueueViewComponent(IReimbursementRepository repo, ILogger<EscalationQueueViewComponent> logger,
            IOptions<ReviewSlaOptions> reviewSla)
        {
            _repo = repo;
            _logger = logger;
            _targetHours = reviewSla.Value.TargetHours;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                return View(ReviewSlaPolicy.Project(await _repo.GetManagerQueueAsync(), DateTime.UtcNow,
                    _targetHours));
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Cannot load escalation queue.");
                ViewData["LoadError"] = "Không thể tải hàng đợi quản lý. Hãy kiểm tra kết nối cơ sở dữ liệu.";
                return View(new ManagerQueueViewModel { TargetHours = _targetHours });
            }
        }
    }
}
