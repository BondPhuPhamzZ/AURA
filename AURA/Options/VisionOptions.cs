using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

public sealed class VisionOptions
{
    public const string SectionName = "Vision";
    public const string OpenRouterProvider = "OpenRouter";
    public const string OllamaProvider = "Ollama";

    [Required]
    public string Provider { get; set; } = OpenRouterProvider;

    public static bool IsSupportedProvider(string? provider) =>
        string.Equals(provider, OpenRouterProvider, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider, OllamaProvider, StringComparison.OrdinalIgnoreCase);
}
