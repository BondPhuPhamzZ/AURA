using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

/// <summary>
/// Local Tesseract OCR that draws evidence regions in the reviewer's receipt viewer. Regions are a
/// reading aid only: they never change extracted facts, policy or decisions. Development-only.
/// </summary>
public sealed class EvidenceLocalizationOptions
{
    public const string SectionName = "EvidenceLocalization";

    public bool Enabled { get; set; }

    /// <summary>Absolute folder holding the pinned *.traineddata files (see tools/Get-TesseractData.ps1).</summary>
    public string TessdataPath { get; set; } = string.Empty;

    [RegularExpression("^[a-z_]{3,10}(\\+[a-z_]{3,10}){0,3}$")]
    public string Languages { get; set; } = "vie+eng";

    [Range(5, 120)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Only a Development host may run the localizer, mirroring the Verify harness gate.</summary>
    public static bool IsActive(EvidenceLocalizationOptions options, string environmentName) =>
        options.Enabled && string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);

    public static bool HasLanguageData(EvidenceLocalizationOptions options) =>
        Path.IsPathFullyQualified(options.TessdataPath) &&
        options.Languages.Split('+').All(language =>
            File.Exists(Path.Combine(options.TessdataPath, language + ".traineddata")));
}
