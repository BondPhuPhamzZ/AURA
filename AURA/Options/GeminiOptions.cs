using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string Model { get; set; } = "gemini-3.6-flash";

    [Required]
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    [Required]
    public string PolicyPath { get; set; } = "BUSINESS_RULES.md";

    [Range(10, 180)]
    public int TimeoutSeconds { get; set; } = 60;
}
