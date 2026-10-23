using System.Security.Cryptography;

namespace AURA.Services;

public static class ReceiptStoredFileIntegrity
{
    public static void ValidateContentHash(ReadOnlySpan<byte> bytes, string expectedSha256)
    {
        if (expectedSha256 is not { Length: 64 } || !expectedSha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Receipt content hash metadata is invalid.");

        var expected = Convert.FromHexString(expectedSha256);
        var actual = SHA256.HashData(bytes);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            throw new InvalidDataException("Receipt content does not match its declared SHA-256.");
    }

    public static async Task<byte[]> ReadVerifiedAsync(Stream source, long expectedSizeBytes,
        string expectedSha256, int maxFileSizeMb, CancellationToken cancellationToken)
    {
        ValidateMetadata(expectedSizeBytes, expectedSha256, maxFileSizeMb);
        try
        {
            await using var buffer = new MemoryStream(checked((int)expectedSizeBytes));
            var chunk = new byte[64 * 1024];
            while (true)
            {
                var count = await source.ReadAsync(chunk, cancellationToken);
                if (count == 0) break;
                if (buffer.Length + count > expectedSizeBytes)
                    throw new VisionExtractionException("RECEIPT_FILE_INTEGRITY",
                        "Ảnh hóa đơn đã lưu vượt kích thước đã tiếp nhận; hồ sơ cần kiểm tra thủ công.");
                await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
            }
            var bytes = buffer.ToArray();
            if (bytes.LongLength != expectedSizeBytes || !string.Equals(
                    Convert.ToHexString(SHA256.HashData(bytes)), expectedSha256,
                    StringComparison.OrdinalIgnoreCase))
                throw new VisionExtractionException("RECEIPT_FILE_INTEGRITY",
                    "Ảnh hóa đơn đã lưu không còn khớp dữ kiện lúc tiếp nhận; hồ sơ cần kiểm tra thủ công.");
            return bytes;
        }
        catch (IOException)
        {
            throw new VisionExtractionException("RECEIPT_FILE_UNAVAILABLE",
                "Không thể đọc ảnh hóa đơn đã lưu; hồ sơ cần kiểm tra thủ công.");
        }
    }

    public static async Task<FileStream> OpenVerifiedAsync(string path, long expectedSizeBytes,
        string expectedSha256, int maxFileSizeMb, CancellationToken cancellationToken)
    {
        ValidateMetadata(expectedSizeBytes, expectedSha256, maxFileSizeMb);

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

    private static void ValidateMetadata(long expectedSizeBytes, string expectedSha256, int maxFileSizeMb)
    {
        if (expectedSizeBytes <= 0 || maxFileSizeMb <= 0 ||
            expectedSizeBytes > maxFileSizeMb * 1024L * 1024L ||
            expectedSha256 is not { Length: 64 } || !expectedSha256.All(Uri.IsHexDigit))
            throw new VisionExtractionException("RECEIPT_FILE_INTEGRITY",
                "Thông tin toàn vẹn ảnh hóa đơn không hợp lệ; hồ sơ cần kiểm tra thủ công.");
    }
}
