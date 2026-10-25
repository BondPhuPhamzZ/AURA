using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AURA.Interfaces;
using AURA.Options;
using AURA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AURA.Tests;

public sealed class ExternalImagePrivacyGateTests
{
    private static readonly byte[] Image = [0xff, 0xd8, 0xff, 0xd9];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Approval_hashes_accept_exact_bytes_only_and_support_UTF8_BOM(bool withBom)
    {
        using var fixture = new Fixture();
        var hash = Convert.ToHexString(SHA256.HashData(Image));
        File.WriteAllText(fixture.ApprovalPath, JsonSerializer.Serialize(new[] { hash }), new UTF8Encoding(withBom));
        var gate = fixture.CreateGate(ExternalImagePrivacyOptions.ApprovedImagesOnly);
        gate.EnsureAllowed(Image);
        var exception = Assert.Throws<VisionExtractionException>(() => gate.EnsureAllowed([0xff, 0xd8, 0xff, 0xda]));
        Assert.Equal(ExternalImagePrivacyGate.ImageNotApprovedCode, exception.Code);
        Assert.DoesNotContain(hash, exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, gate.ApprovedImageCount);
        // Approval is fixed for this process; changing the policy file does not
        // silently approve new data while existing requests are in progress.
        File.WriteAllText(fixture.ApprovalPath, "[]");
        gate.EnsureAllowed(Image);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[\"not-a-sha256\"]")]
    [InlineData("{\"private_key\":\"SYNTHETIC_DO_NOT_LOG\"}")]
    public void Invalid_approval_manifest_blocks_initialization_without_exposing_its_contents(string json)
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.ApprovalPath, json);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            fixture.CreateGate(ExternalImagePrivacyOptions.ApprovedImagesOnly));
        Assert.DoesNotContain("SYNTHETIC_DO_NOT_LOG", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Root, exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void Missing_and_publicly_served_approval_files_fail_closed()
    {
        using var fixture = new Fixture();
        Assert.Throws<InvalidOperationException>(() => fixture.CreateGate(ExternalImagePrivacyOptions.ApprovedImagesOnly));
        var publicPath = Path.Combine(fixture.Environment.WebRootPath, "approvals.json");
        File.WriteAllText(publicPath, JsonSerializer.Serialize(new[] { Convert.ToHexString(SHA256.HashData(Image)) }));
        Assert.Throws<InvalidOperationException>(() => fixture.CreateGate(
            ExternalImagePrivacyOptions.ApprovedImagesOnly, publicPath));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task External_adapters_block_before_HTTP_for_both_file_and_byte_entry_points(bool ollama, bool usePath)
    {
        using var fixture = new Fixture();
        using var handler = new CountingHandler(HttpStatusCode.Unauthorized);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://remote.example.test/") };
        var gate = fixture.CreateGate(ExternalImagePrivacyOptions.BlockExternal);
        IVisionExtractor service = ollama ? fixture.CreateOllama(client, gate, "https://remote.example.test/")
            : fixture.CreateOpenRouter(client, gate);
        var exception = await Assert.ThrowsAsync<VisionExtractionException>(() => usePath
            ? service.ExtractFactsAsync(fixture.ImagePath) : service.ExtractFactsAsync(Image, "receipt.jpg"));
        Assert.Equal(ExternalImagePrivacyGate.ExternalBlockedCode, exception.Code);
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task Approved_external_bytes_reach_HTTP_and_payload_contains_only_the_approved_snapshot()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.ApprovalPath, JsonSerializer.Serialize(new[] { Convert.ToHexString(SHA256.HashData(Image)) }));
        using var handler = new CountingHandler(HttpStatusCode.Unauthorized);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://remote.example.test/") };
        var service = fixture.CreateOpenRouter(client, fixture.CreateGate(ExternalImagePrivacyOptions.ApprovedImagesOnly));
        var exception = await Assert.ThrowsAsync<VisionExtractionException>(() => service.ExtractFactsAsync(Image, "receipt.jpg"));
        Assert.Equal("AI_AUTH_ERROR", exception.Code);
        Assert.Equal(1, handler.Count);
        using var payload = JsonDocument.Parse(handler.Body!);
        var imageUrl = payload.RootElement.GetProperty("messages")[1].GetProperty("content")[1]
            .GetProperty("image_url").GetProperty("url").GetString()!;
        Assert.Equal(Image, Convert.FromBase64String(imageUrl[(imageUrl.IndexOf(',') + 1)..]));
    }

    [Fact]
    public async Task Ollama_privacy_gate_uses_the_effective_HTTP_target_instead_of_the_options_label()
    {
        using var fixture = new Fixture();
        using var handler = new CountingHandler(HttpStatusCode.Unauthorized);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://remote.example.test/") };
        var service = fixture.CreateOllama(client, fixture.CreateGate(ExternalImagePrivacyOptions.BlockExternal),
            "http://127.0.0.1:11434/");
        var exception = await Assert.ThrowsAsync<VisionExtractionException>(() => service.ExtractFactsAsync(Image, "receipt.jpg"));
        Assert.Equal(ExternalImagePrivacyGate.ExternalBlockedCode, exception.Code);
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task Block_external_still_allows_local_loopback_Ollama_without_requiring_an_approval_file()
    {
        using var fixture = new Fixture();
        using var handler = new CountingHandler(HttpStatusCode.Unauthorized);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        var service = fixture.CreateOllama(client, fixture.CreateGate(ExternalImagePrivacyOptions.BlockExternal),
            "http://127.0.0.1:11434/");
        var exception = await Assert.ThrowsAsync<VisionExtractionException>(() => service.ExtractFactsAsync(Image, "receipt.jpg"));
        Assert.NotEqual(ExternalImagePrivacyGate.ExternalBlockedCode, exception.Code);
        Assert.Equal(1, handler.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Privacy_block_never_triggers_or_disappears_inside_provider_fallback(bool blockedOnFallback)
    {
        using var fixture = new Fixture();
        using var external = new CountingHandler(HttpStatusCode.Unauthorized);
        using var local = new CountingHandler(HttpStatusCode.Unauthorized);
        using var externalClient = new HttpClient(external) { BaseAddress = new Uri("https://remote.example.test/") };
        using var localClient = new HttpClient(local) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        var gate = fixture.CreateGate(ExternalImagePrivacyOptions.BlockExternal);
        var configured = Microsoft.Extensions.Options.Options.Create(new VisionOptions
        {
            Provider = blockedOnFallback ? "Ollama" : "OpenRouter",
            FallbackEnabled = true, FallbackProvider = blockedOnFallback ? "OpenRouter" : "Ollama"
        });
        var service = new ConfiguredVisionExtractor(configured, fixture.CreateOpenRouter(externalClient, gate),
            fixture.CreateOllama(localClient, gate, "http://127.0.0.1:11434/"), new VisionExecutionContext(),
            new VisionCircuitBreaker(), new VisionInvocationCoordinator(configured, new ConfigurationBuilder().Build()),
            NullLogger<ConfiguredVisionExtractor>.Instance);
        var exception = await Assert.ThrowsAsync<VisionExtractionException>(() => service.ExtractFactsAsync(Image, "receipt.jpg"));
        Assert.Equal(ExternalImagePrivacyGate.ExternalBlockedCode, exception.Code);
        Assert.Equal(0, external.Count);
        Assert.Equal(blockedOnFallback ? 1 : 0, local.Count);
    }

    private sealed class CountingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Count { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "AuraPrivacyIT_" + Guid.NewGuid().ToString("N"));
        public string ApprovalPath => Path.Combine(Root, "approvals.json");
        public string ImagePath => Path.Combine(Root, "receipt.jpg");
        public TestEnvironment Environment { get; }
        public Fixture()
        {
            Environment = new TestEnvironment(Root);
            Directory.CreateDirectory(Environment.WebRootPath);
            File.WriteAllText(Path.Combine(Root, "BUSINESS_RULES.md"), "Return receipt evidence as JSON.");
            File.WriteAllBytes(ImagePath, Image);
        }
        public ExternalImagePrivacyGate CreateGate(string mode, string? approvalPath = null) => new(
            Microsoft.Extensions.Options.Options.Create(new ExternalImagePrivacyOptions
            {
                Mode = mode, ApprovedHashesFile = approvalPath ?? ApprovalPath
            }), Environment);
        public OpenRouterVisionExtractorService CreateOpenRouter(HttpClient client, ExternalImagePrivacyGate gate) => new(
            client, Microsoft.Extensions.Options.Options.Create(new OpenRouterOptions { ApiKey = "synthetic-test-key" }),
            Environment, NullLogger<OpenRouterVisionExtractorService>.Instance, gate);
        public OllamaVisionExtractorService CreateOllama(HttpClient client, ExternalImagePrivacyGate gate, string url) => new(
            client, Microsoft.Extensions.Options.Options.Create(new OllamaOptions { BaseUrl = url }), Environment,
            NullLogger<OllamaVisionExtractorService>.Instance, gate);
        public void Dispose()
        {
            if (Path.GetDirectoryName(Root)?.TrimEnd(Path.DirectorySeparatorChar) !=
                Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) || !Path.GetFileName(Root).StartsWith("AuraPrivacyIT_"))
                throw new InvalidOperationException("Unsafe privacy test cleanup.");
            Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AURA.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
