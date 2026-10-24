using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

public sealed class ReceiptRetentionOptions
{
    public const string SectionName = "DataRetention";

    /// <summary>Destructive cleanup is opt-in and must be explicitly enabled per environment.</summary>
    public bool Enabled { get; set; }

    [Range(1, 1440)]
    public int SweepIntervalMinutes { get; set; } = 5;

    [Range(1, 500)]
    public int BatchSize { get; set; } = 25;
}
