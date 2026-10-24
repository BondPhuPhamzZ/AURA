using AURA.Models;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptRetentionPolicyTests
{
    [Fact]
    public void Automated_deadline_is_seven_days_after_completed_auto_approval_only()
    {
        var completedAt = new DateTime(2026, 10, 1, 12, 30, 0, DateTimeKind.Utc);
        var request = new ReimbursementRequest
        {
            Status = "AUTO_APPROVE",
            ProcessingState = ProcessingStates.Completed,
            ProcessingCompletedAt = completedAt
        };

        Assert.Equal(completedAt.AddDays(7), ReceiptRetentionPolicy.ForAutomatedDecision(request));

        request.Status = "ESCALATE_FACT";
        Assert.Null(ReceiptRetentionPolicy.ForAutomatedDecision(request));
        request.Status = "AUTO_APPROVE";
        request.ProcessingState = ProcessingStates.Pending;
        Assert.Null(ReceiptRetentionPolicy.ForAutomatedDecision(request));
    }

    [Fact]
    public void Manager_deadline_requires_a_recorded_final_manager_outcome()
    {
        var decidedAt = new DateTime(2026, 10, 1, 12, 30, 0, DateTimeKind.Utc);
        var request = new ReimbursementRequest
        {
            Status = "RETURNED_BY_MANAGER",
            ManagerAnswer = false,
            ManagerDecisionAt = decidedAt,
            ProcessingState = ProcessingStates.Failed
        };

        Assert.Equal(decidedAt.AddDays(7), ReceiptRetentionPolicy.ForManagerDecision(request));
        Assert.True(ReceiptRetentionPolicy.IsFinalized(request));

        request.ManagerAnswer = null;
        request.ManagerDecisionAt = null;
        request.Status = "ESCALATE_SYSTEM_ERROR";
        Assert.Null(ReceiptRetentionPolicy.ForManagerDecision(request));
        Assert.False(ReceiptRetentionPolicy.IsFinalized(request));
    }

    [Fact]
    public void Expiry_is_inclusive_and_does_not_expire_a_null_deadline()
    {
        var now = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(ReceiptRetentionPolicy.IsExpired(now, now));
        Assert.True(ReceiptRetentionPolicy.IsExpired(now.AddTicks(-1), now));
        Assert.False(ReceiptRetentionPolicy.IsExpired(now.AddTicks(1), now));
        Assert.False(ReceiptRetentionPolicy.IsExpired(null, now));
    }
}
