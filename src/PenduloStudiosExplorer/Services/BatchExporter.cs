using System.IO;
using PenduloStudiosExplorer.Core.FileSystem;
using PenduloStudiosExplorer.Core.Formats;

namespace PenduloStudiosExplorer.Services;

/// <summary>Progress event fired after each file has been considered (exported, skipped, or failed).</summary>
public sealed record BatchExportProgress(string RelativePath, BatchExportResult Result, int Index, int Total);

/// <summary>Per-file outcome from a batch export walk.</summary>
public enum BatchExportResult { Exported, SkippedUnsupported, Failed }

/// <summary>Summary of a completed batch export walk.</summary>
public sealed record BatchExportSummary(int ExportedCount, int SkippedCount, int FailedCount, IReadOnlyList<string> FailedPaths);

/// <summary>What the batch walk writes for each kind of entry.</summary>
/// <param name="AnimationFps">Frame rate stamped into the APNGs. The files store none.</param>
/// <param name="VoiceSampleRate">Sample rate assumed for voice lines.</param>
/// <param name="AnimationFrames">Also write one PNG per frame, in a folder next to the APNG.</param>
public sealed record BatchExportOptions(double AnimationFps, int VoiceSampleRate, bool AnimationFrames = false);

/// <summary>
/// Walks a subtree of the virtual file system and writes every entry it knows how to convert,
/// preserving the tree's folder hierarchy under an output root:
/// <list type="bullet">
///   <item><description>backgrounds and overlays → <c>.png</c> (overlays carry <c>_at_X_Y</c>, their screen position);</description></item>
///   <item><description>animations → <c>.png</c> APNG on the union bounding box, plus optionally <c>&lt;name&gt;_frames/frame_NNNN_xX_yY.png</c>;</description></item>
///   <item><description>music, ambient, cinematic and voice → <c>.wav</c>;</description></item>
///   <item><description>videos → restored <c>.bik</c>;</description></item>
///   <item><description>lip-sync tracks → <c>.txt</c>, one value per line;</description></item>
///   <item><description>everything else is skipped.</description></item>
/// </list>
/// A <c>manifest.json</c>-style summary is not written; the file names carry the positions instead.
/// </summary>
public static class BatchExporter
{
    public static BatchExportSummary ExportSubtree(
        FsNode root,
        VirtualFileSystem vfs,
        string outputDirectory,
        BatchExportOptions options,
        Action<BatchExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(vfs);
        ArgumentException.ThrowIfNullOrEmpty(outputDirectory);
        ArgumentNullException.ThrowIfNull(options);

        Directory.CreateDirectory(outputDirectory);

        int exported = 0, skipped = 0, failed = 0;
        var failedPaths = new List<string>();
        string rootPath = root.GetPath();

        List<FsNode> files = root.IsFile ? [root] : vfs.EnumerateFiles(root).ToList();
        int total = files.Count;
        int index = 0;

        foreach (FsNode file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            index++;
            string relativePath = RelativePath(rootPath, file.GetPath());
            int lastSlash = relativePath.LastIndexOf('\\');
            string relativeDir = lastSlash >= 0 ? relativePath[..lastSlash] : string.Empty;
            string destinationDir = Path.Combine(outputDirectory, SanitizeRelativePath(relativeDir));

            BatchExportResult result;
            try
            {
                Directory.CreateDirectory(destinationDir);
                result = ExportOne(file, vfs, destinationDir, options);
            }
            catch (Exception ex)
            {
                Log.Exception($"Batch export '{file.GetPath()}'", ex);
                result = BatchExportResult.Failed;
            }

            switch (result)
            {
                case BatchExportResult.Exported: exported++; break;
                case BatchExportResult.SkippedUnsupported: skipped++; break;
                case BatchExportResult.Failed: failed++; failedPaths.Add(relativePath); break;
            }

            progress?.Invoke(new BatchExportProgress(relativePath, result, index, total));
        }

        return new BatchExportSummary(exported, skipped, failed, failedPaths);
    }

    /// <summary>The file name (without directory) an entry exports as, so single-item export and batch export agree.</summary>
    public static string ExportFileName(FsNode node)
    {
        ImageInfo? img = node.Image;
        return node.Kind switch
        {
            EntryKind.Background when img is not null => $"{node.Name}_{img.Width}x{img.Height}{(img.IsMask ? "_mask" : "")}.png",
            EntryKind.Mask when img is not null => $"{node.Name}_{img.Width}x{img.Height}_mask.png",
            EntryKind.Overlay when img is not null => $"{node.Name}_{img.Width}x{img.Height}_at_{img.X}_{img.Y}.png",
            EntryKind.Animation or EntryKind.CharacterAnimation or EntryKind.ActorAnimation => $"{node.Name}.png",
            EntryKind.Font => $"{node.Name}.png",
            EntryKind.Music or EntryKind.Ambient or EntryKind.Cinematic or EntryKind.Voice =>
                node.Audio?.Format == AudioFormat.Mp3 ? $"{node.Name}.mp3" : $"{node.Name}.wav",
            EntryKind.Video => Path.GetExtension(node.Name).Equals(".smk", StringComparison.OrdinalIgnoreCase)
                ? node.Name
                : $"{Path.GetFileNameWithoutExtension(node.Name)}_{Path.GetExtension(node.Name).TrimStart('.')}.bik",
            EntryKind.Viseme or EntryKind.Dialogue or EntryKind.SceneTable => $"{node.Name}.txt",
            EntryKind.Savegame => $"{node.Name}.png",
            _ when node.ArchivePath is not null
                   && ActorArchive.IsActorArchive(Path.GetFileName(node.ArchivePath), node.GameVersion ?? GameVersion.Runaway1)
                   && ActorArchive.FromDataSlot(node.EntryIndex) is not null => $"{node.Name}.png",
            _ => $"{node.Name}.bin",
        };
    }

    private static BatchExportResult ExportOne(FsNode file, VirtualFileSystem vfs, string destinationDir, BatchExportOptions options)
    {
        string name = SanitizeSegment(ExportFileName(file));
        string path = Path.Combine(destinationDir, name);
        bool indexed = vfs.GameVersion == GameVersion.HollywoodMonsters;
        int bytesPerPixel = indexed ? 1 : 2;

        switch (file.Kind)
        {
            case EntryKind.Background:
            {
                byte[] data = vfs.ReadBytes(file);
                ImageInfo? info = file.Image;
                DecodedImage image;

                // The interface art of the later games is whole PNG, JPEG or BMP files, and Runaway 1
                // and 2 keep their cursors in a keyed raster; only after those does the entry have to be
                // a bare raster of the geometry the scan recorded.
                DecodedImage? container =
                    PngDecoder.IsPng(data) ? PngDecoder.Decode(data)
                    : JpegDecoder.IsJpeg(data) ? JpegDecoder.Decode(data)
                    : BmpDecoder.IsBmp(data) ? BmpDecoder.Decode(data)
                    : CursorAtlasDecoder.IsAtlas(data) ? CursorAtlasDecoder.DecodeAtlas(data)
                    : null;
                if (container is not null)
                {
                    PngWriter.Write(container, path);
                    return BatchExportResult.Exported;
                }

                if (indexed)
                {
                    RasterInfo geometry = info is not null && (long)info.Width * info.Height == data.Length
                        ? new RasterInfo(info.Width, info.Height, 0, false)
                        : RasterDecoder.DetectIndexed(data) ?? throw new InvalidDataException("not an indexed raster");
                    image = RasterDecoder.DecodeIndexed(data, geometry.Width, geometry.Height,
                        vfs.ScenePaletteFor(file) ?? IndexedPalette.Grayscale);
                }
                else
                {
                    image = info is not null && info.Width * info.Height * 2 == data.Length
                        ? RasterDecoder.Decode(data, info.Width, info.Height)
                        : RasterDecoder.TryDecode(data, vfs.GameVersion)?.Image ?? throw new InvalidDataException("not a raster");
                }
                PngWriter.Write(image, path);
                return BatchExportResult.Exported;
            }

            case EntryKind.Mask:
            {
                byte[] data = vfs.ReadBytes(file);
                ImageInfo? info = file.Image;
                int? sceneW = info?.Width;
                int? sceneH = info?.Height;
                if (sceneW is null)
                {
                    var bg = vfs.SceneBackgroundFor(file);
                    if (bg is not null)
                    {
                        sceneW = bg.Width;
                        sceneH = bg.Height;
                    }
                }
                DecodedImage image;
                if (DdsDecoder.IsDds(data))
                {
                    image = DdsDecoder.Decode(data, colorSeed: file.EntryIndex);
                }
                else if (SparseMaskDecoder.Detect(data, sceneW, sceneH) is { } sm)
                {
                    int w = sceneW ?? sm.Width;
                    int h = sceneH ?? sm.Height;
                    image = SparseMaskDecoder.Decode(data, w, h, colorSeed: file.EntryIndex);
                }
                else if (SpanMaskDecoder.IsSpanMask(data))
                {
                    image = SpanMaskDecoder.Decode(data);
                }
                else
                {
                    image = info is not null
                        ? RleMaskDecoder.Decode(data, info.Width, info.Height)
                        : RleMaskDecoder.TryDecode(data, idMap: null, sceneW, sceneH)?.Image ?? throw new InvalidDataException("not an RLE mask");
                }
                PngWriter.Write(image, path);
                return BatchExportResult.Exported;
            }

            case EntryKind.Overlay:
            {
                byte[] data = vfs.ReadBytes(file);
                if (DdsDecoder.IsDds(data))
                {
                    var ddsImg = DdsDecoder.Decode(data, colorSeed: file.EntryIndex);
                    PngWriter.Write(ddsImg, path);
                    return BatchExportResult.Exported;
                }
                if (SpriteAsset.Parse(data, bytesPerPixel) is { } sprite1)
                {
                    sprite1.Palette = vfs.ScenePaletteFor(file);
                    var frame = sprite1.DecodeFrame(0);
                    if (frame.Image is not null)
                        PngWriter.Write(frame.Image, path);
                    return BatchExportResult.Exported;
                }
                (DecodedImage image, _) = OverlayDecoder.TryDecode(data, bytesPerPixel, vfs.ScenePaletteFor(file)) ?? throw new InvalidDataException("not an overlay");
                PngWriter.Write(image, path);
                return BatchExportResult.Exported;
            }

            case EntryKind.Animation:
            {
                SpriteAsset asset = SpriteAsset.Parse(vfs.ReadBytes(file), bytesPerPixel) ?? throw new InvalidDataException("not a sprite");
                asset.Palette = vfs.ScenePaletteFor(file);
                ExportAnimation(asset, path, options.AnimationFps, options.AnimationFrames);
                return BatchExportResult.Exported;
            }

            case EntryKind.CharacterAnimation:
            case EntryKind.ActorAnimation:
            {
                int bpp = vfs.GameVersion == GameVersion.Runaway1 ? 1 : 2;
                CharacterSpriteAsset asset;
                if (file.Kind == EntryKind.ActorAnimation && file.ArchivePath is not null && ActorArchive.FromDataSlot(file.EntryIndex) is { } animation)
                {
                    byte[]? table = ActorArchive.ReadFrameTable(file.ArchivePath, animation);
                    if (table is null) throw new InvalidDataException("no actor frame table");
                    asset = CharacterSpriteAsset.Parse(table, animation.FrameCount, vfs.ReadBytes(file), animation.Geometry, bpp) ?? throw new InvalidDataException("not an actor animation");
                }
                else
                {
                    asset = CharacterSpriteAsset.Parse(vfs.ReadBytes(file), bpp) ?? throw new InvalidDataException("not a character animation");
                }
                if (bpp == 1)
                    asset.Palette = vfs.CharacterPaletteFor(file);
                ExportAnimation(asset, path, options.AnimationFps, options.AnimationFrames);
                return BatchExportResult.Exported;
            }

            case EntryKind.Music:
            case EntryKind.Ambient:
            case EntryKind.Cinematic:
            case EntryKind.Voice:
            {
                AudioInfo pcm = file.Audio ?? VirtualFileSystem.AmbientPcm;
                using Stream src = vfs.OpenFile(file);
                using FileStream dst = File.Create(path);
                if (pcm.Format == AudioFormat.RawPcm)
                {
                    int rate = file.Kind == EntryKind.Voice ? options.VoiceSampleRate : pcm.SampleRate;
                    dst.Write(WavWriter.Header((int)Math.Min(src.Length, int.MaxValue), rate, pcm.Channels, pcm.BitsPerSample));
                }
                src.CopyTo(dst);
                return BatchExportResult.Exported;
            }

            case EntryKind.Video:
            {
                using FileStream dst = File.Create(path);
                return vfs.RestoreVideo(file, dst) ? BatchExportResult.Exported : BatchExportResult.Failed;
            }

            case EntryKind.Viseme:
            {
                File.WriteAllText(path, VisemeArchive.ToText(vfs.ReadBytes(file)));
                return BatchExportResult.Exported;
            }

            case EntryKind.Dialogue:
            {
                File.WriteAllText(path, file.Subtitle ?? "");
                return BatchExportResult.Exported;
            }


            case EntryKind.Font:
            {
                byte[] data = vfs.ReadBytes(file);
                int recordSize = FontDecoder.RecordSizeFor(vfs.GameVersion);
                // Glyph table entries are small; export as text.
                if (data.Length % recordSize == 0 && data.Length <= 10_000)
                {
                    var glyphs = FontDecoder.ReadGlyphTable(data, recordSize);
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < glyphs.Length; i++)
                    {
                        var g = glyphs[i];
                        sb.AppendLine($"[{i}] offset={g.Offset} top={g.Top} height={g.Height} width={g.Width}");
                    }
                    string txtPath = Path.ChangeExtension(path, ".txt");
                    File.WriteAllText(txtPath, sb.ToString());
                    return BatchExportResult.Exported;
                }
                // Bitmap entry: find the table in the sibling slot.
                FsNode? tableNode = file.Parent?.Children.FirstOrDefault(c => c.EntryIndex == file.EntryIndex + 1 && c.Kind == EntryKind.Font);
                if (tableNode is null)
                    return BatchExportResult.Failed;
                byte[] tableData = vfs.ReadBytes(tableNode);
                if (!FontDecoder.IsGlyphTable(tableData, data.Length, recordSize))
                    return BatchExportResult.Failed;
                var glyphRecords = FontDecoder.ReadGlyphTable(tableData, recordSize);
                PngWriter.Write(FontDecoder.Decode(data, glyphRecords), path);
                return BatchExportResult.Exported;
            }

            case EntryKind.Savegame:
            {
                byte[] data = vfs.ReadBytes(file);
                if (data.Length >= SavegameArchive.ThumbnailBytes)
                {
                    DecodedImage thumb = SavegameArchive.DecodeThumbnail(data);
                    PngWriter.Write(thumb, path);
                    return BatchExportResult.Exported;
                }
                return BatchExportResult.Failed;
            }

            case EntryKind.SceneTable:
            {
                byte[] data = vfs.ReadBytes(file);
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Scene Table Entry: {file.Name}");
                if (file.Image is { } img)
                    sb.AppendLine($"Scene Geometry: {img.Width}×{img.Height} px");

                int width = data.Length >= 2 ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data) : 0;
                int height = data.Length >= 4 ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data[2..]) : 0;
                sb.AppendLine($"Dimensions: {width}×{height} px");
                sb.AppendLine($"Record Size: {data.Length} bytes");

                var cameraPoints = new List<(float X, float Y)>();
                for (int p = 20; p + 8 <= data.Length && p < 120; p += 8)
                {
                    float x = System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(p));
                    float y = System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(p + 4));
                    if (float.IsFinite(x) && float.IsFinite(y) && x >= 0 && x <= 4000 && y >= 0 && y <= 3000)
                        cameraPoints.Add((x, y));
                }
                if (cameraPoints.Count > 0)
                {
                    sb.AppendLine($"\nCamera / Viewpoint Coordinates ({cameraPoints.Count}):");
                    for (int i = 0; i < cameraPoints.Count; i++)
                        sb.AppendLine($"  Point {i + 1}: ({cameraPoints[i].X:F1}, {cameraPoints[i].Y:F1})");
                }

                File.WriteAllText(path, sb.ToString());
                return BatchExportResult.Exported;
            }

            default:
                if (file.ArchivePath is not null
                    && ActorArchive.IsActorArchive(Path.GetFileName(file.ArchivePath), vfs.GameVersion)
                    && ActorArchive.FromDataSlot(file.EntryIndex) is { } actorAnim)
                {
                    byte[]? table = ActorArchive.ReadFrameTable(file.ArchivePath, actorAnim);
                    if (table is null) return BatchExportResult.Failed;
                    CharacterSpriteAsset? asset = CharacterSpriteAsset.Parse(
                        table, actorAnim.FrameCount, vfs.ReadBytes(file), actorAnim.Geometry, 1);
                    if (asset is null) return BatchExportResult.Failed;
                    asset.Palette = vfs.CharacterPaletteFor(file);
                    ExportAnimation(asset, path, options.AnimationFps, options.AnimationFrames);
                    return BatchExportResult.Exported;
                }
                return BatchExportResult.SkippedUnsupported;
        }
    }

    /// <summary>
    /// Writes an animation as an APNG at <paramref name="apngPath"/> and, when asked, every frame as a
    /// cropped PNG named with its screen position in <c>&lt;stem&gt;_frames/</c> next to it. Empty frames
    /// get no file; the APNG keeps a 1×1 placeholder so numbering stays 1:1.
    /// </summary>
    public static (int Frames, int FramesWritten) ExportAnimation(IAnimationAsset asset, string apngPath, double fps, bool frames = false)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ApngWriter.Write(asset, apngPath, fps);

        int written = 0;
        if (frames)
        {
            string dir = Path.Combine(Path.GetDirectoryName(apngPath) ?? ".", Path.GetFileNameWithoutExtension(apngPath) + "_frames");
            written = ExportImageSequence(asset, dir);
        }
        return (asset.FrameCount, written);
    }

    /// <summary>Exports each frame of the animation as an individual PNG into <paramref name="dir"/>.</summary>
    public static int ExportImageSequence(IAnimationAsset asset, string dir, string prefix = "frame")
    {
        ArgumentNullException.ThrowIfNull(asset);
        Directory.CreateDirectory(dir);
        int written = 0;
        for (int i = 0; i < asset.FrameCount; i++)
        {
            SpriteFrame frame = asset.DecodeFrame(i);
            if (frame.Image is null)
                continue;
            PngWriter.Write(frame.Image, Path.Combine(dir, $"{prefix}_{i:0000}_x{frame.X}_y{frame.Y}.png"));
            written++;
        }
        return written;
    }

    /// <summary>Exports the animation as a packed sprite sheet PNG and accompanying JSON atlas.</summary>
    public static (int Frames, string PngPath, string JsonPath) ExportSpriteSheet(IAnimationAsset asset, string pngPath, double fps)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrEmpty(pngPath);
        SpriteSheetWriter.Write(asset, pngPath, fps);
        string jsonPath = Path.ChangeExtension(pngPath, ".json");
        return (asset.FrameCount, pngPath, jsonPath);
    }

    private static string RelativePath(string rootPath, string fullPath)
    {
        if (fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            string rel = fullPath[rootPath.Length..].TrimStart('\\');
            if (rel.Length > 0)
                return rel;
        }
        int lastSlash = fullPath.LastIndexOf('\\');
        return lastSlash >= 0 ? fullPath[(lastSlash + 1)..] : fullPath;
    }

    private static string SanitizeRelativePath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return string.Empty;
        return string.Join(Path.DirectorySeparatorChar, relativePath.Split('\\', StringSplitOptions.RemoveEmptyEntries).Select(SanitizeSegment));
    }

    public static string SanitizeSegment(string segment)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var chars = segment.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        }
        string result = new string(chars).Trim();
        return result.Length == 0 ? "_" : result;
    }
}
