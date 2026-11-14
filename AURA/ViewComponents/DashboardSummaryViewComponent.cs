using AURA.Interfaces;
using AURA.Services;
using AURA.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AURA.ViewComponents;

public sealed class DashboardSummaryViewComponent : ViewComponent
{
    private readonly IReimbursementRepository _repository;
    private readonly ILogger<DashboardSummaryViewComponent> _logger;

    public DashboardSummaryViewComponent(IReimbursementRepository repository,
        ILogger<DashboardSummaryViewComponent> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync(int? year = null, int? month = null)
    {
        try
        {
            // The dashboard shows one Vietnam calendar year (monthly table) and a month inside it.
            var utcNow = DateTime.UtcNow;
            var (targetYear, _) = ReportingPeriod.ResolveOrCurrent(year, month, utcNow);
            var (fromUtc, toUtc) = ReportingPeriod.YearUtcRange(targetYear);
            var requests = await _repository.GetRequestsCreatedBetweenAsync(fromUtc, toUtc);
            return View(DashboardSummaryProjector.Project(requests, utcNow, year, month));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cannot load dashboard summary.");
            ViewData["LoadError"] = "Không thể tải dashboard. Hãy kiểm tra kết nối cơ sở dữ liệu.";
            return View(new DashboardSummaryViewModel
            {
                IsAvailable = false,
                PeriodLabel = "Tháng hiện tại",
                YearLabel = "Năm hiện tại",
                SelectedYear = year ?? 0,
                SelectedMonth = month ?? 0
            });
        }
    }
}
