using System.Buffers.Binary;
using System.Text;
using RunawayExplorer.Core.FileSystem;
using RunawayExplorer.Core.Formats;

namespace RunawayExplorer.Core.Tests;

/// <summary>
/// Builders for byte-exact synthetic entries in each of the three scene-archive image formats, so the
/// decoders can be tested without game data. Every builder writes exactly what docs/formats describes.
/// </summary>
public static class SyntheticAssets
{
    static SyntheticAssets()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static ushort Rgb565(int r8, int g8, int b8) => (ushort)(((r8 >> 3) << 11) | ((g8 >> 2) << 5) | (b8 >> 3));

    public static ushort DefaultRasterPixel(int x, int y, int width, int height)
    {
        int hx = (int)(((uint)(x * 2654435761u) >> 24)) * 180 / 255;
        int v = hx + y * 60 / Math.Max(1, height);
        return Rgb565(v, v, v);
    }

    /// <summary>A raster with a distinct vertical gradient per column, so the stride detector has something to lock onto.</summary>
    public static byte[] Raster(int width, int height, Func<int, int, ushort>? pixel = null)
    {
        pixel ??= (x, y) => DefaultRasterPixel(x, y, width, height);
        var data = new byte[width * height * 2];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan((y * width + x) * 2), pixel(x, y));
        return data;
    }

    /// <summary>An overlay from explicit row records.</summary>
    public static byte[] Overlay(IReadOnlyList<(int X, int Y, ushort[] Pixels)> rows)
    {
        using var ms = new MemoryStream();
        Span<byte> u16 = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)rows.Count); ms.Write(u16);
        foreach ((int x, int y, ushort[] px) in rows)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)x); ms.Write(u16);
            BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)y); ms.Write(u16);
            BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)px.Length); ms.Write(u16);
            foreach (ushort p in px) { BinaryPrimitives.WriteUInt16LittleEndian(u16, p); ms.Write(u16); }
        }
        return ms.ToArray();
    }

    /// <summary>An extended overlay whose row records carry an alpha flag and optional alpha channel (Runaway 2).</summary>
    public static byte[] AlphaOverlay(IReadOnlyList<(int X, int Y, byte Flag, (ushort Rgb, byte Alpha)[] Pixels)> rows)
    {
        using var ms = new MemoryStream();
        Span<byte> u16 = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)rows.Count); ms.Write(u16);
        foreach ((int x, int y, byte flag, var px) in rows)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)x); ms.Write(u16);
            BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)y); ms.Write(u16);
            ms.WriteByte(flag);
            BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)px.Length); ms.Write(u16);
            foreach (var p in px)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(u16, p.Rgb); ms.Write(u16);
                if (flag == 1) ms.WriteByte(p.Alpha);
            }
        }
        return ms.ToArray();
    }

    /// <summary>One frame of a sprite: its segments, each an absolute screen run.</summary>
    public sealed record Frame(IReadOnlyList<(int X, int Y, ushort[] Pixels)> Segments);

    /// <summary>
    /// A sprite asset from frames. Each record's box is the frame's own extent (or the union of all
    /// frames when <paramref name="sharedBox"/>), which is how the game files are laid out.
    /// </summary>
    public static byte[] Sprite(IReadOnlyList<Frame> frames, bool sharedBox = true, int descriptorWidth = 0)
    {
        // Frame data first, to know the offsets.
        var frameBytes = new List<byte[]>();
        Span<byte> u16 = stackalloc byte[2];
        foreach (Frame f in frames)
        {
            using var ms = new MemoryStream();
            foreach ((int x, int y, ushort[] px) in f.Segments)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)x); ms.Write(u16);
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)y); ms.Write(u16);
                ms.WriteByte((byte)(px.Length == 1 ? 0 : px.Length)); // count 0 means 1, exercise it
                foreach (ushort p in px) { BinaryPrimitives.WriteUInt16LittleEndian(u16, p); ms.Write(u16); }
            }
            frameBytes.Add(ms.ToArray());
        }

        (int x0, int y0, int x1, int y1) Box(Frame f)
        {
            int bx0 = int.MaxValue, by0 = int.MaxValue, bx1 = 0, by1 = 0;
            foreach ((int x, int y, ushort[] px) in f.Segments)
            {
                bx0 = Math.Min(bx0, x); by0 = Math.Min(by0, y);
                bx1 = Math.Max(bx1, x + px.Length); by1 = Math.Max(by1, y + 1);
            }
            return f.Segments.Count == 0 ? (0, 0, 1, 1) : (bx0, by0, bx1, by1);
        }

        // The union of the non-empty frames. An empty frame has no extent of its own; in the game files
        // its record still carries a box, so here it gets the union (a harmless, in-bounds choice).
        (int ux0, int uy0, int ux1, int uy1) = (int.MaxValue, int.MaxValue, 0, 0);
        foreach (Frame f in frames)
        {
            if (f.Segments.Count == 0) continue;
            var b = Box(f);
            ux0 = Math.Min(ux0, b.x0); uy0 = Math.Min(uy0, b.y0); ux1 = Math.Max(ux1, b.x1); uy1 = Math.Max(uy1, b.y1);
        }
        if (ux0 == int.MaxValue) (ux0, uy0, ux1, uy1) = (0, 0, 1, 1);

        using var output = new MemoryStream();
        Span<byte> w = stackalloc byte[4];
        if (descriptorWidth > 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(w, 0); output.Write(w);
            BinaryPrimitives.WriteUInt16LittleEndian(w, (ushort)descriptorWidth); output.Write(w[..2]);
            BinaryPrimitives.WriteInt16LittleEndian(w, (short)-(descriptorWidth - 1)); output.Write(w[..2]);
            BinaryPrimitives.WriteUInt16LittleEndian(w, 600); output.Write(w[..2]);
            BinaryPrimitives.WriteUInt16LittleEndian(w, 0); output.Write(w[..2]);
            BinaryPrimitives.WriteUInt16LittleEndian(w, 0); output.Write(w[..2]);
        }

        int offset = 0;
        for (int i = 0; i < frames.Count; i++)
        {
            var b = sharedBox || frames[i].Segments.Count == 0 ? (ux0, uy0, ux1, uy1) : Box(frames[i]);
            BinaryPrimitives.WriteUInt32LittleEndian(w, (uint)offset); output.Write(w);
            BinaryPrimitives.WriteUInt16LittleEndian(w, (ushort)b.Item1); output.Write(w[..2]);
            BinaryPrimitives.WriteUInt16LittleEndian(w, (ushort)(b.Item3 - b.Item1)); output.Write(w[..2]);
            BinaryPrimitives.WriteUInt16LittleEndian(w, (ushort)b.Item2); output.Write(w[..2]);
            BinaryPrimitives.WriteUInt16LittleEndian(w, (ushort)(b.Item4 - 1)); output.Write(w[..2]);
            BinaryPrimitives.WriteUInt16LittleEndian(w, (ushort)frames[i].Segments.Count); output.Write(w[..2]);
            offset += frameBytes[i].Length;
        }
        foreach (byte[] fb in frameBytes)
            output.Write(fb);
        return output.ToArray();
    }


    // --- Hollywood Monsters: the same record shapes, but one palette index per pixel. ---

    /// <summary>A palette block: <paramref name="colors"/> 6-bit VGA triples, every component in 0..63.</summary>
    public static byte[] Palette(int colors, int seed = 0)
    {
        var data = new byte[colors * IndexedPalette.BytesPerColor];
        for (int i = 0; i < colors; i++)
        {
            data[i * 3] = (byte)((i + seed) % (IndexedPalette.MaxComponent + 1));
            data[i * 3 + 1] = (byte)((i * 2 + seed) % (IndexedPalette.MaxComponent + 1));
            data[i * 3 + 2] = (byte)((i * 3 + seed) % (IndexedPalette.MaxComponent + 1));
        }
        return data;
    }

    public static byte DefaultIndexedPixel(int x, int y) => (byte)((x * 7 + y * 13) & 0xff);

    /// <summary>
    /// An 8-bit indexed raster. The geometry is not encoded anywhere, so the default is the one screen
    /// size the game uses and the byte count alone is what identifies it.
    /// </summary>
    public static byte[] IndexedRaster(
        int width = RasterDecoder.IndexedScreenWidth,
        int height = RasterDecoder.IndexedScreenHeight,
        Func<int, int, byte>? pixel = null)
    {
        pixel ??= DefaultIndexedPixel;
        var data = new byte[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                data[y * width + x] = pixel(x, y);
        return data;
    }

    /// <summary>An overlay whose row records carry palette indices instead of RGB565 pixels.</summary>
    public static byte[] IndexedOverlay(IReadOnlyList<(int X, int Y, byte[] Pixels)> rows)
    {
        using var ms = new MemoryStream();
        Span<byte> u16 = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)rows.Count); ms.Write(u16);
        foreach ((int x, int y, byte[] px) in rows)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)x); ms.Write(u16);
            BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)y); ms.Write(u16);
            BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)px.Length); ms.Write(u16);
            ms.Write(px);
        }
        return ms.ToArray();
    }

    /// <summary>One frame of an indexed sprite: absolute screen runs of palette indices.</summary>
    public sealed record IndexedFrame(IReadOnlyList<(int X, int Y, byte[] Pixels)> Segments);

    /// <summary>
    /// An indexed sprite: Runaway 1's 14-byte records and 5-byte segment headers with no leading
    /// descriptor record, every record's box being the union of all the frames.
    /// </summary>
    public static byte[] IndexedSprite(IReadOnlyList<IndexedFrame> frames)
    {
        var frameBytes = new List<byte[]>();
        Span<byte> u16 = stackalloc byte[2];
        foreach (IndexedFrame f in frames)
        {
            using var ms = new MemoryStream();
            foreach ((int x, int y, byte[] px) in f.Segments)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)x); ms.Write(u16);
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)y); ms.Write(u16);
                ms.WriteByte((byte)(px.Length == 1 ? 0 : px.Length)); // count 0 means 1, exercise it
                ms.Write(px);
            }
            frameBytes.Add(ms.ToArray());
        }

        (int x0, int y0, int x1, int y1) = (int.MaxValue, int.MaxValue, 0, 0);
        foreach (IndexedFrame f in frames)
            foreach ((int x, int y, byte[] px) in f.Segments)
            {
                x0 = Math.Min(x0, x); y0 = Math.Min(y0, y);
                x1 = Math.Max(x1, x + px.Length); y1 = Math.Max(y1, y + 1);
            }
        if (x0 == int.MaxValue) (x0, y0, x1, y1) = (0, 0, 1, 1);

        using var output = new MemoryStream();
        Span<byte> w = stackalloc byte[4];
        int offset = 0;
        for (int i = 0; i < frames.Count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(w, (uint)offset); output.Write(w);
            BinaryPrimitives.WriteUInt16LittleEndian(w, (ushort)x0); output.Write(w[..2]);
            BinaryPrimitives.WriteUInt16LittleEndian(w, (ushort)(x1 - x0)); output.Write(w[..2]);
            BinaryPrimitives.WriteUInt16LittleEndian(w, (ushort)y0); output.Write(w[..2]);
            BinaryPrimitives.WriteUInt16LittleEndian(w, (ushort)(y1 - 1)); output.Write(w[..2]);
            BinaryPrimitives.WriteUInt16LittleEndian(w, (ushort)frames[i].Segments.Count); output.Write(w[..2]);
            offset += frameBytes[i].Length;
        }
        foreach (byte[] fb in frameBytes)
            output.Write(fb);
        return output.ToArray();
    }

    /// <summary>Reads back the BGRA pixel at (x, y) of a decoded image as (R, G, B, A).</summary>
    public static (byte R, byte G, byte B, byte A) Pixel(DecodedImage image, int x, int y)
    {
        int i = (y * image.Width + x) * 4;
        return (image.Pixels[i + 2], image.Pixels[i + 1], image.Pixels[i], image.Pixels[i + 3]);
    }

    // --- Fonts: outlined glyph bitmaps and their glyph tables. ---

    /// <summary>
    /// A glyph table: <paramref name="glyphs"/> records that tile a bitmap exactly. A record is
    /// { u16 Offset, u8 Top, u8 Height, u8 Width } in Runaway 1 and { u32 Offset, ... } in Runaway 2
    /// and 3, which is what <paramref name="recordSize"/> picks between.
    /// </summary>
    public static byte[] GlyphTable(IReadOnlyList<(byte Top, byte Height, byte Width)> glyphs, int recordSize = 5)
    {
        var data = new byte[glyphs.Count * recordSize];
        uint offset = 0;
        for (int i = 0; i < glyphs.Count; i++)
        {
            Span<byte> r = data.AsSpan(i * recordSize, recordSize);
            if (recordSize == 5)
                BinaryPrimitives.WriteUInt16LittleEndian(r, (ushort)offset);
            else
                BinaryPrimitives.WriteUInt32LittleEndian(r, offset);
            r[recordSize - 3] = glyphs[i].Top;
            r[recordSize - 2] = glyphs[i].Height;
            r[recordSize - 1] = glyphs[i].Width;
            offset += (uint)(glyphs[i].Height * glyphs[i].Width);
        }
        return data;
    }

    /// <summary>
    /// A glyph bitmap whose total size matches the table built by <see cref="GlyphTable"/>.
    /// Each pixel is a shade value from 0x00 (outline) to 0x10 (fill), with 0x12 for the border.
    /// </summary>
    public static byte[] FontBitmap(IReadOnlyList<(byte Top, byte Height, byte Width)> glyphs)
    {
        int totalBytes = 0;
        foreach (var g in glyphs)
            totalBytes += g.Height * g.Width;
        var data = new byte[totalBytes];
        int pos = 0;
        foreach (var g in glyphs)
        {
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                {
                    // Border is transparent (0x12), interior is filled (0x10), edge is outline (0x00).
                    bool border = x == 0 || x == g.Width - 1 || y == 0 || y == g.Height - 1;
                    bool edge = !border && (x == 1 || x == g.Width - 2 || y == 1 || y == g.Height - 2);
                    data[pos++] = border ? (byte)0x12 : edge ? (byte)0x00 : (byte)0x10;
                }
        }
        return data;
    }

    /// <summary>
    /// A Windows BMP of <paramref name="width"/>×<paramref name="height"/>, uncompressed, bottom-up,
    /// 24 or 32 bits per pixel. Pixel (x, y) is (B, G, R) = (x, y, 0x40), and with 32 bpp the alpha is
    /// <paramref name="alpha"/> unless it is null, which writes the zero padding a BMP normally carries.
    /// </summary>
    public static byte[] Bmp(int width, int height, int bpp = 32, byte? alpha = 255)
    {
        int bytesPerPixel = bpp / 8;
        int stride = (width * bytesPerPixel + 3) & ~3;
        int pixels = stride * height;
        var data = new byte[54 + pixels];

        data[0] = (byte)'B';
        data[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(2), (uint)data.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(10), 54);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(22), height);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(26), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(28), (ushort)bpp);

        for (int y = 0; y < height; y++)
        {
            // Bottom-up: the first row stored is the bottom row of the image.
            int row = 54 + (height - 1 - y) * stride;
            for (int x = 0; x < width; x++)
            {
                int i = row + x * bytesPerPixel;
                data[i] = (byte)x;
                data[i + 1] = (byte)y;
                data[i + 2] = 0x40;
                if (bpp == 32)
                    data[i + 3] = alpha ?? 0;
            }
        }

        return data;
    }

    /// <summary>A 904×540 RGB565 cursor atlas with a key-coloured background and one embedded sprite.</summary>
    public static byte[] CursorAtlas()
    {
        const int w = 904, h = 540;
        var data = new byte[w * h * 2];
        ushort key = 0x6841;
        // Fill entirely with key colour.
        for (int i = 0; i < w * h; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(i * 2), key);
        // Paint a small non-key rectangle in the first band (y 10..40, x 20..50) as a synthetic cursor.
        ushort white = Rgb565(255, 255, 255);
        for (int y = 10; y < 40; y++)
            for (int x = 20; x < 50; x++)
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan((y * w + x) * 2), white);
        return data;
    }

    // --- Runaway 1's character sprite library: palette-indexed body runs plus a coverage mask. ---

    /// <summary>
    /// One frame of a character animation, in canvas coordinates. <paramref name="Body"/> runs carry
    /// palette indices; <paramref name="Coverage"/> runs carry either full coverage (kind 0, no bytes) or
    /// per-pixel coverage (kind 1). The builder derives the frame record from these, the way the game's
    /// exporter did.
    /// </summary>
    public sealed record CharacterFrame(
        IReadOnlyList<(int X, int Y, byte[] Indices)> Body,
        IReadOnlyList<(int X, int Y, byte[]? Coverage, int Count)> Coverage);

    /// <summary>
    /// A frame's runs covering the box (<paramref name="x"/>, <paramref name="y"/>,
    /// <paramref name="w"/>, <paramref name="h"/>): the body fills it solid with one index, and the
    /// coverage stream covers the same rows plus one extra row of half-covered "shadow" underneath.
    /// </summary>
    public static CharacterFrame SolidCharacterFrame(int x, int y, int w, int h, byte index)
    {
        var body = new List<(int, int, byte[])>();
        var cover = new List<(int, int, byte[]?, int)>();
        for (int row = 0; row < h - 1; row++)
        {
            var px = new byte[w];
            Array.Fill(px, index);
            body.Add((x, y + row, px));
            cover.Add((x, y + row, null, w));
        }
        // The last row is shadow only: covered, but with no body pixels behind it.
        var partial = new byte[w];
        Array.Fill(partial, (byte)128);
        cover.Add((x, y + h - 1, partial, w));
        return new CharacterFrame(body, cover);
    }

    /// <summary>
    /// One entry of the character sprite library: the fixed 3,200-byte frame table followed by the frame
    /// data. Returns the entry bytes; <see cref="CharacterLibrary"/> wraps entries in the container.
    /// </summary>
    public static byte[] CharacterEntry(IReadOnlyList<CharacterFrame> frames)
    {
        var bodies = new List<byte[]>();
        Span<byte> u16 = stackalloc byte[2];
        foreach (CharacterFrame f in frames)
        {
            using var fs = new MemoryStream();
            foreach ((int x, int y, byte[] px) in f.Body)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)x); fs.Write(u16);
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)y); fs.Write(u16);
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)px.Length); fs.Write(u16);
                fs.Write(px);
            }
            foreach ((int x, int y, byte[]? cov, int count) in f.Coverage)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)x); fs.Write(u16);
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)y); fs.Write(u16);
                fs.WriteByte(cov is null ? (byte)0 : (byte)1);
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)count); fs.Write(u16);
                if (cov is not null) fs.Write(cov);
            }
            bodies.Add(fs.ToArray());
        }

        var table = new byte[CharacterSpriteAsset.FrameTableSize];
        int offset = 0;
        for (int i = 0; i < frames.Count; i++)
        {
            CharacterFrame f = frames[i];
            int minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0;
            foreach ((int x, int y, byte[] px) in f.Body)
            {
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x + px.Length); maxY = Math.Max(maxY, y);
            }
            foreach ((int x, int y, byte[]? _, int count) in f.Coverage)
            {
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x + count); maxY = Math.Max(maxY, y);
            }

            Span<byte> rec = table.AsSpan(i * CharacterSpriteAsset.RecordSize);
            BinaryPrimitives.WriteUInt32LittleEndian(rec, (uint)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(rec[4..], (uint)f.Body.Count);
            // The split between the two stored counts is unknown; only their sum is, so put it all in A.
            BinaryPrimitives.WriteUInt32LittleEndian(rec[8..], (uint)f.Coverage.Count);
            BinaryPrimitives.WriteUInt32LittleEndian(rec[12..], 0);
            // Anchor: the feet, on the bottom-centre of the extent, so they land on one canvas point.
            BinaryPrimitives.WriteUInt32LittleEndian(rec[16..], (uint)(CharacterFeetX - minX));
            BinaryPrimitives.WriteUInt32LittleEndian(rec[20..], (uint)(CharacterFeetY - minY));
            BinaryPrimitives.WriteUInt32LittleEndian(rec[24..], (uint)(maxX - minX));
            BinaryPrimitives.WriteUInt32LittleEndian(rec[28..], (uint)(maxY - minY + 1));
            offset += bodies[i].Length;
        }

        using var ms = new MemoryStream();
        ms.Write(table);
        foreach (byte[] b in bodies)
            ms.Write(b);
        return ms.ToArray();
    }

    /// <summary>One frame of a Runaway 2 character animation with 16-bit direct RGB565 body pixels.</summary>
    public sealed record CharacterFrameR2(
        IReadOnlyList<(int X, int Y, ushort[] Pixels)> Body,
        IReadOnlyList<(int X, int Y, byte[]? Coverage, int Count)> Coverage);

    public static CharacterFrameR2 SolidCharacterFrameR2(int x, int y, int w, int h, ushort color)
    {
        var body = new List<(int, int, ushort[])>();
        var cover = new List<(int, int, byte[]?, int)>();
        for (int row = 0; row < h - 1; row++)
        {
            var px = new ushort[w];
            Array.Fill(px, color);
            body.Add((x, y + row, px));
            cover.Add((x, y + row, null, w));
        }
        var partial = new byte[w];
        Array.Fill(partial, (byte)128);
        cover.Add((x, y + h - 1, partial, w));
        return new CharacterFrameR2(body, cover);
    }

    public static (byte[] Table, byte[] Data) CharacterEntryR2Parts(IReadOnlyList<CharacterFrameR2> frames)
    {
        var bodies = new List<byte[]>();
        Span<byte> u16 = stackalloc byte[2];
        foreach (CharacterFrameR2 f in frames)
        {
            using var fs = new MemoryStream();
            foreach ((int x, int y, ushort[] px) in f.Body)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)x); fs.Write(u16);
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)y); fs.Write(u16);
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)px.Length); fs.Write(u16);
                foreach (ushort p in px)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(u16, p);
                    fs.Write(u16);
                }
            }
            foreach ((int x, int y, byte[]? cov, int count) in f.Coverage)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)x); fs.Write(u16);
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)y); fs.Write(u16);
                fs.WriteByte(cov is null ? (byte)0 : (byte)1);
                BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)count); fs.Write(u16);
                if (cov is not null) fs.Write(cov);
            }
            bodies.Add(fs.ToArray());
        }

        var table = new byte[CharacterSpriteAsset.FrameTableSize];
        int offset = 0;
        for (int i = 0; i < frames.Count; i++)
        {
            CharacterFrameR2 f = frames[i];
            int minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0;
            foreach ((int x, int y, ushort[] px) in f.Body)
            {
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x + px.Length); maxY = Math.Max(maxY, y);
            }
            foreach ((int x, int y, byte[]? _, int count) in f.Coverage)
            {
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x + count); maxY = Math.Max(maxY, y);
            }

            Span<byte> rec = table.AsSpan(i * CharacterSpriteAsset.RecordSize);
            BinaryPrimitives.WriteUInt32LittleEndian(rec, (uint)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(rec[4..], (uint)f.Body.Count);
            BinaryPrimitives.WriteUInt32LittleEndian(rec[8..], (uint)f.Coverage.Count);
            BinaryPrimitives.WriteUInt32LittleEndian(rec[12..], 0);
            BinaryPrimitives.WriteUInt32LittleEndian(rec[16..], (uint)(CharacterFeetX - minX));
            BinaryPrimitives.WriteUInt32LittleEndian(rec[20..], (uint)(CharacterFeetY - minY));
            BinaryPrimitives.WriteUInt32LittleEndian(rec[24..], (uint)(maxX - minX));
            BinaryPrimitives.WriteUInt32LittleEndian(rec[28..], (uint)(maxY - minY + 1));
            offset += bodies[i].Length;
        }

        using var msData = new MemoryStream();
        foreach (byte[] b in bodies)
            msData.Write(b);

        return (table, msData.ToArray());
    }

    public static byte[] CharacterEntryR2(IReadOnlyList<CharacterFrameR2> frames)
    {
        var (table, data) = CharacterEntryR2Parts(frames);
        var combined = new byte[table.Length + data.Length];
        Buffer.BlockCopy(table, 0, combined, 0, table.Length);
        Buffer.BlockCopy(data, 0, combined, table.Length, data.Length);
        return combined;
    }

    /// <summary>The canvas point every synthetic frame puts the character's feet on.</summary>
    public const int CharacterFeetX = 200;

    /// <inheritdoc cref="CharacterFeetX"/>
    public const int CharacterFeetY = 400;

    /// <summary>
    /// A whole <c>Resource.001</c>: the 216-slot offset-then-size table, then the entries. The size half
    /// counts frame data only, so an entry occupies 3,200 more bytes than its slot says.
    /// </summary>
    public static byte[] CharacterLibrary(IReadOnlyDictionary<int, byte[]> entries)
    {
        var table = new byte[CharacterSpriteArchive.TableEnd];
        long offset = CharacterSpriteArchive.TableEnd;
        using var ms = new MemoryStream();
        ms.Write(table);
        foreach (int slot in entries.Keys.Order())
        {
            byte[] entry = entries[slot];
            BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(slot * 4), (uint)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(
                table.AsSpan(CharacterSpriteArchive.SlotCount * 4 + slot * 4),
                (uint)(entry.Length - CharacterSpriteAsset.FrameTableSize));
            ms.Write(entry);
            offset += entry.Length;
        }
        byte[] data = ms.ToArray();
        table.CopyTo(data, 0);
        return data;
    }

    /// <summary>
    /// The run bytes of one animation, laid out the way <c>RESOURCE.000</c> does it: the frames back to
    /// back in a slot of their own, with the records written into <paramref name="table"/> at
    /// <paramref name="at"/> rather than in front of them.
    /// <para>
    /// A <paramref name="geometry"/> of <see cref="CharacterSpriteGeometry.HeadPatch"/> writes the
    /// record the way a talking head's is written: the coverage count in B alone, and a box and anchor
    /// that are some other frame's, so a reader that trusts them gets the wrong size.
    /// </para>
    /// </summary>
    public static byte[] ActorAnimationData(
        IReadOnlyList<CharacterFrame> frames,
        byte[] table,
        int at,
        CharacterSpriteGeometry geometry = CharacterSpriteGeometry.Body)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(table);

        // The entry builder already knows how to serialise the runs and derive the boxes; take its
        // output apart rather than repeating it, so the two layouts cannot drift.
        byte[] entry = CharacterEntry(frames);
        byte[] data = entry[CharacterSpriteAsset.FrameTableSize..];

        for (int i = 0; i < frames.Count; i++)
        {
            Span<byte> src = entry.AsSpan(i * CharacterSpriteAsset.RecordSize, CharacterSpriteAsset.RecordSize);
            Span<byte> dst = table.AsSpan(at + i * CharacterSpriteAsset.RecordSize, CharacterSpriteAsset.RecordSize);
            src.CopyTo(dst);

            if (geometry != CharacterSpriteGeometry.HeadPatch)
                continue;

            uint runs = BinaryPrimitives.ReadUInt32LittleEndian(dst[8..]);
            BinaryPrimitives.WriteUInt32LittleEndian(dst[8..], 32);   // A: not the count, as in the game
            BinaryPrimitives.WriteUInt32LittleEndian(dst[12..], runs); // B: the count
            // The box and anchor of the body underneath, which this patch is nothing like.
            BinaryPrimitives.WriteUInt32LittleEndian(dst[16..], 89);
            BinaryPrimitives.WriteUInt32LittleEndian(dst[20..], 402);
            BinaryPrimitives.WriteUInt32LittleEndian(dst[24..], 168);
            BinaryPrimitives.WriteUInt32LittleEndian(dst[28..], 418);
        }

        return data;
    }

    /// <summary>
    /// A <c>RESOURCE.000</c> holding <paramref name="groups"/> complete actor groups at the slots the
    /// layout fixes, plus whatever <paramref name="extra"/> slots the caller wants. Every animation is
    /// the same short frame list, which is enough for the layout to be the thing under test.
    /// </summary>
    public static byte[] ActorGlobalArchive(IEnumerable<int> groups, IReadOnlyDictionary<int, byte[]>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var slots = new Dictionary<int, byte[]>();

        foreach (int group in groups)
        {
            var walkTable = new byte[ActorArchive.WalkDirections * ActorArchive.WalkFrames * CharacterSpriteAsset.RecordSize];
            for (int dir = 0; dir < ActorArchive.WalkDirections; dir++)
            {
                var animation = new ActorAnimation(group, ActorRole.Walk, dir);
                List<CharacterFrame> frames = [.. Enumerable.Range(0, ActorArchive.WalkFrames)
                    .Select(f => SolidCharacterFrame(40 + f, 30 + dir, 12, 9, (byte)(7 + f)))];
                slots[animation.DataSlot] = ActorAnimationData(frames, walkTable, animation.TableOffset);
            }
            slots[group] = walkTable;

            var headTable = new byte[ActorArchive.Heads * ActorArchive.HeadFrames * CharacterSpriteAsset.RecordSize];
            for (int head = 0; head < ActorArchive.Heads; head++)
            {
                var animation = new ActorAnimation(group, ActorRole.Head, head);
                List<CharacterFrame> frames = [.. Enumerable.Range(0, ActorArchive.HeadFrames)
                    .Select(f => SolidCharacterFrame(114 + head, 32, 7, 5, (byte)(20 + f)))];
                slots[animation.DataSlot] = ActorAnimationData(
                    frames, headTable, animation.TableOffset, CharacterSpriteGeometry.HeadPatch);
            }
            slots[group + ActorArchive.HeadTableSlot] = headTable;

            slots[group + ActorArchive.PaletteSlot] = Rgb565Palette(group);
        }

        if (extra is not null)
            foreach ((int slot, byte[] bytes) in extra)
                slots[slot] = bytes;

        return GlobalArchiveOf(slots);
    }

    /// <summary>A <c>RESOURCE.000</c> holding <paramref name="slots"/> and nothing else.</summary>
    public static byte[] GlobalArchiveOf(IReadOnlyDictionary<int, byte[]> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var list = new byte[]?[slots.Keys.Max() + 1];
        foreach ((int slot, byte[] bytes) in slots)
            list[slot] = bytes;
        return SyntheticArchives.Global(list);
    }

    /// <summary>A 512-byte RGB565 colour table, the shape Runaway 1's character palettes have.</summary>
    public static byte[] Rgb565Palette(int seed = 0)
    {
        var data = new byte[IndexedPalette.Rgb565BlockBytes];
        for (int i = 0; i < 256; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(i * 2), Rgb565((i + seed) & 0xff, (i * 2 + seed) & 0xff, (i * 3 + seed) & 0xff));
        return data;
    }

    /// <summary>Creates a synthetic 149,080-byte Runaway 1 savegame buffer.</summary>
    public static byte[] Savegame(
        string name = "Initial Save",
        string action = "IR A",
        byte sceneId = 7,
        int viewX = 496,
        int viewY = 0,
        int actorX = 1075,
        int actorY = 599,
        int invCount = 4)
    {
        var data = new byte[SavegameArchive.FileSize];
        // Fill thumbnail with recognizable pattern
        for (int y = 0; y < SavegameArchive.ThumbnailHeight; y++)
            for (int x = 0; x < SavegameArchive.ThumbnailWidth; x++)
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan((y * SavegameArchive.ThumbnailWidth + x) * 2), Rgb565(x, y, (x + y) & 0xff));

        // Name
        Encoding.Latin1.GetBytes(name).CopyTo(data.AsSpan(SavegameArchive.OffsetSlotName));
        // Action
        Encoding.Latin1.GetBytes(action).CopyTo(data.AsSpan(SavegameArchive.OffsetAction));
        // Scene ID
        data[SavegameArchive.OffsetSceneId] = sceneId;
        // View and Actor
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(SavegameArchive.OffsetViewX), (ushort)viewX);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(SavegameArchive.OffsetViewY), (ushort)viewY);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(SavegameArchive.OffsetActorX), (ushort)actorX);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(SavegameArchive.OffsetActorY), (ushort)actorY);
        // Inventory count
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(SavegameArchive.OffsetInvCount), (ushort)invCount);

        return data;
    }

    /// <summary>Creates a synthetic RESOURCE.TAB buffer for TNBT or Yesterday.</summary>
    public static byte[] SceneTab(IReadOnlyList<(int SceneId, int Width, int Height, (float X, float Y)[] Points)> scenes)
    {
        using var ms = new MemoryStream();
        ms.WriteByte((byte)scenes.Count);

        Span<byte> u16 = stackalloc byte[2];
        Span<byte> u32 = stackalloc byte[4];
        Span<byte> f = stackalloc byte[4];

        // Scene IDs
        foreach (var s in scenes)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)s.SceneId);
            ms.Write(u16);
        }

        int headerLen = 1 + scenes.Count * 2;
        int tableEnd = headerLen + scenes.Count * 4;

        // Build payloads
        var payloads = new List<byte[]>();
        foreach (var s in scenes)
        {
            var pms = new MemoryStream();
            BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)s.Width); pms.Write(u16);
            BinaryPrimitives.WriteUInt16LittleEndian(u16, (ushort)s.Height); pms.Write(u16);
            // 16 bytes dummy
            pms.Write(new byte[16]);
            // Points
            foreach (var pt in s.Points)
            {
                BinaryPrimitives.WriteSingleLittleEndian(f, pt.X); pms.Write(f);
                BinaryPrimitives.WriteSingleLittleEndian(f, pt.Y); pms.Write(f);
            }
            payloads.Add(pms.ToArray());
        }

        // Write offsets
        int currentOff = tableEnd;
        foreach (var p in payloads)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(u32, (uint)currentOff);
            ms.Write(u32);
            currentOff += p.Length;
        }

        // Write payloads
        foreach (var p in payloads)
            ms.Write(p);

        return ms.ToArray();
    }

    /// <summary>
    /// Creates a synthetic 43,659-byte Runaway 1 route navigation table.
    /// </summary>
    public static byte[] RouteTable(Dictionary<(int From, int To), (IReadOnlyList<(int X, int Y)> Waypoints, IReadOnlyList<byte> Hops)>? routes = null)
    {
        var data = new byte[RouteDecoder.TotalSize];
        if (routes is not null)
        {
            foreach (var ((from, to), (waypoints, hops)) in routes)
            {
                int cell = from * RouteDecoder.ZoneCount + to;
                int coordOffset = cell * RouteDecoder.WaypointSlotBytes;
                for (int w = 0; w < Math.Min(waypoints.Count, RouteDecoder.MaxWaypointsPerRoute); w++)
                {
                    int p = coordOffset + w * RouteDecoder.WaypointBytes;
                    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(p, 2), (ushort)waypoints[w].X);
                    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(p + 2, 2), (ushort)waypoints[w].Y);
                }
                int hopOffset = RouteDecoder.WaypointsTableSize + cell * RouteDecoder.HopSlotBytes;
                for (int h = 0; h < Math.Min(hops.Count, RouteDecoder.HopSlotBytes); h++)
                {
                    data[hopOffset + h] = hops[h];
                }
            }
        }
        return data;
    }

    /// <summary>
    /// Creates a synthetic 335,529-byte Runaway 2 navigation route table (51×51 matrix).
    /// </summary>
    public static byte[] RouteTableR2(Dictionary<(int From, int To), (IReadOnlyList<(int X, int Y)> Waypoints, IReadOnlyList<byte> Hops)>? routes = null)
    {
        var data = new byte[RouteDecoder.TotalSizeR2];
        int wpTableSize = RouteDecoder.RouteCountR2 * RouteDecoder.WaypointSlotBytes; // 208,080

        if (routes is not null)
        {
            foreach (var ((from, to), (waypoints, hops)) in routes)
            {
                int cell = from * RouteDecoder.ZoneCountR2 + to;
                int coordOffset = cell * RouteDecoder.WaypointSlotBytes;
                for (int w = 0; w < Math.Min(waypoints.Count, RouteDecoder.MaxWaypointsPerRoute); w++)
                {
                    int p = coordOffset + w * RouteDecoder.WaypointBytes;
                    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(p, 2), (ushort)waypoints[w].X);
                    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(p + 2, 2), (ushort)waypoints[w].Y);
                }
                int hopOffset = wpTableSize + cell * RouteDecoder.HopSlotBytesR2;
                for (int h = 0; h < Math.Min(hops.Count, RouteDecoder.HopSlotBytesR2); h++)
                {
                    data[hopOffset + h] = hops[h];
                }
            }
        }
        return data;
    }

    /// <summary>
    /// Creates a synthetic 704-byte Runaway 1 perspective walk corridor table.
    /// </summary>
    public static byte[] CorridorTable(IReadOnlyList<(int Width, int Y, int X)>? spans = null)
    {
        var data = new byte[CorridorSpanDecoder.TotalSize];
        ushort count = (ushort)(spans?.Count ?? CorridorSpanDecoder.ExpectedSpans);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0, 2), count);

        if (spans is not null)
        {
            for (int i = 0; i < Math.Min(spans.Count, (CorridorSpanDecoder.TotalSize - 2) / 6); i++)
            {
                int off = 2 + i * 6;
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(off, 2), (ushort)spans[i].Width);
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(off + 2, 2), (ushort)spans[i].Y);
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(off + 4, 2), (ushort)spans[i].X);
            }
        }
        return data;
    }

    /// <summary>
    /// Creates a synthetic 1,792-byte Hollywood Monsters 7-page attribute lookup table.
    /// </summary>
    public static byte[] HollywoodAttributeTable(
        byte[]? walk = null,
        byte[]? items = null,
        byte[]? depth = null,
        byte[]? delta = null,
        byte[]? adjust = null,
        byte[]? footstep = null,
        byte[]? remap = null)
    {
        var data = new byte[HollywoodAttributeTableDecoder.TotalBytes];
        walk?.CopyTo(data.AsSpan(HollywoodAttributeTableDecoder.PageWalkRegion));
        items?.CopyTo(data.AsSpan(HollywoodAttributeTableDecoder.PageSceneItem));
        depth?.CopyTo(data.AsSpan(HollywoodAttributeTableDecoder.PageActorDepthClass));
        delta?.CopyTo(data.AsSpan(HollywoodAttributeTableDecoder.PagePaletteDeltaClass));
        adjust?.CopyTo(data.AsSpan(HollywoodAttributeTableDecoder.PagePaletteAdjustmentClass));
        footstep?.CopyTo(data.AsSpan(HollywoodAttributeTableDecoder.PageFootstepSoundMap));
        remap?.CopyTo(data.AsSpan(HollywoodAttributeTableDecoder.PagePresentationPaletteRemap));
        return data;
    }

    /// <summary>
    /// Creates a synthetic Hollywood Monsters scene metadata and interaction table (Entry 4, 14,678 bytes).
    /// </summary>
    public static byte[] HollywoodSceneMetadata(
        IReadOnlyList<(short InterX, short InterY, short AppX, short AppY, byte Facing, ushort ActionHandlerId)>? items = null)
    {
        var data = new byte[Formats.HollywoodSceneMetadata.MinSize];

        if (items is not null)
        {
            for (int i = 0; i < Math.Min(items.Count, Formats.HollywoodSceneMetadata.SceneItemCount); i++)
            {
                var item = items[i];
                int interOff = Formats.HollywoodSceneMetadata.OffsetSceneItemInteractionPoints + i * 4;
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(interOff, 2), item.InterX);
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(interOff + 2, 2), item.InterY);

                int appOff = Formats.HollywoodSceneMetadata.OffsetSceneItemApproachPoints + i * 4;
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(appOff, 2), item.AppX);
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(appOff + 2, 2), item.AppY);

                data[Formats.HollywoodSceneMetadata.OffsetSceneItemFacing + i] = item.Facing;

                int verbOff = Formats.HollywoodSceneMetadata.OffsetSceneVerbActionRecords + (i * Formats.HollywoodSceneMetadata.VerbCount) * 4;
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(verbOff, 2), item.ActionHandlerId);
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(verbOff + 2, 2), 1); // MovementMode = 1
            }
        }

        return data;
    }

    /// <summary>
    /// Creates a valid synthetic RAD Game Tools Smacker video header (<c>.SMK</c>, 104 bytes).
    /// </summary>
    public static byte[] SyntheticSmackerVideo(
        int width = 640,
        int height = 480,
        int frames = 30,
        int fps = 15,
        bool smk4 = true,
        int audioSampleRate = 22050,
        bool stereo = false,
        bool is16Bit = true)
    {
        var data = new byte[SmackerVideo.HeaderSize];
        byte[] sig = Encoding.ASCII.GetBytes(smk4 ? "SMK4" : "SMK2");
        sig.CopyTo(data, 0);

        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4, 4), (uint)width);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8, 4), (uint)height);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(12, 4), (uint)frames);

        int rawRate = fps > 0 ? 1000 / fps : 66;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(16, 4), rawRate);

        // Track 0 bit is 1 << 1 (bit 1)
        uint flags = 1u << 1;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(20, 4), flags);

        // Track 0 audio rate
        uint rateFlags = (uint)(audioSampleRate & 0x00FFFFFF);
        if (is16Bit) rateFlags |= 0x04000000;
        if (stereo) rateFlags |= 0x08000000;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(72, 4), rateFlags);

        return data;
    }

    /// <summary>
    /// Creates a synthetic Runaway 3 chapter interaction database (<c>RESOURCE.005</c>).
    /// </summary>
    public static byte[] SyntheticInteractionDatabase(IReadOnlyList<(int ChapterId, int SceneId, int ObjectId, int ActionId, string Label)> records)
    {
        var chapters = records.GroupBy(r => r.ChapterId).OrderBy(g => g.Key).ToList();
        uint totalRecords = (uint)records.Count;
        uint chapterCount = (uint)chapters.Count;

        long headerSize = 8 + chapterCount * 12;
        long totalSize = headerSize + totalRecords * ChapterInteractionArchive.RecordSize;
        var data = new byte[totalSize];

        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0, 4), totalRecords);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4, 4), chapterCount);

        long currentOffset = headerSize;
        for (int i = 0; i < chapters.Count; i++)
        {
            var chGroup = chapters[i];
            int chOffset = 8 + i * 12;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(chOffset, 4), (uint)chGroup.Key);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(chOffset + 4, 4), (uint)chGroup.Count());
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(chOffset + 8, 4), (uint)currentOffset);
            currentOffset += chGroup.Count() * ChapterInteractionArchive.RecordSize;
        }

        int recIndex = 0;
        foreach (var chGroup in chapters)
        {
            int r = 0;
            foreach (var rec in chGroup)
            {
                int p = (int)(headerSize + recIndex * ChapterInteractionArchive.RecordSize);
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(p, 4), (uint)rec.ChapterId);
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(p + 4, 4), (uint)rec.SceneId);
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(p + 8, 4), (uint)rec.ObjectId);
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(p + 12, 4), (uint)rec.ActionId);

                // Encode payload with XOR cipher
                byte[] plain = new byte[ChapterInteractionArchive.RecordSize - 16];
                byte[] strBytes = Encoding.GetEncoding(1252).GetBytes(rec.Label);
                strBytes.CopyTo(plain, 0);

                // Encrypt plain
                byte[] raw = new byte[plain.Length];
                raw[0] = (byte)(plain[0] ^ (r & 0xFF));
                for (int j = 1; j < raw.Length; j++)
                {
                    raw[j] = (byte)(plain[j] ^ raw[j - 1]);
                }
                raw.CopyTo(data.AsSpan(p + 16));

                recIndex++;
                r++;
            }
        }

        return data;
    }
}


