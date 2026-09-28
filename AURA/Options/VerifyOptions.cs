using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

public sealed class VerifyOptions
{
    public const string SectionName = "Verify";

    [Range(0, 30000)]
    public int InterCaseDelayMs { get; set; } = 4000;
}
