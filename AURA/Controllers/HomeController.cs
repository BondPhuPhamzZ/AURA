using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AURA.Models;
using System.Threading.Tasks;

namespace AURA.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Index(string? tab, string? dashboardPeriod = null)
    {
        var initialTab = tab?.Trim().ToLowerInvariant() switch
        {
            "reviewer" => "reviewer",
            "audit" => "audit",
            "dashboard" => "dashboard",
            _ => "applicant"
        };

        ViewData["InitialTab"] = initialTab;
        var period = ParseDashboardPeriod(dashboardPeriod);
        if (period.Year is not null)
        {
            ViewData["DashboardYear"] = period.Year;
            ViewData["DashboardMonth"] = period.Month;
        }
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult EmployeeEscalations() => ViewComponent("EmployeeEscalationQueue");

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult ReviewerQueue() => ViewComponent("EscalationQueue");

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult AuditTrail() => ViewComponent("AuditLogQueue");

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult DashboardSummary(string? dashboardPeriod = null)
    {
        var period = ParseDashboardPeriod(dashboardPeriod);
        return ViewComponent("DashboardSummary", new { year = period.Year, month = period.Month });
    }

    private static (int? Year, int? Month) ParseDashboardPeriod(string? value)
    {
        var parts = value?.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts is { Length: 2 } && int.TryParse(parts[0], out var year) &&
            int.TryParse(parts[1], out var month) && year is >= 2000 and <= 2100 && month is >= 1 and <= 12
            ? (year, month) : (null, null);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
