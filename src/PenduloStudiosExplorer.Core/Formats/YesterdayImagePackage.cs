using System.Buffers.Binary;

namespace PenduloStudiosExplorer.Core.Formats;

/// <summary>
/// Yesterday cutscene comic panels and multi-image packs.
/// Stored as an embedded Runaway 2-style offset-size archive table:
/// <code>
/// word0: count * 4 (table half-size in bytes)
/// word1: 4 + word0 * 2 (table end / entry 0 offset)
/// count entry offsets, then count entry sizes
/// followed by individual JPEG and/or PNG images
/// </code>
/// Implements <see cref="IAnimationAsset"/> so the explorer UI displays it in Atlas Grid mode
/// with scrubbing and multi-image export capabilities.
/// </summary>
public sealed class YesterdayImagePackage : IAnimationAsset
{
    public readonly record struct SubImage(int Index, int Offset, int Size, int Width, int Height, bool IsPng);

    private readonly byte[] _data;
    private readonly SubImage[] _entries;
    private readonly (int X, int Y, int Width, int Height) _bounds;

    public YesterdayImagePackage(byte[] data, SubImage[] entries, int maxWidth, int maxHeight)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(entries);
        _data = data;
        _entries = entries;
        _bounds = (0, 0, maxWidth, maxHeight);
    }

    public int FrameCount => _entries.Length;

    public (int X, int Y, int Width, int Height) Bounds => _bounds;

    public IReadOnlyList<SubImage> Entries => _entries;

    public SpriteFrame DecodeFrame(int index)
    {
        if (index < 0 || index >= _entries.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        ref readonly SubImage entry = ref _entries[index];
        byte[] itemData = _data.AsSpan(entry.Offset, entry.Size).ToArray();
        DecodedImage? img = entry.IsPng
            ? PngDecoder.Decode(itemData)
            : JpegDecoder.Decode(itemData);

        img ??= DecodedImage.Transparent(Math.Max(1, entry.Width), Math.Max(1, entry.Height));
        return new SpriteFrame(img, 0, 0);
    }

    public string? Verify() => null;

    /// <summary>
    /// Checks whether <paramref name="data"/> is an embedded image package archive whose entries are all valid JPEG and/or PNG images.
    /// </summary>
    public static YesterdayImagePackage? Detect(byte[] data)
    {
        if (data is null || data.Length < 16)
            return null;

        uint word0 = BinaryPrimitives.ReadUInt32LittleEndian(data);
        uint word1 = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4));

        if (word0 == 0 || word0 % 4 != 0 || word1 != 4 + word0 * 2 || word1 > data.Length)
            return null;

        int count = (int)(word0 / 4);
        if (count < 1 || count > 2000)
            return null;

        var entries = new SubImage[count];
        int maxWidth = 0;
        int maxHeight = 0;

        for (int i = 0; i < count; i++)
        {
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4 + i * 4));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4 + (int)word0 + i * 4));

            if (offset == 0 || size == 0 || (long)offset + size > data.Length)
                return null;

            ReadOnlySpan<byte> sub = data.AsSpan((int)offset, (int)size);
            bool isPng = PngDecoder.IsPng(sub);
            bool isJpeg = !isPng && JpegDecoder.IsJpeg(sub);

            if (!isPng && !isJpeg)
                return null;

            int w, h;
            if (isPng)
            {
                if (!PngDecoder.TryGetDimensions(sub, out w, out h))
                    return null;
            }
            else
            {
                if (!JpegDecoder.TryGetDimensions(sub, out w, out h))
                    return null;
            }

            if (w <= 0 || h <= 0)
                return null;

            if (w > maxWidth) maxWidth = w;
            if (h > maxHeight) maxHeight = h;

            entries[i] = new SubImage(i, (int)offset, (int)size, w, h, isPng);
        }

        return new YesterdayImagePackage(data, entries, maxWidth, maxHeight);
    }
}
