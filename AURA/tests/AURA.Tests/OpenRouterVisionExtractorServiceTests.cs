using System.Net;
using System.Text;
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
                Model = "qwen/qwen3.8-27b:free",
                PolicyPath = "BUSINESS_RULES.md",
                TimeoutSeconds = 90
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
