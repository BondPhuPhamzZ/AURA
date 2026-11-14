using AURA.Models;
using AURA.ViewModels;

namespace AURA.Services;

/// <summary>
/// Measures how long a forwarded escalation has waited for the manager. The result is
/// informational: it never changes a request's status or decision.
/// </summary>
public static class ReviewSlaPolicy
{
    public static ManagerQueueViewModel Project(IEnumerable<ReimbursementRequest> forwardedEscalations,
        DateTime utcNow, int targetHours)
    {
        if (targetHours < 1) throw new ArgumentOutOfRangeException(nameof(targetHours));
        var target = TimeSpan.FromHours(targetHours);
        var items = forwardedEscalations
            .Select(request => Evaluate(request, utcNow, target))
            .ToList();
        return new ManagerQueueViewModel
        {
            Items = items,
            TargetHours = targetHours,
            OverdueCount = items.Count(item => item.IsOverdue)
        };
    }

    internal static ManagerQueueItem Evaluate(ReimbursementRequest request, DateTime utcNow, TimeSpan target)
    {
        if (request.ForwardedAt is not { } forwardedAt)
            return new ManagerQueueItem { Request = request };

        var forwardedUtc = forwardedAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(forwardedAt, DateTimeKind.Utc)
            : forwardedAt.ToUniversalTime();
        // A clock behind the database can make the wait negative; show zero rather than a negative age.
        var waited = utcNow - forwardedUtc;
        if (waited < TimeSpan.Zero) waited = TimeSpan.Zero;
        return new ManagerQueueItem
        {
            Request = request,
            Waited = waited,
            ForwardedAtVietnam = VietnamTime.FromUtc(forwardedUtc),
            IsOverdue = waited > target
        };
    }

    /// <summary>Compact Vietnamese duration such as "45 phút", "3 giờ 12 phút" or "2 ngày 4 giờ".</summary>
    public static string FormatWait(TimeSpan waited)
    {
        if (waited < TimeSpan.FromMinutes(1)) return "Dưới 1 phút";
        if (waited < TimeSpan.FromHours(1)) return $"{(int)waited.TotalMinutes} phút";
        if (waited < TimeSpan.FromDays(1))
            return waited.Minutes == 0
                ? $"{(int)waited.TotalHours} giờ"
                : $"{(int)waited.TotalHours} giờ {waited.Minutes} phút";
        return waited.Hours == 0
            ? $"{(int)waited.TotalDays} ngày"
            : $"{(int)waited.TotalDays} ngày {waited.Hours} giờ";
    }
}
