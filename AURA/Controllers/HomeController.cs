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
        if (!string.IsNullOrWhiteSpace(dashboardPeriod))
        {
            var parts = dashboardPeriod.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out var year) &&
                int.TryParse(parts[1], out var month) && year is >= 2000 and <= 2100 && month is >= 1 and <= 12)
            {
                ViewData["DashboardYear"] = year;
                ViewData["DashboardMonth"] = month;
            }
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

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
