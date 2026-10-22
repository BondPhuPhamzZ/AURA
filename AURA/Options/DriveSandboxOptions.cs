using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

/// <summary>Opt-in, synthetic-only Drive ingestion. Never enabled on a public host.</summary>
public sealed class DriveSandboxOptions
{
    public const string SectionName = "DriveSandbox";
    public bool Enabled { get; set; }
    public string FolderId { get; set; } = string.Empty;
    public string ServiceAccountJsonPath { get; set; } = string.Empty;
    [Range(30, 3600)] public int PollIntervalSeconds { get; set; } = 120;
    [Range(1, 1000)] public int MaxFilesPerScan { get; set; } = 100;
    // No scanner is integrated. This may be false only for an isolated test folder
    // containing synthetic images; a real-data connector must fail closed.
    public bool RequireMalwareScanner { get; set; } = true;
}
