using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

/// <summary>
/// Target time for a manager to decide a forwarded escalation. It only orders and highlights
/// the manager queue; overdue cases are never approved, rejected or re-routed automatically.
/// </summary>
public sealed class ReviewSlaOptions
{
    public const string SectionName = "ReviewSla";

    [Range(1, 720)]
    public int TargetHours { get; set; } = 24;
}
