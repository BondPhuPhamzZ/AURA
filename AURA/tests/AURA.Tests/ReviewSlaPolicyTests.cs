using AURA.Models;
using AURA.Options;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class ReviewSlaPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Waiting_time_is_measured_from_the_employee_handoff()
    {
        var model = ReviewSlaPolicy.Project(new[]
        {
            Request("old", Now.AddHours(-30)),
            Request("fresh", Now.AddMinutes(-45)),
            Request("legacy", null)
        }, Now, targetHours: 24);

        Assert.Equal(24, model.TargetHours);
        Assert.Equal(1, model.OverdueCount);
        Assert.Equal(new[] { "old", "fresh", "legacy" }, model.Items.Select(item => item.Request.Id));

        var old = model.Items[0];
        Assert.True(old.IsOverdue);
        Assert.Equal(TimeSpan.FromHours(30), old.Waited);
        // 13/11 02:00 UTC is 09:00 in Vietnam.
        Assert.Equal(new DateTime(2026, 11, 12, 9, 0, 0), old.ForwardedAtVietnam);

        Assert.False(model.Items[1].IsOverdue);
        Assert.Null(model.Items[2].Waited);
        Assert.False(model.Items[2].IsOverdue);
    }

    [Fact]
    public void Exactly_reaching_the_target_is_not_yet_overdue_and_clock_skew_is_not_negative()
    {
        var model = ReviewSlaPolicy.Project(new[]
        {
            Request("boundary", Now.AddHours(-24)),
            Request("just-over", Now.AddHours(-24).AddSeconds(-1)),
            // SQL rows come back as DateTimeKind.Unspecified; a host clock behind SQL gives a future time.
            Request("future", DateTime.SpecifyKind(Now.AddMinutes(5), DateTimeKind.Unspecified))
        }, Now, targetHours: 24);

        Assert.False(model.Items[0].IsOverdue);
        Assert.True(model.Items[1].IsOverdue);
        Assert.Equal(TimeSpan.Zero, model.Items[2].Waited);
        Assert.False(model.Items[2].IsOverdue);
    }

    [Fact]
    public void The_policy_never_changes_request_state()
    {
        var request = Request("unchanged", Now.AddDays(-10));
        var before = (request.Status, request.ManagerAnswer, request.ManagerDecisionAt, request.IsForwardedToManager);

        _ = ReviewSlaPolicy.Project(new[] { request }, Now, targetHours: 1);

        Assert.Equal(before, (request.Status, request.ManagerAnswer, request.ManagerDecisionAt,
            request.IsForwardedToManager));
    }

    [Theory]
    [InlineData(20, "Dưới 1 phút")]
    [InlineData(45 * 60, "45 phút")]
    [InlineData(3 * 3600, "3 giờ")]
    [InlineData(3 * 3600 + 12 * 60, "3 giờ 12 phút")]
    [InlineData(2 * 86400, "2 ngày")]
    [InlineData(2 * 86400 + 4 * 3600 + 59 * 60, "2 ngày 4 giờ")]
    public void Wait_labels_are_compact_vietnamese(int seconds, string expected)
    {
        Assert.Equal(expected, ReviewSlaPolicy.FormatWait(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(720, true)]
    [InlineData(721, false)]
    public void Target_hours_are_bounded(int hours, bool valid)
    {
        var options = new ReviewSlaOptions { TargetHours = hours };
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(options,
            new System.ComponentModel.DataAnnotations.ValidationContext(options), null, validateAllProperties: true);
        Assert.Equal(valid, isValid);
        Assert.Throws<ArgumentOutOfRangeException>(() => ReviewSlaPolicy.Project([], Now, 0));
    }

    private static ReimbursementRequest Request(string id, DateTime? forwardedAt) => new()
    {
        Id = id,
        Status = "ESCALATE_FACT",
        IsForwardedToManager = true,
        ForwardedAt = forwardedAt
    };
}
