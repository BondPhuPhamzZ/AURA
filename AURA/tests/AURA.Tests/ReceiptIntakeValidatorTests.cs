using AURA.Options;
using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class ReceiptIntakeValidatorTests
{
    private readonly ReceiptStorageOptions _options = new() { Directory = "receipts", MaxFileSizeMb = 5 };

    [Fact]
    public void Accepts_a_jpeg_with_matching_extension_mime_and_magic_bytes()
    {
        var result = ReceiptIntakeValidator.Validate("receipt.jpeg", "image/jpeg",
            [0xFF, 0xD8, 0xFF, 0x01], 0, _options);

        Assert.True(result.IsValid);
        Assert.Equal(".jpg", result.NormalizedExtension);
        Assert.Equal("image/jpeg", result.NormalizedContentType);
    }

    [Fact]
    public void Rejects_a_declared_jpeg_with_invalid_magic_bytes()
    {
        var result = ReceiptIntakeValidator.Validate("receipt.jpg", "image/jpeg",
            [0x01, 0x02, 0x03], 0, _options);

        Assert.False(result.IsValid);
        Assert.Equal("SIGNATURE_MISMATCH", result.ErrorCode);
    }

    [Fact]
    public void Rejects_unsupported_attachment_types_before_storage()
    {
        var result = ReceiptIntakeValidator.Validate("invoice.pdf", "application/pdf",
            [0x25, 0x50, 0x44, 0x46], 0, _options);

        Assert.False(result.IsValid);
        Assert.Equal("UNSUPPORTED_MEDIA_TYPE", result.ErrorCode);
    }

    [Fact]
    public void Rejects_a_file_above_the_configured_limit()
    {
        var options = new ReceiptStorageOptions { Directory = "receipts", MaxFileSizeMb = 1 };
        var bytes = new byte[1024 * 1024 + 1];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;

        var result = ReceiptIntakeValidator.Validate("receipt.jpg", "image/jpeg", bytes, 0, options);

        Assert.False(result.IsValid);
        Assert.Equal("FILE_TOO_LARGE", result.ErrorCode);
    }
}
