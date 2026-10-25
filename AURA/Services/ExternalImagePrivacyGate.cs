using System.Security.Cryptography;
using System.Text.Json;
using AURA.Options;
using Microsoft.Extensions.Options;

namespace AURA.Services;

/// <summary>Approves exact image bytes before egress. This is not a PII detector or redactor.</summary>
public sealed class ExternalImagePrivacyGate
{
    public const string ExternalBlockedCode = "PRIVACY_EXTERNAL_PROCESSING_BLOCKED";
    public const string ImageNotApprovedCode = "PRIVACY_IMAGE_NOT_APPROVED";
    private readonly HashSet<string> _approved = new(StringComparer.OrdinalIgnoreCase);
    public string Mode { get; }
    public int ApprovedImageCount => _approved.Count;

    public ExternalImagePrivacyGate(IOptions<ExternalImagePrivacyOptions> configured,
        IWebHostEnvironment environment)
    {
        var options = configured.Value;
        if (!ExternalImagePrivacyOptions.IsSupported(options.Mode))
            throw new InvalidOperationException("External image privacy mode is invalid.");
        Mode = options.Mode;
        if (!Mode.Equals(ExternalImagePrivacyOptions.ApprovedImagesOnly, StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            if (string.IsNullOrWhiteSpace(options.ApprovedHashesFile) ||
                !Path.IsPathFullyQualified(options.ApprovedHashesFile))
                throw new InvalidDataException();
            var path = Path.GetFullPath(options.ApprovedHashesFile);
            var webRoot = Path.GetFullPath(environment.WebRootPath ??
                Path.Combine(environment.ContentRootPath, "wwwroot"));
            var webPrefix = webRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (path.Equals(webRoot, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(webPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is < 1 or > 256 * 1024) throw new InvalidDataException();
            var bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
            ReadOnlySpan<byte> json = bytes;
            if (json.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) json = json[3..];
            var hashes = JsonSerializer.Deserialize<string[]>(json) ?? throw new InvalidDataException();
            if (hashes.Length is < 1 or > 1000) throw new InvalidDataException();
            foreach (var hash in hashes)
            {
                if (hash is null || hash.Length != 64 || !hash.All(char.IsAsciiHexDigit) || !_approved.Add(hash))
                    throw new InvalidDataException();
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or
            JsonException or ArgumentException or NotSupportedException)
        {
            // Paths, file contents and hashes may themselves identify private data.
            throw new InvalidOperationException("External image approval manifest is missing or invalid; startup is blocked.");
        }
    }

    public void EnsureAllowed(ReadOnlySpan<byte> imageBytes, bool isLocalLoopback = false)
    {
        if (isLocalLoopback || Mode.Equals(ExternalImagePrivacyOptions.LegacyConfigured, StringComparison.OrdinalIgnoreCase))
            return;
        if (Mode.Equals(ExternalImagePrivacyOptions.BlockExternal, StringComparison.OrdinalIgnoreCase))
            throw new VisionExtractionException(ExternalBlockedCode,
                "Chính sách hiện tại chặn gửi ảnh ra dịch vụ ngoài; cần tiếp nhận để xử lý thủ công.");
        if (!_approved.Contains(Convert.ToHexString(SHA256.HashData(imageBytes))))
            throw new VisionExtractionException(ImageNotApprovedCode,
                "Ảnh chưa thuộc danh sách được phép gửi ra dịch vụ ngoài; cần tiếp nhận để xử lý thủ công.");
    }

    public static bool IsPrivacyBlock(string code) => code is ExternalBlockedCode or ImageNotApprovedCode;
}
