namespace PenduloStudiosExplorer.Core.Formats;

/// <summary>
/// Hollywood Monsters inventory item icons library in RESOURCE.000 entry 43:
/// 126 64x64 8-bit indexed icon pages (516,096 bytes total).
/// Colors 176..255 reference RESOURCE.000 entry 49's 80-color palette; 0 is transparent.
/// </summary>
public sealed class HollywoodItemIconsAsset : IAnimationAsset
{
    public const int IconSize = 64;
    public const int IconBytes = IconSize * IconSize;
    public const int IconCount = 126;
    public const int TotalBytes = IconCount * IconBytes;

    private readonly byte[] _data;

    public HollywoodItemIconsAsset(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length != TotalBytes)
            throw new ArgumentException($"Expected {TotalBytes} bytes for inventory item icons, got {data.Length}.");
        _data = data;
    }

    public int FrameCount => IconCount;

    public (int X, int Y, int Width, int Height) Bounds => (0, 0, IconSize, IconSize);

    public IndexedPalette? Palette { get; set; }

    public SpriteFrame DecodeFrame(int index)
    {
        if (index < 0 || index >= IconCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        IndexedPalette palette = Palette ?? IndexedPalette.Grayscale;
        var pixels = new byte[IconBytes * 4];
        int srcOffset = index * IconBytes;

        for (int i = 0; i < IconBytes; i++)
        {
            byte b = _data[srcOffset + i];
            int dst = i * 4;
            if (b == 0)
            {
                // Transparent background
                pixels[dst] = 0;
                pixels[dst + 1] = 0;
                pixels[dst + 2] = 0;
                pixels[dst + 3] = 0;
            }
            else
            {
                palette.ToBgra(b, pixels, dst);
            }
        }

        return new SpriteFrame(new DecodedImage(IconSize, IconSize, pixels), 0, 0);
    }

    public string? Verify() => _data.Length == TotalBytes ? null : "Invalid size for Hollywood Monsters item icons";
}
