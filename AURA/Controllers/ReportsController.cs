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
    public async Task<IActionResult> CurrentMonth(int? year = null, int? month = null)
    {
        try
        {
            var requests = await _repository.GetAllRequestsAsync();
            var localNow = VietnamTime.FromUtc(DateTime.UtcNow);
            var targetYear = year is >= 2000 and <= 2100 ? year.Value : localNow.Year;
            var targetMonth = month is >= 1 and <= 12 ? month.Value : localNow.Month;
            var report = ManagerOperationsReportExporter.ExportMonth(requests, targetYear, targetMonth);
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

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> CurrentMonthWorkbook(int? year = null, int? month = null)
    {
        try
        {
            var requests = await _repository.GetAllRequestsAsync();
            var localNow = VietnamTime.FromUtc(DateTime.UtcNow);
            var targetYear = year is >= 2000 and <= 2100 ? year.Value : localNow.Year;
            var targetMonth = month is >= 1 and <= 12 ? month.Value : localNow.Month;
            var report = ManagerOperationsReportExporter.ExportWorkbookMonth(requests, targetYear, targetMonth);
            return File(report.Content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", report.FileName);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cannot export the selected-month operations workbook.");
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Không thể tạo báo cáo Excel",
                detail: "Hãy kiểm tra kết nối cơ sở dữ liệu rồi thử lại.");
        }
    }
}
