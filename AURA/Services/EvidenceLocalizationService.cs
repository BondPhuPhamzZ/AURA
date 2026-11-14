using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AURA.Services;

/// <summary>
/// Places already-extracted facts on the stored receipt image for the reviewer's viewer.
/// Read-only: it never writes SQL, never re-runs the vision model and never changes a decision.
/// Results are cached in memory by image hash, facts hash and localizer version.
/// </summary>
public sealed class EvidenceLocalizationService
{
    public enum Status { Ready, NotFound }

    private static readonly (string Field, string Label)[] Fields =
    {
        (EvidenceRegionMatcher.MerchantField, "Đơn vị bán"),
        (EvidenceRegionMatcher.IdentifierField, "Định danh giao dịch"),
        (EvidenceRegionMatcher.DateField, "Ngày chứng từ / giao dịch"),
        (EvidenceRegionMatcher.TotalField, "Tổng thanh toán")
    };

    private readonly IReceiptFileStore _fileStore;
    private readonly IReceiptOcrEngine _ocr;
    private readonly IMemoryCache _cache;
    private readonly ReceiptStorageOptions _storage;
    private readonly ILogger<EvidenceLocalizationService> _logger;

    public EvidenceLocalizationService(IReceiptFileStore fileStore, IReceiptOcrEngine ocr, IMemoryCache cache,
        IOptions<ReceiptStorageOptions> storage, IOptions<EvidenceLocalizationOptions> options,
        IWebHostEnvironment environment, ILogger<EvidenceLocalizationService> logger)
    {
        _fileStore = fileStore;
        _ocr = ocr;
        _cache = cache;
        _storage = storage.Value;
        _logger = logger;
        IsActive = EvidenceLocalizationOptions.IsActive(options.Value, environment.EnvironmentName);
    }

    public bool IsActive { get; }

    public string Localizer => $"{_ocr.Name}/{EvidenceRegionMatcher.Version}";

    public async Task<(Status Status, EvidenceLocalizationResult? Result)> LocalizeAsync(
        ReimbursementRequest request, CancellationToken cancellationToken)
    {
        if (!IsActive) return (Status.NotFound, null);
        var facts = ReadFacts(request.ExtractedFactsJson);
        if (facts is null)
            return (Status.Ready, AllUnlocated(request, "Chưa có dữ kiện AI để định vị (ví dụ ca privacy hoặc lỗi hệ thống)."));

        var cacheKey = $"evidence:{Localizer}:{request.FileSha256}:{Hash(request.ExtractedFactsJson!)}";
        if (_cache.TryGetValue(cacheKey, out EvidenceLocalizationResult? cached) && cached is not null)
            return (Status.Ready, cached);

        byte[] bytes;
        try
        {
            await using var stored = await _fileStore.OpenReadAsync(request.StoredFileName, cancellationToken);
            if (stored is null) return (Status.NotFound, null);
            bytes = await ReceiptStoredFileIntegrity.ReadVerifiedAsync(stored, request.FileSizeBytes,
                request.FileSha256, _storage.MaxFileSizeMb, cancellationToken);
        }
        catch (VisionExtractionException)
        {
            return (Status.NotFound, null);
        }

        EvidenceLocalizationResult result;
        if (ImageOrientation.Read(bytes) != ImageOrientation.Upright)
        {
            result = AllUnlocated(request, "Ảnh có thông tin xoay EXIF hoặc định dạng không đọc được; không vẽ khung để tránh lệch vị trí.");
        }
        else
        {
            try
            {
                var page = await _ocr.RecognizeAsync(bytes, cancellationToken);
                var (regions, unlocated) = EvidenceRegionMatcher.Match(page, facts);
                result = new EvidenceLocalizationResult
                {
                    ImageSha256 = request.FileSha256, Localizer = Localizer, Regions = regions, Unlocated = unlocated
                };
            }
            catch (TimeoutException)
            {
                // Not cached: a later open may succeed once the engine is free.
                return (Status.Ready, AllUnlocated(request, "OCR chưa xong trong thời gian cho phép; thử mở lại sau."));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Log only the type: OCR text and receipt content must not reach logs.
                _logger.LogWarning("Evidence localization failed for one receipt: {ErrorType}.", exception.GetType().Name);
                return (Status.Ready, AllUnlocated(request, "OCR cục bộ không chạy được; xem ảnh gốc."));
            }
        }

        _cache.Set(cacheKey, result, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });
        return (Status.Ready, result);
    }

    private EvidenceLocalizationResult AllUnlocated(ReimbursementRequest request, string reason) => new()
    {
        ImageSha256 = request.FileSha256,
        Localizer = Localizer,
        Unlocated = Fields.Select(field => new UnlocatedEvidence(field.Field, field.Label, reason)).ToList()
    };

    private static ReceiptExtractionDto? ReadFacts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<ReceiptExtractionDto>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
