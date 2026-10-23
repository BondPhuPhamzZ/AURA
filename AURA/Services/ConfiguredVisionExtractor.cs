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
    private readonly VisionInvocationCoordinator _coordinator;
    private readonly ILogger<ConfiguredVisionExtractor> _logger;

    public ConfiguredVisionExtractor(IOptions<VisionOptions> options,
        OpenRouterVisionExtractorService openRouter, OllamaVisionExtractorService ollama,
        VisionExecutionContext execution, VisionCircuitBreaker circuitBreaker,
        VisionInvocationCoordinator coordinator,
        ILogger<ConfiguredVisionExtractor> logger)
    {
        _options = options.Value;
        _openRouter = openRouter;
        _ollama = ollama;
        _execution = execution;
        _circuitBreaker = circuitBreaker;
        _coordinator = coordinator;
        _logger = logger;
    }

    public async Task<ReceiptExtractionDto> ExtractFactsAsync(string physicalImagePath,
        CancellationToken cancellationToken = default)
    {
        await using var capacityLease = await _coordinator.EnterAsync(cancellationToken);
        using var invocationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, capacityLease.LostToken);
        var invocationToken = invocationCancellation.Token;
        var primary = NormalizeProvider(_options.Provider);
        var fallback = NormalizeProvider(_options.FallbackProvider);
        _execution.Begin(primary);

        var canFallback = _options.FallbackEnabled && !string.Equals(primary, fallback, StringComparison.OrdinalIgnoreCase);
        var remainingCooldown = await _coordinator.GetRemainingCooldownAsync(primary,
            DateTime.UtcNow, invocationToken);
        if (remainingCooldown is not null)
        {
            _execution.MarkPrimaryFailure("AI_PROVIDER_COOLDOWN");
            if (canFallback)
            {
                _logger.LogWarning(
                    "Skipping primary vision provider {PrimaryProvider} for {CooldownSeconds:F0}s; using {FallbackProvider}.",
                    primary, remainingCooldown.Value.TotalSeconds, fallback);
                return await ExtractWithFallbackAsync(fallback, physicalImagePath, invocationToken,
                    capacityLease);
            }

            throw new VisionExtractionException("AI_PROVIDER_COOLDOWN",
                $"Nhà cung cấp AI đang trong thời gian chờ sau lỗi giới hạn tải. Vui lòng thử lại sau khoảng {Math.Ceiling(remainingCooldown.Value.TotalSeconds)} giây.",
                retryAfter: remainingCooldown, provider: primary);
        }

        if (canFallback && _circuitBreaker.ShouldSkipPrimary(DateTime.UtcNow))
        {
            _execution.MarkPrimaryFailure("CIRCUIT_OPEN");
            return await ExtractWithFallbackAsync(fallback, physicalImagePath, invocationToken,
                capacityLease);
        }

        try
        {
            var result = await ExtractWithProviderAsync(primary, physicalImagePath, invocationToken);
            await capacityLease.AssertOwnershipAsync(invocationToken);
            _circuitBreaker.RecordSuccess();
            await _coordinator.ClearCooldownAsync(primary, invocationToken);
            _execution.MarkSuccess(primary);
            return result;
        }
        catch (OperationCanceledException) when (capacityLease.LostToken.IsCancellationRequested)
        {
            _execution.MarkPrimaryFailure("AI_GATE_LOST");
            throw new VisionExtractionException("AI_GATE_LOST",
                "Quyền gọi AI dùng chung bị gián đoạn; hồ sơ sẽ được xử lý lại sau khi lease hết hạn.");
        }
        catch (VisionExtractionException primaryFailure)
        {
            _execution.MarkPrimaryFailure(primaryFailure.Code);
            if (VisionFallbackPolicy.IsEligible(primaryFailure.Code))
            {
                _circuitBreaker.RecordFailure(DateTime.UtcNow, _options.CircuitBreakFailureThreshold,
                    TimeSpan.FromSeconds(_options.CircuitBreakSeconds));
            }

            if (primaryFailure.Code == "AI_RATE_LIMIT")
            {
                var cooldown = primaryFailure.RetryAfter ?? TimeSpan.FromSeconds(_options.ProviderCooldownSeconds);
                await _coordinator.RecordCooldownAsync(primary, DateTime.UtcNow, cooldown,
                    invocationToken);
                if (!canFallback && primaryFailure.RetryAfter is null)
                {
                    throw new VisionExtractionException(primaryFailure.Code, primaryFailure.UserMessage,
                        primaryFailure, cooldown, primaryFailure.Provider ?? primary,
                        primaryFailure.RequestId);
                }
            }

            if (canFallback && VisionFallbackPolicy.IsEligible(primaryFailure.Code))
            {
                _logger.LogWarning(primaryFailure,
                    "Primary vision provider {PrimaryProvider} failed with {ErrorCode}; trying {FallbackProvider} once.",
                    primary, primaryFailure.Code, fallback);
                return await ExtractWithFallbackAsync(fallback, physicalImagePath, invocationToken,
                    capacityLease);
            }

            throw;
        }
    }

    private async Task<ReceiptExtractionDto> ExtractWithFallbackAsync(string fallback, string imagePath,
        CancellationToken cancellationToken,
        VisionInvocationCoordinator.VisionInvocationLease capacityLease)
    {
        var remainingCooldown = await _coordinator.GetRemainingCooldownAsync(fallback,
            DateTime.UtcNow, cancellationToken);
        if (remainingCooldown is not null)
        {
            throw new VisionExtractionException("AI_ALL_PROVIDERS_UNAVAILABLE",
                $"Provider chính không khả dụng và fallback {fallback} đang trong thời gian chờ sau lỗi giới hạn tải.",
                retryAfter: remainingCooldown, provider: fallback);
        }

        try
        {
            var result = await ExtractWithProviderAsync(fallback, imagePath, cancellationToken);
            await capacityLease.AssertOwnershipAsync(cancellationToken);
            await _coordinator.ClearCooldownAsync(fallback, cancellationToken);
            _execution.MarkSuccess(fallback, fallbackUsed: true);
            return result;
        }
        catch (VisionExtractionException fallbackFailure)
        {
            if (fallbackFailure.Code == "AI_RATE_LIMIT")
            {
                var cooldown = fallbackFailure.RetryAfter ??
                    TimeSpan.FromSeconds(_options.ProviderCooldownSeconds);
                await _coordinator.RecordCooldownAsync(fallback, DateTime.UtcNow, cooldown,
                    cancellationToken);
            }

            throw new VisionExtractionException("AI_ALL_PROVIDERS_UNAVAILABLE",
                $"Provider chính không khả dụng và fallback {fallback} cũng thất bại ({fallbackFailure.Code}).",
                fallbackFailure,
                fallbackFailure.RetryAfter ?? (fallbackFailure.Code == "AI_RATE_LIMIT"
                    ? TimeSpan.FromSeconds(_options.ProviderCooldownSeconds)
                    : null),
                fallbackFailure.Provider ?? fallback,
                fallbackFailure.RequestId);
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
