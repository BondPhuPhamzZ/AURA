using System.Net;
using System.Text;
using System.Text.Json;
using AURA.Options;
using AURA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AURA.Tests;

public sealed class OllamaVisionExtractorServiceTests
{
    [Fact]
    public async Task Sends_image_and_shared_schema_to_local_ollama()
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
                return SuccessResponse();
            });
            var service = CreateService(root, handler);

            var result = await service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg"));

            Assert.Equal("HD-LOCAL-001", result.InvoiceNumber);
            Assert.Equal("http://127.0.0.1:11434/api/chat", requestUri?.ToString());
            using var payload = JsonDocument.Parse(requestJson!);
            var body = payload.RootElement;
            Assert.Equal("qwen3-vl:4b-instruct", body.GetProperty("model").GetString());
            Assert.False(body.GetProperty("stream").GetBoolean());
            Assert.Equal("5m", body.GetProperty("keep_alive").GetString());
            Assert.False(body.GetProperty("format").GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(8192, body.GetProperty("options").GetProperty("num_ctx").GetInt32());
            Assert.Equal(2048, body.GetProperty("options").GetProperty("num_predict").GetInt32());
            Assert.NotEmpty(body.GetProperty("messages")[1].GetProperty("images")[0].GetString()!);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Reports_missing_local_model_with_pull_instruction()
    {
        var root = CreateFixtureRoot();
        try
        {
            var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":\"model not found\"}", Encoding.UTF8, "application/json")
            }));
            var service = CreateService(root, handler);

            var exception = await Assert.ThrowsAsync<VisionExtractionException>(() =>
                service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg")));

            Assert.Equal("AI_MODEL_UNAVAILABLE", exception.Code);
            Assert.Contains("ollama pull qwen3-vl:4b-instruct", exception.UserMessage);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Converts_malformed_local_output_to_safe_schema_error()
    {
        var root = CreateFixtureRoot();
        try
        {
            var responseJson = JsonSerializer.Serialize(new
            {
                message = new { role = "assistant", content = "not-json" },
                done = true,
                done_reason = "stop"
            });
            var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            }));
            var service = CreateService(root, handler);

            var exception = await Assert.ThrowsAsync<VisionExtractionException>(() =>
                service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg")));

            Assert.Equal("AI_SCHEMA_MISMATCH", exception.Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Converts_connection_failure_to_safe_local_unavailable_error()
    {
        var root = CreateFixtureRoot();
        try
        {
            var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));
            var service = CreateService(root, handler);

            var exception = await Assert.ThrowsAsync<VisionExtractionException>(() =>
                service.ExtractFactsAsync(Path.Combine(root, "receipt.jpg")));

            Assert.Equal("AI_LOCAL_UNAVAILABLE", exception.Code);
            Assert.DoesNotContain("connection refused", exception.UserMessage);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static OllamaVisionExtractorService CreateService(string root, HttpMessageHandler handler)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new OllamaOptions());
        var client = new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) };
        return new OllamaVisionExtractorService(client, options, new TestEnvironment(root),
            NullLogger<OllamaVisionExtractorService>.Instance);
    }

    private static HttpResponseMessage SuccessResponse()
    {
        const string factsJson = """
            {"documentType":"RETAIL_RECEIPT","documentStatus":"ISSUED","merchantName":"Local Shop",
            "taxId":null,"merchantId":null,"terminalId":null,"platformName":null,"orderId":null,
            "bookingId":null,"shippingTrackingCode":null,"shippingProvider":null,"orderStatus":"PAID",
            "invoiceNumber":"HD-LOCAL-001","invoiceDate":"2026-09-26","transactionDate":null,
            "completionDate":null,"invoiceTime":"09:30","currency":"VND","subtotal":100000,
            "tax":0,"totalAmount":100000,"lineItems":[{"description":"Stationery","quantity":1,
            "unitPrice":100000,"amount":100000}],"missingFields":[],"warnings":[],
            "suspiciousSignals":[],"confidence":0.94}
            """;
        var responseJson = JsonSerializer.Serialize(new
        {
            message = new { role = "assistant", content = factsJson },
            done = true,
            done_reason = "stop"
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
    }

    private static string CreateFixtureRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "aura-ollama-tests", Guid.NewGuid().ToString("N"));
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
