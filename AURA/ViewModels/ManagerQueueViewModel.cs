using AURA.Models;

namespace AURA.ViewModels;

public sealed class ManagerQueueViewModel
{
    public IReadOnlyList<ManagerQueueItem> Items { get; init; } = Array.Empty<ManagerQueueItem>();
    public int TargetHours { get; init; }
    public int OverdueCount { get; init; }
}

public sealed class ManagerQueueItem
{
    public required ReimbursementRequest Request { get; init; }

    /// <summary>Time since the employee forwarded the case; null for legacy rows without a timestamp.</summary>
    public TimeSpan? Waited { get; init; }

    public DateTime? ForwardedAtVietnam { get; init; }
    public bool IsOverdue { get; init; }
}
