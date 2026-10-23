using System.Security.Cryptography;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptStoredFileIntegrityTests
{
    [Fact]
    public async Task Accepts_original_bytes_and_rejects_same_length_tampering()
    {
        var path = Path.Combine(Path.GetTempPath(), "aura-integrity-" + Guid.NewGuid().ToString("N") + ".jpg");
        byte[] original = [1, 2, 3];
        var hash = Convert.ToHexString(SHA256.HashData(original));
        try
        {
            await File.WriteAllBytesAsync(path, original);
            await using (var verified = await ReceiptStoredFileIntegrity.OpenVerifiedAsync(
                path, original.Length, hash, 5, CancellationToken.None))
            {
                Assert.Equal(original.Length, verified.Length);
            }

            await File.WriteAllBytesAsync(path, [1, 2, 4]);
            var failure = await Assert.ThrowsAsync<VisionExtractionException>(() =>
                ReceiptStoredFileIntegrity.OpenVerifiedAsync(path, original.Length, hash, 5, CancellationToken.None));
            Assert.Equal("RECEIPT_FILE_INTEGRITY", failure.Code);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Rejects_missing_or_excessive_size_metadata_without_reading_file()
    {
        var hash = new string('A', 64);
        var missingSize = await Assert.ThrowsAsync<VisionExtractionException>(() =>
            ReceiptStoredFileIntegrity.OpenVerifiedAsync("missing.jpg", 0, hash, 5, CancellationToken.None));
        var excessiveSize = await Assert.ThrowsAsync<VisionExtractionException>(() =>
            ReceiptStoredFileIntegrity.OpenVerifiedAsync("missing.jpg", 6 * 1024 * 1024, hash, 5,
                CancellationToken.None));
        Assert.Equal("RECEIPT_FILE_INTEGRITY", missingSize.Code);
        Assert.Equal("RECEIPT_FILE_INTEGRITY", excessiveSize.Code);
    }
}
