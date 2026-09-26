using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed class OllamaVisionExtractorService : IVisionExtractor
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<OllamaVisionExtractorService> _logger;

    public OllamaVisionExtractorService(HttpClient httpClient, IOptions<OllamaOptions> options,
        IWebHostEnvironment environment, ILogger<OllamaVisionExtractorService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<ReceiptExtractionDto> ExtractFactsAsync(string physicalImagePath,
        CancellationToken cancellationToken = default)
    {
        var input = await ReceiptExtractionContract.LoadInputAsync(physicalImagePath, _options.PolicyPath,
            _environment.ContentRootPath, cancellationToken);
        var schema = ReceiptExtractionContract.BuildResponseSchema();
        var schemaJson = JsonSerializer.Serialize(schema);

        var firstFacts = await RequestFactsOnceAsync(BuildPayload(input, schema, schemaJson, null), cancellationToken);
        var firstIssues = ReceiptSemanticValidator.Validate(firstFacts);
        if (firstIssues.Count == 0) return firstFacts;

        _logger.LogWarning(
            "Ollama returned schema-valid but semantically inconsistent receipt facts: {Issues}. Retrying once with correction guidance.",
            string.Join(" | ", firstIssues));

        try
        {
            var repairInstruction = ReceiptSemanticValidator.BuildRepairInstruction(firstIssues);
            var repairedFacts = await RequestFactsOnceAsync(
                BuildPayload(input, schema, schemaJson, repairInstruction), cancellationToken);
            var remainingIssues = ReceiptSemanticValidator.Validate(repairedFacts);
            return remainingIssues.Count == 0
                ? repairedFacts
                : ReceiptSemanticValidator.MarkUnresolved(repairedFacts, remainingIssues);
        }
        catch (VisionExtractionException exception)
        {
            // The first response remains useful evidence, but it must never be auto-approved.
            // A failed optional repair is therefore a FACT escalation rather than a provider outage.
            _logger.LogWarning(exception,
                "Ollama semantic repair failed; returning the first extraction marked for manual review.");
            return ReceiptSemanticValidator.MarkUnresolved(firstFacts, firstIssues);
        }
    }

    private object BuildPayload(ReceiptExtractionContract.Input input, object schema, string schemaJson,
        string? repairInstruction) => new
    {
        model = _options.Model,
        messages = new object[]
        {
            new
            {
                role = "system",
                content = $"{input.SystemPrompt}\nJSON Schema:\n{schemaJson}"
            },
            new
            {
                role = "user",
                content = repairInstruction ??
                    "Trích xuất dữ kiện từ ảnh chứng từ này và chỉ trả về JSON đúng schema.",
                images = new[] { input.Base64Image }
            }
        },
        stream = false,
        format = schema,
        keep_alive = _options.KeepAlive,
        options = new
        {
            temperature = 0,
            num_ctx = _options.ContextTokens,
            num_predict = _options.MaxOutputTokens
        }
    };

    private async Task<ReceiptExtractionDto> RequestFactsOnceAsync(object payload,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(_options.ChatPath, payload, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new VisionExtractionException("AI_TIMEOUT",
                $"Ollama không phản hồi trong {_options.TimeoutSeconds} giây; hồ sơ cần kiểm tra thủ công.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new VisionExtractionException("AI_LOCAL_UNAVAILABLE",
                "Không kết nối được Ollama local. Hãy kiểm tra ứng dụng Ollama và model đã cài.", exception);
        }

        using (response)
        {
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw CreateHttpFailure(response.StatusCode, responseText);

            try
            {
                using var document = JsonDocument.Parse(responseText);
                var root = document.RootElement;
                if (root.TryGetProperty("done_reason", out var doneReason) &&
                    string.Equals(doneReason.GetString(), "length", StringComparison.OrdinalIgnoreCase))
                    throw new VisionExtractionException("AI_RESPONSE_TRUNCATED",
                        "Ollama đạt giới hạn output trước khi hoàn tất JSON; hồ sơ cần kiểm tra thủ công.");

                if (!root.TryGetProperty("message", out var message) ||
                    !message.TryGetProperty("content", out var content) ||
                    content.ValueKind != JsonValueKind.String)
                    throw new VisionExtractionException("AI_INVALID_RESPONSE",
                        "Ollama không trả về message JSON hợp lệ; hồ sơ cần kiểm tra thủ công.");

                return ReceiptExtractionContract.ParseFacts(content.GetString() ?? string.Empty);
            }
            catch (VisionExtractionException)
            {
                throw;
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Ollama returned invalid receipt JSON.");
                throw new VisionExtractionException("AI_SCHEMA_MISMATCH",
                    "Ollama trả về kết quả không đúng cấu trúc quy định; hồ sơ cần kiểm tra thủ công.", exception);
            }
        }
    }

    private VisionExtractionException CreateHttpFailure(HttpStatusCode statusCode, string responseBody)
    {
        var providerMessage = ReadOllamaError(responseBody);
        return statusCode switch
        {
            HttpStatusCode.NotFound => new VisionExtractionException("AI_MODEL_UNAVAILABLE",
                $"Ollama chưa có model '{_options.Model}'. Hãy chạy ollama pull {_options.Model}."),
            HttpStatusCode.BadRequest => new VisionExtractionException("AI_REQUEST_INVALID",
                $"Ollama không chấp nhận payload ảnh/JSON hiện tại. {providerMessage}".Trim()),
            _ => new VisionExtractionException("AI_LOCAL_UNAVAILABLE",
                $"Ollama local trả HTTP {(int)statusCode}. {providerMessage}".Trim())
        };
    }

    private static string ReadOllamaError(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.TryGetProperty("error", out var error)
                ? error.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}
