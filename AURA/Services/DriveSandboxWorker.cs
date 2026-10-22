using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AURA.Interfaces;
using AURA.Options;
using Microsoft.Extensions.Options;

namespace AURA.Services;

/// <summary>Single-process, read-only synthetic Drive folder poller for local Development.</summary>
public sealed class DriveSandboxWorker : BackgroundService
{
    private const string FolderMime = "application/vnd.google-apps.folder";
    private readonly DriveSandboxOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReceiptStorageOptions _storage;
    private readonly DriveSandboxClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DriveSandboxWorker> _logger;
    private readonly HashSet<string> _recordedRejections = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _rejectionWriteGate = new(1, 1);
    private bool _rejectionsLoaded;

    public DriveSandboxWorker(IOptions<DriveSandboxOptions> options,
        IOptions<ReceiptStorageOptions> storage, IWebHostEnvironment environment,
        IServiceScopeFactory scopeFactory, DriveSandboxClient client, IConfiguration configuration,
        ILogger<DriveSandboxWorker> logger)
    {
        _options = options.Value;
        _storage = storage.Value;
        _environment = environment;
        _scopeFactory = scopeFactory;
        _client = client;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;
        if (!DriveSandboxRuntimePolicy.IsAllowed(_options, _environment.EnvironmentName,
                _configuration["urls"]))
        {
            _logger.LogCritical("Drive sandbox bị chặn: cần Development, loopback và ảnh synthetic.");
            return;
        }

        _logger.LogInformation("Drive synthetic sandbox bắt đầu đọc thư mục riêng; không sửa/xóa file trên Drive.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Never log raw Google response bodies, key material, file names or folder IDs.
                _logger.LogError("Drive sandbox sync lỗi: {ErrorType}", exception.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task SyncOnceAsync(CancellationToken cancellationToken)
    {
        var token = await _client.GetAccessTokenAsync(cancellationToken);
        using var folder = await _client.GetFolderAsync(token, _options.FolderId, cancellationToken);
        var root = folder.RootElement;
        if (root.GetProperty("id").GetString() != _options.FolderId ||
            root.GetProperty("mimeType").GetString() != FolderMime ||
            (root.TryGetProperty("trashed", out var trashed) && trashed.GetBoolean()))
            throw new DriveSandboxException("Configured Drive folder is not accessible.");

        var files = new List<JsonElement>();
        string? page = null;
        var pages = 0;
        do
        {
            if (++pages > _options.MaxFilesPerScan + 1)
                throw new DriveSandboxException("Drive pagination vượt giới hạn.");
            using var listing = await _client.ListFilesAsync(token, _options.FolderId, page, cancellationToken);
            var listed = listing.RootElement;
            if (listed.TryGetProperty("files", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (files.Count >= _options.MaxFilesPerScan)
                        throw new DriveSandboxException("Drive scan vượt giới hạn; không ingest một phần.");
                    files.Add(item.Clone());
                }
            }
            page = listed.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
        } while (page is not null);

        foreach (var file in files.OrderBy(item => item.GetProperty("id").GetString(),
            StringComparer.Ordinal))
            await ProcessFileAsync(token, file, cancellationToken);
        _logger.LogInformation("Drive sandbox scan hoàn tất: {Count} entries.", files.Count);
    }

    private async Task ProcessFileAsync(string token, JsonElement file, CancellationToken cancellationToken)
    {
        var id = file.GetProperty("id").GetString() ??
            throw new DriveSandboxException("Drive file missing ID.");
        var version = file.TryGetProperty("version", out var versionElement) ?
            versionElement.GetString() : null;
        // A Drive version also changes for metadata-only edits. Content identity
        // must use the file ID and checksum, or one image can create two jobs.
        var sourceKey = $"drive:{_options.FolderId}:{id}:{version}";
        if (string.IsNullOrWhiteSpace(version) || !HasParent(file, _options.FolderId) ||
            (file.TryGetProperty("trashed", out var trashed) && trashed.GetBoolean()))
        {
            await RecordRejectionAsync(sourceKey, "BAD_METADATA", cancellationToken);
            return;
        }

        var name = file.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
        var mime = file.TryGetProperty("mimeType", out var mimeElement) ? mimeElement.GetString() : null;
        var size = file.TryGetProperty("size", out var sizeElement) &&
            long.TryParse(sizeElement.GetString(), out var parsedSize) ? parsedSize : 0;
        var md5 = file.TryGetProperty("md5Checksum", out var md5Element) ?
            md5Element.GetString() : null;
        var canDownload = file.TryGetProperty("capabilities", out var capabilities) &&
            capabilities.TryGetProperty("canDownload", out var download) && download.GetBoolean();
        if (!canDownload || string.IsNullOrWhiteSpace(md5) ||
            ReceiptAttachmentPolicy.ValidateMetadata(name, mime, size, _storage.MaxFileSizeMb) is not null)
        {
            await RecordRejectionAsync(sourceKey, "UNSUPPORTED_FILE", cancellationToken);
            return;
        }

        sourceKey = $"drive:{_options.FolderId}:{id}:{md5.ToLowerInvariant()}";

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReimbursementRepository>();
        if (await repository.GetRequestByIdAsync(ReceiptIntakeService.SourceRequestId(sourceKey)) is not null)
            return;

        byte[] bytes;
        try
        {
            bytes = await _client.DownloadAsync(token, id,
                _storage.MaxFileSizeMb * 1024L * 1024L, cancellationToken);
        }
        catch (DriveSandboxException exception) when (exception.Message == "DRIVE_FILE_TOO_LARGE")
        {
            await RecordRejectionAsync(sourceKey, "TOO_LARGE", cancellationToken);
            return;
        }
        if (bytes.LongLength != size ||
            !string.Equals(Convert.ToHexString(MD5.HashData(bytes)), md5,
                StringComparison.OrdinalIgnoreCase) ||
            ReceiptAttachmentPolicy.Validate(name, mime, bytes, _storage.MaxFileSizeMb) is not null)
        {
            await RecordRejectionAsync(sourceKey, "BAD_FILE_BYTES", cancellationToken);
            return;
        }

        // Catch edits/moves while the bytes were in flight; retry the new version
        // on the next scan instead of ingesting ambiguous bytes.
        using var after = await _client.GetMetadataAsync(token, id, cancellationToken);
        var latest = after.RootElement;
        if (!HasParent(latest, _options.FolderId) ||
            latest.GetProperty("version").GetString() != version ||
            !string.Equals(latest.GetProperty("md5Checksum").GetString(), md5,
                StringComparison.OrdinalIgnoreCase))
        {
            await RecordRejectionAsync(sourceKey, "SOURCE_CHANGED", cancellationToken);
            return;
        }

        var intake = scope.ServiceProvider.GetRequiredService<ReceiptIntakeService>();
        await intake.AcceptAsync(new ReceiptIntakeInput(bytes, name!, mime!, 0,
            "DRIVE-SANDBOX", "Synthetic Drive test", "Sandbox", sourceKey), cancellationToken);
    }

    private static bool HasParent(JsonElement file, string folderId) =>
        file.TryGetProperty("parents", out var parents) &&
        parents.ValueKind == JsonValueKind.Array &&
        parents.EnumerateArray().Any(parent => parent.GetString() == folderId);

    private async Task RecordRejectionAsync(string sourceKey, string reasonCode,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_environment.ContentRootPath, "App_Data", "DriveSandbox");
        Directory.CreateDirectory(directory);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceKey)));
        var rejectionKey = $"{hash}:{reasonCode}";
        var path = Path.Combine(directory, "rejections.jsonl");

        await _rejectionWriteGate.WaitAsync(cancellationToken);
        try
        {
            if (!_rejectionsLoaded)
            {
                if (File.Exists(path))
                {
                    foreach (var existingLine in await File.ReadAllLinesAsync(path, cancellationToken))
                    {
                        try
                        {
                            using var document = JsonDocument.Parse(existingLine);
                            var entry = document.RootElement;
                            var existingHash = entry.GetProperty("sourceHash").GetString();
                            var existingReason = entry.GetProperty("reasonCode").GetString();
                            if (!string.IsNullOrWhiteSpace(existingHash) &&
                                !string.IsNullOrWhiteSpace(existingReason))
                                _recordedRejections.Add($"{existingHash}:{existingReason}");
                        }
                        catch (JsonException)
                        {
                            // Ignore a malformed historical line; never include file identifiers in logs.
                        }
                    }
                }
                _rejectionsLoaded = true;
            }

            if (_recordedRejections.Contains(rejectionKey)) return;

            var line = JsonSerializer.Serialize(new
            {
                sourceHash = hash,
                reasonCode,
                timestampUtc = DateTimeOffset.UtcNow
            }) + Environment.NewLine;
            await File.AppendAllTextAsync(path, line, cancellationToken);
            _recordedRejections.Add(rejectionKey);
        }
        finally
        {
            _rejectionWriteGate.Release();
        }
    }
}
