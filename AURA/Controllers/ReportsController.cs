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
            var (targetYear, targetMonth, requests) = await LoadMonthAsync(year, month);
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
            var (targetYear, targetMonth, requests) = await LoadMonthAsync(year, month);
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

    // Reads only the selected Vietnam month from SQL; the exporter still filters by local month.
    private async Task<(int Year, int Month, List<AURA.Models.ReimbursementRequest> Requests)> LoadMonthAsync(
        int? year, int? month)
    {
        var (targetYear, targetMonth) = ReportingPeriod.ResolveOrCurrent(year, month, DateTime.UtcNow);
        var (fromUtc, toUtc) = ReportingPeriod.MonthUtcRange(targetYear, targetMonth);
        return (targetYear, targetMonth, await _repository.GetRequestsCreatedBetweenAsync(fromUtc, toUtc));
    }
}
