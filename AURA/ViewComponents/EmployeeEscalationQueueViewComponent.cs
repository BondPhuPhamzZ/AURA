using AURA.Interfaces;
using AURA.Models;
using AURA.Services;
using Microsoft.AspNetCore.Mvc;

namespace AURA.ViewComponents;

public sealed class EmployeeEscalationQueueViewComponent : ViewComponent
{
    private readonly IReimbursementRepository _repository;
    private readonly ILogger<EmployeeEscalationQueueViewComponent> _logger;

    public EmployeeEscalationQueueViewComponent(IReimbursementRepository repository,
        ILogger<EmployeeEscalationQueueViewComponent> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            var items = (await _repository.GetAllRequestsAsync())
                .Where(x => !x.IsForwardedToManager && PolicyDecisionEngine.IsEscalation(x.Status));
            return View(items);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cannot load employee escalation queue.");
            ViewData["LoadError"] = "Không thể tải hồ sơ đang chờ chuyển tiếp.";
            return View(Array.Empty<ReimbursementRequest>());
        }
    }
}
