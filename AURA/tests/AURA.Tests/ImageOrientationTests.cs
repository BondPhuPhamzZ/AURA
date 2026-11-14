using AURA.Services;
using Xunit;

namespace AURA.Tests;

public sealed class ImageOrientationTests
{
    private static readonly string ProjectRoot = FindProjectRoot();

    [Fact]
    public void Synthetic_fixture_without_exif_is_upright()
    {
        var bytes = File.ReadAllBytes(Path.Combine(ProjectRoot, "test_kit", "v3_1", "images",
            "R3-01-vat-invoice-number.jpg"));
        Assert.Equal(ImageOrientation.Upright, ImageOrientation.Read(bytes));
    }

    [Theory]
    [InlineData(true, 6)]
    [InlineData(false, 6)]
    [InlineData(true, 1)]
    [InlineData(false, 3)]
    public void Exif_orientation_is_read_in_both_byte_orders(bool littleEndian, int orientation)
    {
        Assert.Equal(orientation, ImageOrientation.Read(JpegWithOrientation(littleEndian, orientation)));
    }

    [Fact]
    public void Png_without_exif_is_upright_and_garbage_is_unknown()
    {
        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0, (byte)'I', (byte)'E', (byte)'N', (byte)'D', 0xAE, 0x42, 0x60, 0x82];
        Assert.Equal(ImageOrientation.Upright, ImageOrientation.Read(png));
        Assert.Equal(ImageOrientation.Unknown, ImageOrientation.Read(new byte[] { 1, 2, 3, 4 }));
        var truncated = JpegWithOrientation(true, 6)[..20];
        Assert.Equal(ImageOrientation.Unknown, ImageOrientation.Read(truncated));
        var invalid = JpegWithOrientation(true, 9);
        Assert.Equal(ImageOrientation.Unknown, ImageOrientation.Read(invalid));
    }

    private static byte[] JpegWithOrientation(bool little, int orientation)
    {
        byte[] U16(int value) => little ? [(byte)value, (byte)(value >> 8)] : [(byte)(value >> 8), (byte)value];
        byte[] U32(int value) => little
            ? [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)]
            : [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
        var tiff = new List<byte>();
        tiff.AddRange(little ? "II"u8.ToArray() : "MM"u8.ToArray());
        tiff.AddRange(U16(42));
        tiff.AddRange(U32(8));
        tiff.AddRange(U16(1));                 // one IFD entry
        tiff.AddRange(U16(0x0112));            // Orientation
        tiff.AddRange(U16(3));                 // SHORT
        tiff.AddRange(U32(1));
        tiff.AddRange(U16(orientation));
        tiff.AddRange(U16(0));
        tiff.AddRange(U32(0));                 // no next IFD
        var segment = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        var length = segment.Length + 2;
        return new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, (byte)(length >> 8), (byte)length }
            .Concat(segment)
            .Concat(new byte[] { 0xFF, 0xDA, 0x00, 0x02, 0xFF, 0xD9 })
            .ToArray();
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AURA.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy AURA.csproj từ thư mục test output.");
    }
}
