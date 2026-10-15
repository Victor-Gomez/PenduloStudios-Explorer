using System.IO;
using System.Text;
using RunawayExplorer.Core.FileSystem;
using RunawayExplorer.Core.Formats;
using RunawayExplorer.Core.Settings;

namespace RunawayExplorer.Services;

/// <summary>
/// Turns a tree node into something a viewer can show. Dispatches on <see cref="FsNode.Kind"/> -- the
/// classification made at scan time -- rather than re-probing the bytes, so what the tree says an entry
/// is and what the viewer shows can't disagree. Every decode failure becomes an <see cref="ErrorResource"/>
/// rather than an exception, so one bad entry never takes the window down.
/// </summary>
public static class ResourceLoader
{
    public static ResourceContent Load(FsNode node, VirtualFileSystem vfs, AppSettings settings, TempFileTracker tempFiles)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(vfs);

        bool indexed = vfs.GameVersion == GameVersion.HollywoodMonsters;
        int bytesPerPixel = indexed ? 1 : 2;

        if (node.IsDirectory)
        {
            if (node.ArchivePath is not null && Path.GetFileName(node.ArchivePath).Equals("RESOURCE.005", StringComparison.OrdinalIgnoreCase))
            {
                byte[] dbBytes = File.ReadAllBytes(node.ArchivePath);
                if (ChapterInteractionArchive.IsInteractionDatabase(dbBytes))
                {
                    var db = ChapterInteractionArchive.Parse(dbBytes);
                    if (node.Name.StartsWith("Scene ", StringComparison.OrdinalIgnoreCase) && int.TryParse(node.Name.AsSpan(6), out int sceneId))
                    {
                        int chapterId = node.Parent is { } parent && parent.Name.StartsWith("Chapter ", StringComparison.OrdinalIgnoreCase) && int.TryParse(parent.Name.AsSpan(8), out int ch) ? ch : 0;
                        string graph = db.FormatSceneGraph(chapterId, sceneId);
                        string mermaid = db.GenerateMermaid(chapterId, sceneId);
                        return new TextResource($"{graph}\nMermaid Flowchart:\n```mermaid\n{mermaid}\n```\n");
                    }
                    if (node.Name.StartsWith("Chapter ", StringComparison.OrdinalIgnoreCase) && int.TryParse(node.Name.AsSpan(8), out int chapterNum))
                    {
                        var recs = db.GetRecordsForChapter(chapterNum).ToList();
                        var sb = new StringBuilder();
                        sb.AppendLine($"=== Runaway 3 — Chapter {chapterNum} Interaction Directory ===");
                        sb.AppendLine($"Total Records: {recs.Count:N0}");
                        var scenes = db.GetScenesForChapter(chapterNum);
                        sb.AppendLine($"Scenes ({scenes.Count}): {string.Join(", ", scenes.Select(s => $"Scene {s:D3}"))}");
                        sb.AppendLine();
                        sb.AppendLine("Scene Summary:");
                        sb.AppendLine("Scene ID | Triggers | Objects | Sample Triggers");
                        sb.AppendLine("---------+----------+---------+--------------------------------");
                        foreach (int sc in scenes)
                        {
                            var sRecs = db.GetRecordsForScene(chapterNum, sc);
                            int objs = sRecs.Select(r => r.ObjectId).Distinct().Count();
                            var samples = sRecs.Select(r => r.DecodedLabel).Where(l => !string.IsNullOrWhiteSpace(l)).Take(3);
                            string sampleText = string.Join(", ", samples);
                            if (sampleText.Length > 40) sampleText = sampleText[..37] + "...";
                            sb.AppendLine($"Scene {sc,3:D3} |  {sRecs.Count,5}   |   {objs,3}   | {sampleText}");
                        }
                        return new TextResource(sb.ToString());
                    }
                    return new TextResource(ChapterInteractionArchive.FormatSummary(db));
                }
            }
        }

        try
        {
            switch (node.Kind)
            {
                case EntryKind.Background:
                {
                    byte[] data = vfs.ReadBytes(node);
                    if (indexed)
                    {
                        ImageInfo? bgInfo = node.Image;
                        if (bgInfo is null || (long)bgInfo.Width * bgInfo.Height != data.Length)
                        {
                            if (RasterDecoder.DetectIndexed(data) is not { } detected)
                                return new ErrorResource($"'{node.GetPath()}' no longer decodes as an indexed raster.");
                            bgInfo = new ImageInfo { Width = detected.Width, Height = detected.Height };
                        }
                        IndexedPalette bgPalette = vfs.ScenePaletteFor(node) ?? IndexedPalette.Grayscale;
                        return new ImageResource(
                            RasterDecoder.DecodeIndexed(data, bgInfo.Width, bgInfo.Height, bgPalette),
                            0, 0, false, "background");
                    }
                    if (PngDecoder.IsPng(data))
                    {
                        if (PngDecoder.Decode(data) is { } pngImg)
                            return new ImageResource(pngImg, 0, 0, false, "background");
                        return new ErrorResource($"'{node.GetPath()}' failed to decode as PNG.");
                    }
                    if (JpegDecoder.IsJpeg(data))
                    {
                        if (JpegDecoder.Decode(data) is { } jpgImg)
                            return new ImageResource(jpgImg, 0, 0, false, "background");
                        return new ErrorResource($"'{node.GetPath()}' failed to decode as JPEG.");
                    }
                    if (BmpDecoder.IsBmp(data))
                    {
                        if (BmpDecoder.Decode(data) is { } bmpImg)
                            return new ImageResource(bmpImg, 0, 0, false, "background");
                        return new ErrorResource($"'{node.GetPath()}' failed to decode as BMP.");
                    }
                    if (CursorAtlasDecoder.IsAtlas(data))
                    {
                        return new ImageResource(CursorAtlasDecoder.DecodeAtlas(data), 0, 0, false, "cursor atlas");
                    }
                    ImageInfo? info = node.Image;
                    if (info is null || info.Width * info.Height * 2 != data.Length)
                    {
                        // The scan said raster but the geometry is missing or stale: detect again.
                        if (RasterDecoder.TryDecode(data, vfs.GameVersion) is not { } re)
                            return new ErrorResource($"'{node.GetPath()}' no longer decodes as a raster.");
                        return new ImageResource(re.Image, 0, 0, false, re.Info.IsMask ? "mask layer" : "background");
                    }
                    DecodedImage image = RasterDecoder.Decode(data, info.Width, info.Height);
                    return new ImageResource(image, 0, 0, false, info.IsMask ? "mask layer" : "background");
                }

                case EntryKind.Mask:
                {
                    byte[] data = vfs.ReadBytes(node);
                    if (PngDecoder.IsPng(data))
                    {
                        if (PngDecoder.Decode(data) is { } pngImg)
                            return new ImageResource(pngImg, node.Image?.X ?? 0, node.Image?.Y ?? 0, true, "mask layer");
                        return new ErrorResource($"'{node.GetPath()}' failed to decode as PNG mask.");
                    }
                    byte[]? table1536 = vfs.SceneAttributeTableFor(node);
                    byte[]? idMap = table1536 is not null ? RleMaskDecoder.ExtractObjectMapping(table1536) : null;
                    ImageInfo? info = node.Image;

                    int? sceneW = info?.Width;
                    int? sceneH = info?.Height;
                    if (sceneW is null)
                    {
                        var bg = vfs.SceneBackgroundFor(node);
                        if (bg is not null)
                        {
                            sceneW = bg.Width;
                            sceneH = bg.Height;
                        }
                    }

                    if (SparseMaskDecoder.Detect(data, sceneW, sceneH) is { } smInfo)
                    {
                        int w = sceneW ?? smInfo.Width;
                        int h = sceneH ?? smInfo.Height;
                        DecodedImage img = SparseMaskDecoder.Decode(data, w, h, idMap, colorSeed: node.EntryIndex);
                        return new ImageResource(img, 0, 0, true, "scene mask");
                    }

                    if (SpanMaskDecoder.IsSpanMask(data))
                    {
                        DecodedImage img = SpanMaskDecoder.Decode(data);
                        return new ImageResource(img, info?.X ?? 0, info?.Y ?? 0, true, "polygon mask");
                    }

                    if (info is null)
                    {
                        if (RleMaskDecoder.TryDecode(data, idMap, sceneW, sceneH, table1536) is not { } md)
                            return new ErrorResource($"'{node.GetPath()}' no longer parses as an RLE scene mask.");
                        return new ImageResource(md.Image, 0, 0, true, "scene mask");
                    }
                    DecodedImage image = RleMaskDecoder.Decode(data, info.Width, info.Height, idMap, table1536);
                    return new ImageResource(image, 0, 0, true, "scene mask");
                }

                case EntryKind.Overlay:
                {
                    byte[] data = vfs.ReadBytes(node);
                    if (PngDecoder.IsPng(data))
                    {
                        if (PngDecoder.Decode(data) is { } pngImg)
                        {
                            bool hasBg = vfs.SceneBackgroundFor(node) is not null;
                            int x = node.Image?.X ?? 0;
                            int y = node.Image?.Y ?? 0;
                            bool positioned = hasBg || x != 0 || y != 0;
                            string kind = positioned ? "overlay" : "sprite";
                            return new ImageResource(pngImg, x, y, positioned, kind);
                        }
                        return new ErrorResource($"'{node.GetPath()}' failed to decode as PNG.");
                    }
                    if (JpegDecoder.IsJpeg(data))
                    {
                        if (JpegDecoder.Decode(data) is { } jpgImg)
                            return new ImageResource(jpgImg, node.Image?.X ?? 0, node.Image?.Y ?? 0, true, "overlay");
                        return new ErrorResource($"'{node.GetPath()}' failed to decode as JPEG.");
                    }
                    if (SpriteAsset.Parse(data, bytesPerPixel) is { } sprite1)
                    {
                        sprite1.Palette = vfs.ScenePaletteFor(node);
                        var frame = sprite1.DecodeFrame(0);
                        if (frame.Image is null)
                            return new ErrorResource($"'{node.GetPath()}' has empty overlay frame.");
                        return new ImageResource(frame.Image, frame.X, frame.Y, true, "overlay");
                    }
                    if (OverlayDecoder.TryDecode(data, bytesPerPixel, vfs.ScenePaletteFor(node)) is not { } ov)
                        return new ErrorResource($"'{node.GetPath()}' no longer parses as an overlay.");
                    return new ImageResource(ov.Image, ov.Info.X, ov.Info.Y, true, ov.Info.IsRectangular ? "overlay (rectangular)" : "overlay");
                }

                case EntryKind.Animation:
                {
                    byte[] data = vfs.ReadBytes(node);
                    if (vfs.GameVersion == GameVersion.HollywoodMonsters && data.Length == HollywoodItemIconsAsset.TotalBytes)
                    {
                        var icons = new HollywoodItemIconsAsset(data);
                        IndexedPalette? sharedPal = vfs.SharedPalette(80);
                        icons.Palette = sharedPal is not null ? IndexedPalette.Grayscale.WithTail(sharedPal) : vfs.ScenePaletteFor(node);
                        return new AnimationResource(icons);
                    }
                    SpriteAsset? asset = SpriteAsset.Parse(data, bytesPerPixel);
                    if (asset is null)
                        return new ErrorResource($"'{node.GetPath()}' no longer parses as a sprite animation.");
                    asset.Palette = vfs.ScenePaletteFor(node);
                    return new AnimationResource(asset);
                }

                case EntryKind.CharacterAnimation:
                {
                    byte[] data = vfs.ReadBytes(node);
                    int bpp = vfs.GameVersion == GameVersion.Runaway1 ? 1 : 2;
                    CharacterSpriteAsset? asset = CharacterSpriteAsset.Parse(data, bpp);
                    if (asset is null)
                        return new ErrorResource($"'{node.GetPath()}' no longer parses as a character animation.");
                    if (bpp == 1)
                        asset.Palette = vfs.CharacterPaletteFor(node);
                    return new AnimationResource(asset);
                }

                case EntryKind.ActorAnimation:
                {
                    // The node carries the run slot; everything else about the animation follows from
                    // which slot that is, so the frame table is fetched rather than remembered.
                    if (node.ArchivePath is null || ActorArchive.FromDataSlot(node.EntryIndex) is not { } animation)
                        return new ErrorResource($"'{node.GetPath()}' is not a slot any actor group claims.");

                    byte[]? table = ActorArchive.ReadFrameTable(node.ArchivePath, animation);
                    if (table is null)
                        return new ErrorResource($"'{node.GetPath()}' has no frame table at slot {animation.TableSlot}.");

                    byte[] data = vfs.ReadBytes(node);
                    CharacterSpriteAsset? asset = CharacterSpriteAsset.Parse(
                        table, animation.FrameCount, data, animation.Geometry);
                    if (asset is null)
                        return new ErrorResource($"'{node.GetPath()}' no longer parses as an actor animation.");
                    asset.Palette = vfs.CharacterPaletteFor(node);
                    return new AnimationResource(asset);
                }

                case EntryKind.Music:
                case EntryKind.Ambient:
                case EntryKind.Cinematic:
                case EntryKind.Voice:
                {
                    AudioInfo pcm = node.Audio ?? VirtualFileSystem.AmbientPcm;
                    // The adjustable voice rate exists because Runaway's lines carry none. Hollywood Monsters'
                    // do not carry one either, but its rate is known (22,050 Hz 8-bit mono), so leave it alone.
                    if (node.Kind == EntryKind.Voice && pcm.Format == AudioFormat.RawPcm
                        && vfs.GameVersion != GameVersion.HollywoodMonsters)
                        pcm = new AudioInfo { Format = AudioFormat.RawPcm, SampleRate = settings.VoiceSampleRate, Channels = pcm.Channels, BitsPerSample = pcm.BitsPerSample };

                    if (pcm.Format == AudioFormat.Mp3)
                    {
                        string path = tempFiles.CreateTempFile(".mp3");
                        using (Stream src = vfs.OpenFile(node))
                        using (FileStream dst = File.Create(path))
                        {
                            src.CopyTo(dst);
                        }
                        return new SoundResource(path, pcm, pcm.DurationSeconds(node.Size));
                    }
                    else if (pcm.Format == AudioFormat.Wav)
                    {
                        string path = tempFiles.CreateTempFile(".wav");
                        using (Stream src = vfs.OpenFile(node))
                        using (FileStream dst = File.Create(path))
                        {
                            src.CopyTo(dst);
                        }
                        return new SoundResource(path, pcm, pcm.DurationSeconds(node.Size));
                    }
                    else
                    {
                        string path = tempFiles.CreateTempFile(".wav");
                        using (Stream src = vfs.OpenFile(node))
                        using (FileStream dst = File.Create(path))
                        {
                            dst.Write(WavWriter.Header((int)Math.Min(node.Size, int.MaxValue), pcm.SampleRate, pcm.Channels, pcm.BitsPerSample));
                            src.CopyTo(dst);
                        }
                        return new SoundResource(path, pcm, pcm.DurationSeconds(node.Size));
                    }
                }

                case EntryKind.Video:
                {
                    string ext = Path.GetExtension(node.Name).Equals(".smk", StringComparison.OrdinalIgnoreCase) ? ".smk" : ".bik";
                    string path = tempFiles.CreateTempFile(ext);
                    using (FileStream dst = File.Create(path))
                    {
                        if (!vfs.RestoreVideo(node, dst))
                            return new ErrorResource($"No header for '{node.Name}' in the DATAVC00 keyfile; the video cannot be restored.");
                    }
                    return new VideoResource(path, node.Subtitle);
                }

                case EntryKind.Viseme:
                {
                    byte[] data = vfs.ReadBytes(node);
                    var sb = new StringBuilder();
                    sb.Append("Lip-sync track ").Append(node.EntryIndex).Append(": ").Append(data.Length)
                      .Append(" tick(s), one mouth shape (0-5) per animation tick. Pairs with voice clip VOICE_")
                      .Append(node.EntryIndex.ToString("00000")).Append(".\n\n");
                    sb.Append(VisemeArchive.ToText(data));
                    return new TextResource(sb.ToString());
                }

                case EntryKind.Dialogue:
                {
                    var sb = new StringBuilder();
                    if (indexed || node.VoiceClipIndex >= 0 || node.Name.StartsWith("line ") || node.Name.StartsWith("label "))
                    {
                        // Script stores the pairing rather than implying it from the
                        // row number, and it names a recording for only some of its rows.
                        sb.Append("Script row ").Append(node.Name);
                        if (node.Parent is { } stage)
                            sb.Append(" of ").Append(stage.Name);
                        sb.Append(node.VoiceClipIndex >= 0
                            ? $"\nPairs with voice clip: {node.VoiceClipIndex:00000}\n\n"
                            : "\nNo voice clip is cued for this row.\n\n");
                    }
                    else
                    {
                        sb.Append("Dialogue phrase ").Append(node.Name);
                        if (node.EntryIndex >= 0)
                        {
                            sb.Append(" (index ").Append(node.EntryIndex)
                              .Append(")\nPairs with voice clip: VOICE_").Append(node.EntryIndex.ToString("00000")).Append("\n\n");
                        }
                        else
                        {
                            sb.Append("\n\n");
                        }
                    }

                    sb.Append(node.Subtitle ?? "(empty)");
                    return new TextResource(sb.ToString());
                }

                case EntryKind.Font:
                {
                    if (node.ArchivePath is not null && Path.GetFileName(node.ArchivePath).Equals("RESOURCE.FNT", StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] fntBytes = File.ReadAllBytes(node.ArchivePath);
                        var fonts = YesterdayFontArchive.ReadFonts(fntBytes);
                        var match = fonts.FirstOrDefault(f => f.Index == node.EntryIndex) ?? (fonts.Count > 0 ? fonts[0] : null);
                        if (match is not null)
                        {
                            DecodedImage img = FontDecoder.DecodeRgb24(match.BitmapRgb, match.Glyphs);
                            return new ImageResource(img, 0, 0, false, $"{match.Name}, {match.Glyphs.Length} glyphs (24-bit outlined)");
                        }
                    }

                    byte[] data = vfs.ReadBytes(node);
                    int recordSize = FontDecoder.RecordSizeFor(vfs.GameVersion);
                    // A glyph table entry (a whole number of records, small) → informational text.
                    if (data.Length % recordSize == 0 && data.Length <= 10_000)
                    {
                        var glyphs = FontDecoder.ReadGlyphTable(data, recordSize);
                        int lineHeight = 0;
                        foreach (var g in glyphs)
                            lineHeight = Math.Max(lineHeight, g.Top + g.Height);
                        var sb = new StringBuilder();
                        sb.Append($"Glyph table: {glyphs.Length} glyphs, line height {lineHeight}\n\n");
                        for (int i = 0; i < glyphs.Length; i++)
                        {
                            var g = glyphs[i];
                            sb.Append($"  [{i,3}]  offset {g.Offset,5}  top {g.Top,2}  {g.Width}×{g.Height}\n");
                        }
                        return new TextResource(sb.ToString());
                    }
                    // A glyph bitmap entry: find the neighbouring table to decode it.
                    // The table is the next slot; look for it in the tree.
                    byte[]? tableData = FindFontTableData(node, vfs);
                    if (tableData is not null && FontDecoder.IsGlyphTable(tableData, data.Length, recordSize))
                    {
                        var glyphs = FontDecoder.ReadGlyphTable(tableData, recordSize);
                        return new ImageResource(FontDecoder.Decode(data, glyphs), 0, 0, false, $"font, {glyphs.Length} glyphs");
                    }
                    return new ErrorResource($"'{node.GetPath()}' is tagged as a font but its glyph table was not found.");
                }

                case EntryKind.Savegame:
                {
                    byte[] data = vfs.ReadBytes(node);
                    if (data.Length >= SavegameArchive.ThumbnailBytes)
                    {
                        DecodedImage thumb = SavegameArchive.DecodeThumbnail(data);
                        int slot = SavegameArchive.GetSlotNumber(node.Name);
                        SavegameInfo info = SavegameArchive.ReadMetadata(data, slot, vfs.GameVersion);
                        string sceneDesc = info.SceneArchiveName is not null
                            ? $"{info.SceneArchiveName} — {RunawayExplorer.Core.Metadata.SceneCatalog.GetSceneTitle(info.SceneArchiveName, vfs.Language, vfs.GameVersion)}"
                            : $"Scene {info.SceneId}";

                        var sb = new StringBuilder();
                        sb.AppendLine($"Savegame Slot {slot:000}");
                        if (!string.IsNullOrEmpty(info.Name))
                            sb.AppendLine($"Name: {info.Name}");
                        if (!string.IsNullOrEmpty(info.Action))
                            sb.AppendLine($"Action: {info.Action}");
                        sb.AppendLine($"Scene: {sceneDesc}");
                        sb.AppendLine($"Actor Position: ({info.ActorX}, {info.ActorY})");
                        sb.AppendLine($"Scene Viewport: ({info.ViewX}, {info.ViewY})");
                        sb.AppendLine($"Inventory Count: {info.InventoryCount} items");
                        sb.AppendLine($"Thumbnail: 204×120 RGB565 (5x scale reduction)");

                        return new ImageResource(thumb, 0, 0, false, sb.ToString().TrimEnd());
                    }
                    return new ErrorResource($"Save file '{node.Name}' is truncated ({data.Length} bytes).");
                }

                case EntryKind.SceneTable:
                {
                    byte[] data = vfs.ReadBytes(node);
                    var sb = new StringBuilder();
                    sb.AppendLine($"Scene Table Entry: {node.Name}");
                    if (node.Image is { } img)
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

                    sb.AppendLine("\nRaw hex view:");
                    sb.Append(RawFormat.ToHexDump(data.AsSpan(0, Math.Min(data.Length, 256))));
                    return new TextResource(sb.ToString());
                }

                case EntryKind.Data:
                case EntryKind.GlobalData:
                case EntryKind.RawFile:
                default:
                {
                    if (node.Kind == EntryKind.Data && (node.Size == RouteDecoder.TotalSizeR1 || node.Size == RouteDecoder.TotalSizeR2))
                    {
                        byte[] routeData = vfs.ReadBytes(node);
                        if (RouteDecoder.IsRouteTable(routeData))
                        {
                            var matrix = RouteDecoder.Parse(routeData);
                            DecodedImage? bg = null;
                            try { bg = vfs.SceneBackgroundFor(node); } catch { }

                            DecodedImage? walkMask = null;
                            FsNode? archive = node.Parent;
                            if (archive is not null && bg is not null)
                            {
                                FsNode? maskChild = archive.Children.FirstOrDefault(c => c.Kind == EntryKind.Mask);
                                FsNode? attrChild = archive.Children.FirstOrDefault(c => c.Kind == EntryKind.Data && c.Size == 1536);
                                if (maskChild is not null)
                                {
                                    try
                                    {
                                        byte[] maskBytes = vfs.ReadBytes(maskChild);
                                        byte[]? attrBytes = attrChild is not null ? vfs.ReadBytes(attrChild) : null;
                                        byte[]? idMap = attrBytes is not null ? RleMaskDecoder.ExtractObjectMapping(attrBytes) : null;
                                        walkMask = RleMaskDecoder.Decode(maskBytes, bg.Width, bg.Height, idMap, attrBytes, MaskLayers.Walk, transparentBackground: true);
                                    }
                                    catch { }
                                }
                            }

                            return new RouteResource(matrix, bg, RouteDecoder.FormatSummary(matrix), node, walkMask);
                        }
                    }

                    using Stream s = vfs.OpenFile(node);
                    int take = (int)Math.Min(s.Length, RawFormat.MaxDumpBytes);
                    var head = new byte[take];
                    s.ReadExactly(head);
                    byte[]? fullData = node.Kind == EntryKind.Data && (node.Size == 1536 || node.Size == RouteDecoder.TotalSizeR1 || node.Size == RouteDecoder.TotalSizeR2 || node.Size == CorridorSpanDecoder.TotalSize || node.Size == ChapterInteractionArchive.RecordSize || (node.ArchivePath is not null && Path.GetFileName(node.ArchivePath).Equals("RESOURCE.005", StringComparison.OrdinalIgnoreCase))) ? vfs.ReadBytes(node) : null;
                    string header = DescribeRaw(node, s.Length, fullData, indexed);
                    return new TextResource(header + RawFormat.ToHexDump(head) +
                        (s.Length > take ? $"\n... {s.Length - take:N0} more byte(s) not shown.\n" : string.Empty));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Exception($"Load '{node.GetPath()}'", ex);
            return new ErrorResource($"Failed to load '{node.GetPath()}':\n{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>What a scene archive holds, for the folder view.</summary>
    public static SceneResource LoadScene(FsNode archive, VirtualFileSystem vfs)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(vfs);

        int backgrounds = 0, overlays = 0, animations = 0, frames = 0, data = 0;
        foreach (FsNode child in archive.Children)
        {
            switch (child.Kind)
            {
                case EntryKind.Background: backgrounds++; break;
                case EntryKind.Overlay: overlays++; break;
                case EntryKind.Animation: animations++; frames += child.Image?.Frames ?? 0; break;
                default: data++; break;
            }
        }

        DecodedImage? background = null;
        try
        {
            background = vfs.SceneBackgroundFor(archive);
        }
        catch (Exception ex)
        {
            Log.Exception($"Scene background for '{archive.GetPath()}'", ex);
        }

        string summary = $"{archive.Name}: {backgrounds} background(s), {overlays} overlay(s), {animations} animation(s) with {frames} frame(s), {data} data entr{(data == 1 ? "y" : "ies")}";
        return new SceneResource(background, summary, archive);
    }

    private static string DescribeRaw(FsNode node, long length, byte[]? fullData = null, bool indexed = false)
    {
        string what = node.Kind switch
        {
            EntryKind.Data when length == 1536 => "Scene-archive data entry: 1,536-byte Scene Attribute Table (6 parallel 256-byte lookup tables indexed by Mask ID 0..255).",
            EntryKind.Data when length == RouteDecoder.TotalSizeR1 => "Scene-archive route table: 43,659-byte Precomputed Walk Mesh (21×21 waypoint navigation matrix and zone transition paths).",
            EntryKind.Data when length == RouteDecoder.TotalSizeR2 => "Scene-archive navigation mesh: 335,529-byte Precomputed Walk Mesh (51×51 waypoint navigation matrix and zone transition paths).",
            EntryKind.Data when length == CorridorSpanDecoder.TotalSize => "Scene-archive walk corridor table: 704-byte Walkway Perspective Corridor Table (117 scanline trapezoid bounds from Y=483 to Y=599).",
            EntryKind.Data when length == RouteDecoder.TotalSizeTnbtWaypoints => "Scene-archive waypoint table: 194,560-byte 1080p Waypoint Navigation Table (320-byte route slots with 32-bit (X, Y) coordinate points).",
            EntryKind.Data when length == RouteDecoder.TotalSizeTnbtHops => "Scene-archive transition table: 589,824-byte 1080p Zone Hop Transition Table.",
            EntryKind.Data when length == ChapterInteractionArchive.RecordSize && (node.ArchivePath is not null && Path.GetFileName(node.ArchivePath).Equals("RESOURCE.005", StringComparison.OrdinalIgnoreCase)) =>
                "Runaway 3 interaction record: 220-byte object interaction and event trigger descriptor.",
            EntryKind.Data when indexed && IsPaletteSized(length) => "Scene-archive palette block: 6-bit VGA triples filling the bottom of the 256-colour table. Scenes that ship 176 colours take the top 80 from RESOURCE.000.",
            EntryKind.Data => "Scene-archive data entry. Not an image: one of the per-scene tables whose purpose is still open (see docs/formats).",
            EntryKind.GlobalData => "RESOURCE.000 entry: UI atlas, localised text bitmap, or other global data not yet decoded (see docs/formats/global-data.md).",
            EntryKind.RawFile => "No decoder claims this file; showing its bytes.",
            _ => "Raw bytes.",
        };
        string baseDesc = $"{what}\n{length:N0} byte(s).\n\n";
        if (fullData is not null)
        {
            if (length == RouteDecoder.TotalSizeR1 || length == RouteDecoder.TotalSizeR2)
            {
                var matrix = RouteDecoder.Parse(fullData);
                string routeBreakdown = RouteDecoder.FormatSummary(matrix);
                baseDesc += routeBreakdown + "\n\nRaw hex dump:\n";
            }
            else if (length == CorridorSpanDecoder.TotalSize)
            {
                var corridor = CorridorSpanDecoder.Parse(fullData);
                string corridorBreakdown = CorridorSpanDecoder.FormatSummary(corridor);
                baseDesc += corridorBreakdown + "\n\nRaw hex dump:\n";
            }
            else if (length == ChapterInteractionArchive.RecordSize && (node.ArchivePath is not null && Path.GetFileName(node.ArchivePath).Equals("RESOURCE.005", StringComparison.OrdinalIgnoreCase)))
            {
                int chId = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(fullData);
                int scId = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(fullData.AsSpan(4));
                int obId = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(fullData.AsSpan(8));
                int acId = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(fullData.AsSpan(12));
                byte[] payload = fullData[16..];
                var rec = new InteractionRecord(node.EntryIndex, node.EntryIndex, chId, scId, obId, acId, payload, node.Offset);
                baseDesc += $"Chapter ID: {chId}\nScene ID: {scId:D3}\nObject ID: {obId:D3}\nAction: {rec.VerbName} (ID {acId})\nTrigger Label: \"{rec.DecodedLabel}\"\nState Flags: {rec.StateTrailer.Count(b => b != 0)} active byte(s) in payload\n\nDecrypted Label Text: \"{rec.DecodedLabel}\"\n";
            }
            else if (ChapterInteractionArchive.IsInteractionDatabase(fullData))
            {
                var db = ChapterInteractionArchive.Parse(fullData);
                baseDesc += ChapterInteractionArchive.FormatSummary(db) + "\n\nRaw hex dump:\n";
            }
            else
            {
                string breakdown = FormatAttributeTableBreakdown(fullData);
                if (!string.IsNullOrEmpty(breakdown))
                    baseDesc += breakdown + "\n\nRaw hex dump:\n";
            }
        }
        return baseDesc;
    }

    private static bool IsPaletteSized(long length) =>
        length is >= IndexedPalette.MinBlockBytes and <= IndexedPalette.FullBlockBytes &&
        length % IndexedPalette.BytesPerColor == 0;

    private static string FormatAttributeTableBreakdown(byte[] data)
    {
        int nonZero = 0;
        for (int i = 0; i < Math.Min(data.Length, 1536); i++)
            if (data[i] != 0) nonZero++;

        if (nonZero == 0)
            return string.Empty;

        var sb = new StringBuilder();
        // Six pages of 256 bytes, indexed by zone id. Page 4 is never written by the exporter -- it
        // holds whatever was in its buffer -- so it is not shown. See docs/formats/masks.md.
        sb.AppendLine("Active Zones (non-zero attributes in mask lookup):");
        sb.AppendLine("Mask ID | Walk region | Scene item | Depth plane | Brightness | Footstep surface");
        sb.AppendLine("--------+-------------+------------+-------------+------------+-----------------");

        for (int id = 0; id < 256; id++)
        {
            byte walk = data[id];
            byte item = data.Length > 256 + id ? data[256 + id] : (byte)0;
            byte depth = data.Length > 512 + id ? data[512 + id] : (byte)0;
            byte light = data.Length > 768 + id ? data[768 + id] : (byte)0;
            byte mat = data.Length > 1280 + id ? data[1280 + id] : (byte)0;

            if (walk == 0 && item == 0 && depth == 0 && light == 0 && mat == 0)
                continue;

            sb.AppendLine($"  {id,3}   |     {walk,3}     |    {item,3}     |     {depth,3}     |    {light,3}     |       {mat,3}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Locates the glyph table sibling for a font bitmap node. The table sits in the slot immediately
    /// after the bitmap in the same archive folder.
    /// </summary>
    private static byte[]? FindFontTableData(FsNode bitmapNode, VirtualFileSystem vfs)
    {
        if (bitmapNode.Parent is not { } parent)
            return null;

        int nextIndex = bitmapNode.EntryIndex + 1;
        FsNode? tableNode = parent.Children.FirstOrDefault(c => c.EntryIndex == nextIndex && c.Kind == EntryKind.Font);
        if (tableNode is null)
            return null;

        return vfs.ReadBytes(tableNode);
    }
}
