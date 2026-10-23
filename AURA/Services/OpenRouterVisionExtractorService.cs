using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed class OpenRouterVisionExtractorService : IVisionExtractor
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouterOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<OpenRouterVisionExtractorService> _logger;

    public OpenRouterVisionExtractorService(HttpClient httpClient, IOptions<OpenRouterOptions> options,
        IWebHostEnvironment environment, ILogger<OpenRouterVisionExtractorService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<ReceiptExtractionDto> ExtractFactsAsync(string physicalImagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new VisionExtractionException("AI_NOT_CONFIGURED",
                "OpenRouter API chưa được cấu hình; hồ sơ cần được kiểm tra thủ công.");

        var input = await ReceiptExtractionContract.LoadInputAsync(physicalImagePath, _options.PolicyPath,
            _environment.ContentRootPath, cancellationToken);
        var responseSchema = ReceiptExtractionContract.BuildResponseSchema();

        var firstFacts = await RequestFactsWithMalformedRetryAsync(
            instruction => BuildPayload(input, responseSchema, instruction), null, cancellationToken);
        var firstIssues = ReceiptSemanticValidator.Validate(firstFacts);
        if (firstIssues.Count == 0) return firstFacts;

        _logger.LogWarning(
            "Qwen returned schema-valid but semantically inconsistent receipt facts: {Issues}. Retrying once with correction guidance.",
            string.Join(" | ", firstIssues));

        try
        {
            var repairInstruction = ReceiptSemanticValidator.BuildRepairInstruction(firstIssues);
            var repairedFacts = await RequestFactsWithMalformedRetryAsync(
                instruction => BuildPayload(input, responseSchema, instruction), repairInstruction, cancellationToken);
            ReceiptSemanticValidator.MarkRepairAttempt(repairedFacts, firstIssues);
            var remainingIssues = ReceiptSemanticValidator.Validate(repairedFacts);
            return remainingIssues.Count == 0
                ? repairedFacts
                : ReceiptSemanticValidator.MarkUnresolved(repairedFacts, remainingIssues);
        }
        catch (VisionExtractionException exception)
        {
            _logger.LogWarning(exception,
                "Qwen semantic repair failed; returning the first extraction marked for manual review.");
            ReceiptSemanticValidator.MarkRepairAttempt(firstFacts, firstIssues);
            return ReceiptSemanticValidator.MarkUnresolved(firstFacts, firstIssues);
        }
    }

    private object BuildPayload(ReceiptExtractionContract.Input input, object responseSchema,
        string? repairInstruction)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["messages"] = new object[]
            {
                new { role = "system", content = input.SystemPrompt },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = repairInstruction ??
                                "Trích xuất dữ kiện từ ảnh chứng từ này và chỉ trả về JSON đúng schema."
                        },
                        new { type = "image_url", image_url = new { url = $"data:{input.MimeType};base64,{input.Base64Image}" } }
                    }
                }
            },
            ["temperature"] = 0,
            ["max_tokens"] = _options.MaxOutputTokens,
            ["provider"] = new
            {
                data_collection = _options.DenyProviderDataCollection ? "deny" : "allow",
                zdr = _options.RequireZeroDataRetention
            },
            ["plugins"] = new[] { new { id = "response-healing" } },
            ["response_format"] = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "aura_receipt_extraction",
                    strict = true,
                    schema = responseSchema
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(_options.ReasoningEffort))
        {
            payload["reasoning"] = new { effort = _options.ReasoningEffort.Trim().ToLowerInvariant() };
        }

        return payload;
    }

    private async Task<ReceiptExtractionDto> RequestFactsWithMalformedRetryAsync(
        Func<string?, object> buildPayload, string? instruction, CancellationToken cancellationToken)
    {
        const int maxMalformedResponseAttempts = 2;
        for (var attempt = 1; attempt <= maxMalformedResponseAttempts; attempt++)
        {
            try
            {
                var attemptInstruction = attempt == 1 ? instruction :
                    $"{instruction} Previous response failed the required JSON schema. Return one complete JSON object with every required field and evidenceContractVersion={ReceiptExtractionContract.CurrentEvidenceContractVersion}; use null or empty arrays when evidence is absent. No prose.";
                return await RequestFactsOnceAsync(buildPayload(attemptInstruction), cancellationToken);
            }
            catch (VisionExtractionException exception) when (
                attempt < maxMalformedResponseAttempts && IsMalformedResponse(exception.Code))
            {
                _logger.LogWarning(
                    "Qwen returned malformed structured output ({ErrorCode}); retrying once (attempt {Attempt}/{MaxAttempts}).",
                    exception.Code, attempt, maxMalformedResponseAttempts);
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }

        throw new UnreachableException();
    }

    private async Task<ReceiptExtractionDto> RequestFactsOnceAsync(object payload,
        CancellationToken cancellationToken)
    {
        ProviderResponse response;
        try
        {
            response = await SendWithRetryAsync(payload, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new VisionExtractionException("AI_TIMEOUT",
                $"Qwen không phản hồi trong {_options.TimeoutSeconds} giây. Đây là lỗi timeout mạng/dịch vụ hoặc model đang quá tải, không phải do nội dung 'kiểm thử' trong ảnh.", exception);
        }

        var (statusCode, responseText, retryAfter, requestId) = response;
        if ((int)statusCode is < 200 or >= 300)
        {
            var details = ReadProviderError(responseText);
            _logger.LogWarning(
                "OpenRouter returned HTTP {StatusCode} after retries. UpstreamProvider={UpstreamProvider}; RequestId={RequestId}; RetryAfterSeconds={RetryAfterSeconds}; ProviderMessage={ProviderMessage}",
                (int)statusCode, details.Provider ?? "<unknown>", requestId ?? "<none>",
                retryAfter?.TotalSeconds, details.Message);
            throw CreateHttpFailure(statusCode, details, _options.Model, retryAfter, requestId);
        }

        using var responseDocument = ParseResponseDocument(responseText);
        var root = responseDocument.RootElement;
        LogUsage(root);

        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            throw new VisionExtractionException("AI_EMPTY_RESPONSE",
                "Qwen không trả về kết quả trích xuất; hồ sơ cần kiểm tra thủ công.");

        var choice = choices[0];
        if (choice.TryGetProperty("finish_reason", out var finishReasonElement) &&
            string.Equals(finishReasonElement.GetString(), "length", StringComparison.OrdinalIgnoreCase))
            throw new VisionExtractionException("AI_RESPONSE_TRUNCATED",
                "Qwen đã dừng vì đạt giới hạn output trước khi hoàn tất JSON; hồ sơ cần kiểm tra thủ công.");

        if (!choice.TryGetProperty("message", out var message))
            throw new VisionExtractionException("AI_INVALID_RESPONSE",
                "Qwen không trả về message hợp lệ; hồ sơ cần kiểm tra thủ công.");

        if (!message.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.String)
            throw new VisionExtractionException("AI_INVALID_RESPONSE",
                "Qwen không trả về dữ liệu JSON hợp lệ; hồ sơ cần kiểm tra thủ công.");

        var contentStr = contentElement.GetString() ?? string.Empty;

        ReceiptExtractionDto facts;
        try
        {
            facts = ReceiptExtractionContract.ParseFacts(contentStr);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception,
                "Failed to parse Qwen JSON response ({ContentLength} characters) at JSON path {JsonPath}.",
                contentStr.Length, exception.Path ?? "<unknown>");
            throw new VisionExtractionException("AI_SCHEMA_MISMATCH",
                "Qwen trả về kết quả không đúng cấu trúc quy định; hồ sơ cần kiểm tra thủ công.", exception);
        }

        return facts;
    }

    private void LogUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
            return;

        var promptTokens = ReadInt64(usage, "prompt_tokens");
        var completionTokens = ReadInt64(usage, "completion_tokens");
        var totalTokens = ReadInt64(usage, "total_tokens");
        _logger.LogInformation(
            "OpenRouter token usage for {Model}: prompt={PromptTokens}, completion={CompletionTokens}, total={TotalTokens}, configured output cap={MaxOutputTokens}.",
            _options.Model, promptTokens, completionTokens, totalTokens, _options.MaxOutputTokens);
    }

    private static long? ReadInt64(JsonElement source, string propertyName) =>
        source.TryGetProperty(propertyName, out var value) && value.TryGetInt64(out var number)
            ? number
            : null;

    private async Task<ProviderResponse> SendWithRetryAsync(
        object payload, CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.ChatCompletionsPath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            request.Headers.TryAddWithoutValidation("HTTP-Referer", _options.HttpReferer);
            request.Headers.TryAddWithoutValidation("X-Title", _options.AppTitle);
            request.Headers.TryAddWithoutValidation("X-OpenRouter-Metadata", "enabled");
            request.Content = JsonContent.Create(payload);

            using var response = await _httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var retryAfter = response.Headers.RetryAfter?.Delta;
            var requestId = ReadFirstHeader(response, "X-Request-Id") ??
                ReadFirstHeader(response, "X-OpenRouter-Generation-Id");
            if (response.IsSuccessStatusCode || !IsTransient(response.StatusCode) || attempt == maxAttempts)
                return new ProviderResponse(response.StatusCode, body, retryAfter, requestId);

            await Task.Delay(retryAfter ?? TimeSpan.FromMilliseconds(750 * attempt), cancellationToken);
        }
        throw new UnreachableException();
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or
        HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static bool IsMalformedResponse(string code) => code is
        "AI_EMPTY_RESPONSE" or "AI_INVALID_RESPONSE" or
        "AI_RESPONSE_TRUNCATED" or "AI_SCHEMA_MISMATCH";

    private static VisionExtractionException CreateHttpFailure(HttpStatusCode statusCode,
        ProviderErrorDetails details, string model, TimeSpan? retryAfter, string? requestId)
    {
        var providerMessage = details.Message;
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new VisionExtractionException("AI_AUTH_ERROR",
                "OpenRouter từ chối API key hoặc quyền truy cập model. Hãy kiểm tra OpenRouter:ApiKey và quyền của key."),
            HttpStatusCode.PaymentRequired => new VisionExtractionException("AI_CREDITS_REQUIRED",
                "Tài khoản OpenRouter không đủ credit hoặc key không được phép sử dụng model đã chọn."),
            HttpStatusCode.NotFound => new VisionExtractionException("AI_MODEL_UNAVAILABLE",
                $"OpenRouter không tìm thấy model '{model}' hoặc model đã bị gỡ khỏi catalog (HTTP 404). {providerMessage}".Trim(),
                retryAfter: retryAfter, provider: details.Provider, requestId: requestId),
            HttpStatusCode.TooManyRequests => new VisionExtractionException("AI_RATE_LIMIT",
                "OpenRouter hoặc upstream provider đang giới hạn tải (HTTP 429). Hệ thống tạm dừng lời gọi tiếp theo để bảo vệ quota.",
                retryAfter: retryAfter, provider: details.Provider, requestId: requestId),
            HttpStatusCode.BadRequest => new VisionExtractionException("AI_REQUEST_INVALID",
                $"OpenRouter không chấp nhận payload ảnh/JSON hiện tại (HTTP 400). {providerMessage}".Trim()),
            HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.GatewayTimeout => new VisionExtractionException("AI_TEMPORARILY_UNAVAILABLE",
                    $"OpenRouter hoặc nhà cung cấp Qwen đang tạm thời không khả dụng (HTTP {(int)statusCode}) sau 2 lần thử."),
            _ => new VisionExtractionException("AI_HTTP_ERROR",
                $"OpenRouter từ chối yêu cầu (HTTP {(int)statusCode}). {providerMessage}".Trim())
        };
    }

    private static ProviderErrorDetails ReadProviderError(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object)
            {
                var message = error.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString() ?? string.Empty
                    : string.Empty;
                string? provider = null;
                if (error.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
                {
                    provider = ReadString(metadata, "provider_name") ?? ReadString(metadata, "provider");
                }

                return new ProviderErrorDetails(SanitizeDiagnostic(message) ?? string.Empty,
                    SanitizeDiagnostic(provider));
            }
        }
        catch (JsonException)
        {
            // Keep the status-specific message; never echo an unknown raw response body.
        }

        return new ProviderErrorDetails(string.Empty, null);
    }

    private static string? ReadFirstHeader(HttpResponseMessage response, string headerName) =>
        response.Headers.TryGetValues(headerName, out var values) ? values.FirstOrDefault() : null;

    private static string? ReadString(JsonElement source, string propertyName) =>
        source.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? SanitizeDiagnostic(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var compact = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= 300 ? compact : compact[..300];
    }

    private sealed record ProviderResponse(HttpStatusCode StatusCode, string Body,
        TimeSpan? RetryAfter, string? RequestId);

    private sealed record ProviderErrorDetails(string Message, string? Provider);

    private static JsonDocument ParseResponseDocument(string responseText)
    {
        try
        {
            return JsonDocument.Parse(responseText);
        }
        catch (JsonException exception)
        {
            throw new VisionExtractionException("AI_INVALID_RESPONSE",
                "Qwen trả về phản hồi không phải JSON hợp lệ; hồ sơ cần kiểm tra thủ công.", exception);
        }
    }

}

