namespace AURA.ViewModels;

public sealed class DashboardSummaryViewModel
{
    public bool IsAvailable { get; init; }
    public required string PeriodLabel { get; init; }
    public required string YearLabel { get; init; }
    public int Received { get; init; }
    public int Completed { get; init; }
    public int AutoApproved { get; init; }
    public int Escalated { get; init; }
    public int PrivacyReview { get; init; }
    public int Failed { get; init; }
    public decimal TotalAmountVnd { get; init; }
    public long AverageLatencyMs { get; init; }
    public int YearReceived { get; init; }
    public int YearCompleted { get; init; }
    public decimal YearTotalAmountVnd { get; init; }
    public IReadOnlyList<DailyReceiptSummaryViewModel> Days { get; init; } = [];
    public IReadOnlyList<MonthlyReceiptSummaryViewModel> Months { get; init; } = [];
}

public sealed class MonthlyReceiptSummaryViewModel
{
    public int Year { get; init; }
    public int Month { get; init; }
    public int Received { get; init; }
    public int Completed { get; init; }
    public int AutoApproved { get; init; }
    public int Escalated { get; init; }
    public int PrivacyReview { get; init; }
    public int Failed { get; init; }
    public decimal TotalAmountVnd { get; init; }
}

public sealed class DailyReceiptSummaryViewModel
{
    public DateOnly Date { get; init; }
    public int Received { get; init; }
    public int Completed { get; init; }
    public int AutoApproved { get; init; }
    public int Escalated { get; init; }
    public int PrivacyReview { get; init; }
    public int Failed { get; init; }
    public decimal TotalAmountVnd { get; init; }
}
