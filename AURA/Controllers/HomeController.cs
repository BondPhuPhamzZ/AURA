using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AURA.Models;
using AURA.Interfaces;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AURA.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IVisualValidator _validator;

    public HomeController(ILogger<HomeController> logger, IVisualValidator validator)
    {
        _logger = logger;
        _validator = validator;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> RunVerifyHarness()
    {
        // Mocking the 5 test cases from the seed data
        var testCases = new List<ReimbursementRequest>
        {
            new ReimbursementRequest { EmployeeName = "Nguyen Van A", ClaimedAmount = 150000, ImageUrl = "/test_data/images/tc1_grab.png" },
            new ReimbursementRequest { EmployeeName = "Tran Thi B", ClaimedAmount = 80000, ImageUrl = "/test_data/images/tc2_pho.png" },
            new ReimbursementRequest { EmployeeName = "Le Van C", ClaimedAmount = 350000, ImageUrl = "/test_data/images/tc3_vpp.png" },
            new ReimbursementRequest { EmployeeName = "Pham Thi D", ClaimedAmount = 200000, ImageUrl = "/test_data/images/tc4_blur.png" },
            new ReimbursementRequest { EmployeeName = "Hoang Van E", ClaimedAmount = 850000, ImageUrl = "/test_data/images/tc5_alcohol.png" }
        };

        var results = new List<ReimbursementRequest>();
        foreach(var tc in testCases)
        {
            var result = await _validator.ValidateReceiptAsync(tc);
            results.Add(result);
        }

        return Json(results);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
