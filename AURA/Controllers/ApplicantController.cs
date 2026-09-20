using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using AURA.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AURA.Controllers;

public sealed class ApplicantController : Controller
{
    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png" };

    private readonly IWebHostEnvironment _environment;
    private readonly IReimbursementRepository _repository;
    private readonly IAuditLogger _audit;
    private readonly IVisionExtractor _vision;
    private readonly ReceiptStorageOptions _storageOptions;
    private readonly ILogger<ApplicantController> _logger;

    public ApplicantController(IWebHostEnvironment environment, IReimbursementRepository repository,
        IAuditLogger audit, IVisionExtractor vision, IOptions<ReceiptStorageOptions> storageOptions,
        ILogger<ApplicantController> logger)
    {
        _environment = environment;
        _repository = repository;
        _audit = audit;
        _vision = vision;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index() => RedirectToAction("Index", "Home");

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Receipt(string id, CancellationToken cancellationToken)
    {
        var request = await _repository.GetRequestByIdAsync(id);
        if (request is null || string.IsNullOrWhiteSpace(request.StoredFileName)) return NotFound();

        var storageRoot = GetStorageRoot();
        var fullPath = Path.GetFullPath(Path.Combine(storageRoot, Path.GetFileName(request.StoredFileName)));
        if (!fullPath.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
            return NotFound();

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return File(stream, request.ContentType, enableRangeProcessing: true);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    public async Task<IActionResult> UploadReceipt(IFormFile? receiptFile, decimal claimedAmount,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateUpload(receiptFile, claimedAmount);
        if (validationError is not null) return BadRequest(new { error = validationError });

        var file = receiptFile!;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var bytes = new byte[checked((int)file.Length)];
        await using (var input = file.OpenReadStream())
        {
            await input.ReadExactlyAsync(bytes, cancellationToken);
        }

        if (!HasValidSignature(bytes, extension))
            return BadRequest(new { error = "Nội dung file không khớp định dạng JPG/PNG đã khai báo." });

        var id = Guid.NewGuid().ToString("N");
        var storedFileName = id + extension;
        var storageRoot = GetStorageRoot();
        Directory.CreateDirectory(storageRoot);
        var physicalPath = Path.Combine(storageRoot, storedFileName);
        await System.IO.File.WriteAllBytesAsync(physicalPath, bytes, cancellationToken);

        var sha256 = Convert.ToHexString(SHA256.HashData(bytes));
        var duplicate = await _repository.ExistsByFileHashAsync(sha256);
        var request = new ReimbursementRequest
        {
            Id = id,
            ClaimedAmount = claimedAmount,
            OriginalFileName = Path.GetFileName(file.FileName),
            StoredFileName = storedFileName,
            ContentType = extension == ".png" ? "image/png" : "image/jpeg",
            FileSizeBytes = file.Length,
            FileSha256 = sha256,
            ImageUrl = Url.Action(nameof(Receipt), "Applicant", new { id }) ?? $"/Applicant/Receipt/{id}",
            CreatedAt = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var facts = await _vision.ExtractFactsAsync(physicalPath, cancellationToken);
            request.ExtractedFactsJson = JsonSerializer.Serialize(facts);
            var decision = PolicyDecisionEngine.Evaluate(facts, request.ClaimedAmount, duplicate);
            request.Status = decision.Status;
            request.AiReasoning = decision.Reason;
            request.ManagerQuestion = decision.ManagerQuestion;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Vision extraction failed for request {RequestId}.", request.Id);
            request.Status = "ESCALATE_SYSTEM_ERROR";
            request.AiReasoning = "AI không thể trích xuất hóa đơn do lỗi kỹ thuật; không có quyết định tự động nào được đưa ra.";
            request.ManagerQuestion = "AI không xử lý được ảnh. Anh/chị có đồng ý chuyển hồ sơ sang kiểm tra thủ công không? [CÓ/KHÔNG]";
        }
        finally
        {
            stopwatch.Stop();
            request.ProcessingLatencyMs = stopwatch.ElapsedMilliseconds;
        }

        await _repository.AddRequestAsync(request);
        await _audit.LogActionAsync(request.Id, $"AI_PROCESSED_{request.Status}",
            $"File={request.OriginalFileName}; SHA256={request.FileSha256}; Reason={request.AiReasoning}; Latency={request.ProcessingLatencyMs}ms");

        return Json(new
        {
            caseId = request.Id,
            image = request.OriginalFileName,
            expected = "N/A",
            actual = request.Status,
            pass = request.Status != "ESCALATE_SYSTEM_ERROR",
            reason = request.AiReasoning,
            question = request.ManagerQuestion,
            latencyMs = request.ProcessingLatencyMs,
            timestamp = request.CreatedAt,
            receiptUrl = request.ImageUrl
        });
    }

    private string? ValidateUpload(IFormFile? file, decimal claimedAmount)
    {
        if (file is null || file.Length == 0) return "Vui lòng chọn file hóa đơn hợp lệ.";
        if (claimedAmount <= 0) return "Số tiền đề nghị hoàn ứng phải lớn hơn 0.";
        if (file.Length > _storageOptions.MaxFileSizeMb * 1024L * 1024L)
            return $"Kích thước file không được vượt quá {_storageOptions.MaxFileSizeMb}MB.";

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png") || !AllowedContentTypes.Contains(file.ContentType))
            return "Chỉ chấp nhận ảnh JPG hoặc PNG có MIME hợp lệ.";
        return null;
    }

    private string GetStorageRoot()
    {
        var contentRoot = Path.GetFullPath(_environment.ContentRootPath);
        var root = Path.GetFullPath(Path.Combine(contentRoot, _storageOptions.Directory));
        if (!root.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ReceiptStorage:Directory phải nằm trong thư mục ứng dụng.");
        return root;
    }

    private static bool HasValidSignature(byte[] bytes, string extension) => extension switch
    {
        ".png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        _ => false
    };
}
