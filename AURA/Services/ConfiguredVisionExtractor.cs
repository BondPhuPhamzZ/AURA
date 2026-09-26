using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed class ConfiguredVisionExtractor : IVisionExtractor
{
    private readonly VisionOptions _options;
    private readonly OpenRouterVisionExtractorService _openRouter;
    private readonly OllamaVisionExtractorService _ollama;

    public ConfiguredVisionExtractor(IOptions<VisionOptions> options,
        OpenRouterVisionExtractorService openRouter, OllamaVisionExtractorService ollama)
    {
        _options = options.Value;
        _openRouter = openRouter;
        _ollama = ollama;
    }

    public Task<ReceiptExtractionDto> ExtractFactsAsync(string physicalImagePath,
        CancellationToken cancellationToken = default) =>
        _options.Provider.ToUpperInvariant() switch
        {
            "OPENROUTER" => _openRouter.ExtractFactsAsync(physicalImagePath, cancellationToken),
            "OLLAMA" => _ollama.ExtractFactsAsync(physicalImagePath, cancellationToken),
            _ => throw new InvalidOperationException($"Vision provider '{_options.Provider}' is not supported.")
        };
}
