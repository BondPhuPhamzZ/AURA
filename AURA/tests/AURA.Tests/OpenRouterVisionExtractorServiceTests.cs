using System.Net;
using System.Text;
using System.Text.Json;
using AURA.Options;
using AURA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AURA.Tests;

public sealed class OpenRouterVisionExtractorServiceTests
{
    [Fact]
    public async Task Sends_image_to_openrouter_chat_completions_with_configured_model()
    {
        var root = CreateFixtureRoot();
        try
        {
            string? requestJson = null;
            Uri? requestUri = null;
            var handler = new StubHandler(async request =>
            {
                requestUri = request.RequestUri;
                requestJson = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"error\":{\"message\":\"No endpoints found\"}}", Encoding.UTF8, "application/json")
                };
            });
            var options = Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions
            {
                ApiKey = "test-key",
                BaseUrl = "https://openrouter.ai/api/v1/",
                ChatCompletionsPath = "chat/completions",
                Model = "qwen/qwen3-vl-8b-instruct",
                PolicyPath = "BUSINESS_RULES.md",
                TimeoutSeconds = 90,
                MaxOutputTokens = 4096,
                HttpReferer = "https://github.com/BondPhuPhamzZ/AURA",
                AppTitle = "AURA - The Escalation Referee"
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) };
            var service = new OpenRouterVisionExtractorService(client, options,
                new TestEnvironment(root), NullLogger<OpenRouterVisionExtractorService>.Instance);

            var exception = await Assert.ThrowsAsync<VisionExtractionException>(() =>
                service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg")));

            Assert.Equal("AI_MODEL_UNAVAILABLE", exception.Code);
            Assert.Contains(options.Value.Model, exception.UserMessage);
            Assert.Equal("https://openrouter.ai/api/v1/chat/completions", requestUri?.ToString());
            Assert.Contains($"\"model\":\"{options.Value.Model}\"", requestJson);
            Assert.Contains("\"type\":\"text\"", requestJson);
            Assert.Contains("\"type\":\"image_url\"", requestJson);
            using var payload = JsonDocument.Parse(requestJson!);
            var rootElement = payload.RootElement;
            Assert.Equal(4096, rootElement.GetProperty("max_tokens").GetInt32());
            var responseFormat = rootElement.GetProperty("response_format");
            Assert.Equal("json_schema", responseFormat.GetProperty("type").GetString());
            var jsonSchema = responseFormat.GetProperty("json_schema");
            Assert.True(jsonSchema.GetProperty("strict").GetBoolean());
            Assert.False(jsonSchema.GetProperty("schema").GetProperty("additionalProperties").GetBoolean());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Parses_strict_qwen_json_response_without_calling_external_api()
    {
        var root = CreateFixtureRoot();
        try
        {
            var factsJson = JsonSerializer.Serialize(new
            {
                documentType = "RETAIL_RECEIPT",
                documentStatus = "ISSUED",
                merchantName = "Cửa hàng thử nghiệm",
                taxId = (string?)null,
                merchantId = (string?)null,
                terminalId = (string?)null,
                platformName = (string?)null,
                orderId = (string?)null,
                bookingId = (string?)null,
                shippingTrackingCode = (string?)null,
                shippingProvider = (string?)null,
                orderStatus = "PAID",
                invoiceNumber = "HD-001",
                invoiceDate = "2026-09-22",
                transactionDate = (string?)null,
                completionDate = (string?)null,
                invoiceTime = "10:30",
                currency = "VND",
                subtotal = 100000m,
                tax = 0m,
                totalAmount = 100000m,
                lineItems = new[] { new { description = "Văn phòng phẩm", quantity = 1m, unitPrice = 100000m, amount = 100000m } },
                missingFields = Array.Empty<string>(),
                warnings = Array.Empty<string>(),
                suspiciousSignals = Array.Empty<string>(),
                confidence = 0.97
            });
            var responseJson = JsonSerializer.Serialize(new
            {
                choices = new[] { new { finish_reason = "stop", message = new { content = factsJson } } }
            });
            var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            }));
            var options = Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions
            {
                ApiKey = "test-key",
                Model = "qwen/qwen3-vl-8b-instruct",
                PolicyPath = "BUSINESS_RULES.md"
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) };
            var service = new OpenRouterVisionExtractorService(client, options,
                new TestEnvironment(root), NullLogger<OpenRouterVisionExtractorService>.Instance);

            var result = await service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg"));

            Assert.Equal("RETAIL_RECEIPT", result.DocumentType);
            Assert.Equal(100000m, result.TotalAmount);
            Assert.Single(result.LineItems);
            Assert.Equal(0.97, result.Confidence, 3);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Tolerates_qwen_wrapper_numeric_strings_and_provider_metadata_without_external_api()
    {
        var root = CreateFixtureRoot();
        try
        {
            const string factsJson = """
                Analysis complete.
                ```json
                {
                  "documentType":"RETAIL_RECEIPT","documentStatus":"ISSUED","merchantName":"Cửa hàng thử nghiệm",
                  "taxId":null,"merchantId":null,"terminalId":null,"platformName":null,"orderId":null,
                  "bookingId":null,"shippingTrackingCode":null,"shippingProvider":null,"orderStatus":"PAID",
                  "invoiceNumber":"HD-002","invoiceDate":"2026-09-22","transactionDate":null,
                  "completionDate":null,"invoiceTime":"10:30","currency":"VND","subtotal":"88000",
                  "tax":"0","totalAmount":"88000","lineItems":[{"description":"Phở bò","quantity":"1",
                  "unitPrice":"88000","amount":"88000"}],"missingFields":[],"warnings":[],
                  "suspiciousSignals":[],"confidence":"0.91","providerNote":"ignored safely"
                }
                ```
                """;
            var responseJson = JsonSerializer.Serialize(new
            {
                choices = new[] { new { finish_reason = "stop", message = new { content = factsJson } } }
            });
            var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            }));
            var options = Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions
            {
                ApiKey = "test-key", Model = "qwen/qwen3-vl-8b-instruct", PolicyPath = "BUSINESS_RULES.md"
            });
            var service = new OpenRouterVisionExtractorService(
                new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) }, options,
                new TestEnvironment(root), NullLogger<OpenRouterVisionExtractorService>.Instance);

            var result = await service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg"));

            Assert.Equal(88000m, result.TotalAmount);
            Assert.Equal(0.91, result.Confidence, 3);
            Assert.Single(result.LineItems);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateFixtureRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "aura-openrouter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "BUSINESS_RULES.md"), "Return receipt evidence as JSON.");
        File.WriteAllBytes(Path.Combine(root, "receipt.jpg"), [0xFF, 0xD8, 0xFF, 0xD9]);
        return root;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => callback(request);
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AURA.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }
}
