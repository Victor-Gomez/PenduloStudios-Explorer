using System.Text.Json;

namespace RunawayExplorer.Core.Formats;

/// <summary>
/// Packs an <see cref="IAnimationAsset"/> into a single sprite sheet image and accompanying JSON atlas.
/// Each cell in the grid has the size of <see cref="IAnimationAsset.Bounds"/> with frames aligned to their
/// native relative screen position, ensuring exact pixel registration.
/// </summary>
public static class SpriteSheetWriter
{
    public sealed record FrameMeta(
        int Index,
        int X,
        int Y,
        int Width,
        int Height,
        int OffsetX,
        int OffsetY,
        int FrameWidth,
        int FrameHeight);

    public sealed record SheetMeta(
        int FrameCount,
        int Columns,
        int Rows,
        int CellWidth,
        int CellHeight,
        int SheetWidth,
        int SheetHeight,
        double Fps,
        int BoundsX,
        int BoundsY,
        int BoundsWidth,
        int BoundsHeight);

    public sealed record SheetManifest(
        List<FrameMeta> Frames,
        SheetMeta Meta);

    /// <summary>
    /// Renders all frames of <paramref name="asset"/> into a packed grid <see cref="DecodedImage"/>.
    /// </summary>
    public static (DecodedImage Image, SheetManifest Manifest) BuildSheet(IAnimationAsset asset, double fps = 15.0)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.FrameCount <= 0)
            throw new ArgumentException("Asset must contain at least one frame.", nameof(asset));

        (int bx, int by, int bw, int bh) = asset.Bounds;
        if (bw <= 0 || bh <= 0)
        {
            bw = Math.Max(1, bw);
            bh = Math.Max(1, bh);
        }

        int count = asset.FrameCount;
        int columns = (int)Math.Ceiling(Math.Sqrt(count));
        int rows = (int)Math.Ceiling((double)count / columns);
        int sheetWidth = columns * bw;
        int sheetHeight = rows * bh;

        var sheet = DecodedImage.Transparent(sheetWidth, sheetHeight);
        var frameMetas = new List<FrameMeta>(count);

        for (int i = 0; i < count; i++)
        {
            SpriteFrame frame = asset.DecodeFrame(i);
            int col = i % columns;
            int row = i / columns;
            int cellX = col * bw;
            int cellY = row * bh;

            int ox = 0, oy = 0, fw = 0, fh = 0;
            if (frame.Image is not null)
            {
                ox = frame.X - bx;
                oy = frame.Y - by;
                fw = frame.Image.Width;
                fh = frame.Image.Height;
                sheet.Blit(frame.Image, cellX + ox, cellY + oy);
            }

            frameMetas.Add(new FrameMeta(
                Index: i,
                X: cellX,
                Y: cellY,
                Width: bw,
                Height: bh,
                OffsetX: ox,
                OffsetY: oy,
                FrameWidth: fw,
                FrameHeight: fh));
        }

        var meta = new SheetMeta(
            FrameCount: count,
            Columns: columns,
            Rows: rows,
            CellWidth: bw,
            CellHeight: bh,
            SheetWidth: sheetWidth,
            SheetHeight: sheetHeight,
            Fps: fps,
            BoundsX: bx,
            BoundsY: by,
            BoundsWidth: bw,
            BoundsHeight: bh);

        return (sheet, new SheetManifest(frameMetas, meta));
    }

    /// <summary>
    /// Encodes the animation asset into a sprite sheet PNG byte array.
    /// </summary>
    public static byte[] WritePng(IAnimationAsset asset, double fps = 15.0)
    {
        var (sheet, _) = BuildSheet(asset, fps);
        return PngWriter.ToBytes(sheet);
    }

    /// <summary>
    /// Generates the JSON atlas manifest describing the sprite sheet frames.
    /// </summary>
    public static string WriteJson(IAnimationAsset asset, double fps = 15.0)
    {
        var (_, manifest) = BuildSheet(asset, fps);
        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Writes both the sprite sheet PNG and its JSON atlas file to disk.
    /// </summary>
    public static void Write(IAnimationAsset asset, string pngPath, double fps = 15.0) =>
        Write(asset, pngPath, Path.ChangeExtension(pngPath, ".json"), fps);

    /// <summary>
    /// Writes both the sprite sheet PNG and its JSON atlas file to disk.
    /// </summary>
    public static void Write(IAnimationAsset asset, string pngPath, string jsonPath, double fps = 15.0)
    {
        var (sheet, manifest) = BuildSheet(asset, fps);
        PngWriter.Write(sheet, pngPath);
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }
}
