using System.Buffers.Binary;

namespace AURA.Services;

/// <summary>
/// Reads the EXIF orientation tag. Browsers rotate images by this tag while OCR reads stored
/// pixels, so evidence boxes are only drawn for upright images (orientation 1 or no tag).
/// </summary>
public static class ImageOrientation
{
    public const int Upright = 1;
    public const int Unknown = 0;

    public static int Read(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8) return ReadJpeg(bytes);
            if (bytes.Length > 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                return ReadPng(bytes);
            return Unknown;
        }
        catch (ArgumentOutOfRangeException)
        {
            return Unknown;
        }
    }

    private static int ReadJpeg(ReadOnlySpan<byte> bytes)
    {
        var offset = 2;
        while (offset + 4 <= bytes.Length)
        {
            if (bytes[offset] != 0xFF) return Unknown;
            var marker = bytes[offset + 1];
            if (marker == 0xFF) { offset++; continue; }
            if (marker is 0xD9 or 0xDA) return Upright; // end of image / start of scan: no EXIF.
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 2, 2));
            if (length < 2 || offset + 2 + length > bytes.Length) return Unknown;
            var segment = bytes.Slice(offset + 4, length - 2);
            if (marker == 0xE1 && segment.Length > 6 && segment[..6].SequenceEqual("Exif\0\0"u8))
                return ReadTiff(segment[6..]);
            offset += 2 + length;
        }
        return Unknown;
    }

    private static int ReadPng(ReadOnlySpan<byte> bytes)
    {
        var offset = 8;
        while (offset + 12 <= bytes.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            if (length < 0 || offset + 12 + length > bytes.Length) return Unknown;
            var type = bytes.Slice(offset + 4, 4);
            if (type.SequenceEqual("eXIf"u8)) return ReadTiff(bytes.Slice(offset + 8, length));
            if (type.SequenceEqual("IDAT"u8) || type.SequenceEqual("IEND"u8)) return Upright;
            offset += 12 + length;
        }
        return Unknown;
    }

    private static int ReadTiff(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8) return Unknown;
        var little = tiff[0] == (byte)'I' && tiff[1] == (byte)'I';
        if (!little && !(tiff[0] == (byte)'M' && tiff[1] == (byte)'M')) return Unknown;
        if (U16(tiff, 2, little) != 42) return Unknown;
        var ifd = U32(tiff, 4, little);
        if (ifd > int.MaxValue - 2 || ifd + 2 > tiff.Length) return Unknown;
        int count = U16(tiff, (int)ifd, little);
        for (var index = 0; index < count; index++)
        {
            var entry = (int)ifd + 2 + index * 12;
            if (entry + 12 > tiff.Length) return Unknown;
            if (U16(tiff, entry, little) != 0x0112) continue;
            int value = U16(tiff, entry + 8, little);
            return value is >= 1 and <= 8 ? value : Unknown;
        }
        return Upright;
    }

    private static ushort U16(ReadOnlySpan<byte> data, int at, bool little) => little
        ? BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(at, 2))
        : BinaryPrimitives.ReadUInt16BigEndian(data.Slice(at, 2));

    private static uint U32(ReadOnlySpan<byte> data, int at, bool little) => little
        ? BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(at, 4))
        : BinaryPrimitives.ReadUInt32BigEndian(data.Slice(at, 4));
}
