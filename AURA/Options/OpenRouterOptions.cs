using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

public sealed class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    [Required]
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/";

    [Required]
    public string ChatCompletionsPath { get; set; } = "chat/completions";

    [Required]
    public string Model { get; set; } = "qwen/qwen3.8-flash";

    [Required]
    public string PolicyPath { get; set; } = "BUSINESS_RULES.md";

    public string? ApiKey { get; set; }

    [Range(10, 180)]
    public int TimeoutSeconds { get; set; } = 90;

    [Range(512, 16384)]
    public int MaxOutputTokens { get; set; } = 4096;

    /// <summary>
    /// Optional OpenRouter reasoning effort. AURA defaults to "none" because this adapter performs
    /// deterministic extraction and reasoning tokens otherwise consume the JSON output budget.
    /// Set an empty value only when a separately benchmarked model should use its provider default.
    /// </summary>
    public string? ReasoningEffort { get; set; } = "none";

    [Required]
    public string HttpReferer { get; set; } = "https://github.com/BondPhuPhamzZ/AURA";

    [Required]
    public string AppTitle { get; set; } = "AURA - The Escalation Referee";

    public static bool IsSupportedReasoningEffort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;

        return value.Trim().ToLowerInvariant() is
            "none" or "minimal" or "low" or "medium" or "high" or "xhigh" or "max";
    }
}
