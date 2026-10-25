namespace AURA.Options;

public sealed class ExternalImagePrivacyOptions
{
    public const string SectionName = "ExternalImagePrivacy";
    public const string LegacyConfigured = "LegacyConfigured";
    public const string BlockExternal = "BlockExternal";
    public const string ApprovedImagesOnly = "ApprovedImagesOnly";

    // Preserve the contest configuration; the separate synthetic launcher opts
    // into approval. LegacyConfigured is not a personal-data protection claim.
    public string Mode { get; set; } = LegacyConfigured;
    public string? ApprovedHashesFile { get; set; }

    public static bool IsSupported(string? mode) =>
        new[] { LegacyConfigured, BlockExternal, ApprovedImagesOnly }
            .Contains(mode, StringComparer.OrdinalIgnoreCase);
}
