using System.Security.Cryptography;
using System.Text;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AURA.Services;

public sealed record ReceiptIntakeInput(byte[] Bytes, string FileName, string ContentType,
    decimal ClaimedAmount, string? SubmitterCode, string? SubmitterDisplayName,
    string? SubmitterDepartment, string? SourceKey = null);

public sealed class ReceiptIntakeException(string message) : Exception(message);

public sealed class ReceiptIntakeService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IReimbursementRepository _repository;
    private readonly ReceiptStorageOptions _storageOptions;
    private readonly DecisionPolicyOptions _policyOptions;
    private readonly ReceiptProcessingQueue _queue;

    public ReceiptIntakeService(IWebHostEnvironment environment, IReimbursementRepository repository,
        IOptions<ReceiptStorageOptions> storageOptions, IOptions<DecisionPolicyOptions> policyOptions,
        ReceiptProcessingQueue queue)
    {
        _environment = environment;
        _repository = repository;
        _storageOptions = storageOptions.Value;
        _policyOptions = policyOptions.Value;
        _queue = queue;
    }

    public async Task<ReimbursementRequest> AcceptAsync(ReceiptIntakeInput input,
        CancellationToken cancellationToken = default)
    {
        if (input.ClaimedAmount < 0) throw new ReceiptIntakeException("Số tiền đối chiếu không được là số âm.");
        var error = ReceiptAttachmentPolicy.Validate(input.FileName, input.ContentType, input.Bytes,
            _storageOptions.MaxFileSizeMb);
        if (error is not null) throw new ReceiptIntakeException(error);

        // The caller supplies a stable source/content identity. The SQL primary
        // key supplies idempotency across polls and restarts without a migration.
        var id = input.SourceKey is null ? Guid.NewGuid().ToString("N") :
            SourceRequestId(input.SourceKey);
        var sha256 = Convert.ToHexString(SHA256.HashData(input.Bytes));
        if (input.SourceKey is not null)
        {
            var prior = await _repository.GetRequestByIdAsync(id);
            if (prior is not null)
            {
                if (!string.Equals(prior.FileSha256, sha256, StringComparison.OrdinalIgnoreCase))
                    throw new ReceiptIntakeException("Định danh nguồn đã được dùng cho nội dung ảnh khác.");
                return prior;
            }
        }

        var extension = Path.GetExtension(input.FileName).ToLowerInvariant();
        var storedFileName = id + extension;
        var storageRoot = ReceiptStoragePath.ResolveRoot(_environment, _storageOptions.Directory);
        Directory.CreateDirectory(storageRoot);
        var physicalPath = ReceiptStoragePath.ResolveFile(storageRoot, storedFileName);
        var stagedPath = ReceiptStoragePath.ResolveFile(storageRoot,
            $".{storedFileName}.{Guid.NewGuid():N}.tmp");
        var createdFinal = false;
        var persistAttempted = false;
        try
        {
            await using (var output = new FileStream(stagedPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            {
                await output.WriteAsync(input.Bytes, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            try
            {
                File.Move(stagedPath, physicalPath);
                createdFinal = true;
            }
            catch (IOException) when (input.SourceKey is not null && File.Exists(physicalPath))
            {
                // A previous attempt may have committed the bytes but died before SQL.
                // Never overwrite a conflicting file or accept a partial write.
                await VerifyExistingFileAsync(physicalPath, input.Bytes.LongLength, sha256,
                    cancellationToken);
            }
            var duplicate = await _repository.ExistsByFileHashAsync(sha256);
            var now = DateTime.UtcNow;
            var safeFileName = new string(Path.GetFileName(input.FileName)
                .Where(character => !char.IsControl(character)).Take(255).ToArray());
            var request = new ReimbursementRequest
            {
                Id = id,
                ClaimedAmount = input.ClaimedAmount,
                OriginalFileName = safeFileName,
                StoredFileName = storedFileName,
                ContentType = extension == ".png" ? "image/png" : "image/jpeg",
                FileSizeBytes = input.Bytes.LongLength,
                FileSha256 = sha256,
                ImageUrl = $"/Applicant/Receipt/{id}",
                CreatedAt = now,
                QueuedAt = now,
                ProcessingState = ProcessingStates.Pending,
                Status = ProcessingStates.Pending,
                DuplicateDetected = duplicate,
                DuplicatePolicyEnabled = _policyOptions.EscalateDuplicateReceipts,
                SubmitterCode = NormalizeIdentity(input.SubmitterCode, "DEMO-EMPLOYEE", 100),
                SubmitterDisplayName = NormalizeIdentity(input.SubmitterDisplayName, "Nhân viên demo", 200),
                SubmitterDepartment = NormalizeIdentity(input.SubmitterDepartment, "Demo", 200)
            };
            persistAttempted = true;
            try
            {
                await _repository.AddRequestWithAuditAsync(request, "AI_QUEUED",
                    $"Source={(input.SourceKey is null ? "MANUAL" : "DRIVE_SANDBOX")}; " +
                    $"AmountSource={(input.ClaimedAmount > 0 ? AmountSources.UserProvided : "AI_PENDING")}; " +
                    $"DuplicateDetected={duplicate}; " +
                    $"DuplicatePolicyEnabled={request.DuplicatePolicyEnabled}");
            }
            catch (DbUpdateException) when (input.SourceKey is not null)
            {
                // The primary key arbitrates concurrent pollers. A second insert may
                // lose after the first transaction has committed; resolve that race
                // to the durable row when its bytes match rather than failing the scan.
                var concurrent = await _repository.GetRequestByIdAsync(id);
                if (concurrent is null ||
                    !string.Equals(concurrent.FileSha256, sha256, StringComparison.OrdinalIgnoreCase))
                    throw;

                _queue.Signal(id);
                return concurrent;
            }
            _queue.Signal(id);
            return request;
        }
        catch
        {
            // After a database write starts, its result can be ambiguous on network failure.
            // Preserve the complete final file for source-key retry instead of orphaning a row.
            if (createdFinal && !persistAttempted) File.Delete(physicalPath);
            throw;
        }
        finally
        {
            if (File.Exists(stagedPath)) File.Delete(stagedPath);
        }
    }

    private static async Task VerifyExistingFileAsync(string path, long expectedSize,
        string expectedSha256, CancellationToken cancellationToken)
    {
        try
        {
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (file.Length != expectedSize ||
                !string.Equals(Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken)),
                    expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new ReceiptIntakeException("File nguồn đã lưu không khớp nội dung; cần kiểm tra thủ công.");
        }
        catch (IOException)
        {
            throw new ReceiptIntakeException("File nguồn đang được ghi hoặc không thể đọc; hãy thử lại.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new ReceiptIntakeException("Không có quyền kiểm tra file nguồn đã lưu.");
        }
    }

    public static string SourceRequestId(string sourceKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceKey)))[..32].ToLowerInvariant();

    private static string NormalizeIdentity(string? value, string fallback, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
