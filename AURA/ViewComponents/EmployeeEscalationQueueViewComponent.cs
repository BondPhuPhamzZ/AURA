using AURA.Interfaces;
using AURA.Models;
using AURA.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AURA.Options;

namespace AURA.ViewComponents;

public sealed class EmployeeEscalationQueueViewComponent : ViewComponent
{
    private readonly IReimbursementRepository _repository;
    private readonly ILogger<EmployeeEscalationQueueViewComponent> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly DriveSandboxOptions _drive;

    public EmployeeEscalationQueueViewComponent(IReimbursementRepository repository,
        ILogger<EmployeeEscalationQueueViewComponent> logger,
        IWebHostEnvironment environment, IOptions<DriveSandboxOptions> drive)
    {
        _repository = repository;
        _logger = logger;
        _environment = environment;
        _drive = drive.Value;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            var items = SelectRows(await _repository.GetAllRequestsAsync(),
                _environment.IsDevelopment() && _drive.Enabled, DateTime.UtcNow);
            return View(items);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cannot load employee escalation queue.");
            ViewData["LoadError"] = "Không thể tải hồ sơ đang chờ chuyển tiếp.";
            return View(Array.Empty<ReimbursementRequest>());
        }
    }

    internal static IReadOnlyList<ReimbursementRequest> SelectRows(
        IEnumerable<ReimbursementRequest> requests, bool showDrive, DateTime utcNow)
    {
        var all = requests.ToList();
        var escalations = all.Where(x => !x.IsForwardedToManager &&
                PolicyDecisionEngine.IsEscalation(x.Status));
        // Show the latest synthetic Drive jobs while they move through SQL and AI.
        // Keep this local-only; public hosting has no Drive intake or identity boundary.
        var recentDrive = showDrive
                ? all.Where(x => x.SubmitterCode == "DRIVE-SANDBOX" &&
                        !x.IsForwardedToManager && x.CreatedAt >= utcNow.AddDays(-1))
                    .OrderByDescending(x => x.QueuedAt).Take(20)
                : Enumerable.Empty<ReimbursementRequest>();
        return escalations.Concat(recentDrive).DistinctBy(x => x.Id)
            .OrderByDescending(x => x.QueuedAt).ToList();
    }
}
