using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

public sealed class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    [Required]
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/";

    [Required]
    public string Model { get; set; } = "qwen/qwen3.8-27b:free";

    [Required]
    public string PolicyPath { get; set; } = "BUSINESS_RULES.md";

    public string? ApiKey { get; set; }

    [Range(10, 180)]
    public int TimeoutSeconds { get; set; } = 90;
}
