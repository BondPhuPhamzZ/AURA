using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed class OpenRouterVisionExtractorService : IVisionExtractor
{
    private const int MaxResponseBytes = 2 * 1024 * 1024;
    private readonly HttpClient _httpClient;
    private readonly OpenRouterOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<OpenRouterVisionExtractorService> _logger;
    private readonly ExternalImagePrivacyGate? _privacyGate;

    public OpenRouterVisionExtractorService(HttpClient httpClient, IOptions<OpenRouterOptions> options,
        IWebHostEnvironment environment, ILogger<OpenRouterVisionExtractorService> logger,
        ExternalImagePrivacyGate? privacyGate = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
        _privacyGate = privacyGate;
    }

    public async Task<ReceiptExtractionDto> ExtractFactsAsync(string physicalImagePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(physicalImagePath))
            throw new VisionExtractionException("IMAGE_NOT_FOUND", "Không tìm thấy ảnh hóa đơn cần phân tích.");
        var bytes = await File.ReadAllBytesAsync(physicalImagePath, cancellationToken);
        return await ExtractFactsAsync(bytes, Path.GetFileName(physicalImagePath), cancellationToken);
    }

    public async Task<ReceiptExtractionDto> ExtractFactsAsync(ReadOnlyMemory<byte> imageBytes,
        string imageFileName, CancellationToken cancellationToken = default)
    {
        // Keep the approved bytes immutable from the caller's perspective across
        // policy-file awaits and all malformed/semantic retry requests.
        imageBytes = imageBytes.ToArray();
        _privacyGate?.EnsureAllowed(imageBytes.Span);
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new VisionExtractionException("AI_NOT_CONFIGURED",
                "OpenRouter API chưa được cấu hình; hồ sơ cần được kiểm tra thủ công.");

        var input = await ReceiptExtractionContract.LoadInputAsync(imageBytes, imageFileName,
            _options.PolicyPath, _environment.ContentRootPath, cancellationToken);
        var responseSchema = ReceiptExtractionContract.BuildResponseSchema();

        var firstFacts = await RequestFactsWithMalformedRetryAsync(
            instruction => BuildPayload(input, responseSchema, instruction), null, cancellationToken);
        var firstIssues = ReceiptSemanticValidator.Validate(firstFacts);
        if (firstIssues.Count == 0) return firstFacts;

        _logger.LogWarning(
            "Qwen returned schema-valid but semantically inconsistent receipt facts: {IssueCodes}. Retrying once with correction guidance.",
            string.Join(" | ", ReceiptSemanticValidator.ToSafeIssueCodes(firstIssues)));

        try
        {
            var repairInstruction = ReceiptSemanticValidator.BuildRepairInstruction(firstIssues);
            var repairedFacts = await RequestFactsWithMalformedRetryAsync(
                instruction => BuildPayload(input, responseSchema, instruction), repairInstruction, cancellationToken);
            ReceiptPrivacyClassifier.PreserveModelMarkers(firstFacts, repairedFacts);
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
                data_collection = _options.DeniesProviderDataCollection ? "deny" : "allow",
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
        catch (HttpRequestException exception)
        {
            // Transport diagnostics may contain URLs or other sensitive values.
            // Preserve a known provider failure without logging the raw exception
            // or automatically repeating a request whose delivery is uncertain.
            _logger.LogWarning("OpenRouter transport failed with {TransportError}.", exception.HttpRequestError);
            throw new VisionExtractionException("AI_TEMPORARILY_UNAVAILABLE",
                "Không kết nối được dịch vụ AI. Hãy kiểm tra mạng/DNS và thử lại khi kết nối ổn định; hồ sơ đã được giữ để kiểm tra thủ công.");
        }

        var (statusCode, responseText, retryAfter, requestId) = response;
        if ((int)statusCode is < 200 or >= 300)
        {
            var details = ReadProviderError(responseText);
            _logger.LogWarning(
                "OpenRouter returned HTTP {StatusCode} after retries. UpstreamProviderKind={UpstreamProviderKind}; RequestId={RequestId}; RetryAfterSeconds={RetryAfterSeconds}.",
                (int)statusCode, details.ProviderKind, requestId ?? "<none>", retryAfter?.TotalSeconds);
            throw CreateHttpFailure(statusCode, details, _options.Model, retryAfter, requestId);
        }

        using var responseDocument = ParseResponseDocument(responseText);
        var root = responseDocument.RootElement;
        LogProviderTelemetry(root);

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            throw new VisionExtractionException("AI_EMPTY_RESPONSE",
                "Qwen không trả về kết quả trích xuất; hồ sơ cần kiểm tra thủ công.");

        var choice = choices[0];
        if (choice.ValueKind != JsonValueKind.Object)
            throw new VisionExtractionException("AI_INVALID_RESPONSE",
                "Qwen không trả về choice hợp lệ; hồ sơ cần kiểm tra thủ công.");
        var finishReason = choice.TryGetProperty("finish_reason", out var finishReasonElement) &&
                           finishReasonElement.ValueKind == JsonValueKind.String
            ? finishReasonElement.GetString()
            : null;
        if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
            throw new VisionExtractionException("AI_RESPONSE_TRUNCATED",
                "Qwen đã dừng vì đạt giới hạn output trước khi hoàn tất JSON; hồ sơ cần kiểm tra thủ công.");

        if (!choice.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object)
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
            var diagnostic = ReceiptExtractionContract.DiagnoseParseFailure(contentStr, exception);
            var safeFinishReason = finishReason is "stop" or "length" or "tool_calls"
                ? finishReason
                : "other";
            _logger.LogWarning(
                "Qwen schema mismatch: {ReasonCode}; schema field={SchemaField}; response characters={ContentLength}; finish reason={FinishReason}.",
                diagnostic.ReasonCode, diagnostic.SchemaField, contentStr.Length, safeFinishReason);
            throw new VisionExtractionException("AI_SCHEMA_MISMATCH",
                "Qwen trả về kết quả không đúng cấu trúc quy định; hồ sơ cần kiểm tra thủ công.");
        }

        return facts;
    }

    private void LogProviderTelemetry(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return;

        var providerName = ReadSelectedProviderName(root);
        var providerKind = ClassifyProvider(providerName);
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            _logger.LogInformation("OpenRouter response metadata for {Model}: provider={ProviderName}; provider kind={ProviderKind}; token usage unavailable.",
                _options.Model, providerName, providerKind);
            return;
        }
        var promptTokens = ReadInt64(usage, "prompt_tokens");
        var completionTokens = ReadInt64(usage, "completion_tokens");
        var totalTokens = ReadInt64(usage, "total_tokens");
        var costUsd = usage.TryGetProperty("cost", out var cost) &&
                      cost.ValueKind == JsonValueKind.Number && cost.TryGetDecimal(out var parsedCost) &&
                      parsedCost >= 0
            ? (decimal?)parsedCost
            : null;
        var reasoningTokens = usage.TryGetProperty("completion_tokens_details", out var details) &&
                              details.ValueKind == JsonValueKind.Object
            ? ReadInt64(details, "reasoning_tokens")
            : null;
        _logger.LogInformation(
            "OpenRouter token usage for {Model}: provider={ProviderName}; provider kind={ProviderKind}; prompt={PromptTokens}, completion={CompletionTokens}, reasoning={ReasoningTokens}, total={TotalTokens}, cost USD={CostUsd}, configured output cap={MaxOutputTokens}.",
            _options.Model, providerName, providerKind, promptTokens, completionTokens, reasoningTokens, totalTokens, costUsd,
            _options.MaxOutputTokens);
    }

    private static string ReadSelectedProviderName(JsonElement root)
    {
        if (!root.TryGetProperty("openrouter_metadata", out var metadata) ||
            metadata.ValueKind != JsonValueKind.Object ||
            !metadata.TryGetProperty("endpoints", out var endpoints) ||
            endpoints.ValueKind != JsonValueKind.Object ||
            !endpoints.TryGetProperty("available", out var available) ||
            available.ValueKind != JsonValueKind.Array)
            return "Unknown";

        foreach (var endpoint in available.EnumerateArray())
        {
            if (endpoint.ValueKind != JsonValueKind.Object ||
                !endpoint.TryGetProperty("selected", out var selected) ||
                selected.ValueKind != JsonValueKind.True ||
                !endpoint.TryGetProperty("provider", out var provider) ||
                provider.ValueKind != JsonValueKind.String)
                continue;
            var safeName = new string(provider.GetString()!
                .Where(character => char.IsAsciiLetterOrDigit(character) || character is ' ' or '-' or '_' or '.')
                .Take(64)
                .ToArray()).Trim();
            return string.IsNullOrWhiteSpace(safeName) ? "Unknown" : safeName;
        }

        return "Unknown";
    }

    private static string ClassifyProvider(string? provider) => provider switch
    {
        null or "" => "Unknown",
        _ when provider.Contains("Alibaba", StringComparison.OrdinalIgnoreCase) => "Alibaba",
        "Parasail" => "Parasail",
        _ => "Other"
    };

    private static long? ReadInt64(JsonElement source, string propertyName) =>
        source.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
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
            var body = await ReadBoundedResponseBodyAsync(response.Content, cancellationToken);
            var retryAfter = response.Headers.RetryAfter?.Delta;
            var requestId = ReadFirstHeader(response, "X-Request-Id") ??
                ReadFirstHeader(response, "X-OpenRouter-Generation-Id");
            if (response.IsSuccessStatusCode || !IsTransient(response.StatusCode) || attempt == maxAttempts)
                return new ProviderResponse(response.StatusCode, body, retryAfter, requestId);

            await Task.Delay(retryAfter ?? TimeSpan.FromMilliseconds(750 * attempt), cancellationToken);
        }
        throw new UnreachableException();
    }

    private static async Task<string> ReadBoundedResponseBodyAsync(HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaxResponseBytes)
            throw new VisionExtractionException("AI_RESPONSE_TOO_LARGE",
                "Phản hồi AI vượt giới hạn kích thước an toàn; hồ sơ cần kiểm tra thủ công.");

        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var remainingWithSentinel = MaxResponseBytes - (int)destination.Length + 1;
            var read = await source.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remainingWithSentinel)),
                cancellationToken);
            if (read == 0) break;
            if (destination.Length + read > MaxResponseBytes)
                throw new VisionExtractionException("AI_RESPONSE_TOO_LARGE",
                    "Phản hồi AI vượt giới hạn kích thước an toàn; hồ sơ cần kiểm tra thủ công.");
            destination.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(destination.GetBuffer().AsSpan(0, (int)destination.Length));
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
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new VisionExtractionException("AI_AUTH_ERROR",
                "OpenRouter từ chối API key hoặc quyền truy cập model. Hãy kiểm tra OpenRouter:ApiKey và quyền của key."),
            HttpStatusCode.PaymentRequired => new VisionExtractionException("AI_CREDITS_REQUIRED",
                "Tài khoản OpenRouter không đủ credit hoặc key không được phép sử dụng model đã chọn."),
            HttpStatusCode.NotFound => new VisionExtractionException("AI_MODEL_UNAVAILABLE",
                $"OpenRouter không tìm thấy model '{model}' hoặc model đã bị gỡ khỏi catalog (HTTP 404).",
                retryAfter: retryAfter, provider: details.ProviderKind, requestId: requestId),
            HttpStatusCode.TooManyRequests => new VisionExtractionException("AI_RATE_LIMIT",
                "OpenRouter hoặc upstream provider đang giới hạn tải (HTTP 429). Hệ thống tạm dừng lời gọi tiếp theo để bảo vệ quota.",
                retryAfter: retryAfter, provider: details.ProviderKind, requestId: requestId),
            HttpStatusCode.BadRequest => new VisionExtractionException("AI_REQUEST_INVALID",
                "OpenRouter không chấp nhận payload ảnh/JSON hiện tại (HTTP 400)."),
            HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.GatewayTimeout => new VisionExtractionException("AI_TEMPORARILY_UNAVAILABLE",
                    $"OpenRouter hoặc nhà cung cấp Qwen đang tạm thời không khả dụng (HTTP {(int)statusCode}) sau 2 lần thử."),
            _ => new VisionExtractionException("AI_HTTP_ERROR",
                $"OpenRouter từ chối yêu cầu (HTTP {(int)statusCode}).")
        };
    }

    private static ProviderErrorDetails ReadProviderError(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object)
            {
                string? provider = null;
                if (error.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
                {
                    provider = ReadString(metadata, "provider_name") ?? ReadString(metadata, "provider");
                }

                return new ProviderErrorDetails(ClassifyProvider(provider));
            }
        }
        catch (JsonException)
        {
            // Keep the status-specific message; never echo an unknown raw response body.
        }

        return new ProviderErrorDetails("Unknown");
    }

    private static string? ReadFirstHeader(HttpResponseMessage response, string headerName) =>
        response.Headers.TryGetValues(headerName, out var values) &&
        values.FirstOrDefault() is { } value &&
        value.Length is > 0 and <= 80 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
            ? value
            : null;

    private static string? ReadString(JsonElement source, string propertyName) =>
        source.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private sealed record ProviderResponse(HttpStatusCode StatusCode, string Body,
        TimeSpan? RetryAfter, string? RequestId);

    private sealed record ProviderErrorDetails(string ProviderKind);

    private static JsonDocument ParseResponseDocument(string responseText)
    {
        try
        {
            return JsonDocument.Parse(responseText);
        }
        catch (JsonException)
        {
            throw new VisionExtractionException("AI_INVALID_RESPONSE",
                "Qwen trả về phản hồi không phải JSON hợp lệ; hồ sơ cần kiểm tra thủ công.");
        }
    }

}

