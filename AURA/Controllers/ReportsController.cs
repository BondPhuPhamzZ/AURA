using AURA.Interfaces;
using AURA.Services;
using Microsoft.AspNetCore.Mvc;

namespace AURA.Controllers;

public sealed class ReportsController : Controller
{
    private readonly IReimbursementRepository _repository;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IReimbursementRepository repository,
        ILogger<ReportsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> CurrentMonth()
    {
        try
        {
            var requests = await _repository.GetAllRequestsAsync();
            var report = ManagerOperationsReportExporter.ExportCurrentMonth(requests, DateTime.UtcNow);
            return File(report.Content, "text/csv; charset=utf-8", report.FileName);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cannot export the current-month operations report.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Không thể tạo báo cáo",
                detail: "Hãy kiểm tra kết nối cơ sở dữ liệu rồi thử lại.");
        }
    }
}
