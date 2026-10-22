namespace AURA.Services;

/// <summary>The same byte-level admission rules apply to browser and Drive intake.</summary>
public static class ReceiptAttachmentPolicy
{
    public static string? Validate(string? fileName, string? contentType, byte[]? bytes, int maxFileSizeMb)
    {
        var metadataError = ValidateMetadata(fileName, contentType, bytes?.LongLength ?? 0, maxFileSizeMb);
        if (metadataError is not null) return metadataError;
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        var validSignature = extension == ".png"
            ? bytes!.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            : bytes!.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        return validSignature ? null : "Nội dung file không khớp định dạng JPG/PNG đã khai báo.";
    }

    public static string? ValidateMetadata(string? fileName, string? contentType, long sizeBytes, int maxFileSizeMb)
    {
        if (sizeBytes <= 0) return "Vui lòng chọn file hóa đơn hợp lệ.";
        if (sizeBytes > maxFileSizeMb * 1024L * 1024L)
            return $"Kích thước file không được vượt quá {maxFileSizeMb}MB.";

        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png"))
            return "Chỉ chấp nhận ảnh JPG hoặc PNG có MIME hợp lệ.";
        if (extension == ".png" && !string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase) ||
            extension is ".jpg" or ".jpeg" &&
            !string.Equals(contentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
            return "Nội dung file không khớp MIME JPG/PNG đã khai báo.";

        return null;
    }
}
