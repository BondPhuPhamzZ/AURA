using AURA.Options;

namespace AURA.Services;

public static class ReceiptIntakeValidator
{
    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png" };

    public static ReceiptIntakeValidationResult Validate(
        string? fileName,
        string? contentType,
        byte[]? bytes,
        decimal claimedAmount,
        ReceiptStorageOptions storageOptions)
    {
        if (bytes is null || bytes.Length == 0)
            return Invalid("EMPTY_FILE", "Vui lòng chọn file hóa đơn hợp lệ.");
        if (claimedAmount < 0)
            return Invalid("NEGATIVE_AMOUNT", "Số tiền đối chiếu không được là số âm.");
        if (bytes.LongLength > storageOptions.MaxFileSizeMb * 1024L * 1024L)
            return Invalid("FILE_TOO_LARGE", $"Kích thước file không được vượt quá {storageOptions.MaxFileSizeMb}MB.");

        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png") ||
            string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
        {
            return Invalid("UNSUPPORTED_MEDIA_TYPE", "Chỉ chấp nhận ảnh JPG hoặc PNG có MIME hợp lệ.");
        }

        if (!HasValidSignature(bytes, extension))
            return Invalid("SIGNATURE_MISMATCH", "Nội dung file không khớp định dạng JPG/PNG đã khai báo.");

        return new ReceiptIntakeValidationResult(true, null, null,
            extension == ".png" ? ".png" : ".jpg",
            extension == ".png" ? "image/png" : "image/jpeg");
    }

    private static bool HasValidSignature(byte[] bytes, string extension) => extension switch
    {
        ".png" => bytes.Length >= 8 && bytes.AsSpan(0, 8)
            .SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        _ => false
    };

    private static ReceiptIntakeValidationResult Invalid(string code, string error) =>
        new(false, code, error, null, null);
}

public sealed record ReceiptIntakeValidationResult(
    bool IsValid,
    string? ErrorCode,
    string? Error,
    string? NormalizedExtension,
    string? NormalizedContentType);
