using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using AURA.Interfaces;
using AURA.Models;
using AURA.Services;
using AURA.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;

namespace AURA.Controllers;

public sealed class VerifyController : Controller
{
    private readonly IVisionExtractor _vision;
    private readonly IWebHostEnvironment _environment;
    private readonly IReimbursementRepository _repository;
    private readonly ILogger<VerifyController> _logger;
    private readonly WorkflowOperationGate _operationGate;
    private readonly VisionExecutionContext _execution;
    private readonly VerifyOptions _verifyOptions;

    public VerifyController(IVisionExtractor vision, IWebHostEnvironment environment,
        IReimbursementRepository repository, ILogger<VerifyController> logger,
        WorkflowOperationGate operationGate, VisionExecutionContext execution,
        IOptions<VerifyOptions> verifyOptions)
    {
        _vision = vision;
        _environment = environment;
        _repository = repository;
        _logger = logger;
        _operationGate = operationGate;
        _execution = execution;
        _verifyOptions = verifyOptions.Value;
    }

    [HttpGet]
    public IActionResult Index() => RedirectToAction("Index", "Home");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("verify-harness")]
    public async Task<IActionResult> RunHarness(CancellationToken cancellationToken)
    {
        if (!VerifyExecutionPolicy.IsAllowed(_verifyOptions.Enabled, _environment.EnvironmentName)) return NotFound(new
        {
            error = "Verify Harness đang tắt trên môi trường này. Chỉ bật trong phiên kiểm thử có giám sát."
        });

        if (!_operationGate.TryEnter("Verify Harness đang chạy", out var lease))
        {
            return Conflict(new
            {
                error = "Hệ thống đang xử lý một thao tác khác. Vui lòng chờ thao tác hiện tại hoàn tất rồi thử lại.",
                currentOperation = _operationGate.CurrentOperation
            });
        }
        using var operation = lease!;

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
        VisionExtractionException? blockingProviderFailure = null;
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
            ReceiptExtractionDto? extractedFacts = null;
            var aiAttempted = false;
            string? primaryProvider = null;
            string? servedProvider = null;
            string? providerErrorCode = null;
            var fallbackUsed = false;
            var preInferencePrivacyBlocked = false;

            if (blockingProviderFailure is not null)
            {
                actualStatus = "ESCALATE_SYSTEM_ERROR";
                reason = $"Đã bỏ qua lời gọi AI để bảo vệ quota sau lỗi {blockingProviderFailure.Code}: {blockingProviderFailure.UserMessage}";
                question = "AI đang không khả dụng. Quản lý có đồng ý tiếp nhận để kiểm tra thủ công không? [CÓ/KHÔNG]";
            }
            else if (!imagePath.StartsWith(imageRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(imagePath))
            {
                actualStatus = "ESCALATE_SYSTEM_ERROR";
                reason = "Thiếu ảnh kiểm thử trong manifest.";
                question = "Ảnh kiểm thử bị thiếu. Quản lý có đồng ý tiếp nhận để kiểm tra cấu hình thủ công không? [CÓ/KHÔNG]";
            }
            else
            {
                try
                {
                    if (results.Count > 0 && _verifyOptions.InterCaseDelayMs > 0)
                        await Task.Delay(_verifyOptions.InterCaseDelayMs, cancellationToken);
                    aiAttempted = true;
                    extractedFacts = await _vision.ExtractFactsAsync(imagePath, cancellationToken);
                    extractedFactsJson = JsonSerializer.Serialize(extractedFacts);
                    var decision = PolicyDecisionEngine.Evaluate(extractedFacts, testCase.ClaimedAmount);
                    actualStatus = decision.Status;
                    reason = decision.Reason;
                    question = decision.ManagerQuestion;
                }
                catch (VisionExtractionException exception) when (ExternalImagePrivacyGate.IsPrivacyBlock(exception.Code))
                {
                    preInferencePrivacyBlocked = true;
                    actualStatus = "ESCALATE_PRIVACY";
                    reason = exception.UserMessage;
                    providerErrorCode = exception.Code;
                    question = "Ảnh chưa được gửi tới AI bên ngoài. Quản lý có đồng ý tiếp nhận để xử lý thủ công không? [CÓ/KHÔNG]";
                }
                catch (VisionExtractionException exception)
                {
                    _logger.LogWarning(exception, "Verify case {CaseId} stopped with {ErrorCode}.", testCase.Id, exception.Code);
                    actualStatus = "ESCALATE_SYSTEM_ERROR";
                    reason = $"{exception.UserMessage} Mã lỗi: {exception.Code}.";
                    question = "AI chưa xử lý được ảnh. Quản lý có đồng ý tiếp nhận để kiểm tra thủ công không? [CÓ/KHÔNG]";
                    if (IsBlockingProviderFailure(exception.Code))
                        blockingProviderFailure = exception;
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

            if (aiAttempted)
            {
                primaryProvider = _execution.PrimaryProvider;
                servedProvider = _execution.ServedProvider;
                providerErrorCode ??= _execution.PrimaryErrorCode;
                fallbackUsed = _execution.FallbackUsed;
            }
            else if (blockingProviderFailure is not null)
            {
                providerErrorCode = blockingProviderFailure.Code;
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
                ProcessingLatencyMs = stopwatch.ElapsedMilliseconds,
                ProcessingState = actualStatus == "ESCALATE_SYSTEM_ERROR"
                    ? ProcessingStates.Failed
                    : ProcessingStates.Completed,
                QueuedAt = DateTime.UtcNow,
                ProcessingStartedAt = DateTime.UtcNow,
                ProcessingCompletedAt = DateTime.UtcNow,
                ProcessingAttemptCount = aiAttempted ? 1 : 0,
                PrimaryProvider = primaryProvider,
                ServedProvider = servedProvider,
                ProviderErrorCode = providerErrorCode,
                FallbackUsed = fallbackUsed,
                SubmitterCode = "VERIFY-HARNESS",
                SubmitterDisplayName = "Verify Harness",
                SubmitterDepartment = "Quality Assurance"
            };
            request.RetentionExpiresAt = ReceiptRetentionPolicy.ForAutomatedDecision(request);
            await _repository.AddRequestWithAuditAsync(request, $"VERIFY_{actualStatus}",
                $"Case={testCase.Id}; Expected={testCase.ExpectedStatus}; PreInferencePrivacyBlocked={preInferencePrivacyBlocked}; PrimaryProvider={primaryProvider}; " +
                $"ServedProvider={servedProvider}; FallbackUsed={fallbackUsed}; ProviderErrorCode={providerErrorCode}; " +
                $"Reason={reason}; Latency={stopwatch.ElapsedMilliseconds}ms");

            results.Add(new
            {
                // Workflow actions must use the persisted request key. The manifest ID
                // (for example TC-04) is only a human-readable Verify Harness label.
                caseId = request.Id,
                testCaseId = testCase.Id,
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
                    : null,
                primaryProvider,
                servedProvider,
                fallbackUsed,
                providerErrorCode,
                facts = extractedFacts
            });
        }

        return Json(results);
    }

    private static bool StatusMatches(string expected, string actual) =>
        string.Equals(expected, actual, StringComparison.Ordinal) ||
        string.Equals(expected, "ESCALATE", StringComparison.Ordinal) && PolicyDecisionEngine.IsEscalation(actual);

    private static bool IsBlockingProviderFailure(string code) =>
        VisionProviderFailurePolicy.IsBlocking(code);
}

