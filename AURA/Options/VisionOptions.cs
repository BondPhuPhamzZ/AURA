using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

public sealed class VisionOptions
{
    public const string SectionName = "Vision";
    public const string OpenRouterProvider = "OpenRouter";
    public const string OllamaProvider = "Ollama";

    [Required]
    public string Provider { get; set; } = OpenRouterProvider;

    public bool FallbackEnabled { get; set; }

    [Required]
    public string FallbackProvider { get; set; } = OllamaProvider;

    [Range(1, 10)]
    public int CircuitBreakFailureThreshold { get; set; } = 3;

    [Range(10, 3600)]
    public int CircuitBreakSeconds { get; set; } = 60;

    public static bool IsSupportedProvider(string? provider) =>
        string.Equals(provider, OpenRouterProvider, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider, OllamaProvider, StringComparison.OrdinalIgnoreCase);
}
