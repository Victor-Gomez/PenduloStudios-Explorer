using System.Buffers.Binary;
using PenduloStudiosExplorer.Core.FileSystem;
using PenduloStudiosExplorer.Core.Formats;
using PenduloStudiosExplorer.Core.Metadata;
using PenduloStudiosExplorer.Core.Settings;
using Xunit;

namespace PenduloStudiosExplorer.Core.Tests;

public class YesterdayTests
{
    public const string YesterdaySteamDir = @"F:\Games\Steam\steamapps\common\Yesterday";

    [Fact]
    public void GameDetector_DetectsTheSignature()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Yesterday.exe"), "dummy");
            Assert.Equal(GameVersion.Yesterday, GameDetector.Detect(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GameDetector_DetectsARealInstall()
    {
        if (Directory.Exists(YesterdaySteamDir))
            Assert.Equal(GameVersion.Yesterday, GameDetector.Detect(YesterdaySteamDir));
    }

    [Fact]
    public void AppSettings_ManagesTheYesterdayPath()
    {
        var settings = new AppSettings
        {
            YesterdayDir = @"C:\Games\Yesterday",
            ActiveGame = GameVersion.Yesterday
        };

        Assert.Equal(@"C:\Games\Yesterday", settings.GetGameDir(GameVersion.Yesterday));
        Assert.Equal(@"C:\Games\Yesterday", settings.ActiveGameDir);

        settings.SetGameDir(GameVersion.Yesterday, @"D:\Yesterday");
        Assert.Equal(@"D:\Yesterday", settings.GetGameDir(GameVersion.Yesterday));
    }

    [Fact]
    public void SceneArchive_RecognizesYesterdayNames()
    {
        // Scene archives
        Assert.True(SceneArchive.IsSceneArchiveName("RESOURCE.A00", GameVersion.Yesterday));
        Assert.True(SceneArchive.IsSceneArchiveName("RESOURCE.H01", GameVersion.Yesterday));
        Assert.True(SceneArchive.IsSceneArchiveName("RESOURCE.SP8", GameVersion.Yesterday));

        // Non-scene archives
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.DIS", GameVersion.Yesterday));
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.TAB", GameVersion.Yesterday));
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.CRD", GameVersion.Yesterday));
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.B31", GameVersion.Yesterday));
    }

    [Fact]
    public void RealInstall_CanLoadAndScan()
    {
        if (!Directory.Exists(YesterdaySteamDir))
            return;

        var cache = ScanCache.Ephemeral();
        var vfs = VirtualFileSystem.Init(YesterdaySteamDir, cache: cache);
        Assert.Equal(GameVersion.Yesterday, vfs.GameVersion);
        Assert.True(vfs.Summary.SceneArchives > 0);
        Assert.True(vfs.Summary.Backgrounds > 0);
        Assert.True(vfs.Summary.Videos > 0);

        // Second scan with same cache must hit cache
        var vfs2 = VirtualFileSystem.Init(YesterdaySteamDir, cache: cache);
        Assert.True(vfs2.Summary.FromCache);
        Assert.Equal(vfs.Summary.SceneArchives, vfs2.Summary.SceneArchives);
        Assert.Equal(vfs.Summary.Backgrounds, vfs2.Summary.Backgrounds);
        Assert.True(vfs.Summary.Animations > 0, "Must classify Yesterday sprite animations");
        Assert.True(vfs.Summary.Masks > 0, "Must classify Yesterday scene masks");
        Assert.True(vfs.Summary.Overlays > 0, "Must classify Yesterday overlays");
    }

    [Fact]
    public void Yesterday_AssetsDecodeProperly()
    {
        if (!Directory.Exists(YesterdaySteamDir)) return;
        string resA01 = Path.Combine(YesterdaySteamDir, "Resource", "RESOURCE.A01");
        if (!File.Exists(resA01)) return;

        using var stream = File.OpenRead(resA01);
        byte[] tableHeader = new byte[65536];
        int tableRead = stream.Read(tableHeader, 0, tableHeader.Length);
        var entries = SceneArchive.ReadEntries(tableHeader.AsSpan(0, tableRead), stream.Length);

        // 1. Entry 0: JPEG Background (1920x1080)
        var e0 = entries[0];
        byte[] e0Bytes = new byte[e0.Size];
        stream.Seek(e0.Offset, SeekOrigin.Begin);
        stream.ReadExactly(e0Bytes, 0, e0Bytes.Length);

        Assert.True(JpegDecoder.IsJpeg(e0Bytes));
        Assert.True(JpegDecoder.TryGetDimensions(e0Bytes, out int jw, out int jh));
        Assert.Equal(1920, jw);
        Assert.Equal(1080, jh);

        var bgImage = JpegDecoder.Decode(e0Bytes);
        Assert.NotNull(bgImage);
        Assert.Equal(1920, bgImage.Width);
        Assert.Equal(1080, bgImage.Height);
        Assert.Equal(1920 * 1080 * 4, bgImage.Pixels.Length);

        // Classifier classifies Entry 0 as Background
        var c0 = EntryClassifier.Classify(e0Bytes, 1920, 1080, GameVersion.Yesterday);
        Assert.Equal(EntryKind.Background, c0.Kind);

        // 2. Entry 1: 4-byte RLE Mask (1920x1080)
        var e1 = entries[1];
        byte[] e1Bytes = new byte[e1.Size];
        stream.Seek(e1.Offset, SeekOrigin.Begin);
        stream.ReadExactly(e1Bytes, 0, e1Bytes.Length);

        var maskInfo = RleMaskDecoder.Detect(e1Bytes, 1920, 1080);
        Assert.NotNull(maskInfo);
        Assert.Equal(1920, maskInfo.Value.Width);
        Assert.Equal(1080, maskInfo.Value.Height);
        Assert.Equal(4, maskInfo.Value.RunBytes);

        var maskImage = RleMaskDecoder.Decode(e1Bytes, 1920, 1080);
        Assert.Equal(1920, maskImage.Width);
        Assert.Equal(1080, maskImage.Height);

        var c1 = EntryClassifier.Classify(e1Bytes, 1920, 1080, GameVersion.Yesterday);
        Assert.Equal(EntryKind.Mask, c1.Kind);

        // 3. Entry 9: Sprite animation (flag 6 / BGR24)
        var e9 = entries[9];
        byte[] e9Bytes = new byte[e9.Size];
        stream.Seek(e9.Offset, SeekOrigin.Begin);
        stream.ReadExactly(e9Bytes, 0, e9Bytes.Length);

        var sprite = SpriteAsset.Parse(e9Bytes);
        Assert.NotNull(sprite);
        Assert.Equal(2, sprite.FrameCount);
        Assert.Null(sprite.Verify());

        var frame0 = sprite.DecodeFrame(0);
        Assert.NotNull(frame0.Image);
        Assert.True(frame0.Image.Width > 0);
        Assert.True(frame0.Image.Height > 0);

        var c9 = EntryClassifier.Classify(e9Bytes, 1920, 1080, GameVersion.Yesterday);
        Assert.Equal(EntryKind.Animation, c9.Kind);
    }

    [Fact]
    public void Yesterday_ResourceC02_Entry04_DecodesAsPng()
    {
        if (!Directory.Exists(YesterdaySteamDir)) return;
        string resC02 = Path.Combine(YesterdaySteamDir, "Resource", "RESOURCE.C02");
        if (!File.Exists(resC02)) return;

        using var stream = File.OpenRead(resC02);
        byte[] tableHeader = new byte[65536];
        int tableRead = stream.Read(tableHeader, 0, tableHeader.Length);
        var entries = SceneArchive.ReadEntries(tableHeader.AsSpan(0, tableRead), stream.Length);
        Assert.True(entries.Count > 4);

        var e4 = entries[4];
        byte[] e4Bytes = new byte[e4.Size];
        stream.Seek(e4.Offset, SeekOrigin.Begin);
        stream.ReadExactly(e4Bytes, 0, e4Bytes.Length);

        Assert.True(PngDecoder.IsPng(e4Bytes));
        var decoded = PngDecoder.Decode(e4Bytes);
        Assert.NotNull(decoded);
        Assert.Equal(1920, decoded.Width);
        Assert.Equal(1080, decoded.Height);
        Assert.Equal(1920 * 1080 * 4, decoded.Pixels.Length);

        var classification = EntryClassifier.Classify(e4Bytes, 1920, 1080, GameVersion.Yesterday);
        Assert.Equal(EntryKind.Mask, classification.Kind);
        Assert.True(classification.Image!.IsMask);
    }

    [Fact]
    public void Yesterday_ResourceD02_Entries04_05_06_ClassifiedAsMasks()
    {
        if (!Directory.Exists(YesterdaySteamDir)) return;
        string resD02 = Path.Combine(YesterdaySteamDir, "Resource", "RESOURCE.D02");
        if (!File.Exists(resD02)) return;

        using var stream = File.OpenRead(resD02);
        byte[] tableHeader = new byte[65536];
        int tableRead = stream.Read(tableHeader, 0, tableHeader.Length);
        var entries = SceneArchive.ReadEntries(tableHeader.AsSpan(0, tableRead), stream.Length);

        // Entries 4, 5, 6 are grayscale PNG mask layers
        for (int i = 4; i <= 6; i++)
        {
            var e = entries[i];
            byte[] bytes = new byte[e.Size];
            stream.Seek(e.Offset, SeekOrigin.Begin);
            stream.ReadExactly(bytes, 0, bytes.Length);

            Assert.True(PngDecoder.IsPng(bytes));
            var c = EntryClassifier.Classify(bytes, 1920, 1080, GameVersion.Yesterday);
            Assert.Equal(EntryKind.Mask, c.Kind);
            Assert.True(c.Image!.IsMask);
            Assert.Equal(1920, c.Image.Width);
            Assert.Equal(1080, c.Image.Height);

            // ResourceLoader / PngDecoder decodes it successfully
            var img = PngDecoder.Decode(bytes);
            Assert.NotNull(img);
            Assert.Equal(1920, img.Width);
            Assert.Equal(1080, img.Height);
        }
    }

    [Fact]
    public void Yesterday_ResourceE00_Entries00_to_04_ClassifiedAsSprites()
    {
        if (!Directory.Exists(YesterdaySteamDir)) return;
        string resE00 = Path.Combine(YesterdaySteamDir, "Resource", "RESOURCE.E00");
        if (!File.Exists(resE00)) return;

        using var stream = File.OpenRead(resE00);
        byte[] tableHeader = new byte[65536];
        int tableRead = stream.Read(tableHeader, 0, tableHeader.Length);
        var entries = SceneArchive.ReadEntries(tableHeader.AsSpan(0, tableRead), stream.Length);

        // Entries 0 through 4 are object sprite sheets (RGBA PNGs with transparency)
        for (int i = 0; i <= 4; i++)
        {
            var e = entries[i];
            byte[] bytes = new byte[e.Size];
            stream.Seek(e.Offset, SeekOrigin.Begin);
            stream.ReadExactly(bytes, 0, bytes.Length);

            Assert.True(PngDecoder.IsPng(bytes));
            Assert.Equal(6, bytes[25]); // colorType == 6 (RGBA)

            var c = EntryClassifier.Classify(bytes, null, null, GameVersion.Yesterday);
            // Must be classified as Overlay/Sprite, NEVER as Background
            Assert.Equal(EntryKind.Overlay, c.Kind);
            Assert.NotNull(c.Image);
            Assert.Equal(0, c.Image.X);
            Assert.Equal(0, c.Image.Y);

            var node = new FsNode
            {
                Name = $"e{i:D2}",
                Kind = c.Kind,
                Image = c.Image,
            };
            string label = SceneCatalog.FormatSceneEntryLabel(node, "en");
            Assert.StartsWith("sprite ", label);
        }

        // Test VFS scan: RESOURCE.E00 has no scene background
        var vfs = VirtualFileSystem.Init(YesterdaySteamDir, cache: ScanCache.Ephemeral());
        var e00Archive = vfs.Root.Children
            .FirstOrDefault(c => c.Name == VirtualFileSystem.ScenesFolder)?
            .Children.FirstOrDefault(c => c.Name.Equals("RESOURCE.E00", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(e00Archive);
        Assert.Null(vfs.SceneBackgroundFor(e00Archive));

        foreach (var child in e00Archive.Children.Take(5))
        {
            Assert.Equal(EntryKind.Overlay, child.Kind);
            Assert.NotNull(child.Image);
        }
    }

    [Fact]
    public void ResourceD02_ClassifiesAndDecodesCorrectly()
    {
        if (!Directory.Exists(YesterdaySteamDir))
            return;

        var vfs = VirtualFileSystem.Init(YesterdaySteamDir, cache: ScanCache.Ephemeral());
        var d02 = vfs.Root.Children
            .FirstOrDefault(c => c.Name == VirtualFileSystem.ScenesFolder)?
            .Children.FirstOrDefault(c => c.Name.Equals("RESOURCE.D02", StringComparison.OrdinalIgnoreCase));
        if (d02 is null)
            return;

        var e00 = d02.Children.FirstOrDefault(c => c.EntryIndex == 0);
        Assert.NotNull(e00);
        Assert.Equal(EntryKind.Background, e00.Kind);
        Assert.Equal(1920, e00.Image?.Width);

        // Yesterday's D02 has no 1536-byte attribute table.
        Assert.Null(vfs.SceneAttributeTableFor(d02));
    }
}

/// <summary>
/// Yesterday's RESOURCE.IFZ, which is The Next BIG Thing's interface container: the same plain offset
/// table of PNGs, except that it aliases dozens of slots onto one image, so a size read as the gap to the
/// next slot rather than to the next distinct offset leaves most of the archive empty.
/// </summary>
public class YesterdayInterfaceTests
{
    [Fact]
    public void RealInstall_AliasedSlotsStillResolveToTheirImage()
    {
        string path = Path.Combine(YesterdayTests.YesterdaySteamDir, "Resource", "RESOURCE.IFZ");
        if (!File.Exists(path)) return;

        using FileStream fs = File.OpenRead(path);
        List<ArchiveEntry> entries = InterfaceArchive.ReadEntries(fs);
        Assert.Equal(69, entries.Count);

        int images = 0;
        var aliased = new Dictionary<long, int>();
        foreach (ArchiveEntry e in entries)
        {
            var head = new byte[64];
            fs.Position = e.Offset;
            int read = fs.Read(head, 0, (int)Math.Min(head.Length, e.Size));
            if (!PngDecoder.TryGetDimensions(head.AsSpan(0, read), out _, out _))
                continue;

            images++;
            aliased.TryGetValue(e.Offset, out int n);
            aliased[e.Offset] = n + 1;
        }

        Assert.Equal(61, images);
        Assert.True(aliased.Values.Max() > 40, "expected one image shared by dozens of slots");
    }

    [Fact]
    public void RealInstall_IsCoveredByTheShippedScanCache()
    {
        if (!Directory.Exists(YesterdayTests.YesterdaySteamDir)) return;

        // Scanning adds whatever the shipped cache did not already answer for. Scanning again must add
        // nothing: that is what "the install is fully covered" means, and it needs no list of archives
        // here that could drift from the one the scan actually walks.
        ShippedCacheBuilder.AddInstall(YesterdayTests.YesterdaySteamDir);
        Assert.Equal(0, ShippedCacheBuilder.AddInstall(YesterdayTests.YesterdaySteamDir));
    }

    [Fact]
    public void RealInstall_Yesterday_LocalVideosAndPauseScreens()
    {
        if (!Directory.Exists(YesterdayTests.YesterdaySteamDir)) return;

        // Bink videos in uk folder
        string crd = Path.Combine(YesterdayTests.YesterdaySteamDir, "Resource", "uk", "RESOURCE.CRD");
        string b31 = Path.Combine(YesterdayTests.YesterdaySteamDir, "Resource", "uk", "RESOURCE.B31");
        if (File.Exists(crd))
        {
            using var fs = File.OpenRead(crd);
            byte[] magic = new byte[4];
            fs.ReadExactly(magic);
            Assert.Equal("BIKi"u8.ToArray(), magic);
        }
        if (File.Exists(b31))
        {
            using var fs = File.OpenRead(b31);
            byte[] magic = new byte[4];
            fs.ReadExactly(magic);
            Assert.Equal("BIKi"u8.ToArray(), magic);
        }

        // RESOURCE.PSE: container of 5 PNG pause screens
        string pse = Path.Combine(YesterdayTests.YesterdaySteamDir, "Resource", "uk", "RESOURCE.PSE");
        if (File.Exists(pse))
        {
            using var fs = File.OpenRead(pse);
            var entries = SceneArchive.ReadEntries(fs);
            Assert.Equal(5, entries.Count);
            foreach (var e in entries)
            {
                byte[] head = new byte[8];
                fs.Seek(e.Offset, SeekOrigin.Begin);
                fs.ReadExactly(head);
                Assert.True(PngDecoder.IsPng(head));
            }
        }
    }

    [Fact]
    public void SyntheticYesterdayFont_DecodesRgb24Correctly()
    {
        // 1 glyph of 4x4
        var glyphs = new[] { new FontDecoder.Glyph(0, 2, 4, 4) };
        var rgbData = new byte[4 * 4 * 3];

        // Pixel 0,0: transparent (0, 0, 0)
        // Pixel 1,1: pure outline (255, 0, 255)
        int p11 = (1 * 4 + 1) * 3;
        rgbData[p11] = 255;
        rgbData[p11 + 1] = 0;
        rgbData[p11 + 2] = 255;

        // Pixel 2,2: pure fill (0, 255, 0)
        int p22 = (2 * 4 + 2) * 3;
        rgbData[p22] = 0;
        rgbData[p22 + 1] = 255;
        rgbData[p22 + 2] = 0;

        DecodedImage img = FontDecoder.DecodeRgb24(rgbData, glyphs, maxWidth: 100);
        Assert.NotNull(img);
        Assert.True(img.Width >= 4);
        Assert.True(img.Height >= 6); // Top 2 + Height 4

        // Transparent pixel
        int di00 = 0;
        Assert.Equal(0, img.Pixels[di00 + 3]); // Alpha

        // Outline pixel: dy = 1 + 2 = 3, dx = 1
        int di11 = (3 * img.Width + 1) * 4;
        Assert.Equal(255, img.Pixels[di11 + 3]); // Full alpha
        Assert.Equal(0, img.Pixels[di11]);       // Black outline (B=0)

        // Fill pixel: dy = 2 + 2 = 4, dx = 2
        int di22 = (4 * img.Width + 2) * 4;
        Assert.Equal(255, img.Pixels[di22 + 3]); // Full alpha
        Assert.Equal(255, img.Pixels[di22]);     // White body (B=255)
    }

    [Fact]
    public void SyntheticCutsceneSubtitles_ParsesCues()
    {
        string raw = """
            //[angustiado] ¡PUES SIGAN BUSCANDO!
            X
            1224
            Y
            880
            B
            110
            E
            150

            //LO PROTEGERÉ DE CUALQUIERA
            X
            784
            Y
            664
            B
            172
            E
            244
            """;

        List<SubtitleCue> cues = CutsceneSubtitles.Parse(raw);
        Assert.Equal(2, cues.Count);
        Assert.Equal("[angustiado] ¡PUES SIGAN BUSCANDO!", cues[0].Text);
        Assert.Equal(1224, cues[0].X);
        Assert.Equal(880, cues[0].Y);
        Assert.Equal(110, cues[0].BeginFrame);
        Assert.Equal(150, cues[0].EndFrame);

        Assert.Equal("LO PROTEGERÉ DE CUALQUIERA", cues[1].Text);
        Assert.Equal(172, cues[1].BeginFrame);
        Assert.Equal(244, cues[1].EndFrame);

        string summary = CutsceneSubtitles.ToSummary(cues);
        Assert.Contains("[110 - 150] [angustiado] ¡PUES SIGAN BUSCANDO!", summary);
        Assert.Contains("[172 - 244] LO PROTEGERÉ DE CUALQUIERA", summary);
    }

    [Fact]
    public void RealInstall_Yesterday_FontArchiveAndCutscenes()
    {
        if (!Directory.Exists(YesterdayTests.YesterdaySteamDir)) return;

        string fntPath = Path.Combine(YesterdayTests.YesterdaySteamDir, "Resource", "uk", "RESOURCE.FNT");
        if (File.Exists(fntPath))
        {
            byte[] fntBytes = File.ReadAllBytes(fntPath);
            Assert.True(YesterdayFontArchive.IsFontArchive(fntBytes));

            List<YesterdayFont> fonts = YesterdayFontArchive.ReadFonts(fntBytes);
            Assert.Equal(2, fonts.Count);

            Assert.Equal(196, fonts[0].Glyphs.Length);
            DecodedImage img0 = FontDecoder.DecodeRgb24(fonts[0].BitmapRgb, fonts[0].Glyphs);
            Assert.True(img0.Width > 0 && img0.Height > 0);

            Assert.Equal(191, fonts[1].Glyphs.Length);
            DecodedImage img1 = FontDecoder.DecodeRgb24(fonts[1].BitmapRgb, fonts[1].Glyphs);
            Assert.True(img1.Width > 0 && img1.Height > 0);
        }

        string subPath = Path.Combine(YesterdayTests.YesterdaySteamDir, "Datav", "VC10.001");
        if (File.Exists(subPath))
        {
            string content = File.ReadAllText(subPath, System.Text.Encoding.Latin1);
            List<SubtitleCue> cues = CutsceneSubtitles.Parse(content);
            Assert.Equal(4, cues.Count);
            Assert.Equal(110, cues[0].BeginFrame);
            Assert.Contains("PUES SIGAN BUSCANDO", cues[0].Text);
        }

        // Test VFS integration
        var vfs = VirtualFileSystem.Init(YesterdayTests.YesterdaySteamDir, cache: ScanCache.Ephemeral(includeShipped: true));
        FsNode global = Assert.Single(vfs.Root.Children, c => c.Name == VirtualFileSystem.GlobalFolder);
        FsNode? fntNode = global.Children.FirstOrDefault(c => c.Name == "RESOURCE.FNT");
        Assert.NotNull(fntNode);
        Assert.Equal(2, fntNode.Children.Count);
        Assert.All(fntNode.Children, c => Assert.Equal(EntryKind.Font, c.Kind));

        FsNode videoFolder = Assert.Single(vfs.Root.Children, c => c.Name == VirtualFileSystem.VideoFolder);
        FsNode? vc10Node = videoFolder.Children.FirstOrDefault(c => c.Name == "VC10.000");
        Assert.NotNull(vc10Node);
        Assert.NotNull(vc10Node.Subtitle);
        Assert.Contains("PUES SIGAN BUSCANDO", vc10Node.Subtitle);
    }

    [Fact]
    public void RealInstall_Yesterday_ResourceX03()
    {
        string path = Path.Combine(YesterdayTests.YesterdaySteamDir, "Resource", "RESOURCE.X03");
        if (!File.Exists(path)) return;

        byte[] bytes = File.ReadAllBytes(path);
        List<ArchiveEntry> entries = SceneArchive.ReadEntries(bytes, bytes.Length);
        Assert.Equal(8, entries.Count);

        for (int i = 0; i < entries.Count; i++)
        {
            ArchiveEntry e = entries[i];
            byte[] entryData = bytes.AsSpan((int)e.Offset, (int)e.Size).ToArray();
            Assert.True(PngDecoder.IsPng(entryData), $"Entry {i} is not PNG");
            Assert.True(PngDecoder.TryGetDimensions(entryData, out int w, out int h));
            Assert.Equal(1920, w);
            Assert.Equal(1080, h);
            byte colorType = entryData[25];
            var c = EntryClassifier.Classify(entryData, null, null, GameVersion.Yesterday);
            Assert.True(c.Kind is EntryKind.Background or EntryKind.Overlay, $"Entry {i} kind is {c.Kind}");
        }

        var vfs = VirtualFileSystem.Init(YesterdayTests.YesterdaySteamDir, cache: ScanCache.Ephemeral());
        FsNode scenes = Assert.Single(vfs.Root.Children, c => c.Name == VirtualFileSystem.ScenesFolder);
        FsNode? x03 = scenes.Children.FirstOrDefault(c => c.Name == "RESOURCE.X03");
        Assert.NotNull(x03);
        Assert.Equal(8, x03.Children.Count);
        Assert.All(x03.Children, c => Assert.True(c.Kind is EntryKind.Background or EntryKind.Overlay));
    }

    [Fact]
    public void SyntheticYesterday_ResourceX03_ExtractsAndDecodesAllPngs()
    {
        byte[] fakePng = PngWriter.ToBytes(DecodedImage.Transparent(1920, 1080));
        byte[] archive = SyntheticArchives.Scene([fakePng, fakePng, fakePng]);
        List<ArchiveEntry> entries = SceneArchive.ReadEntries(archive, archive.Length);
        Assert.Equal(3, entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            byte[] entryData = archive.AsSpan((int)entries[i].Offset, (int)entries[i].Size).ToArray();
            Assert.True(PngDecoder.IsPng(entryData));
            Assert.True(PngDecoder.TryGetDimensions(entryData, out int w, out int h));
            Assert.Equal(1920, w);
            Assert.Equal(1080, h);
        }
    }

    [Fact]
    public void RealInstall_Yesterday_ResourceX69_CutsceneArchive()
    {
        string path = Path.Combine(YesterdayTests.YesterdaySteamDir, "Resource", "RESOURCE.X69");
        if (!File.Exists(path)) return;

        byte[] bytes = File.ReadAllBytes(path);
        List<ArchiveEntry> entries = SceneArchive.ReadEntries(bytes, bytes.Length);
        Assert.Equal(9, entries.Count);

        // Entries 2..8 are 25-frame cutscene animation payloads (1,172,852 bytes each)
        for (int i = 2; i <= 8; i++)
        {
            ArchiveEntry e = entries[i];
            Assert.Equal(1_172_852, e.Size);
            byte[] entryData = bytes.AsSpan((int)e.Offset, (int)e.Size).ToArray();
            ushort fc = BinaryPrimitives.ReadUInt16LittleEndian(entryData);
            Assert.Equal(25, fc);
            uint fo0 = BinaryPrimitives.ReadUInt32LittleEndian(entryData.AsSpan(2));
            Assert.Equal(352u, fo0); // 2 + 25 * 14 = 352
        }

        var vfs = VirtualFileSystem.Init(YesterdayTests.YesterdaySteamDir, cache: ScanCache.Ephemeral());
        FsNode scenes = Assert.Single(vfs.Root.Children, c => c.Name == VirtualFileSystem.ScenesFolder);
        FsNode? x69 = scenes.Children.FirstOrDefault(c => c.Name == "RESOURCE.X69");
        Assert.NotNull(x69);
        Assert.Equal(9, x69.Children.Count);
    }

    [Fact]
    public void RealInstall_Yesterday_ResourceIfz()
    {
        string path = Path.Combine(YesterdayTests.YesterdaySteamDir, "Resource", "RESOURCE.IFZ");
        if (!File.Exists(path)) return;

        using FileStream fs = File.OpenRead(path);
        List<ArchiveEntry> entries = InterfaceArchive.ReadEntries(fs);
        Assert.NotEmpty(entries);
        Assert.Equal(69, entries.Count);

        var vfs = VirtualFileSystem.Init(YesterdayTests.YesterdaySteamDir, cache: ScanCache.Ephemeral());
        FsNode global = Assert.Single(vfs.Root.Children, c => c.Name == VirtualFileSystem.GlobalFolder);
        FsNode? ifz = global.Children.FirstOrDefault(c => c.Name == "RESOURCE.IFZ");
        Assert.NotNull(ifz);
        Assert.Equal("User Interface & HUD (RESOURCE.IFZ)", ifz.FriendlyName);
        Assert.Equal(69, ifz.Children.Count);

        var backgrounds = ifz.Children.Where(c => c.Kind == EntryKind.Background).ToList();
        Assert.NotEmpty(backgrounds);
        Assert.All(backgrounds, b => Assert.True(b.Image?.Width > 0 && b.Image?.Height > 0));

        vfs.ApplyLanguage("es");
        Assert.Equal("Interfaz de usuario y menús (RESOURCE.IFZ)", ifz.FriendlyName);
    }

    [Fact]
    public void SyntheticYesterday_ResourceIfz_DecodesPngAndAliases()
    {
        byte[] fakePng1 = PngWriter.ToBytes(DecodedImage.Transparent(100, 50));
        byte[] fakePng2 = PngWriter.ToBytes(DecodedImage.Transparent(64, 64));
        byte[] archive = SyntheticArchives.Interface([fakePng1, fakePng1, null, fakePng2]);

        List<ArchiveEntry> entries = InterfaceArchive.ReadEntries(archive, archive.Length);
        Assert.Equal(4, entries.Count);
        Assert.Equal(fakePng1.Length, entries[0].Size);
        Assert.Equal(fakePng1.Length, entries[1].Size);
        Assert.Equal(fakePng1.Length, entries[2].Size);
        Assert.Equal(fakePng2.Length, entries[3].Size);
    }
}

