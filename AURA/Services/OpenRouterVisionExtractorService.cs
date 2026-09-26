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

        var payload = new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = input.SystemPrompt },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = "Trích xuất dữ kiện từ ảnh chứng từ này và chỉ trả về JSON đúng schema." },
                        new { type = "image_url", image_url = new { url = $"data:{input.MimeType};base64,{input.Base64Image}" } }
                    }
                }
            },
            temperature = 0,
            max_tokens = _options.MaxOutputTokens,
            plugins = new[] { new { id = "response-healing" } },
            response_format = new
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

        const int maxMalformedResponseAttempts = 2;
        for (var attempt = 1; attempt <= maxMalformedResponseAttempts; attempt++)
        {
            try
            {
                return await RequestFactsOnceAsync(payload, cancellationToken);
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
        (HttpStatusCode statusCode, string responseText) response;
        try
        {
            response = await SendWithRetryAsync(payload, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new VisionExtractionException("AI_TIMEOUT",
                $"Qwen không phản hồi trong {_options.TimeoutSeconds} giây. Đây là lỗi timeout mạng/dịch vụ hoặc model đang quá tải, không phải do nội dung 'kiểm thử' trong ảnh.", exception);
        }

        var (statusCode, responseText) = response;
        if ((int)statusCode is < 200 or >= 300)
        {
            _logger.LogWarning("OpenRouter returned HTTP {StatusCode} after retries.", (int)statusCode);
            throw CreateHttpFailure(statusCode, responseText, _options.Model);
        }

        using var responseDocument = ParseResponseDocument(responseText);
        var root = responseDocument.RootElement;

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

    private async Task<(HttpStatusCode StatusCode, string Body)> SendWithRetryAsync(
        object payload, CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.ChatCompletionsPath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            request.Headers.TryAddWithoutValidation("HTTP-Referer", _options.HttpReferer);
            request.Headers.TryAddWithoutValidation("X-Title", _options.AppTitle);
            request.Content = JsonContent.Create(payload);

            using var response = await _httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode || !IsTransient(response.StatusCode) || attempt == maxAttempts)
                return (response.StatusCode, body);

            var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMilliseconds(750 * attempt);
            await Task.Delay(retryAfter, cancellationToken);
        }
        throw new UnreachableException();
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or
        HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static bool IsMalformedResponse(string code) => code is
        "AI_EMPTY_RESPONSE" or "AI_INVALID_RESPONSE" or
        "AI_RESPONSE_TRUNCATED" or "AI_SCHEMA_MISMATCH";

    private static VisionExtractionException CreateHttpFailure(HttpStatusCode statusCode, string responseBody, string model)
    {
        var providerMessage = ReadProviderError(responseBody);
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new VisionExtractionException("AI_AUTH_ERROR",
                "OpenRouter từ chối API key hoặc quyền truy cập model. Hãy kiểm tra OpenRouter:ApiKey và quyền của key."),
            HttpStatusCode.PaymentRequired => new VisionExtractionException("AI_CREDITS_REQUIRED",
                "Tài khoản OpenRouter không đủ credit hoặc key không được phép sử dụng model đã chọn."),
            HttpStatusCode.NotFound => new VisionExtractionException("AI_MODEL_UNAVAILABLE",
                $"OpenRouter không tìm thấy model '{model}' hoặc model đã bị gỡ khỏi catalog (HTTP 404). {providerMessage}".Trim()),
            HttpStatusCode.TooManyRequests => new VisionExtractionException("AI_RATE_LIMIT",
                "OpenRouter hoặc Alibaba đang giới hạn tần suất gọi (HTTP 429). Hệ thống không tự retry để tránh phát sinh thêm chi phí."),
            HttpStatusCode.BadRequest => new VisionExtractionException("AI_REQUEST_INVALID",
                $"OpenRouter không chấp nhận payload ảnh/JSON hiện tại (HTTP 400). {providerMessage}".Trim()),
            HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.GatewayTimeout => new VisionExtractionException("AI_TEMPORARILY_UNAVAILABLE",
                    $"OpenRouter hoặc nhà cung cấp Qwen đang tạm thời không khả dụng (HTTP {(int)statusCode}) sau 2 lần thử."),
            _ => new VisionExtractionException("AI_HTTP_ERROR",
                $"OpenRouter từ chối yêu cầu (HTTP {(int)statusCode}). {providerMessage}".Trim())
        };
    }

    private static string ReadProviderError(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
                return message.GetString() ?? string.Empty;
        }
        catch (JsonException)
        {
            // Keep the status-specific message; never echo an unknown raw response body.
        }

        return string.Empty;
    }

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

