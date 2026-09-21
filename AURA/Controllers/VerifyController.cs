using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using AURA.Interfaces;
using AURA.Models;
using AURA.Services;
using Microsoft.AspNetCore.Mvc;

namespace AURA.Controllers;

public sealed class VerifyController : Controller
{
    private readonly IVisionExtractor _vision;
    private readonly IWebHostEnvironment _environment;
    private readonly IReimbursementRepository _repository;
    private readonly IAuditLogger _audit;
    private readonly ILogger<VerifyController> _logger;

    public VerifyController(IVisionExtractor vision, IWebHostEnvironment environment,
        IReimbursementRepository repository, IAuditLogger audit, ILogger<VerifyController> logger)
    {
        _vision = vision;
        _environment = environment;
        _repository = repository;
        _audit = audit;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index() => RedirectToAction("Index", "Home");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunHarness(CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(_environment.WebRootPath, "test_data", "expected-results.json");
        if (!System.IO.File.Exists(manifestPath))
            return NotFound(new { error = "Không tìm thấy manifest kiểm thử." });

        List<ExpectedResult>? testCases;
        try
        {
            await using var stream = System.IO.File.OpenRead(manifestPath);
            testCases = await JsonSerializer.DeserializeAsync<List<ExpectedResult>>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Invalid Verify manifest.");
            return StatusCode(500, new { error = "Manifest kiểm thử không hợp lệ." });
        }

        if (testCases is null || testCases.Count != 5)
            return StatusCode(500, new { error = "Verify Harness phải có đúng 5 ca (3 thường quy, 2 chuyển tiếp)." });

        var results = new List<object>(testCases.Count);
        foreach (var testCase in testCases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            var imagePath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "test_data", "images", testCase.ImageName));
            var imageRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "test_data", "images"));
            string actualStatus;
            string reason;
            string question;
            string? extractedFactsJson = null;

            if (!imagePath.StartsWith(imageRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(imagePath))
            {
                actualStatus = "ESCALATE_SYSTEM_ERROR";
                reason = "Thiếu ảnh kiểm thử trong manifest.";
                question = "Ảnh kiểm thử bị thiếu. Quản lý có đồng ý tiếp nhận để kiểm tra cấu hình thủ công không? [CÓ/KHÔNG]";
            }
            else
            {
                try
                {
                    var facts = await _vision.ExtractFactsAsync(imagePath, cancellationToken);
                    extractedFactsJson = JsonSerializer.Serialize(facts);
                    var decision = PolicyDecisionEngine.Evaluate(facts, testCase.ClaimedAmount);
                    actualStatus = decision.Status;
                    reason = decision.Reason;
                    question = decision.ManagerQuestion;
                }
                catch (VisionExtractionException exception)
                {
                    _logger.LogWarning(exception, "Verify case {CaseId} stopped with {ErrorCode}.", testCase.Id, exception.Code);
                    actualStatus = "ESCALATE_SYSTEM_ERROR";
                    reason = $"{exception.UserMessage} Mã lỗi: {exception.Code}.";
                    question = "AI chưa xử lý được ảnh. Quản lý có đồng ý tiếp nhận để kiểm tra thủ công không? [CÓ/KHÔNG]";
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Verify case {CaseId} failed.", testCase.Id);
                    actualStatus = "ESCALATE_SYSTEM_ERROR";
                    reason = "AI không xử lý được ca kiểm thử do lỗi kỹ thuật.";
                    question = "AI không xử lý được ảnh. Quản lý có đồng ý tiếp nhận để kiểm tra thủ công không? [CÓ/KHÔNG]";
                }
            }

            stopwatch.Stop();
            var bytes = System.IO.File.Exists(imagePath)
                ? await System.IO.File.ReadAllBytesAsync(imagePath, cancellationToken)
                : [];
            var request = new ReimbursementRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                ClaimedAmount = testCase.ClaimedAmount,
                ImageUrl = $"/test_data/images/{Uri.EscapeDataString(testCase.ImageName)}",
                OriginalFileName = testCase.ImageName,
                StoredFileName = string.Empty,
                ContentType = Path.GetExtension(testCase.ImageName).Equals(".png", StringComparison.OrdinalIgnoreCase)
                    ? "image/png" : "image/jpeg",
                FileSizeBytes = bytes.LongLength,
                FileSha256 = bytes.Length == 0 ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(bytes)),
                ExtractedFactsJson = extractedFactsJson,
                CreatedAt = DateTime.UtcNow,
                Status = actualStatus,
                AiReasoning = reason,
                ManagerQuestion = question,
                ProcessingLatencyMs = stopwatch.ElapsedMilliseconds
            };
            await _repository.AddRequestAsync(request);
            await _audit.LogActionAsync(request.Id, $"VERIFY_{actualStatus}",
                $"Case={testCase.Id}; Expected={testCase.ExpectedStatus}; Reason={reason}; Latency={stopwatch.ElapsedMilliseconds}ms");

            results.Add(new
            {
                caseId = testCase.Id,
                image = testCase.ImageName,
                expected = testCase.ExpectedStatus,
                actual = actualStatus,
                pass = StatusMatches(testCase.ExpectedStatus, actualStatus),
                reason,
                question,
                latencyMs = stopwatch.ElapsedMilliseconds,
                timestamp = DateTime.UtcNow,
                receiptUrl = request.ImageUrl,
                canForward = PolicyDecisionEngine.IsEscalation(actualStatus),
                handoffPrompt = PolicyDecisionEngine.IsEscalation(actualStatus)
                    ? EscalationWorkflow.EmployeeHandoffPrompt
                    : null
            });
        }

        return Json(results);
    }

    private static bool StatusMatches(string expected, string actual) =>
        string.Equals(expected, actual, StringComparison.Ordinal) ||
        string.Equals(expected, "ESCALATE", StringComparison.Ordinal) && PolicyDecisionEngine.IsEscalation(actual);
}
