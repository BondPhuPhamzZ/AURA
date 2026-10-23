using System.Security.Cryptography;

namespace AURA.Services;

public static class ReceiptStoredFileIntegrity
{
    public static async Task<FileStream> OpenVerifiedAsync(string path, long expectedSizeBytes,
        string expectedSha256, int maxFileSizeMb, CancellationToken cancellationToken)
    {
        if (expectedSizeBytes <= 0 || maxFileSizeMb <= 0 ||
            expectedSizeBytes > maxFileSizeMb * 1024L * 1024L ||
            expectedSha256 is not { Length: 64 } || !expectedSha256.All(Uri.IsHexDigit))
            throw new VisionExtractionException("RECEIPT_FILE_INTEGRITY",
                "Thông tin toàn vẹn ảnh hóa đơn không hợp lệ; hồ sơ cần kiểm tra thủ công.");

        FileStream? stream = null;
        var verified = false;
        try
        {
            // Keep the read-only handle open through inference so another ordinary
            // Windows/SMB writer cannot replace the verified bytes in between.
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length != expectedSizeBytes)
                throw new VisionExtractionException("RECEIPT_FILE_INTEGRITY",
                    "Kích thước ảnh hóa đơn đã lưu không khớp; hồ sơ cần kiểm tra thủ công.");

            var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new VisionExtractionException("RECEIPT_FILE_INTEGRITY",
                    "Ảnh hóa đơn đã lưu không còn khớp dữ kiện lúc tiếp nhận; hồ sơ cần kiểm tra thủ công.");
            stream.Position = 0;
            verified = true;
            return stream;
        }
        catch (IOException)
        {
            throw new VisionExtractionException("RECEIPT_FILE_UNAVAILABLE",
                "Không thể đọc ảnh hóa đơn đã lưu; hồ sơ cần kiểm tra thủ công.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new VisionExtractionException("RECEIPT_FILE_UNAVAILABLE",
                "Không có quyền đọc ảnh hóa đơn đã lưu; hồ sơ cần kiểm tra thủ công.");
        }
        finally
        {
            if (stream is not null && !verified)
                await stream.DisposeAsync();
        }
    }
}
