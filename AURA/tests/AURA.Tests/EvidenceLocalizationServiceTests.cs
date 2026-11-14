using System.Security.Cryptography;
using System.Text.Json;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using AURA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace AURA.Tests;

public sealed class EvidenceLocalizationServiceTests
{
    private static readonly byte[] UprightJpeg = { 0xFF, 0xD8, 0xFF, 0xDA, 0x00, 0x02, 0xFF, 0xD9 };

    [Fact]
    public void Localizer_is_active_only_when_enabled_on_a_development_host()
    {
        var enabled = new EvidenceLocalizationOptions { Enabled = true };
        Assert.True(EvidenceLocalizationOptions.IsActive(enabled, "Development"));
        Assert.False(EvidenceLocalizationOptions.IsActive(enabled, "Production"));
        Assert.False(EvidenceLocalizationOptions.IsActive(new EvidenceLocalizationOptions(), "Development"));
        Assert.False(EvidenceLocalizationOptions.HasLanguageData(new EvidenceLocalizationOptions
        {
            TessdataPath = "relative/tessdata"
        }));
    }

    [Fact]
    public async Task Disabled_or_production_hosts_never_read_the_image_or_run_ocr()
    {
        var ocr = new CountingOcr();
        var store = new MemoryStore(UprightJpeg);
        foreach (var (enabled, environment) in new[] { (false, "Development"), (true, "Production") })
        {
            var service = Create(ocr, store, enabled, environment);
            var (status, result) = await service.LocalizeAsync(Request(UprightJpeg, Facts()), CancellationToken.None);
            Assert.False(service.IsActive);
            Assert.Equal(EvidenceLocalizationService.Status.NotFound, status);
            Assert.Null(result);
        }
        Assert.Equal(0, ocr.Calls);
        Assert.Equal(0, store.Reads);
    }

    [Fact]
    public async Task Requests_without_ai_facts_report_every_field_unlocated_without_ocr()
    {
        var ocr = new CountingOcr();
        var service = Create(ocr, new MemoryStore(UprightJpeg));

        var (status, result) = await service.LocalizeAsync(Request(UprightJpeg, facts: null), CancellationToken.None);

        Assert.Equal(EvidenceLocalizationService.Status.Ready, status);
        Assert.Empty(result!.Regions);
        Assert.Equal(4, result.Unlocated.Count);
        Assert.Equal(0, ocr.Calls);
    }

    [Fact]
    public async Task A_changed_image_is_not_localized()
    {
        var ocr = new CountingOcr();
        var request = Request(UprightJpeg, Facts());
        var tampered = UprightJpeg.ToArray();
        tampered[^1] = 0x00;

        var (status, _) = await Create(ocr, new MemoryStore(tampered)).LocalizeAsync(request, CancellationToken.None);

        Assert.Equal(EvidenceLocalizationService.Status.NotFound, status);
        Assert.Equal(0, ocr.Calls);
    }

    [Fact]
    public async Task Rotated_images_are_not_sent_to_ocr()
    {
        var rotated = ImageOrientationTestsJpeg.WithOrientation(6);
        var ocr = new CountingOcr();

        var (_, result) = await Create(ocr, new MemoryStore(rotated))
            .LocalizeAsync(Request(rotated, Facts()), CancellationToken.None);

        Assert.Empty(result!.Regions);
        Assert.All(result.Unlocated, item => Assert.Contains("EXIF", item.Reason));
        Assert.Equal(0, ocr.Calls);
    }

    [Fact]
    public async Task Results_are_cached_by_image_and_facts_but_timeouts_are_not()
    {
        var ocr = new CountingOcr();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new MemoryStore(UprightJpeg);
        var request = Request(UprightJpeg, Facts());

        var first = await Create(ocr, store, cache: cache).LocalizeAsync(request, CancellationToken.None);
        var second = await Create(ocr, store, cache: cache).LocalizeAsync(request, CancellationToken.None);
        Assert.Equal(1, ocr.Calls);
        Assert.Same(first.Result, second.Result);
        Assert.Equal("totalAmount", Assert.Single(first.Result!.Regions).Field);
        Assert.Equal(request.FileSha256, first.Result.ImageSha256);

        var slow = new CountingOcr { Throw = new TimeoutException() };
        var timedOut = Create(slow, store, cache: new MemoryCache(new MemoryCacheOptions()));
        await timedOut.LocalizeAsync(request, CancellationToken.None);
        await timedOut.LocalizeAsync(request, CancellationToken.None);
        Assert.Equal(2, slow.Calls);
    }

    private static EvidenceLocalizationService Create(IReceiptOcrEngine ocr, IReceiptFileStore store,
        bool enabled = true, string environment = "Development", IMemoryCache? cache = null) =>
        new(store, ocr, cache ?? new MemoryCache(new MemoryCacheOptions()),
            MsOptions.Create(new ReceiptStorageOptions { MaxFileSizeMb = 5 }),
            MsOptions.Create(new EvidenceLocalizationOptions { Enabled = enabled }),
            new FakeEnvironment(environment), NullLogger<EvidenceLocalizationService>.Instance);

    private static ReceiptExtractionDto Facts() => new()
    {
        TotalAmount = 241920m,
        TotalAmountEvidence = "TỔNG THANH TOÁN 241.920 đ"
    };

    private static ReimbursementRequest Request(byte[] bytes, ReceiptExtractionDto? facts) => new()
    {
        Id = "evidence-test",
        StoredFileName = "evidence-test.jpg",
        FileSizeBytes = bytes.Length,
        FileSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
        ExtractedFactsJson = facts is null ? null : JsonSerializer.Serialize(facts)
    };

    private sealed class CountingOcr : IReceiptOcrEngine
    {
        public int Calls;
        public Exception? Throw { get; init; }
        public string Name => "fake-ocr";

        public Task<OcrPage> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            if (Throw is not null) throw Throw;
            return Task.FromResult(new OcrPage(1000, 1000, new List<OcrWord>
            {
                new("TỔNG", 100, 800, 200, 830, 1), new("THANH", 210, 800, 300, 830, 1),
                new("TOÁN", 310, 800, 400, 830, 1), new("241.920", 800, 805, 900, 830, 2)
            }));
        }
    }

    private sealed class MemoryStore(byte[] bytes) : IReceiptFileStore
    {
        public int Reads;
        public Task<bool> PutIfAbsentAsync(string storedFileName, ReadOnlyMemory<byte> content, string contentType,
            string sha256, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Reads);
            return Task.FromResult<Stream?>(new MemoryStream(bytes, writable: false));
        }
        public Task DeleteAsync(string storedFileName, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FakeEnvironment(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "AURA";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

internal static class ImageOrientationTestsJpeg
{
    /// <summary>Minimal JPEG with a little-endian EXIF orientation tag.</summary>
    public static byte[] WithOrientation(int orientation)
    {
        byte[] tiff =
        [
            (byte)'I', (byte)'I', 42, 0, 8, 0, 0, 0, 1, 0, 0x12, 0x01, 3, 0, 1, 0, 0, 0,
            (byte)orientation, 0, 0, 0, 0, 0, 0, 0
        ];
        var segment = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        var length = segment.Length + 2;
        return new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, (byte)(length >> 8), (byte)length }
            .Concat(segment).Concat(new byte[] { 0xFF, 0xDA, 0x00, 0x02, 0xFF, 0xD9 }).ToArray();
    }
}
