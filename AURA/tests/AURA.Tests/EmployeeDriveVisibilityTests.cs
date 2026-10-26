using AURA.Models;
using AURA.ViewComponents;
using Xunit;

namespace AURA.Tests;

public sealed class EmployeeDriveVisibilityTests
{
    [Fact]
    public void Recent_Drive_jobs_are_visible_through_processing_and_result_but_not_on_public_profile()
    {
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        ReimbursementRequest Make(string id, string state, string status, int hoursAgo = 1,
            bool forwarded = false, string source = "DRIVE-SANDBOX") => new()
            {
                Id = id, ProcessingState = state, Status = status,
                SubmitterCode = source, CreatedAt = now.AddHours(-hoursAgo),
                QueuedAt = now.AddHours(-hoursAgo), IsForwardedToManager = forwarded
            };
        var requests = new[]
        {
            Make("pending", ProcessingStates.Pending, "PENDING"),
            Make("working", ProcessingStates.Processing, "PROCESSING"),
            Make("approved", ProcessingStates.Completed, "AUTO_APPROVE"),
            Make("escalated", ProcessingStates.Completed, "ESCALATE_POLICY"),
            Make("forwarded", ProcessingStates.Completed, "ESCALATE_POLICY", forwarded: true),
            Make("old", ProcessingStates.Completed, "AUTO_APPROVE", hoursAgo: 25),
            Make("manual", ProcessingStates.Completed, "AUTO_APPROVE", source: "EMP-001")
        };

        var local = EmployeeEscalationQueueViewComponent.SelectRows(requests, true, now);
        Assert.Equal(new[] { "approved", "escalated", "pending", "working" },
            local.Select(request => request.Id).OrderBy(id => id));
        Assert.Single(local, request => request.Id == "escalated");

        var publicRows = EmployeeEscalationQueueViewComponent.SelectRows(requests, false, now);
        Assert.Equal(new[] { "escalated" }, publicRows.Select(request => request.Id));
    }
}
