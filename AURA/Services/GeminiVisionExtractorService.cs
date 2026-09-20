using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed class GeminiVisionExtractorService : IVisionExtractor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<GeminiVisionExtractorService> _logger;

    public GeminiVisionExtractorService(HttpClient httpClient, IOptions<GeminiOptions> options,
        IWebHostEnvironment environment, ILogger<GeminiVisionExtractorService> logger)
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
            throw new InvalidOperationException("Gemini API chưa được cấu hình. Hãy đặt secret 'Gemini:ApiKey'.");

        var fullImagePath = Path.GetFullPath(physicalImagePath);
        if (!File.Exists(fullImagePath))
            throw new FileNotFoundException("Không tìm thấy ảnh hóa đơn cần phân tích.", fullImagePath);

        var contentRoot = Path.GetFullPath(_environment.ContentRootPath);
        var policyPath = Path.GetFullPath(Path.Combine(contentRoot, _options.PolicyPath));
        var contentRootPrefix = contentRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!policyPath.StartsWith(contentRootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(policyPath))
            throw new InvalidOperationException($"Không tìm thấy tài liệu chính sách bắt buộc tại '{_options.PolicyPath}'.");

        var imageBytes = await File.ReadAllBytesAsync(fullImagePath, cancellationToken);
        var policy = await File.ReadAllTextAsync(policyPath, cancellationToken);
        var payload = new
        {
            systemInstruction = new { parts = new[] { new { text = policy } } },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = "Trích xuất dữ kiện từ ảnh hóa đơn này. Không tự đưa ra quyết định duyệt hay chuyển tiếp." },
                        new { inlineData = new { mimeType = GetMimeType(fullImagePath), data = Convert.ToBase64String(imageBytes) } }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0,
                maxOutputTokens = 4096,
                responseMimeType = "application/json",
                responseJsonSchema = BuildResponseSchema(),
                thinkingConfig = new { thinkingLevel = "LOW" }
            }
        };

        var (statusCode, responseText) = await SendWithRetryAsync(payload, cancellationToken);
        if ((int)statusCode is < 200 or >= 300)
        {
            _logger.LogWarning("Gemini returned HTTP {StatusCode} after retries.", (int)statusCode);
            throw new InvalidOperationException($"Gemini không xử lý được yêu cầu (HTTP {(int)statusCode}).");
        }

        using var responseDocument = JsonDocument.Parse(responseText);
        var root = responseDocument.RootElement;
        if (root.TryGetProperty("promptFeedback", out var feedback) &&
            feedback.TryGetProperty("blockReason", out var blockReason))
            throw new InvalidOperationException($"Gemini đã từ chối đầu vào: {blockReason.GetString()}.");

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            throw new InvalidOperationException("Gemini không trả về kết quả trích xuất.");

        var candidate = candidates[0];
        if (candidate.TryGetProperty("finishReason", out var finishReason) &&
            finishReason.GetString() is not ("STOP" or "MAX_TOKENS"))
            throw new InvalidOperationException($"Gemini dừng bất thường: {finishReason.GetString() ?? "không rõ"}.");

        if (!candidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0 ||
            !parts[0].TryGetProperty("text", out var textElement))
            throw new InvalidOperationException("Phản hồi Gemini không chứa JSON trích xuất.");

        var facts = JsonSerializer.Deserialize<ReceiptExtractionDto>(textElement.GetString() ?? string.Empty, JsonOptions)
            ?? throw new InvalidOperationException("Không thể đọc JSON do Gemini trả về.");
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
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"models/{Uri.EscapeDataString(_options.Model)}:generateContent");
            request.Headers.Add("x-goog-api-key", _options.ApiKey);
            request.Content = JsonContent.Create(payload);

            using var response = await _httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode || !IsTransient(response.StatusCode) || attempt == maxAttempts)
                return (response.StatusCode, body);

            var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMilliseconds(500 * (1 << (attempt - 1)));
            _logger.LogWarning("Gemini returned transient HTTP {StatusCode}; retry {Attempt}/{MaxAttempts} after {DelayMs}ms.",
                (int)response.StatusCode, attempt + 1, maxAttempts, retryAfter.TotalMilliseconds);
            await Task.Delay(retryAfter, cancellationToken);
        }

        throw new InvalidOperationException("Gemini retry loop ended unexpectedly.");
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or
        HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static string GetMimeType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => throw new InvalidOperationException("Định dạng ảnh không được hỗ trợ.")
    };

    private static object BuildResponseSchema() => new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "documentType", "merchantName", "taxId", "bookingId", "invoiceNumber",
            "invoiceDate", "invoiceTime", "currency", "subtotal", "tax", "totalAmount", "lineItems",
            "missingFields", "warnings", "suspiciousSignals", "confidence" },
        properties = new Dictionary<string, object>
        {
            ["documentType"] = NullableString(), ["merchantName"] = NullableString(),
            ["taxId"] = NullableString(), ["bookingId"] = NullableString(),
            ["invoiceNumber"] = NullableString(), ["invoiceDate"] = NullableString(),
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
