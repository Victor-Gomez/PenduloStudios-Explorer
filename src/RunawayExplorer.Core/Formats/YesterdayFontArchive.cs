using System.Buffers.Binary;

namespace RunawayExplorer.Core.Formats;

/// <summary>
/// A single 24-bit RGB outlined font parsed from Yesterday's <c>RESOURCE.FNT</c>.
/// </summary>
public sealed class YesterdayFont(int index, string name, FontDecoder.Glyph[] glyphs, byte[] bitmapRgb)
{
    public int Index { get; } = index;
    public string Name { get; } = name;
    public FontDecoder.Glyph[] Glyphs { get; } = glyphs;
    public byte[] BitmapRgb { get; } = bitmapRgb;
}

/// <summary>
/// Decodes <i>Yesterday</i>'s 24-bit outlined font library (<c>RESOURCE.FNT</c>, ~784 KB).
/// Holds two anti-aliased and outlined 24-bit fonts:
/// <list type="bullet">
///   <item><b>Font 0:</b> In-game dialogue and subtitles font (196 glyphs, 7-byte records).</item>
///   <item><b>Font 1:</b> User interface and menu typography font (191 glyphs, 7-byte records).</item>
/// </list>
/// Pixel data is stored as 24-bit RGB (3 bytes per pixel: channel 0 = outline alpha, channel 1 = body alpha,
/// channel 2 = outline alpha).
/// </summary>
public static class YesterdayFontArchive
{
    public const int HeaderSize = 36;

    public static bool IsFontArchive(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
            return false;

        uint f0TableOff = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
        uint f0TableLen = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
        uint f1TableOff = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4));
        uint f1BmpOff = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));
        uint f1BmpLen = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(32, 4));

        return f0TableOff == HeaderSize &&
               f0TableLen > 0 &&
               f1TableOff > f0TableOff &&
               f1BmpOff >= f1TableOff &&
               (long)f1BmpOff + f1BmpLen <= data.Length;
    }

    public static List<YesterdayFont> ReadFonts(ReadOnlySpan<byte> data)
    {
        var fonts = new List<YesterdayFont>();
        if (!IsFontArchive(data))
            return fonts;

        uint f0TableOff = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
        uint f0TableLen = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4));
        uint f1TableOff = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4));
        uint f1BmpOff = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));
        uint f1TableLen = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(20, 4));
        uint f1BmpLen = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(32, 4));

        // Font 0
        int f0GlyphCount = (int)(f0TableLen / FontDecoder.RecordSizeLater);
        var f0GlyphTableSpan = data.Slice((int)f0TableOff, f0GlyphCount * FontDecoder.RecordSizeLater);
        var f0Glyphs = FontDecoder.ReadGlyphTable(f0GlyphTableSpan, FontDecoder.RecordSizeLater);

        int f0BmpOff = (int)(f0TableOff + f0TableLen);
        int f0BmpLen = (int)(f1TableOff - f0BmpOff);
        byte[] f0Bmp = data.Slice(f0BmpOff, Math.Max(0, f0BmpLen)).ToArray();
        fonts.Add(new YesterdayFont(0, "Dialogue & Subtitle Font", f0Glyphs, f0Bmp));

        // Font 1
        int f1GlyphCount = (int)(f1TableLen / FontDecoder.RecordSizeLater);
        var f1GlyphTableSpan = data.Slice((int)f1TableOff, f1GlyphCount * FontDecoder.RecordSizeLater);
        var f1Glyphs = FontDecoder.ReadGlyphTable(f1GlyphTableSpan, FontDecoder.RecordSizeLater);

        byte[] f1Bmp = data.Slice((int)f1BmpOff, (int)f1BmpLen).ToArray();
        fonts.Add(new YesterdayFont(1, "Interface Typography Font", f1Glyphs, f1Bmp));

        return fonts;
    }
}
