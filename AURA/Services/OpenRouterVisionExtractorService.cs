using System.Diagnostics;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed class OpenRouterVisionExtractorService : IVisionExtractor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
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

        var fullImagePath = Path.GetFullPath(physicalImagePath);
        if (!File.Exists(fullImagePath))
            throw new VisionExtractionException("IMAGE_NOT_FOUND", "Không tìm thấy ảnh hóa đơn cần phân tích.");

        var contentRoot = Path.GetFullPath(_environment.ContentRootPath);
        var policyPath = Path.GetFullPath(Path.Combine(contentRoot, _options.PolicyPath));
        var contentRootPrefix = contentRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!policyPath.StartsWith(contentRootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(policyPath))
            throw new VisionExtractionException("POLICY_NOT_FOUND",
                "Không tìm thấy tài liệu chính sách bắt buộc; không thể trích xuất dữ kiện an toàn.");
        
        var imageBytes = await File.ReadAllBytesAsync(fullImagePath, cancellationToken);
        var policy = await File.ReadAllTextAsync(policyPath, cancellationToken);
        var mimeType = Path.GetExtension(fullImagePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => throw new InvalidOperationException("Định dạng ảnh không được hỗ trợ.")
        };
        var base64Image = Convert.ToBase64String(imageBytes);

        var schemaJson = JsonSerializer.Serialize(BuildResponseSchema(), JsonOptions);
        var systemPrompt = $"You are an expert accountant system. Extract the facts from the image strictly following these business rules:\n{policy}\n\nReturn ONLY a valid JSON object matching exactly this schema:\n{schemaJson}";

        var payload = new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = "Trích xuất dữ kiện từ ảnh chứng từ này và chỉ trả về JSON đúng schema." },
                        new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } }
                    }
                }
            },
            temperature = 0,
            response_format = new { type = "json_object" }
        };

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

        var message = choices[0].GetProperty("message");
        if (!message.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.String)
            throw new VisionExtractionException("AI_INVALID_RESPONSE",
                "Qwen không trả về dữ liệu JSON hợp lệ; hồ sơ cần kiểm tra thủ công.");

        var contentStr = contentElement.GetString() ?? string.Empty;
        
        // Strip markdown backticks if returned
        var match = Regex.Match(contentStr, @"`(?:json)?\s*(.*?)\s*`", RegexOptions.Singleline);
        if (match.Success) contentStr = match.Groups[1].Value;

        ReceiptExtractionDto facts;
        try
        {
            facts = JsonSerializer.Deserialize<ReceiptExtractionDto>(contentStr, JsonOptions)
                ?? throw new JsonException("Empty extraction object.");
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Failed to parse JSON from AI: {Content}", contentStr);
            throw new VisionExtractionException("AI_SCHEMA_MISMATCH",
                "Qwen trả về kết quả không đúng cấu trúc quy định; hồ sơ cần kiểm tra thủ công.", exception);
        }

        facts.LineItems ??= [];
        facts.MissingFields ??= [];
        facts.Warnings ??= [];
        facts.SuspiciousSignals ??= [];
        facts.Confidence = Math.Clamp(facts.Confidence, 0, 1);
        return facts;
    }

    private async Task<(HttpStatusCode StatusCode, string Body)> SendWithRetryAsync(
        object payload, CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            request.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
            request.Headers.Add("HTTP-Referer", "https://github.com/");
            request.Headers.Add("X-Title", "AURA Escalation Referee");
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

    private static VisionExtractionException CreateHttpFailure(HttpStatusCode statusCode, string responseBody, string model)
    {
        var providerMessage = ReadProviderError(responseBody);
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new VisionExtractionException("AI_AUTH_ERROR",
                "OpenRouter từ chối API key hoặc quyền truy cập model. Hãy kiểm tra OpenRouter:ApiKey và quyền của key."),
            HttpStatusCode.PaymentRequired => new VisionExtractionException("AI_CREDITS_REQUIRED",
                "OpenRouter yêu cầu credit hoặc model này không còn lượt miễn phí cho tài khoản hiện tại."),
            HttpStatusCode.NotFound => new VisionExtractionException("AI_MODEL_UNAVAILABLE",
                $"OpenRouter không tìm thấy model '{model}' hoặc model đã bị gỡ khỏi catalog (HTTP 404). {providerMessage}".Trim()),
            HttpStatusCode.TooManyRequests => new VisionExtractionException("AI_RATE_LIMIT",
                "OpenRouter đang giới hạn lượt gọi hoặc quota miễn phí (HTTP 429). Hệ thống không tự retry để bảo vệ quota."),
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

    private static object BuildResponseSchema() => new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "documentType", "documentStatus", "merchantName", "taxId", "merchantId", "terminalId",
            "platformName", "orderId", "bookingId", "shippingTrackingCode", "shippingProvider", "orderStatus",
            "invoiceNumber", "invoiceDate", "transactionDate", "completionDate", "invoiceTime", "currency",
            "subtotal", "tax", "totalAmount", "lineItems",
            "missingFields", "warnings", "suspiciousSignals", "confidence" },
        properties = new Dictionary<string, object>
        {
            ["documentType"] = NullableString(), ["documentStatus"] = NullableString(),
            ["merchantName"] = NullableString(), ["taxId"] = NullableString(),
            ["merchantId"] = NullableString(), ["terminalId"] = NullableString(),
            ["platformName"] = NullableString(), ["orderId"] = NullableString(), ["bookingId"] = NullableString(),
            ["shippingTrackingCode"] = NullableString(), ["shippingProvider"] = NullableString(),
            ["orderStatus"] = NullableString(), ["invoiceNumber"] = NullableString(),
            ["invoiceDate"] = NullableString(), ["transactionDate"] = NullableString(),
            ["completionDate"] = NullableString(),
            ["invoiceTime"] = NullableString(), ["currency"] = NullableString(),
            ["subtotal"] = NullableNumber(), ["tax"] = NullableNumber(), ["totalAmount"] = NullableNumber(),
            ["lineItems"] = new { type = "array", items = new { type = "object", additionalProperties = false,
                required = new[] { "description", "quantity", "unitPrice", "amount" },
                properties = new Dictionary<string, object> { ["description"] = new { type = "string" },
                    ["quantity"] = NullableNumber(), ["unitPrice"] = NullableNumber(), ["amount"] = NullableNumber() } } },
            ["missingFields"] = StringArray(), ["warnings"] = StringArray(),
            ["suspiciousSignals"] = StringArray(),
            ["confidence"] = new { type = "number", minimum = 0, maximum = 1 }
        }
    };

    private static object NullableString() => new { type = new[] { "string", "null" } };
    private static object NullableNumber() => new { type = new[] { "number", "null" } };
    private static object StringArray() => new { type = "array", items = new { type = "string" } };
}

