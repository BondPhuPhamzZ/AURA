using System.ComponentModel.DataAnnotations;

namespace AURA.Options;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    [Required]
    public string BaseUrl { get; set; } = "http://127.0.0.1:11434/";

    [Required]
    public string ChatPath { get; set; } = "api/chat";

    [Required]
    public string Model { get; set; } = "qwen3-vl:4b-instruct";

    [Required]
    public string PolicyPath { get; set; } = "BUSINESS_RULES.md";

    [Range(30, 600)]
    public int TimeoutSeconds { get; set; } = 180;

    [Range(512, 8192)]
    public int MaxOutputTokens { get; set; } = 2048;

    [Range(2048, 32768)]
    public int ContextTokens { get; set; } = 8192;

    [Required]
    public string KeepAlive { get; set; } = "5m";
}
