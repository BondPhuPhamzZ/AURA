using System.Text.Json;
using AURA.Models;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class DashboardSummaryProjectorTests
{
    [Fact]
    public void Preserves_pre_inference_privacy_signal_after_manual_resolution_without_AI_facts()
    {
        var now = new DateTime(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc);
        var request = Request("blocked", now, "MANUAL_REVIEW_ACCEPTED", ProcessingStates.Completed, 0, 1);
        request.ExtractedFactsJson = null;
        request.ProviderErrorCode = ExternalImagePrivacyGate.ImageNotApprovedCode;
        request.ManagerAnswer = true;
        request.ManagerDecisionAt = now;
        var result = DashboardSummaryProjector.Project([request], now);
        Assert.Equal(1, result.PrivacyReview);
        Assert.Equal(1, Assert.Single(result.Days).PrivacyReview);
        Assert.Equal(1, Assert.Single(result.Months).PrivacyReview);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public void Projects_current_vietnam_month_and_daily_breakdown()
    {
        var requests = new[]
        {
            Request("approved", new DateTime(2026, 10, 16, 17, 30, 0, DateTimeKind.Utc),
                "AUTO_APPROVE", ProcessingStates.Completed, 120_000m, 1_000),
            Request("privacy", new DateTime(2026, 10, 17, 1, 30, 0, DateTimeKind.Utc),
                "ESCALATE_PRIVACY", ProcessingStates.Completed, 80_000m, 3_000, privacy: true),
            Request("failed", new DateTime(2026, 10, 17, 2, 30, 0, DateTimeKind.Utc),
                "ESCALATE_SYSTEM_ERROR", ProcessingStates.Failed, 0, 0),
            Request("old", new DateTime(2026, 9, 30, 1, 0, 0, DateTimeKind.Utc),
                "AUTO_APPROVE", ProcessingStates.Completed, 999_000m, 500)
        };

        var result = DashboardSummaryProjector.Project(requests,
            new DateTime(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc));

        Assert.Equal("Tháng 10/2026", result.PeriodLabel);
        Assert.Equal("Năm 2026", result.YearLabel);
        Assert.Equal(3, result.Received);
        Assert.Equal(3, result.Completed);
        Assert.Equal(1, result.AutoApproved);
        Assert.Equal(2, result.Escalated);
        Assert.Equal(1, result.PrivacyReview);
        Assert.Equal(1, result.Failed);
        Assert.Equal(200_000m, result.TotalAmountVnd);
        Assert.Equal(2_000, result.AverageLatencyMs);
        Assert.Equal(4, result.YearReceived);
        Assert.Equal(4, result.YearCompleted);
        Assert.Equal(1_199_000m, result.YearTotalAmountVnd);
        var day = Assert.Single(result.Days);
        Assert.Equal(new DateOnly(2026, 10, 17), day.Date);
        Assert.Equal(3, day.Received);
        Assert.Equal(2, result.Months.Count);
        Assert.Equal(3, result.Months[0].Received);
        Assert.Equal(1, result.Months[1].Received);
    }

    [Fact]
    public void Projects_a_selected_historical_month_instead_of_fixing_the_current_month()
    {
        var requests = new[]
        {
            Request("september", new DateTime(2026, 9, 12, 2, 0, 0, DateTimeKind.Utc),
                "AUTO_APPROVE", ProcessingStates.Completed, 90_000m, 900),
            Request("october", new DateTime(2026, 10, 12, 2, 0, 0, DateTimeKind.Utc),
                "AUTO_APPROVE", ProcessingStates.Completed, 120_000m, 1_200)
        };

        var result = DashboardSummaryProjector.Project(requests,
            new DateTime(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc), 2026, 9);

        Assert.Equal("Tháng 09/2026", result.PeriodLabel);
        Assert.Equal(2026, result.SelectedYear);
        Assert.Equal(9, result.SelectedMonth);
        Assert.Equal(1, result.Received);
        Assert.Equal(90_000m, result.TotalAmountVnd);
        Assert.Equal(2, result.YearReceived);
    }

    private static ReimbursementRequest Request(string id, DateTime createdAt, string status,
        string processingState, decimal amount, long latency, bool privacy = false)
    {
        var facts = new ReceiptExtractionDto
        {
            RequiresPrivacyReview = privacy,
            PersonalDataCategories = privacy ? ["PHONE"] : []
        };
        return new ReimbursementRequest
        {
            Id = id,
            OriginalFileName = id + ".jpg",
            StoredFileName = id + ".jpg",
            ContentType = "image/jpeg",
            FileSha256 = new string('A', 64),
            ImageUrl = "/receipt/" + id,
            CreatedAt = createdAt,
            Status = status,
            ProcessingState = processingState,
            ClaimedAmount = amount,
            ProcessingLatencyMs = latency,
            ExtractedFactsJson = JsonSerializer.Serialize(facts)
        };
    }
}
