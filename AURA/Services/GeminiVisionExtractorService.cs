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
            throw new VisionExtractionException("AI_NOT_CONFIGURED",
                "Gemini API chÆ°a Ä‘Æ°á»£c cáº¥u hÃ¬nh; há»“ sÆ¡ cáº§n Ä‘Æ°á»£c kiá»ƒm tra thá»§ cÃ´ng.");

        var fullImagePath = Path.GetFullPath(physicalImagePath);
        if (!File.Exists(fullImagePath))
            throw new VisionExtractionException("IMAGE_NOT_FOUND", "KhÃ´ng tÃ¬m tháº¥y áº£nh hÃ³a Ä‘Æ¡n cáº§n phÃ¢n tÃ­ch.");

        var contentRoot = Path.GetFullPath(_environment.ContentRootPath);
        var policyPath = Path.GetFullPath(Path.Combine(contentRoot, _options.PolicyPath));
        var contentRootPrefix = contentRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!policyPath.StartsWith(contentRootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(policyPath))
            throw new VisionExtractionException("POLICY_NOT_FOUND",
                "KhÃ´ng tÃ¬m tháº¥y tÃ i liá»‡u chÃ­nh sÃ¡ch báº¯t buá»™c; khÃ´ng thá»ƒ ra quyáº¿t Ä‘á»‹nh tá»± Ä‘á»™ng.");

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
                        new { text = "TrÃ­ch xuáº¥t dá»¯ kiá»‡n tá»« áº£nh hÃ³a Ä‘Æ¡n nÃ y. KhÃ´ng tá»± Ä‘Æ°a ra quyáº¿t Ä‘á»‹nh duyá»‡t hay chuyá»ƒn tiáº¿p." },
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

        (HttpStatusCode statusCode, string responseText) response;
        try
        {
            response = await SendWithRetryAsync(payload, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new VisionExtractionException("AI_TIMEOUT",
                $"Gemini khÃ´ng pháº£n há»“i trong {_options.TimeoutSeconds} giÃ¢y. ÄÃ¢y lÃ  lá»—i timeout máº¡ng/dá»‹ch vá»¥ hoáº·c model Ä‘ang quÃ¡ táº£i, khÃ´ng pháº£i do ná»™i dung 'kiá»ƒm thá»­' trong áº£nh.", exception);
        }

        var (statusCode, responseText) = response;
        if ((int)statusCode is < 200 or >= 300)
        {
            _logger.LogWarning("Gemini returned HTTP {StatusCode} after retries.", (int)statusCode);
            throw CreateHttpFailure(statusCode);
        }

        using var responseDocument = ParseResponseDocument(responseText);
        var root = responseDocument.RootElement;
        if (root.TryGetProperty("promptFeedback", out var feedback) &&
            feedback.TryGetProperty("blockReason", out var blockReason))
            throw new VisionExtractionException("AI_INPUT_BLOCKED",
                $"Gemini tá»« chá»‘i xá»­ lÃ½ Ä‘áº§u vÃ o ({blockReason.GetString() ?? "khÃ´ng rÃµ lÃ½ do"}); há»“ sÆ¡ cáº§n kiá»ƒm tra thá»§ cÃ´ng.");

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            throw new VisionExtractionException("AI_EMPTY_RESPONSE",
                "Gemini khÃ´ng tráº£ vá» káº¿t quáº£ trÃ­ch xuáº¥t; há»“ sÆ¡ cáº§n kiá»ƒm tra thá»§ cÃ´ng.");

        var candidate = candidates[0];
        if (candidate.TryGetProperty("finishReason", out var finishReason) &&
            finishReason.GetString() is not "STOP")
            throw new VisionExtractionException("AI_ABNORMAL_FINISH",
                $"Gemini dá»«ng báº¥t thÆ°á»ng ({finishReason.GetString() ?? "khÃ´ng rÃµ"}); há»“ sÆ¡ cáº§n kiá»ƒm tra thá»§ cÃ´ng.");

        if (!candidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0 ||
            !parts[0].TryGetProperty("text", out var textElement))
            throw new VisionExtractionException("AI_INVALID_RESPONSE",
                "Gemini khÃ´ng tráº£ vá» dá»¯ liá»‡u JSON há»£p lá»‡; há»“ sÆ¡ cáº§n kiá»ƒm tra thá»§ cÃ´ng.");

        ReceiptExtractionDto facts;
        try
        {
            facts = JsonSerializer.Deserialize<ReceiptExtractionDto>(textElement.GetString() ?? string.Empty, JsonOptions)
                ?? throw new JsonException("Empty extraction object.");
        }
        catch (JsonException exception)
        {
            throw new VisionExtractionException("AI_INVALID_JSON",
                "Gemini tráº£ vá» JSON khÃ´ng Ä‘á»c Ä‘Æ°á»£c; há»“ sÆ¡ cáº§n kiá»ƒm tra thá»§ cÃ´ng.", exception);
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
        const int maxAttempts = 5;
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

            var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMilliseconds(4000 * (1 << (attempt - 1)));
            _logger.LogWarning("Gemini returned transient HTTP {StatusCode}; retry {Attempt}/{MaxAttempts} after {DelayMs}ms.",
                (int)response.StatusCode, attempt + 1, maxAttempts, retryAfter.TotalMilliseconds);
            await Task.Delay(retryAfter, cancellationToken);
        }

        throw new InvalidOperationException("Gemini retry loop ended unexpectedly.");
    }

    // Do not retry 429 automatically: repeated quota requests make a demo slower and can exhaust
    // the remaining request budget. Operators can retry deliberately after the provider reset.
    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or
        HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static VisionExtractionException CreateHttpFailure(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.TooManyRequests => new VisionExtractionException("AI_RATE_LIMIT",
            "Gemini Ä‘ang giá»›i háº¡n lÆ°á»£t gá»i hoáº·c Ä‘Ã£ cháº¡m quota (HTTP 429) sau 3 láº§n thá»­; hÃ£y Ä‘á»£i rá»“i thá»­ láº¡i hoáº·c chuyá»ƒn kiá»ƒm tra thá»§ cÃ´ng."),
        HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout => new VisionExtractionException("AI_TEMPORARILY_UNAVAILABLE",
                $"Gemini Ä‘ang táº¡m thá»i khÃ´ng kháº£ dá»¥ng (HTTP {(int)statusCode}) sau 3 láº§n thá»­; há»“ sÆ¡ cáº§n kiá»ƒm tra thá»§ cÃ´ng."),
        _ => new VisionExtractionException("AI_HTTP_ERROR",
            $"Gemini tá»« chá»‘i yÃªu cáº§u (HTTP {(int)statusCode}); há»“ sÆ¡ cáº§n kiá»ƒm tra thá»§ cÃ´ng.")
    };

    private static JsonDocument ParseResponseDocument(string responseText)
    {
        try
        {
            return JsonDocument.Parse(responseText);
        }
        catch (JsonException exception)
        {
            throw new VisionExtractionException("AI_INVALID_RESPONSE",
                "Gemini tráº£ vá» pháº£n há»“i khÃ´ng pháº£i JSON há»£p lá»‡; há»“ sÆ¡ cáº§n kiá»ƒm tra thá»§ cÃ´ng.", exception);
        }
    }

    private static string GetMimeType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => throw new InvalidOperationException("Äá»‹nh dáº¡ng áº£nh khÃ´ng Ä‘Æ°á»£c há»— trá»£.")
    };

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



