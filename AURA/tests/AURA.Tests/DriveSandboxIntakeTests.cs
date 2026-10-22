using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using AURA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace AURA.Tests;

public sealed class DriveSandboxIntakeTests
{
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0];

    [Theory]
    [InlineData("receipt.png", "image/jpeg")]
    [InlineData("receipt.pdf", "application/pdf")]
    [InlineData("../receipt.exe", "image/png")]
    public void File_gate_rejects_extension_and_mime_mismatch(string fileName, string mime) =>
        Assert.NotNull(ReceiptAttachmentPolicy.Validate(fileName, mime, Png, 5));

    [Fact]
    public void File_gate_rejects_spoofed_png_bytes() =>
        Assert.NotNull(ReceiptAttachmentPolicy.Validate("receipt.png", "image/png", [1, 2, 3], 5));

    [Fact]
    public void File_gate_rejects_oversized_attachment() =>
        Assert.NotNull(ReceiptAttachmentPolicy.Validate("receipt.png", "image/png",
            new byte[5 * 1024 * 1024 + 1], 5));

    [Fact]
    public async Task Same_Drive_file_version_creates_only_one_SQL_job_and_private_file()
    {
        var root = NewTestRoot();
        try
        {
            var repository = new FakeRepository();
            var service = CreateIntake(root, repository);
            var input = new ReceiptIntakeInput(Png, "receipt.png", "image/png", 0,
                "DRIVE-SANDBOX", "Synthetic", "Test", "drive:folder:file:1");
            var first = await service.AcceptAsync(input);
            var second = await service.AcceptAsync(input);

            Assert.Equal(first.Id, second.Id);
            Assert.Single(repository.Requests);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "receipts")));
            Assert.Contains("Source=DRIVE_SANDBOX", repository.LastAudit);
            Assert.DoesNotContain("File=", repository.LastAudit);
            Assert.DoesNotContain("Submitter=", repository.LastAudit);
        }
        finally { DeleteTestRoot(root); }
    }

    [Theory]
    [InlineData("Production", false)]
    [InlineData("Development", true)]
    public async Task Connector_never_calls_Google_when_production_or_scanner_required(
        string environmentName, bool scannerRequired)
    {
        var handler = new CountingHandler();
        var options = OptionsFactory.Create(new DriveSandboxOptions
        {
            Enabled = true,
            FolderId = "folder123",
            ServiceAccountJsonPath = "not-used",
            RequireMalwareScanner = scannerRequired
        });
        using var http = new HttpClient(handler);
        using var provider = new ServiceCollection().BuildServiceProvider();
        var worker = new DriveSandboxWorker(options, OptionsFactory.Create(new ReceiptStorageOptions()),
            new TestEnvironment(Path.GetTempPath()) { EnvironmentName = environmentName },
            provider.GetRequiredService<IServiceScopeFactory>(), new DriveSandboxClient(http, options),
            LocalConfig(), NullLogger<DriveSandboxWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);
        Assert.Equal(0, handler.Count);
    }

    [Theory]
    [InlineData("http://0.0.0.0:5097")]
    [InlineData("https://example.org")]
    [InlineData("")]
    public void Connector_requires_explicit_loopback_binding(string urls)
    {
        var options = new DriveSandboxOptions { Enabled = true, RequireMalwareScanner = false };
        Assert.False(DriveSandboxRuntimePolicy.IsAllowed(options, "Development", urls));
    }

    [Fact]
    public async Task Local_fake_Drive_API_ingests_one_synthetic_file_without_network()
    {
        var root = NewTestRoot();
        try
        {
            using var rsa = RSA.Create(2048);
            var keyPath = WriteFakeServiceAccount(root, rsa);
            var repository = new FakeRepository();
            var environment = new TestEnvironment(root);
            var options = OptionsFactory.Create(new DriveSandboxOptions
            {
                Enabled = true,
                FolderId = "folder123",
                ServiceAccountJsonPath = keyPath,
                RequireMalwareScanner = false,
                PollIntervalSeconds = 30
            });
            var storage = OptionsFactory.Create(new ReceiptStorageOptions
                { Directory = "receipts", MaxFileSizeMb = 5 });
            using var provider = BuildProvider(root, repository);
            var handler = new FakeDriveHandler(rsa);
            using var http = new HttpClient(handler);
            var worker = new DriveSandboxWorker(options, storage, environment,
                provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            await repository.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await worker.StopAsync(CancellationToken.None);

            Assert.Single(repository.Requests);
            Assert.Single(Directory.GetFiles(Path.Combine(root, "receipts")));
            Assert.Contains("Source=DRIVE_SANDBOX", repository.LastAudit);
            Assert.Equal(1, handler.DownloadCount);
            Assert.True(handler.JwtSignatureVerified);
        }
        finally { DeleteTestRoot(root); }
    }

    [Theory]
    [InlineData("metadata-bump", 1)]
    [InlineData("content-edit", 2)]
    public async Task Drive_content_identity_ignores_metadata_only_changes_but_accepts_new_bytes(
        string mode, int expectedJobs)
    {
        var root = NewTestRoot();
        try
        {
            using var rsa = RSA.Create(2048);
            var repository = new FakeRepository();
            var options = OptionsFactory.Create(new DriveSandboxOptions
            {
                Enabled = true, FolderId = "folder123",
                ServiceAccountJsonPath = WriteFakeServiceAccount(root, rsa),
                RequireMalwareScanner = false, PollIntervalSeconds = 30
            });
            using var provider = BuildProvider(root, repository);
            var handler = new FakeDriveHandler(rsa, mode);
            using var http = new HttpClient(handler);
            var storage = OptionsFactory.Create(new ReceiptStorageOptions
                { Directory = "receipts", MaxFileSizeMb = 5 });
            var environment = new TestEnvironment(root);

            var first = new DriveSandboxWorker(options, storage, environment,
                provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);
            await first.StartAsync(CancellationToken.None);
            await repository.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await first.StopAsync(CancellationToken.None);

            var second = new DriveSandboxWorker(options, storage, environment,
                provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);
            await second.StartAsync(CancellationToken.None);
            for (var attempt = 0; attempt < 50 &&
                (handler.ListRequestCount < 2 || repository.Requests.Count < expectedJobs); attempt++)
                await Task.Delay(100);
            await Task.Delay(200);
            await second.StopAsync(CancellationToken.None);

            Assert.Equal(2, handler.ListRequestCount);
            Assert.Equal(expectedJobs, repository.Requests.Count);
            Assert.Equal(expectedJobs, handler.DownloadCount);
        }
        finally { DeleteTestRoot(root); }
    }

    [Theory]
    [InlineData("rate-limited-once", 2, 1)]
    [InlineData("forbidden", 1, 0)]
    public async Task Drive_client_retries_transient_error_but_not_permission_denial(
        string mode, int expectedFolderRequests, int expectedJobs)
    {
        var root = NewTestRoot();
        try
        {
            using var rsa = RSA.Create(2048);
            var options = OptionsFactory.Create(new DriveSandboxOptions
            {
                Enabled = true, FolderId = "folder123",
                ServiceAccountJsonPath = WriteFakeServiceAccount(root, rsa),
                RequireMalwareScanner = false, PollIntervalSeconds = 30
            });
            var repository = new FakeRepository();
            using var provider = BuildProvider(root, repository);
            var handler = new FakeDriveHandler(rsa, mode);
            using var http = new HttpClient(handler);
            var worker = new DriveSandboxWorker(options,
                OptionsFactory.Create(new ReceiptStorageOptions
                    { Directory = "receipts", MaxFileSizeMb = 5 }),
                new TestEnvironment(root), provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            if (expectedJobs == 1)
                await repository.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            else
                await Task.Delay(300);
            await worker.StopAsync(CancellationToken.None);

            Assert.Equal(expectedFolderRequests, handler.FolderRequestCount);
            Assert.Equal(expectedJobs, repository.Requests.Count);
        }
        finally { DeleteTestRoot(root); }
    }

    [Theory]
    [InlineData("wrong-parent", 0)]
    [InlineData("bad-md5", 1)]
    [InlineData("changed-version", 1)]
    public async Task Rejects_wrong_folder_or_changed_file_before_SQL_intake(
        string mode, int expectedDownloads)
    {
        var root = NewTestRoot();
        try
        {
            using var rsa = RSA.Create(2048);
            var keyPath = WriteFakeServiceAccount(root, rsa);
            var repository = new FakeRepository();
            var options = OptionsFactory.Create(new DriveSandboxOptions
            {
                Enabled = true, FolderId = "folder123", ServiceAccountJsonPath = keyPath,
                RequireMalwareScanner = false, PollIntervalSeconds = 30
            });
            using var provider = BuildProvider(root, repository);
            var handler = new FakeDriveHandler(rsa, mode);
            using var http = new HttpClient(handler);
            var worker = new DriveSandboxWorker(options,
                OptionsFactory.Create(new ReceiptStorageOptions
                    { Directory = "receipts", MaxFileSizeMb = 5 }),
                new TestEnvironment(root), provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            var rejections = Path.Combine(root, "App_Data", "DriveSandbox", "rejections.jsonl");
            for (var attempt = 0; attempt < 50 && !File.Exists(rejections); attempt++)
                await Task.Delay(100);
            await worker.StopAsync(CancellationToken.None);

            Assert.True(File.Exists(rejections));
            Assert.Empty(repository.Requests);
            Assert.Equal(expectedDownloads, handler.DownloadCount);
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Repeated_poll_does_not_duplicate_persistent_rejection_log_entry()
    {
        var root = NewTestRoot();
        try
        {
            using var rsa = RSA.Create(2048);
            var keyPath = WriteFakeServiceAccount(root, rsa);
            var repository = new FakeRepository();
            var options = OptionsFactory.Create(new DriveSandboxOptions
            {
                Enabled = true, FolderId = "folder123", ServiceAccountJsonPath = keyPath,
                RequireMalwareScanner = false, PollIntervalSeconds = 1
            });
            using var provider = BuildProvider(root, repository);
            var handler = new FakeDriveHandler(rsa, "unsupported-file");
            using var http = new HttpClient(handler);
            var worker = new DriveSandboxWorker(options,
                OptionsFactory.Create(new ReceiptStorageOptions
                    { Directory = "receipts", MaxFileSizeMb = 5 }),
                new TestEnvironment(root), provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromMilliseconds(2300));
            await worker.StopAsync(CancellationToken.None);

            var rejections = Path.Combine(root, "App_Data", "DriveSandbox", "rejections.jsonl");
            Assert.True(File.Exists(rejections));
            Assert.Single(await File.ReadAllLinesAsync(rejections));

            var restartedWorker = new DriveSandboxWorker(options,
                OptionsFactory.Create(new ReceiptStorageOptions
                    { Directory = "receipts", MaxFileSizeMb = 5 }),
                new TestEnvironment(root), provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);
            await restartedWorker.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromMilliseconds(1100));
            await restartedWorker.StopAsync(CancellationToken.None);

            Assert.Single(await File.ReadAllLinesAsync(rejections));
            Assert.Empty(repository.Requests);
            Assert.Equal(0, handler.DownloadCount);
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Drive_scan_over_configured_file_limit_fails_closed_without_partial_intake()
    {
        var root = NewTestRoot();
        try
        {
            using var rsa = RSA.Create(2048);
            var keyPath = WriteFakeServiceAccount(root, rsa);
            var repository = new FakeRepository();
            var options = OptionsFactory.Create(new DriveSandboxOptions
            {
                Enabled = true, FolderId = "folder123", ServiceAccountJsonPath = keyPath,
                RequireMalwareScanner = false, MaxFilesPerScan = 100, PollIntervalSeconds = 30
            });
            using var provider = BuildProvider(root, repository);
            var handler = new FakeDriveHandler(rsa, listedEntryCount: 101);
            using var http = new HttpClient(handler);
            var worker = new DriveSandboxWorker(options,
                OptionsFactory.Create(new ReceiptStorageOptions
                    { Directory = "receipts", MaxFileSizeMb = 5 }),
                new TestEnvironment(root), provider.GetRequiredService<IServiceScopeFactory>(),
                new DriveSandboxClient(http, options), LocalConfig(),
                NullLogger<DriveSandboxWorker>.Instance);

            await worker.StartAsync(CancellationToken.None);
            await handler.ListCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            await worker.StopAsync(CancellationToken.None);

            Assert.Empty(repository.Requests);
            Assert.Equal(0, handler.DownloadCount);
            Assert.False(Directory.Exists(Path.Combine(root, "receipts")));
        }
        finally { DeleteTestRoot(root); }
    }

    [Fact]
    public async Task Download_never_buffers_beyond_limit_even_if_server_omits_content_length()
    {
        using var http = new HttpClient(new OversizeHandler());
        var client = new DriveSandboxClient(http, OptionsFactory.Create(new DriveSandboxOptions()));
        var error = await Assert.ThrowsAsync<DriveSandboxException>(() =>
            client.DownloadAsync("fake", "file123", 10, CancellationToken.None));
        Assert.Equal("DRIVE_FILE_TOO_LARGE", error.Message);
    }

    private static ServiceProvider BuildProvider(string root, FakeRepository repository)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IReimbursementRepository>(repository);
        services.AddSingleton(new ReceiptProcessingQueue());
        services.AddSingleton(provider => CreateIntake(root,
            (FakeRepository)provider.GetRequiredService<IReimbursementRepository>(),
            provider.GetRequiredService<ReceiptProcessingQueue>()));
        return services.BuildServiceProvider();
    }

    private static IConfiguration LocalConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
            { ["urls"] = "http://127.0.0.1:5097" }).Build();

    private static ReceiptIntakeService CreateIntake(string root, FakeRepository repository,
        ReceiptProcessingQueue? queue = null) =>
        new(new TestEnvironment(root), repository,
            OptionsFactory.Create(new ReceiptStorageOptions { Directory = "receipts", MaxFileSizeMb = 5 }),
            OptionsFactory.Create(new DecisionPolicyOptions()), queue ?? new ReceiptProcessingQueue());

    private static string NewTestRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "aura-drive-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTestRoot(string root)
    {
        if (Path.GetDirectoryName(root)?.TrimEnd(Path.DirectorySeparatorChar) !=
            Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) ||
            !Path.GetFileName(root).StartsWith("aura-drive-test-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to delete outside test temp root.");
        Directory.Delete(root, recursive: true);
    }

    private static string WriteFakeServiceAccount(string root, RSA rsa)
    {
        var path = Path.Combine(root, "fake-service-account.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            type = "service_account",
            client_email = "aura-test@project.iam.gserviceaccount.com",
            private_key = rsa.ExportPkcs8PrivateKeyPem(),
            token_uri = "https://oauth2.googleapis.com/token"
        }));
        return path;
    }

    private sealed class FakeDriveHandler(RSA publicKey, string mode = "valid",
        int listedEntryCount = 1) : HttpMessageHandler
    {
        private readonly string _md5 = Convert.ToHexString(MD5.HashData(Png)).ToLowerInvariant();
        private readonly byte[] _editedPng = [137, 80, 78, 71, 13, 10, 26, 10, 0, 1];
        public int DownloadCount { get; private set; }
        public int FolderRequestCount { get; private set; }
        public int ListRequestCount { get; private set; }
        public bool JwtSignatureVerified { get; private set; }
        public TaskCompletionSource ListCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/token")
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                var assertion = Uri.UnescapeDataString(body.Split("assertion=")[1]).Replace('+', ' ');
                var parts = assertion.Split('.');
                var signed = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
                var signature = Convert.FromBase64String(Pad(parts[2].Replace('-', '+').Replace('_', '/')));
                JwtSignatureVerified = publicKey.VerifyData(signed, signature,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var claims = JsonDocument.Parse(Convert.FromBase64String(
                    Pad(parts[1].Replace('-', '+').Replace('_', '/'))));
                Assert.Equal("https://www.googleapis.com/auth/drive.readonly",
                    claims.RootElement.GetProperty("scope").GetString());
                return Json("""{"access_token":"fake","expires_in":3600}""");
            }
            if (path == "/drive/v3/files/folder123")
            {
                FolderRequestCount++;
                if (mode == "rate-limited-once" && FolderRequestCount == 1)
                    return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                if (mode == "forbidden")
                    return new HttpResponseMessage(HttpStatusCode.Forbidden);
                return Json("""{"id":"folder123","mimeType":"application/vnd.google-apps.folder","trashed":false}""");
            }
            if (path == "/drive/v3/files" && request.RequestUri.Query.Contains("q=", StringComparison.Ordinal))
            {
                ListRequestCount++;
                var response = Json(JsonSerializer.Serialize(new
                {
                    files = Enumerable.Range(0, listedEntryCount).Select(_ => FileMetadata()).ToArray()
                }));
                ListCompleted.TrySetResult();
                return response;
            }
            if (path == "/drive/v3/files/file123" && request.RequestUri.Query.Contains("alt=media",
                    StringComparison.Ordinal))
            {
                DownloadCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new ByteArrayContent(CurrentBytes()) };
            }
            if (path == "/drive/v3/files/file123")
                return Json(JsonSerializer.Serialize(FileMetadata(afterDownload: true)));
            throw new InvalidOperationException("Unexpected fake Drive endpoint.");
        }

        private byte[] CurrentBytes() => mode == "content-edit" && ListRequestCount >= 2 ? _editedPng : Png;

        private object FileMetadata(bool afterDownload = false) => new
        {
            id = "file123",
            name = mode == "unsupported-file" ? "receipt.txt" : "receipt.png",
            mimeType = mode == "unsupported-file" ? "text/plain" : "image/png",
            size = CurrentBytes().Length.ToString(),
            md5Checksum = mode == "bad-md5" ? "00000000000000000000000000000000" :
                mode == "content-edit" ? Convert.ToHexString(MD5.HashData(CurrentBytes())).ToLowerInvariant() : _md5,
            version = afterDownload && mode == "changed-version" ? "2" :
                (mode is "metadata-bump" or "content-edit") && ListRequestCount >= 2 ? "2" : "1",
            parents = new[] { mode == "wrong-parent" ? "other-folder" : "folder123" },
            trashed = false,
            capabilities = new { canDownload = true }
        };

        private static HttpResponseMessage Json(string value) =>
            new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };

        private static string Pad(string value) => value + new string('=', (4 - value.Length % 4) % 4);
    }

    private sealed class OversizeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(new byte[20]))
            });
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Count { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Count++;
            throw new InvalidOperationException("Drive sandbox must not call Google.");
        }
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AURA.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }

    private sealed class FakeRepository : IReimbursementRepository
    {
        public List<ReimbursementRequest> Requests { get; } = [];
        public string LastAudit { get; private set; } = string.Empty;
        public TaskCompletionSource Accepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ReimbursementRequest?> GetRequestByIdAsync(string id) =>
            Task.FromResult(Requests.SingleOrDefault(item => item.Id == id));
        public Task<bool> ExistsByFileHashAsync(string sha256) =>
            Task.FromResult(Requests.Any(item => item.FileSha256 == sha256));
        public Task AddRequestWithAuditAsync(ReimbursementRequest request, string action, string details)
        {
            Requests.Add(request);
            LastAudit = details;
            Accepted.TrySetResult();
            return Task.CompletedTask;
        }
        public Task<List<ReimbursementRequest>> GetAllRequestsAsync() => throw new NotSupportedException();
        public Task UpdateRequestAsync(ReimbursementRequest request) => throw new NotSupportedException();
        public Task UpdateRequestWithAuditAsync(ReimbursementRequest request, string action, string details) =>
            throw new NotSupportedException();
        public Task UpdateRequestsWithAuditAsync(IEnumerable<(ReimbursementRequest Request, string Details)> requests,
            string action) => throw new NotSupportedException();
    }
}
