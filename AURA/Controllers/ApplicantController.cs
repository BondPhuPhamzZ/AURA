using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using AURA.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;

namespace AURA.Controllers;

public sealed class ApplicantController : Controller
{
    private readonly IWebHostEnvironment _environment;
    private readonly IReimbursementRepository _repository;
    private readonly ReceiptStorageOptions _storageOptions;
    private readonly ReceiptIntakeService _intake;

    public ApplicantController(IWebHostEnvironment environment, IReimbursementRepository repository,
        IOptions<ReceiptStorageOptions> storageOptions,
        ReceiptIntakeService intake)
    {
        _environment = environment;
        _repository = repository;
        _storageOptions = storageOptions.Value;
        _intake = intake;
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
    public async Task<IActionResult> ForwardToManager(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest(new { error = "Thiếu mã hồ sơ." });
        var request = await _repository.GetRequestByIdAsync(id);
        if (request is null) return NotFound(new { error = "Không tìm thấy hồ sơ." });
        if (!PolicyDecisionEngine.IsEscalation(request.Status))
            return Conflict(new { error = "Chỉ hồ sơ cần chuyển tiếp mới được gửi cho quản lý." });

        if (!request.IsForwardedToManager)
        {
            request.IsForwardedToManager = true;
            request.ForwardedAt = DateTime.UtcNow;
            try
            {
                await _repository.UpdateRequestWithAuditAsync(request, "EMPLOYEE_FORWARDED_TO_MANAGER",
                    $"Mode=SINGLE; Question={request.ManagerQuestion}; Status={request.Status}");
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { error = "Hồ sơ vừa được cập nhật ở thao tác khác. Vui lòng tải lại trang." });
            }
        }

        TempData["Success"] = $"Đã chuyển hồ sơ {request.Id[..8]} đến cửa sổ quản lý.";
        var redirectUrl = Url.Action("Index", "Home", new { tab = "reviewer" }) ?? "/?tab=reviewer";
        if (!string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return Redirect(redirectUrl);

        return Json(new
        {
            message = "Chuyển tiếp thành công. Quản lý có thể đồng ý duyệt hoặc từ chối duyệt.",
            redirectUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForwardAllToManager()
    {
        var pending = (await _repository.GetAllRequestsAsync())
            .Where(x => !x.IsForwardedToManager && PolicyDecisionEngine.IsEscalation(x.Status))
            .ToList();

        var forwardedAt = DateTime.UtcNow;
        foreach (var request in pending)
        {
            request.IsForwardedToManager = true;
            request.ForwardedAt = forwardedAt;
        }

        try
        {
            await _repository.UpdateRequestsWithAuditAsync(pending.Select(request =>
                    (request, $"Mode=BULK; Question={request.ManagerQuestion}; Status={request.Status}")),
                "EMPLOYEE_FORWARDED_TO_MANAGER");
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Danh sách hồ sơ vừa thay đổi. Vui lòng tải lại trang rồi thử lại." });
        }

        TempData["Success"] = pending.Count == 0
            ? "Không có hồ sơ mới cần chuyển tiếp."
            : $"Đã chuyển tiếp {pending.Count} hồ sơ đến cửa sổ quản lý.";
        if (string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            var redirectUrl = Url.Action("Index", "Home", new { tab = pending.Count == 0 ? "applicant" : "reviewer" })
                ?? "/?tab=reviewer";
            return Json(new
            {
                message = pending.Count == 0
                    ? "Không có hồ sơ mới cần chuyển tiếp."
                    : $"Đã chuyển tiếp {pending.Count} hồ sơ đến cửa sổ quản lý.",
                forwardedCount = pending.Count,
                redirectUrl
            });
        }
        return RedirectToAction("Index", "Home", new { tab = pending.Count == 0 ? "applicant" : "reviewer" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("receipt-intake")]
    public async Task<IActionResult> UploadReceipt(IFormFile? receiptFile, decimal? claimedAmount,
        string? submitterCode, string? submitterDisplayName, string? submitterDepartment,
        CancellationToken cancellationToken)
    {
        var preliminaryError = ReceiptAttachmentPolicy.ValidateMetadata(receiptFile?.FileName,
            receiptFile?.ContentType, receiptFile?.Length ?? 0, _storageOptions.MaxFileSizeMb);
        if (preliminaryError is not null) return BadRequest(new { error = preliminaryError });

        var file = receiptFile!;
        var bytes = new byte[checked((int)file.Length)];
        await using (var input = file.OpenReadStream())
        {
            await input.ReadExactlyAsync(bytes, cancellationToken);
        }

        ReimbursementRequest request;
        try
        {
            request = await _intake.AcceptAsync(new ReceiptIntakeInput(bytes, file.FileName, file.ContentType,
                claimedAmount.GetValueOrDefault(), submitterCode, submitterDisplayName, submitterDepartment),
                cancellationToken);
        }
        catch (ReceiptIntakeException exception)
        {
            return BadRequest(new { error = exception.Message });
        }

        var result = ReimbursementResultDto.FromRequest(request);
        return AcceptedAtAction(nameof(Status), new { id = request.Id }, new
        {
            result.CaseId,
            result.Image,
            result.Actual,
            result.Pass,
            result.ProcessingState,
            result.Reason,
            result.Question,
            result.LatencyMs,
            result.Timestamp,
            result.ReceiptUrl,
            result.CanForward,
            result.HandoffPrompt,
            result.DuplicateDetected,
            result.DuplicatePolicyEnabled,
            result.PrimaryProvider,
            result.ServedProvider,
            result.FallbackUsed,
            result.ProviderErrorCode,
            result.Facts,
            statusUrl = Url.Action(nameof(Status), "Applicant", new { id = request.Id })
        });
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Status(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest(new { error = "Thiếu mã hồ sơ." });
        var request = await _repository.GetRequestByIdAsync(id);
        if (request is null) return NotFound(new { error = "Không tìm thấy hồ sơ." });
        return Json(ReimbursementResultDto.FromRequest(request));
    }

    private string GetStorageRoot()
    {
        if (string.IsNullOrWhiteSpace(_storageOptions.Directory))
            throw new InvalidOperationException("ReceiptStorage:Directory không được để trống.");

        // Relative paths stay anchored to ContentRoot. Cloud hosts can provide an absolute
        // mount such as /home/data/receipts for durable evidence storage.
        return Path.GetFullPath(Path.IsPathRooted(_storageOptions.Directory)
            ? _storageOptions.Directory
            : Path.Combine(_environment.ContentRootPath, _storageOptions.Directory));
    }

}
