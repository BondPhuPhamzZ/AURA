using System.Security.Cryptography;
using System.Text;
using AURA.Interfaces;
using AURA.Models;
using AURA.Options;
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
        if (input.SourceKey is not null)
        {
            var prior = await _repository.GetRequestByIdAsync(id);
            if (prior is not null) return prior;
        }

        var extension = Path.GetExtension(input.FileName).ToLowerInvariant();
        var storedFileName = id + extension;
        var storageRoot = Path.GetFullPath(Path.IsPathRooted(_storageOptions.Directory)
            ? _storageOptions.Directory
            : Path.Combine(_environment.ContentRootPath, _storageOptions.Directory));
        Directory.CreateDirectory(storageRoot);
        var physicalPath = Path.Combine(storageRoot, storedFileName);
        var created = false;
        try
        {
            await using (var output = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            {
                created = true;
                await output.WriteAsync(input.Bytes, cancellationToken);
            }
            var sha256 = Convert.ToHexString(SHA256.HashData(input.Bytes));
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
            await _repository.AddRequestWithAuditAsync(request, "AI_QUEUED",
                $"Source={(input.SourceKey is null ? "MANUAL" : "DRIVE_SANDBOX")}; " +
                $"AmountSource={(input.ClaimedAmount > 0 ? AmountSources.UserProvided : "AI_PENDING")}; " +
                $"DuplicateDetected={duplicate}; " +
                $"DuplicatePolicyEnabled={request.DuplicatePolicyEnabled}");
            _queue.Signal(id);
            return request;
        }
        catch
        {
            if (created) File.Delete(physicalPath);
            throw;
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
