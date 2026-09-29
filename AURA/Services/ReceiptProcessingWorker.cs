using System.Diagnostics;
using System.Text.Json;
using AURA.Data;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed class ReceiptProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReceiptProcessingQueue _queue;
    private readonly ReceiptProcessingOptions _options;
    private readonly ReceiptStorageOptions _storageOptions;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ReceiptProcessingWorker> _logger;

    public ReceiptProcessingWorker(IServiceScopeFactory scopeFactory, ReceiptProcessingQueue queue,
        IOptions<ReceiptProcessingOptions> options, IOptions<ReceiptStorageOptions> storageOptions,
        IWebHostEnvironment environment, ILogger<ReceiptProcessingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _options = options.Value;
        _storageOptions = storageOptions.Value;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consecutiveFailureCount = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var processedAny = false;
            try
            {
                while (await ProcessNextAsync(stoppingToken)) processedAny = true;

                if (consecutiveFailureCount > 0)
                {
                    _logger.LogInformation(
                        "Receipt background processing recovered after {FailureCount} consecutive infrastructure failures.",
                        consecutiveFailureCount);
                    consecutiveFailureCount = 0;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                consecutiveFailureCount++;
                var failureDelay = ReceiptProcessingBackoff.Calculate(
                    consecutiveFailureCount,
                    TimeSpan.FromMilliseconds(_options.PollIntervalMs),
                    TimeSpan.FromSeconds(_options.FailureBackoffMaxSeconds));

                if (consecutiveFailureCount == 1 || consecutiveFailureCount % 5 == 0)
                {
                    _logger.LogError(exception,
                        "Receipt background processing is unavailable. Pending jobs remain durable in the database; " +
                        "attempt {FailureCount}, retrying in {RetryDelaySeconds:F0}s.",
                        consecutiveFailureCount, failureDelay.TotalSeconds);
                }
                else
                {
                    _logger.LogWarning(
                        "Receipt background processing remains unavailable ({ExceptionType}); " +
                        "attempt {FailureCount}, retrying in {RetryDelaySeconds:F0}s.",
                        exception.GetType().Name, consecutiveFailureCount, failureDelay.TotalSeconds);
                }

                try
                {
                    await Task.Delay(failureDelay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            if (!processedAny)
            {
                await _queue.WaitForSignalOrTimeoutAsync(
                    TimeSpan.FromMilliseconds(_options.PollIntervalMs), stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var vision = scope.ServiceProvider.GetRequiredService<IVisionExtractor>();
        var execution = scope.ServiceProvider.GetRequiredService<VisionExecutionContext>();
        var now = DateTime.UtcNow;

        var requestId = await db.ReimbursementRequests
            .Where(request =>
                request.ProcessingState == ProcessingStates.Pending ||
                request.ProcessingState == ProcessingStates.Processing && request.ProcessingLeaseUntil < now)
            .OrderBy(request => request.QueuedAt)
            .Select(request => request.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (requestId is null) return false;

        var leaseUntil = now.AddSeconds(_options.LeaseSeconds);
        var claimed = await db.ReimbursementRequests
            .Where(request => request.Id == requestId &&
                (request.ProcessingState == ProcessingStates.Pending ||
                 request.ProcessingState == ProcessingStates.Processing && request.ProcessingLeaseUntil < now))
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(request => request.ProcessingState, ProcessingStates.Processing)
                .SetProperty(request => request.ProcessingStartedAt, now)
                .SetProperty(request => request.ProcessingLeaseUntil, leaseUntil)
                .SetProperty(request => request.ProcessingAttemptCount,
                    request => request.ProcessingAttemptCount + 1), cancellationToken);
        if (claimed == 0) return true;

        var request = await db.ReimbursementRequests.SingleAsync(x => x.Id == requestId, cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        ReceiptExtractionDto? extractedFacts = null;
        string auditAction;

        try
        {
            var physicalPath = ResolveReceiptPath(request.StoredFileName);
            if (!File.Exists(physicalPath))
                throw new VisionExtractionException("RECEIPT_FILE_MISSING",
                    "Không tìm thấy file hóa đơn đã lưu để xử lý.");

            extractedFacts = await vision.ExtractFactsAsync(physicalPath, cancellationToken);
            request.ExtractedFactsJson = JsonSerializer.Serialize(extractedFacts);
            var enforceDuplicatePolicy = request.DuplicateDetected && request.DuplicatePolicyEnabled;
            var decision = PolicyDecisionEngine.Evaluate(extractedFacts, request.ClaimedAmount, enforceDuplicatePolicy);
            request.Status = decision.Status;
            request.AiReasoning = decision.Reason;
            request.ManagerQuestion = decision.ManagerQuestion;
            request.ProcessingState = ProcessingStates.Completed;
            auditAction = $"AI_PROCESSED_{request.Status}";
        }
        catch (VisionExtractionException exception)
        {
            _logger.LogWarning(exception, "Vision extraction stopped with {ErrorCode} for request {RequestId}.",
                exception.Code, request.Id);
            request.Status = "ESCALATE_SYSTEM_ERROR";
            request.AiReasoning = $"{exception.UserMessage} Mã lỗi: {exception.Code}. Không có quyết định tự động nào được đưa ra.";
            request.ManagerQuestion = "AI chưa xử lý được ảnh. Quản lý có đồng ý tiếp nhận hồ sơ để kiểm tra thủ công không? [CÓ/KHÔNG]";
            request.ProviderErrorCode = exception.Code;
            request.ProcessingState = ProcessingStates.Failed;
            auditAction = "AI_PROCESSED_ESCALATE_SYSTEM_ERROR";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Leave the lease in place. A later worker can safely reclaim it after expiry.
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Vision extraction failed for request {RequestId}.", request.Id);
            request.Status = "ESCALATE_SYSTEM_ERROR";
            request.AiReasoning = "AI không thể trích xuất hóa đơn do lỗi kỹ thuật; không có quyết định tự động nào được đưa ra.";
            request.ManagerQuestion = "AI không xử lý được ảnh. Quản lý có đồng ý tiếp nhận hồ sơ để kiểm tra thủ công không? [CÓ/KHÔNG]";
            request.ProviderErrorCode = "AI_UNEXPECTED_ERROR";
            request.ProcessingState = ProcessingStates.Failed;
            auditAction = "AI_PROCESSED_ESCALATE_SYSTEM_ERROR";
        }
        finally
        {
            stopwatch.Stop();
        }

        request.ProcessingLatencyMs = stopwatch.ElapsedMilliseconds;
        request.ProcessingCompletedAt = DateTime.UtcNow;
        request.ProcessingLeaseUntil = null;
        request.PrimaryProvider = execution.PrimaryProvider;
        request.ServedProvider = execution.ServedProvider;
        request.FallbackUsed = execution.FallbackUsed;
        request.ProviderErrorCode ??= execution.PrimaryErrorCode;
        db.AuditLogs.Add(new AuditLog
        {
            RequestId = request.Id,
            Action = auditAction,
            Timestamp = DateTime.UtcNow,
            Details = $"File={request.OriginalFileName}; Submitter={request.SubmitterCode}; SHA256={request.FileSha256}; " +
                $"DuplicateDetected={request.DuplicateDetected}; DuplicatePolicyEnabled={request.DuplicatePolicyEnabled}; " +
                $"PrimaryProvider={request.PrimaryProvider}; ServedProvider={request.ServedProvider}; " +
                $"FallbackUsed={request.FallbackUsed}; ProviderErrorCode={request.ProviderErrorCode}; " +
                $"Reason={request.AiReasoning}; Latency={request.ProcessingLatencyMs}ms"
        });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private string ResolveReceiptPath(string storedFileName)
    {
        var storageRoot = Path.GetFullPath(Path.IsPathRooted(_storageOptions.Directory)
            ? _storageOptions.Directory
            : Path.Combine(_environment.ContentRootPath, _storageOptions.Directory));
        var path = Path.GetFullPath(Path.Combine(storageRoot, Path.GetFileName(storedFileName)));
        if (!path.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase))
            throw new VisionExtractionException("RECEIPT_PATH_INVALID", "Đường dẫn file hóa đơn không hợp lệ.");
        return path;
    }
}
