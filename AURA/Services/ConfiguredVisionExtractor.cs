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
    private readonly VisionExecutionContext _execution;
    private readonly VisionCircuitBreaker _circuitBreaker;
    private readonly ILogger<ConfiguredVisionExtractor> _logger;

    public ConfiguredVisionExtractor(IOptions<VisionOptions> options,
        OpenRouterVisionExtractorService openRouter, OllamaVisionExtractorService ollama,
        VisionExecutionContext execution, VisionCircuitBreaker circuitBreaker,
        ILogger<ConfiguredVisionExtractor> logger)
    {
        _options = options.Value;
        _openRouter = openRouter;
        _ollama = ollama;
        _execution = execution;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }

    public async Task<ReceiptExtractionDto> ExtractFactsAsync(string physicalImagePath,
        CancellationToken cancellationToken = default)
    {
        var primary = NormalizeProvider(_options.Provider);
        var fallback = NormalizeProvider(_options.FallbackProvider);
        _execution.Begin(primary);

        var canFallback = _options.FallbackEnabled && !string.Equals(primary, fallback, StringComparison.OrdinalIgnoreCase);
        if (canFallback && _circuitBreaker.ShouldSkipPrimary(DateTime.UtcNow))
        {
            _execution.MarkPrimaryFailure("CIRCUIT_OPEN");
            return await ExtractWithFallbackAsync(fallback, physicalImagePath, cancellationToken);
        }

        try
        {
            var result = await ExtractWithProviderAsync(primary, physicalImagePath, cancellationToken);
            _circuitBreaker.RecordSuccess();
            _execution.MarkSuccess(primary);
            return result;
        }
        catch (VisionExtractionException primaryFailure) when (canFallback && VisionFallbackPolicy.IsEligible(primaryFailure.Code))
        {
            _execution.MarkPrimaryFailure(primaryFailure.Code);
            _circuitBreaker.RecordFailure(DateTime.UtcNow, _options.CircuitBreakFailureThreshold,
                TimeSpan.FromSeconds(_options.CircuitBreakSeconds));
            _logger.LogWarning(primaryFailure,
                "Primary vision provider {PrimaryProvider} failed with {ErrorCode}; trying {FallbackProvider} once.",
                primary, primaryFailure.Code, fallback);
            return await ExtractWithFallbackAsync(fallback, physicalImagePath, cancellationToken);
        }
        catch (VisionExtractionException primaryFailure)
        {
            _execution.MarkPrimaryFailure(primaryFailure.Code);
            throw;
        }
    }

    private async Task<ReceiptExtractionDto> ExtractWithFallbackAsync(string fallback, string imagePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExtractWithProviderAsync(fallback, imagePath, cancellationToken);
            _execution.MarkSuccess(fallback, fallbackUsed: true);
            return result;
        }
        catch (VisionExtractionException fallbackFailure)
        {
            throw new VisionExtractionException("AI_ALL_PROVIDERS_UNAVAILABLE",
                $"Provider chính không khả dụng và fallback {fallback} cũng thất bại ({fallbackFailure.Code}).",
                fallbackFailure);
        }
    }

    private Task<ReceiptExtractionDto> ExtractWithProviderAsync(string provider, string physicalImagePath,
        CancellationToken cancellationToken) => provider.ToUpperInvariant() switch
        {
            "OPENROUTER" => _openRouter.ExtractFactsAsync(physicalImagePath, cancellationToken),
            "OLLAMA" => _ollama.ExtractFactsAsync(physicalImagePath, cancellationToken),
            _ => throw new InvalidOperationException($"Vision provider '{provider}' is not supported.")
        };

    private static string NormalizeProvider(string provider) =>
        string.Equals(provider, VisionOptions.OpenRouterProvider, StringComparison.OrdinalIgnoreCase)
            ? VisionOptions.OpenRouterProvider
            : string.Equals(provider, VisionOptions.OllamaProvider, StringComparison.OrdinalIgnoreCase)
                ? VisionOptions.OllamaProvider
                : provider;
}
