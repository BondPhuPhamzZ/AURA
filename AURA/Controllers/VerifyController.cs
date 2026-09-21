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
    private readonly ILogger<VerifyController> _logger;

    public VerifyController(IVisionExtractor vision, IWebHostEnvironment environment,
        IReimbursementRepository repository, ILogger<VerifyController> logger)
    {
        _vision = vision;
        _environment = environment;
        _repository = repository;
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
            return NotFound(new { error = "KhÃ´ng tÃ¬m tháº¥y manifest kiá»ƒm thá»­." });

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
            return StatusCode(500, new { error = "Manifest kiá»ƒm thá»­ khÃ´ng há»£p lá»‡." });
        }

        if (testCases is null || testCases.Count != 5)
            return StatusCode(500, new { error = "Verify Harness pháº£i cÃ³ Ä‘Ãºng 5 ca (3 thÆ°á»ng quy, 2 chuyá»ƒn tiáº¿p)." });

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

            if (blockingProviderFailure is not null)
            {
                actualStatus = "ESCALATE_SYSTEM_ERROR";
                reason = $"ÄÃ£ bá» qua lá»i gá»i AI Ä‘á»ƒ báº£o vá»‡ quota sau lá»—i {blockingProviderFailure.Code}: {blockingProviderFailure.UserMessage}";
                question = "AI Ä‘ang khÃ´ng kháº£ dá»¥ng. Quáº£n lÃ½ cÃ³ Ä‘á»“ng Ã½ tiáº¿p nháº­n Ä‘á»ƒ kiá»ƒm tra thá»§ cÃ´ng khÃ´ng? [CÃ“/KHÃ”NG]";
            }
            else if (!imagePath.StartsWith(imageRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(imagePath))
            {
                actualStatus = "ESCALATE_SYSTEM_ERROR";
                reason = "Thiáº¿u áº£nh kiá»ƒm thá»­ trong manifest.";
                question = "áº¢nh kiá»ƒm thá»­ bá»‹ thiáº¿u. Quáº£n lÃ½ cÃ³ Ä‘á»“ng Ã½ tiáº¿p nháº­n Ä‘á»ƒ kiá»ƒm tra cáº¥u hÃ¬nh thá»§ cÃ´ng khÃ´ng? [CÃ“/KHÃ”NG]";
            }
            else
            {
                try
                {
                    if (results.Count > 0) await Task.Delay(4000, cancellationToken);
                    extractedFacts = await _vision.ExtractFactsAsync(imagePath, cancellationToken);
                    extractedFactsJson = JsonSerializer.Serialize(extractedFacts);
                    var decision = PolicyDecisionEngine.Evaluate(extractedFacts, testCase.ClaimedAmount);
                    actualStatus = decision.Status;
                    reason = decision.Reason;
                    question = decision.ManagerQuestion;
                }
                catch (VisionExtractionException exception)
                {
                    _logger.LogWarning(exception, "Verify case {CaseId} stopped with {ErrorCode}.", testCase.Id, exception.Code);
                    actualStatus = "ESCALATE_SYSTEM_ERROR";
                    reason = $"{exception.UserMessage} MÃ£ lá»—i: {exception.Code}.";
                    question = "AI chÆ°a xá»­ lÃ½ Ä‘Æ°á»£c áº£nh. Quáº£n lÃ½ cÃ³ Ä‘á»“ng Ã½ tiáº¿p nháº­n Ä‘á»ƒ kiá»ƒm tra thá»§ cÃ´ng khÃ´ng? [CÃ“/KHÃ”NG]";
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
                    reason = "AI khÃ´ng xá»­ lÃ½ Ä‘Æ°á»£c ca kiá»ƒm thá»­ do lá»—i ká»¹ thuáº­t.";
                    question = "AI khÃ´ng xá»­ lÃ½ Ä‘Æ°á»£c áº£nh. Quáº£n lÃ½ cÃ³ Ä‘á»“ng Ã½ tiáº¿p nháº­n Ä‘á»ƒ kiá»ƒm tra thá»§ cÃ´ng khÃ´ng? [CÃ“/KHÃ”NG]";
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
            await _repository.AddRequestWithAuditAsync(request, $"VERIFY_{actualStatus}",
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
                    : null,
                facts = extractedFacts
            });
        }

        return Json(results);
    }

    private static bool StatusMatches(string expected, string actual) =>
        string.Equals(expected, actual, StringComparison.Ordinal) ||
        string.Equals(expected, "ESCALATE", StringComparison.Ordinal) && PolicyDecisionEngine.IsEscalation(actual);

    private static bool IsBlockingProviderFailure(string code) => code is
        "AI_TIMEOUT" or "AI_RATE_LIMIT" or "AI_NOT_CONFIGURED" or "AI_TEMPORARILY_UNAVAILABLE";
}

