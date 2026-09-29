using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

public sealed class ReceiptProcessingOptions
{
    public const string SectionName = "ReceiptProcessing";

    [Range(250, 30000)]
    public int PollIntervalMs { get; set; } = 5000;

    [Range(30, 3600)]
    public int LeaseSeconds { get; set; } = 600;

    [Range(5, 300)]
    public int FailureBackoffMaxSeconds { get; set; } = 60;
}
