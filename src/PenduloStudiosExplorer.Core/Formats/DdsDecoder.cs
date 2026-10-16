using System.Buffers.Binary;
using PenduloStudiosExplorer.Core.FileSystem;

namespace PenduloStudiosExplorer.Core.Formats;

/// <summary>
/// Decodes DirectDraw Surface (DDS) textures used for full-screen foreground occlusion masks
/// and alpha mattes in <em>The Next BIG Thing</em>.
/// </summary>
public static class DdsDecoder
{
    private const uint DdsMagic = 0x20534444; // "DDS " in little-endian
    private const uint DdsHeaderSize = 124;
    private const uint DdsPixelFormatSize = 32;
    private const uint DdpfAlpha = 0x00000002;
    private const uint DdpfRgb = 0x00000040;

    /// <summary>Returns true if the given data starts with a valid DDS header.</summary>
    public static bool IsDds(ReadOnlySpan<byte> data)
    {
        if (data.Length < 128)
            return false;

        return BinaryPrimitives.ReadUInt32LittleEndian(data) == DdsMagic &&
               BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4)) == DdsHeaderSize &&
               BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(76)) == DdsPixelFormatSize;
    }

    /// <summary>
    /// Inspects the header of a DDS texture. If valid and supported, returns image information.
    /// </summary>
    public static ImageInfo? Detect(ReadOnlySpan<byte> data)
    {
        if (!IsDds(data))
            return null;

        int height = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12));
        int width = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16));
        if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
            return null;

        uint pfFlags = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(80));
        uint bitCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(88));

        // 8-bit alpha occlusion mask (D3DFMT_A8)
        if ((pfFlags & DdpfAlpha) != 0 || bitCount == 8)
        {
            if (data.Length < 128 + width * height)
                return null;

            return new ImageInfo
            {
                Width = width,
                Height = height,
                IsMask = true,
                Sharpness = 99,
            };
        }

        // 32-bit or 24-bit RGB/RGBA
        if ((pfFlags & DdpfRgb) != 0)
        {
            if (bitCount == 32 && data.Length >= 128 + width * height * 4)
                return new ImageInfo { Width = width, Height = height, Sharpness = 99 };
            if (bitCount == 24 && data.Length >= 128 + width * height * 3)
                return new ImageInfo { Width = width, Height = height, Sharpness = 99 };
        }

        return null;
    }

    /// <summary>
    /// Decodes a DDS texture into a <see cref="DecodedImage"/> (BGRA32).
    /// For 8-bit alpha masks, pixels are tinted using <paramref name="colorSeed"/> with the alpha channel from the texture.
    /// </summary>
    public static DecodedImage Decode(ReadOnlySpan<byte> data, int? colorSeed = null)
    {
        if (!IsDds(data))
            throw new InvalidOperationException("Not a valid DDS image.");

        int height = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12));
        int width = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16));
        uint pfFlags = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(80));
        uint bitCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(88));

        byte[] pixels = new byte[width * height * 4];
        ReadOnlySpan<byte> payload = data.Slice(128);

        // 8-bit alpha texture (D3DFMT_A8)
        if ((pfFlags & DdpfAlpha) != 0 || bitCount == 8)
        {
            var palette = RleMaskDecoder.GeneratePalette();
            int colorIndex = colorSeed.HasValue && colorSeed.Value > 0 ? ((colorSeed.Value - 1) % 254) + 1 : 1;
            (byte baseB, byte baseG, byte baseR, _) = palette[colorIndex];

            for (int i = 0; i < width * height && i < payload.Length; i++)
            {
                byte a = payload[i];
                if (a > 0)
                {
                    int dst = i * 4;
                    pixels[dst + 0] = baseB;
                    pixels[dst + 1] = baseG;
                    pixels[dst + 2] = baseR;
                    pixels[dst + 3] = a;
                }
            }

            return new DecodedImage(width, height, pixels);
        }

        // 32-bit RGB/RGBA
        if ((pfFlags & DdpfRgb) != 0 && bitCount == 32)
        {
            uint rMask = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(92));
            bool isBgra = rMask == 0x00FF0000;
            for (int i = 0; i < width * height && (i + 1) * 4 <= payload.Length; i++)
            {
                int src = i * 4;
                int dst = i * 4;
                if (isBgra)
                {
                    pixels[dst + 0] = payload[src + 0];
                    pixels[dst + 1] = payload[src + 1];
                    pixels[dst + 2] = payload[src + 2];
                    pixels[dst + 3] = payload[src + 3];
                }
                else
                {
                    pixels[dst + 0] = payload[src + 2];
                    pixels[dst + 1] = payload[src + 1];
                    pixels[dst + 2] = payload[src + 0];
                    pixels[dst + 3] = payload[src + 3];
                }
            }

            return new DecodedImage(width, height, pixels);
        }

        // 24-bit RGB
        if ((pfFlags & DdpfRgb) != 0 && bitCount == 24)
        {
            uint rMask = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(92));
            bool isBgr = rMask == 0x00FF0000;
            for (int i = 0; i < width * height && (i * 3 + 3) <= payload.Length; i++)
            {
                int src = i * 3;
                int dst = i * 4;
                if (isBgr)
                {
                    pixels[dst + 0] = payload[src + 0];
                    pixels[dst + 1] = payload[src + 1];
                    pixels[dst + 2] = payload[src + 2];
                }
                else
                {
                    pixels[dst + 0] = payload[src + 2];
                    pixels[dst + 1] = payload[src + 1];
                    pixels[dst + 2] = payload[src + 0];
                }
                pixels[dst + 3] = 255;
            }

            return new DecodedImage(width, height, pixels);
        }

        throw new NotSupportedException($"Unsupported DDS pixel format: pfFlags=0x{pfFlags:X}, bitCount={bitCount}");
    }
}
