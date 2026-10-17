using System.Text.Json;
using AURA.Models;
using AURA.ViewModels;

namespace AURA.Services;

public static class DashboardSummaryProjector
{
    public static DashboardSummaryViewModel Project(
        IEnumerable<ReimbursementRequest> requests, DateTime utcNow)
    {
        var localNow = VietnamTime.FromUtc(utcNow);
        var projected = requests
            .Select(request => new ProjectedRequest(request, VietnamTime.FromUtc(request.CreatedAt),
                HasPrivacyReview(request)))
            .ToArray();
        var yearly = projected
            .Where(item => item.LocalCreatedAt.Year == localNow.Year)
            .ToArray();
        var monthly = yearly
            .Where(item => item.LocalCreatedAt.Year == localNow.Year &&
                           item.LocalCreatedAt.Month == localNow.Month)
            .ToArray();

        var completed = monthly.Where(item =>
            item.Request.ProcessingState is ProcessingStates.Completed or ProcessingStates.Failed).ToArray();
        var measuredLatency = completed.Where(item => item.Request.ProcessingLatencyMs > 0).ToArray();
        var days = monthly
            .GroupBy(item => DateOnly.FromDateTime(item.LocalCreatedAt))
            .OrderByDescending(group => group.Key)
            .Select(group => BuildDay(group.Key, group))
            .ToArray();
        var months = yearly
            .GroupBy(item => item.LocalCreatedAt.Month)
            .OrderByDescending(group => group.Key)
            .Select(group => BuildMonth(localNow.Year, group.Key, group))
            .ToArray();

        return new DashboardSummaryViewModel
        {
            IsAvailable = true,
            PeriodLabel = $"Tháng {localNow:MM/yyyy}",
            YearLabel = $"Năm {localNow:yyyy}",
            Received = monthly.Length,
            Completed = completed.Length,
            AutoApproved = monthly.Count(item => item.Request.Status == "AUTO_APPROVE"),
            Escalated = monthly.Count(item => PolicyDecisionEngine.IsEscalation(item.Request.Status)),
            PrivacyReview = monthly.Count(item => item.HasPrivacyReview),
            Failed = monthly.Count(item => item.Request.ProcessingState == ProcessingStates.Failed),
            TotalAmountVnd = monthly.Sum(item => Math.Max(0, item.Request.ClaimedAmount)),
            AverageLatencyMs = measuredLatency.Length == 0
                ? 0
                : (long)Math.Round(measuredLatency.Average(item => item.Request.ProcessingLatencyMs)),
            YearReceived = yearly.Length,
            YearCompleted = yearly.Count(item =>
                item.Request.ProcessingState is ProcessingStates.Completed or ProcessingStates.Failed),
            YearTotalAmountVnd = yearly.Sum(item => Math.Max(0, item.Request.ClaimedAmount)),
            Days = days,
            Months = months
        };
    }

    private static MonthlyReceiptSummaryViewModel BuildMonth(int year, int month,
        IEnumerable<ProjectedRequest> requests)
    {
        var items = requests.ToArray();
        return new MonthlyReceiptSummaryViewModel
        {
            Year = year,
            Month = month,
            Received = items.Length,
            Completed = items.Count(item =>
                item.Request.ProcessingState is ProcessingStates.Completed or ProcessingStates.Failed),
            AutoApproved = items.Count(item => item.Request.Status == "AUTO_APPROVE"),
            Escalated = items.Count(item => PolicyDecisionEngine.IsEscalation(item.Request.Status)),
            PrivacyReview = items.Count(item => item.HasPrivacyReview),
            Failed = items.Count(item => item.Request.ProcessingState == ProcessingStates.Failed),
            TotalAmountVnd = items.Sum(item => Math.Max(0, item.Request.ClaimedAmount))
        };
    }

    private static DailyReceiptSummaryViewModel BuildDay(DateOnly date,
        IEnumerable<ProjectedRequest> requests)
    {
        var items = requests.ToArray();
        return new DailyReceiptSummaryViewModel
        {
            Date = date,
            Received = items.Length,
            Completed = items.Count(item =>
                item.Request.ProcessingState is ProcessingStates.Completed or ProcessingStates.Failed),
            AutoApproved = items.Count(item => item.Request.Status == "AUTO_APPROVE"),
            Escalated = items.Count(item => PolicyDecisionEngine.IsEscalation(item.Request.Status)),
            PrivacyReview = items.Count(item => item.HasPrivacyReview),
            Failed = items.Count(item => item.Request.ProcessingState == ProcessingStates.Failed),
            TotalAmountVnd = items.Sum(item => Math.Max(0, item.Request.ClaimedAmount))
        };
    }

    private static bool HasPrivacyReview(ReimbursementRequest request)
    {
        if (request.Status == "ESCALATE_PRIVACY") return true;
        if (string.IsNullOrWhiteSpace(request.ExtractedFactsJson)) return false;

        try
        {
            var facts = JsonSerializer.Deserialize<ReceiptExtractionDto>(request.ExtractedFactsJson);
            return facts?.RequiresPrivacyReview == true || facts?.PersonalDataCategories.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record ProjectedRequest(
        ReimbursementRequest Request,
        DateTime LocalCreatedAt,
        bool HasPrivacyReview);
}
