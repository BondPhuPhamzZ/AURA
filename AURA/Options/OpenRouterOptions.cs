using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

public sealed class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    [Required]
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/";

    [Required]
    public string Model { get; set; } = "qwen/qwen-vl-max";

    public string? ApiKey { get; set; }

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 60;
}
