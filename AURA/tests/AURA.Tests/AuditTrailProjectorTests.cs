using AURA.Models;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class AuditTrailProjectorTests
{
    [Fact]
    public void Projects_one_history_row_per_request_and_preserves_full_timeline()
    {
        var first = new ReimbursementRequest { Id = "request-a", Status = "MANUAL_REVIEW_ACCEPTED" };
        var second = new ReimbursementRequest { Id = "request-b", Status = "AUTO_APPROVE" };
        var baseTime = new DateTime(2026, 9, 22, 1, 0, 0, DateTimeKind.Utc);
        var logs = new[]
        {
            new AuditLog { Id = 1, RequestId = first.Id, Action = "VERIFY_ESCALATE_FACT", Timestamp = baseTime },
            new AuditLog { Id = 2, RequestId = first.Id, Action = "EMPLOYEE_FORWARDED_TO_MANAGER", Timestamp = baseTime.AddMinutes(1) },
            new AuditLog { Id = 3, RequestId = first.Id, Action = "MANAGER_YES", Timestamp = baseTime.AddMinutes(2) },
            new AuditLog { Id = 4, RequestId = second.Id, Action = "VERIFY_AUTO_APPROVE", Timestamp = baseTime.AddMinutes(3) }
        };
        var requests = new Dictionary<string, ReimbursementRequest>
        {
            [first.Id] = first,
            [second.Id] = second
        };

        var result = AuditTrailProjector.Project(logs, requests);

        Assert.Equal(2, result.Count);
        Assert.Equal(second.Id, result[0].Log.RequestId);
        var escalated = Assert.Single(result, entry => entry.Log.RequestId == first.Id);
        Assert.Equal("MANAGER_YES", escalated.Log.Action);
        Assert.Equal(3, escalated.Timeline.Count);
        Assert.Equal("VERIFY_ESCALATE_FACT", escalated.Timeline[^1].Action);
    }
}
