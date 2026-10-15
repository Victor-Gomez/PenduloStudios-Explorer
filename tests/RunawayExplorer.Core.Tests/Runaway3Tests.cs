using System.Buffers.Binary;
using RunawayExplorer.Core.FileSystem;
using RunawayExplorer.Core.Formats;
using RunawayExplorer.Core.Metadata;
using RunawayExplorer.Core.Settings;
using Xunit;
using static RunawayExplorer.Core.Tests.SyntheticAssets;

namespace RunawayExplorer.Core.Tests;

public class Runaway3Tests
{
    public const string R3SteamDir = @"F:\Games\Steam\steamapps\common\Runaway A Twist Of Fate";

    #region Synthetic Builders

    /// <summary>
    /// Builds a synthetic Runaway 3 sprite:
    /// Record header (14 bytes per frame):
    ///   u32 fo (absolute offset from start of entry: 2 + fc * 14 + frame_relative_offset)
    ///   u16 x0, u16 w, u16 y0, u16 y1, u16 segCount
    /// Segments (7 bytes + pixel data):
    ///   u16 x, u16 y, u8 flag, u16 count, [pixels]
    ///   flag 0: RGB565 (2 bytes/pixel)
    ///   flag 1: RGB565 + 8-bit alpha (3 bytes/pixel)
    /// </summary>
    public static byte[] SyntheticSpriteR3()
    {
        ushort frameCount = 1;
        int headerSize = 2 + frameCount * 14;

        using var segMs = new MemoryStream();
        Span<byte> u16 = stackalloc byte[2];

        // Segment 0: x=10, y=5, flag=0 (RGB565, 2 bpp), count=2
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 10); segMs.Write(u16);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 5); segMs.Write(u16);
        segMs.WriteByte(0);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 2); segMs.Write(u16);
        // 2 pixels: red (0xF800), green (0x07E0)
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 0xF800); segMs.Write(u16);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 0x07E0); segMs.Write(u16);

        // Segment 1: x=15, y=5, flag=1 (RGB565 + Alpha, 3 bpp), count=1
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 15); segMs.Write(u16);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 5); segMs.Write(u16);
        segMs.WriteByte(1);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 1); segMs.Write(u16);
        // 1 pixel: blue (0x001F) + alpha 128
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 0x001F); segMs.Write(u16);
        segMs.WriteByte(128);

        byte[] segData = segMs.ToArray();

        using var ms = new MemoryStream();
        BinaryPrimitives.WriteUInt16LittleEndian(u16, frameCount); ms.Write(u16);

        // Frame 0 record: fo = absolute offset = headerSize
        Span<byte> u32 = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(u32, (uint)headerSize); ms.Write(u32);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 10); ms.Write(u16); // x0
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 16); ms.Write(u16); // x1 (x0 + w)
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 5); ms.Write(u16);  // y0
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 5); ms.Write(u16);  // y1
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 2); ms.Write(u16);  // segCount = 2

        ms.Write(segData);
        return ms.ToArray();
    }

    /// <summary>
    /// Builds a synthetic multi-frame Runaway 3 sprite exercising all segment flags:
    /// - Frame 0: Flag 0 (RGB565, 2 bpp) and Flag 1 (RGB565 + 8-bit alpha, 3 bpp)
    /// - Frame 1: Flag 6 (BGR 24-bit opaque, 3 bpp)
    /// - Frame 2: Flag 7 (BGRA 32-bit straight alpha, 4 bpp)
    /// </summary>
    public static byte[] SyntheticSpriteR3MultiFrameAllFlags()
    {
        ushort frameCount = 3;
        int headerSize = 2 + frameCount * 14;

        Span<byte> u16 = stackalloc byte[2];
        Span<byte> u32 = stackalloc byte[4];

        // Frame 0 segments:
        using var f0Segs = new MemoryStream();
        // Seg 0: x=10, y=5, flag=0, count=2 -> 7 bytes header + 4 bytes pixels
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 10); f0Segs.Write(u16);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 5); f0Segs.Write(u16);
        f0Segs.WriteByte(0);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 2); f0Segs.Write(u16);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 0xF800); f0Segs.Write(u16); // Red
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 0x07E0); f0Segs.Write(u16); // Green

        // Seg 1: x=15, y=5, flag=1, count=1 -> 7 bytes header + 3 bytes pixels
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 15); f0Segs.Write(u16);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 5); f0Segs.Write(u16);
        f0Segs.WriteByte(1);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 1); f0Segs.Write(u16);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 0x001F); f0Segs.Write(u16); // Blue
        f0Segs.WriteByte(128); // Alpha 128
        byte[] f0Data = f0Segs.ToArray();

        // Frame 1 segments:
        using var f1Segs = new MemoryStream();
        // Seg 0: x=12, y=6, flag=6, count=3 -> 7 bytes header + 9 bytes (BGR * 3)
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 12); f1Segs.Write(u16);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 6); f1Segs.Write(u16);
        f1Segs.WriteByte(6);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 3); f1Segs.Write(u16);
        // Pixel 0: B=20, G=40, R=60
        f1Segs.WriteByte(20); f1Segs.WriteByte(40); f1Segs.WriteByte(60);
        // Pixel 1: B=100, G=150, R=200
        f1Segs.WriteByte(100); f1Segs.WriteByte(150); f1Segs.WriteByte(200);
        // Pixel 2: B=255, G=250, R=245
        f1Segs.WriteByte(255); f1Segs.WriteByte(250); f1Segs.WriteByte(245);
        byte[] f1Data = f1Segs.ToArray();

        // Frame 2 segments:
        using var f2Segs = new MemoryStream();
        // Seg 0: x=11, y=7, flag=7, count=2 -> 7 bytes header + 8 bytes (BGRA * 2)
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 11); f2Segs.Write(u16);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 7); f2Segs.Write(u16);
        f2Segs.WriteByte(7);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 2); f2Segs.Write(u16);
        // Pixel 0: B=10, G=20, R=30, A=180
        f2Segs.WriteByte(10); f2Segs.WriteByte(20); f2Segs.WriteByte(30); f2Segs.WriteByte(180);
        // Pixel 1: B=50, G=60, R=70, A=220
        f2Segs.WriteByte(50); f2Segs.WriteByte(60); f2Segs.WriteByte(70); f2Segs.WriteByte(220);
        byte[] f2Data = f2Segs.ToArray();

        using var ms = new MemoryStream();
        BinaryPrimitives.WriteUInt16LittleEndian(u16, frameCount); ms.Write(u16);

        // Frame 0 record: fo = headerSize
        uint fo0 = (uint)headerSize;
        BinaryPrimitives.WriteUInt32LittleEndian(u32, fo0); ms.Write(u32);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 10); ms.Write(u16); // x0
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 16); ms.Write(u16); // x1 (x0 + w = 10 + 6)
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 5); ms.Write(u16);  // y0
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 5); ms.Write(u16);  // y1
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 2); ms.Write(u16);  // segCount = 2

        // Frame 1 record: fo = headerSize + f0Data.Length
        uint fo1 = fo0 + (uint)f0Data.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(u32, fo1); ms.Write(u32);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 12); ms.Write(u16); // x0
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 15); ms.Write(u16); // x1 (x0 + w = 12 + 3)
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 6); ms.Write(u16);  // y0
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 6); ms.Write(u16);  // y1
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 1); ms.Write(u16);  // segCount = 1

        // Frame 2 record: fo = fo1 + f1Data.Length
        uint fo2 = fo1 + (uint)f1Data.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(u32, fo2); ms.Write(u32);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 11); ms.Write(u16); // x0
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 13); ms.Write(u16); // x1 (x0 + w = 11 + 2)
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 7); ms.Write(u16);  // y0
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 7); ms.Write(u16);  // y1
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 1); ms.Write(u16);  // segCount = 1

        ms.Write(f0Data);
        ms.Write(f1Data);
        ms.Write(f2Data);
        return ms.ToArray();
    }

    /// <summary>
    /// Builds a synthetic continuous RLE mask:
    /// Runs of 3 bytes: { u8 id, u16 len } summing to width * height.
    /// </summary>
    public static byte[] SyntheticContinuousMask(IReadOnlyList<(byte Id, ushort Length)> runs)
    {
        using var ms = new MemoryStream();
        Span<byte> u16 = stackalloc byte[2];
        foreach (var (id, len) in runs)
        {
            ms.WriteByte(id);
            BinaryPrimitives.WriteUInt16LittleEndian(u16, len);
            ms.Write(u16);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Builds a synthetic sparse antialiased mask:
    /// u16 n records of:
    ///   u16 x, u16 y, u8 flag, u16 count, [count bytes of alpha feathering if flag == 4]
    /// </summary>
    public static byte[] SyntheticSparseMask()
    {
        using var ms = new MemoryStream();
        Span<byte> u16 = stackalloc byte[2];

        ushort n = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(u16, n); ms.Write(u16);

        // Record 1: x=10, y=20, flag=2 (solid run), count=50
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 10); ms.Write(u16);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 20); ms.Write(u16);
        ms.WriteByte(2);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 50); ms.Write(u16);

        // Record 2: x=60, y=20, flag=4 (antialiased feathering), count=4, alphas=[192, 128, 64, 16]
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 60); ms.Write(u16);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 20); ms.Write(u16);
        ms.WriteByte(4);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 4); ms.Write(u16);
        ms.Write([192, 128, 64, 16]);

        return ms.ToArray();
    }

    #endregion

    #region GameDetector Tests

    [Fact]
    public void GameDetector_DetectsRunaway3Signatures()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "runaway3_detect_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // RATOF.exe
            string exe = Path.Combine(tempDir, "RATOF.exe");
            File.WriteAllBytes(exe, [1, 2, 3]);
            Assert.Equal(GameVersion.Runaway3, GameDetector.Detect(tempDir));
            File.Delete(exe);

            // ratof.config
            string cfg = Path.Combine(tempDir, "ratof.config");
            File.WriteAllBytes(cfg, [1, 2, 3]);
            Assert.Equal(GameVersion.Runaway3, GameDetector.Detect(tempDir));
            File.Delete(cfg);

            // RATOF-Config.exe
            string cfgExe = Path.Combine(tempDir, "RATOF-Config.exe");
            File.WriteAllBytes(cfgExe, [1, 2, 3]);
            Assert.Equal(GameVersion.Runaway3, GameDetector.Detect(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GameDetector_DetectsRealRunaway3()
    {
        if (Directory.Exists(R3SteamDir))
        {
            Assert.Equal(GameVersion.Runaway3, GameDetector.Detect(R3SteamDir));
        }
    }

    #endregion

    #region Format Recognition & Parsing Tests

    [Fact]
    public void SceneArchive_RecognizesRunaway3Names()
    {
        Assert.True(SceneArchive.IsSceneArchiveName("RESOURCE.002", GameVersion.Runaway3)); // Cutscene archive in R3
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.002", GameVersion.Runaway1)); // Audio in R1
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.002", GameVersion.Runaway2)); // Audio in R2

        Assert.True(SceneArchive.IsSceneArchiveName("RESOURCE.D01", GameVersion.Runaway3));
        Assert.True(SceneArchive.IsSceneArchiveName("RESOURCE.001", GameVersion.Runaway3));
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.000", GameVersion.Runaway3)); // Global
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.004", GameVersion.Runaway3)); // Lip-sync
    }

    [Fact]
    public void SpriteDecoder_ParsesRunaway3Sprite()
    {
        byte[] spriteData = SyntheticSpriteR3();
        var asset = SpriteAsset.Parse(spriteData);

        Assert.NotNull(asset);
        Assert.True(asset.IsRunaway3);
        Assert.False(asset.IsRunaway2);
        Assert.Equal(1, asset.FrameCount);
        Assert.Null(asset.Verify());

        // Check frame bounds and segments
        var frameRec = asset.Records[0];
        Assert.Equal(10, frameRec.X0);
        Assert.Equal(6, frameRec.W);
        Assert.Equal(5, frameRec.Y0);
        Assert.Equal(5, frameRec.Y1);
        Assert.Equal(2, frameRec.SegmentCount);

        // Decode frame to RGBA8888
        var segments = asset.ReadSegments(0);
        Assert.Equal(2, segments.Count);

        // Segment 0: 2 pixels
        Assert.Equal(10, segments[0].X);
        Assert.Equal(5, segments[0].Y);
        Assert.Equal(2, segments[0].Count);
        Assert.False(segments[0].HasAlpha);

        // Segment 1: 1 pixel with alpha
        Assert.Equal(15, segments[1].X);
        Assert.Equal(5, segments[1].Y);
        Assert.Equal(1, segments[1].Count);
        Assert.True(segments[1].HasAlpha);

        SpriteFrame frame = asset.DecodeFrame(0);
        Assert.NotNull(frame.Image);
        Assert.Equal(6, frame.Image.Width);
        Assert.Equal(1, frame.Image.Height);

        byte[] rgba = frame.Image.Pixels;
        // Verify pixel 0 (red: 0xF800 -> B=0, G=0, R=248, A=255)
        Assert.Equal(0, rgba[0]);   // B
        Assert.Equal(0, rgba[1]);   // G
        Assert.True(rgba[2] > 240); // R
        Assert.Equal(255, rgba[3]); // A

        // Verify pixel 5 (blue with alpha 128: 0x001F -> B=248, G=0, R=0, A=128)
        int px5 = 5 * 4;
        Assert.True(rgba[px5] > 240); // B
        Assert.Equal(0, rgba[px5 + 1]); // G
        Assert.Equal(0, rgba[px5 + 2]); // R
        Assert.Equal(128, rgba[px5 + 3]); // A
    }

    [Fact]
    public void SpriteDecoder_ParsesRunaway3MultiFrame_AllFlags()
    {
        byte[] spriteData = SyntheticSpriteR3MultiFrameAllFlags();
        var asset = SpriteAsset.Parse(spriteData);

        Assert.NotNull(asset);
        Assert.True(asset.IsRunaway3);
        Assert.Equal(3, asset.FrameCount);
        Assert.Null(asset.Verify());

        // Union bounds: X=10, Y=5, Width=6, Height=3
        Assert.Equal(10, asset.Bounds.X);
        Assert.Equal(5, asset.Bounds.Y);
        Assert.Equal(6, asset.Bounds.Width);
        Assert.Equal(3, asset.Bounds.Height);

        // Frame 0: (Red pixel + Blue with alpha)
        SpriteFrame f0 = asset.DecodeFrame(0);
        Assert.NotNull(f0.Image);
        Assert.Equal(6, f0.Image.Width);
        Assert.Equal(1, f0.Image.Height);
        Assert.Equal(10, f0.X);
        Assert.Equal(5, f0.Y);
        // Pixel 0 (Red)
        Assert.True(f0.Image.Pixels[2] > 240); // R
        Assert.Equal(255, f0.Image.Pixels[3]); // A
        // Pixel 5 (Blue + alpha 128)
        int px5 = 5 * 4;
        Assert.True(f0.Image.Pixels[px5] > 240); // B
        Assert.Equal(128, f0.Image.Pixels[px5 + 3]); // A

        // Frame 1: Flag 6 (24-bit BGR opaque)
        SpriteFrame f1 = asset.DecodeFrame(1);
        Assert.NotNull(f1.Image);
        Assert.Equal(3, f1.Image.Width);
        Assert.Equal(1, f1.Image.Height);
        Assert.Equal(12, f1.X);
        Assert.Equal(6, f1.Y);
        // Pixel 0 (BGR: 20, 40, 60, 255)
        Assert.Equal(20, f1.Image.Pixels[0]);  // B
        Assert.Equal(40, f1.Image.Pixels[1]);  // G
        Assert.Equal(60, f1.Image.Pixels[2]);  // R
        Assert.Equal(255, f1.Image.Pixels[3]); // A
        // Pixel 1 (BGR: 100, 150, 200, 255)
        Assert.Equal(100, f1.Image.Pixels[4]);
        Assert.Equal(150, f1.Image.Pixels[5]);
        Assert.Equal(200, f1.Image.Pixels[6]);
        Assert.Equal(255, f1.Image.Pixels[7]);

        // Frame 2: Flag 7 (32-bit BGRA with straight alpha)
        SpriteFrame f2 = asset.DecodeFrame(2);
        Assert.NotNull(f2.Image);
        Assert.Equal(2, f2.Image.Width);
        Assert.Equal(1, f2.Image.Height);
        Assert.Equal(11, f2.X);
        Assert.Equal(7, f2.Y);
        // Pixel 0 (BGRA: 10, 20, 30, 180)
        Assert.Equal(10, f2.Image.Pixels[0]);
        Assert.Equal(20, f2.Image.Pixels[1]);
        Assert.Equal(30, f2.Image.Pixels[2]);
        Assert.Equal(180, f2.Image.Pixels[3]);
        // Pixel 1 (BGRA: 50, 60, 70, 220)
        Assert.Equal(50, f2.Image.Pixels[4]);
        Assert.Equal(60, f2.Image.Pixels[5]);
        Assert.Equal(70, f2.Image.Pixels[6]);
        Assert.Equal(220, f2.Image.Pixels[7]);

        // Export to APNG
        using var apngMs = new MemoryStream();
        ApngWriter.Write(asset, apngMs, 15);
        byte[] apng = apngMs.ToArray();
        Assert.NotEmpty(apng);
        Assert.Equal(0x89, apng[0]);
        Assert.Equal((byte)'P', apng[1]);
        Assert.Equal((byte)'N', apng[2]);
        Assert.Equal((byte)'G', apng[3]);
    }

    [Fact]
    public void ContinuousMaskDecoder_DetectsAndDecodes()
    {
        // 1280x720 image: 921,600 pixels total
        int remaining = 1280 * 720;
        var exactRuns = new List<(byte Id, ushort Length)>();
        byte currentId = 1;
        while (remaining > 0)
        {
            ushort step = (ushort)Math.Min(remaining, 50000);
            exactRuns.Add((currentId, step));
            remaining -= step;
            currentId = (byte)(currentId == 1 ? 2 : 1);
        }

        byte[] maskData = SyntheticContinuousMask(exactRuns);

        var maskInfo = RleMaskDecoder.DetectContinuous(maskData);
        Assert.NotNull(maskInfo);
        Assert.Equal(1280, maskInfo.Value.Width);
        Assert.Equal(720, maskInfo.Value.Height);

        var decoded = RleMaskDecoder.Decode(maskData, 1280, 720);
        Assert.Equal(1280 * 720 * 4, decoded.Pixels.Length);
        Assert.True(decoded.Pixels[3] > 0); // Alpha > 0
    }

    [Fact]
    public void SparseMaskDecoder_DetectsAndDecodes()
    {
        byte[] sparseData = SyntheticSparseMask();

        var maskInfo = SparseMaskDecoder.Detect(sparseData);
        Assert.NotNull(maskInfo);
        Assert.Equal(2, maskInfo.Value.RunCount);

        // Decode into 100x50 canvas
        DecodedImage image = SparseMaskDecoder.Decode(sparseData, 100, 50, colorSeed: 1);
        Assert.Equal(100 * 50 * 4, image.Pixels.Length);

        // Record 1: x=10..59 at y=20 has flag 2 (solid run, alpha=255)
        for (int x = 10; x < 60; x++)
        {
            int idx = (20 * 100 + x) * 4;
            Assert.Equal(255, image.Pixels[idx + 3]);
        }

        // Record 2: x=60..63 at y=20 has flag 4 (antialiased feathering: [192, 128, 64, 16])
        Assert.Equal(192, image.Pixels[(20 * 100 + 60) * 4 + 3]);
        Assert.Equal(128, image.Pixels[(20 * 100 + 61) * 4 + 3]);
        Assert.Equal(64, image.Pixels[(20 * 100 + 62) * 4 + 3]);
        Assert.Equal(16, image.Pixels[(20 * 100 + 63) * 4 + 3]);

        // Pixels outside the mask should be transparent (alpha == 0)
        Assert.Equal(0, image.Pixels[(20 * 100 + 9) * 4 + 3]);
        Assert.Equal(0, image.Pixels[(20 * 100 + 64) * 4 + 3]);
        Assert.Equal(0, image.Pixels[(19 * 100 + 20) * 4 + 3]);
    }

    [Fact]
    public void SceneCatalog_HasRunaway3Metadata()
    {
        Assert.Equal(7, SceneCatalog.ChaptersR3.Count);
        Assert.Equal("Brian Basco Is Dead", SceneCatalog.ChaptersR3[0].TitleEn);
        Assert.Equal("Epilogue & Flashbacks", SceneCatalog.ChaptersR3[6].TitleEn);

        var ch1 = SceneCatalog.GetChapter(1, GameVersion.Runaway3);
        Assert.NotNull(ch1);
        Assert.Equal("Brian Basco Is Dead", ch1.TitleEn);

        string a01Title = SceneCatalog.GetSceneTitle("RESOURCE.A01", "en", GameVersion.Runaway3);
        Assert.Contains("Brian's Grave", a01Title);

        string d01Title = SceneCatalog.GetSceneTitle("RESOURCE.D01", "en", GameVersion.Runaway3);
        Assert.Contains("Autopsy Lab", d01Title);

        string cutTitle = SceneCatalog.GetSceneTitle("RESOURCE.002", "en", GameVersion.Runaway3);
        Assert.Contains("Cinematic Cutscene", cutTitle);
    }

    #endregion

    #region Real Installation Integration Tests

    [Fact]
    public void RealInstall_SceneArchivesAndEntries()
    {
        if (!Directory.Exists(R3SteamDir)) return;

        string resDir = Path.Combine(R3SteamDir, "Resource");
        if (!Directory.Exists(resDir)) return;

        // Verify RESOURCE.D01
        string d01Path = Path.Combine(resDir, "RESOURCE.D01");
        if (File.Exists(d01Path))
        {
            using var f = File.OpenRead(d01Path);
            var entries = SceneArchive.ReadEntries(f);
            Assert.NotEmpty(entries);

            // Entry 0 should be raster: D01 is a 2-story tall scene 1508x1540
            var e0 = entries[0];
            byte[] e0Bytes = new byte[e0.Size];
            f.Position = e0.Offset;
            Assert.Equal(e0.Size, f.Read(e0Bytes));
            var rasterInfo = RasterDecoder.Detect(e0Bytes, GameVersion.Runaway3);
            Assert.NotNull(rasterInfo);
            Assert.Equal(1508, rasterInfo.Value.Width);
            Assert.Equal(1540, rasterInfo.Value.Height);

            // Entry 1 should be continuous mask matching D01 dimensions
            var e1 = entries[1];
            byte[] e1Bytes = new byte[e1.Size];
            f.Position = e1.Offset;
            Assert.Equal(e1.Size, f.Read(e1Bytes));
            var maskInfo = RleMaskDecoder.DetectContinuous(e1Bytes, 1508, 1540);
            Assert.NotNull(maskInfo);
            Assert.Equal(1508, maskInfo.Value.Width);
            Assert.Equal(1540, maskInfo.Value.Height);
        }

        // Verify RESOURCE.A02: vertical 2-screen tall scene 1280x1440
        string a02Path = Path.Combine(resDir, "RESOURCE.A02");
        if (File.Exists(a02Path))
        {
            using var f = File.OpenRead(a02Path);
            var entries = SceneArchive.ReadEntries(f);
            Assert.NotEmpty(entries);

            // Entry 0: 1280x1440 background
            var e0 = entries[0];
            byte[] e0Bytes = new byte[e0.Size];
            f.Position = e0.Offset;
            Assert.Equal(e0.Size, f.Read(e0Bytes));
            var rasterInfo = RasterDecoder.Detect(e0Bytes, GameVersion.Runaway3);
            Assert.NotNull(rasterInfo);
            Assert.Equal(1280, rasterInfo.Value.Width);
            Assert.Equal(1440, rasterInfo.Value.Height);

            // Entry 1: 1280x1440 continuous mask
            var e1 = entries[1];
            byte[] e1Bytes = new byte[e1.Size];
            f.Position = e1.Offset;
            Assert.Equal(e1.Size, f.Read(e1Bytes));
            var maskInfo = RleMaskDecoder.Detect(e1Bytes, 1280, 1440);
            Assert.NotNull(maskInfo);
            Assert.Equal(1280, maskInfo.Value.Width);
            Assert.Equal(1440, maskInfo.Value.Height);

            // Entry 8: sparse mask on 1280x1440 scene
            var e8 = entries[8];
            byte[] e8Bytes = new byte[e8.Size];
            f.Position = e8.Offset;
            Assert.Equal(e8.Size, f.Read(e8Bytes));
            var sparseInfo = SparseMaskDecoder.Detect(e8Bytes, 1280, 1440);
            Assert.NotNull(sparseInfo);
            Assert.Equal(1280, sparseInfo.Value.Width);
            Assert.Equal(1440, sparseInfo.Value.Height);
        }

        // Verify RESOURCE.002 is a scene archive in R3
        string r002Path = Path.Combine(resDir, "RESOURCE.002");
        if (File.Exists(r002Path))
        {
            using var f = File.OpenRead(r002Path);
            var entries = SceneArchive.ReadEntries(f);
            Assert.Equal(14, entries.Count); // 14 animations
        }

        // Verify Audio RESOURCE.S00
        string s00Path = Path.Combine(resDir, "RESOURCE.S00");
        if (File.Exists(s00Path))
        {
            using var f = File.OpenRead(s00Path);
            var entries = AudioArchive.ReadEntries(f);
            Assert.NotEmpty(entries);
        }

        // Verify Lip-sync RESOURCE.004
        string r004Path = Path.Combine(resDir, "RESOURCE.004");
        if (File.Exists(r004Path))
        {
            using var f = File.OpenRead(r004Path);
            byte[] r004Bytes = new byte[f.Length];
            Assert.Equal((int)f.Length, f.Read(r004Bytes));
            var entries = VisemeArchive.ReadEntries(r004Bytes, f.Length);
            Assert.NotEmpty(entries);
        }
    }

    [Fact]
    public void RealInstall_VoiceArchive()
    {
        if (!Directory.Exists(R3SteamDir)) return;

        string voicePath = Path.Combine(R3SteamDir, "Dataa", "DATAAA.000");
        if (!File.Exists(voicePath)) return;

        using var f = File.OpenRead(voicePath);
        var clips = VoiceArchive.ReadSingleArchiveClips(f, voicePath);
        Assert.Equal(7228, clips.Count);
    }

    [Fact]
    public void RealInstall_VirtualFileSystem_FullScan()
    {
        if (!Directory.Exists(R3SteamDir)) return;

        var vfs = VirtualFileSystem.Init(R3SteamDir, cache: ScanCache.Ephemeral(includeShipped: true));

        Assert.Equal(GameVersion.Runaway3, vfs.GameVersion);
        Assert.True(vfs.Summary.FromCache);
        Assert.True(vfs.Summary.SceneArchives > 0);
        Assert.True(vfs.Summary.AudioClips > 0);
        Assert.True(vfs.Summary.VoiceClips > 0);
        Assert.True(vfs.Summary.Videos > 0);

        // Verify scenes folder exists
        var scenesNode = vfs.Root.Children.FirstOrDefault(c => c.Name == VirtualFileSystem.ScenesFolder);
        Assert.NotNull(scenesNode);
        Assert.True(scenesNode.Children.Count >= 50);

        // Verify cutscenes from RESOURCE.002 are present
        Assert.Contains(scenesNode.Children, s => s.Name == "RESOURCE.002");
    }

    [Fact]
    public void RealInstall_IsCoveredByTheShippedScanCache()
    {
        if (!Directory.Exists(R3SteamDir)) return;

        // Scanning adds whatever the shipped cache did not already answer for. Scanning again must add
        // nothing: that is what "the install is fully covered" means, and it needs no list of archives
        // here that could drift from the one the scan actually walks.
        ShippedCacheBuilder.AddInstall(R3SteamDir);
        Assert.Equal(0, ShippedCacheBuilder.AddInstall(R3SteamDir));
        Assert.True(ShippedCacheBuilder.Covers(Path.Combine(R3SteamDir, "Resource", "RESOURCE.000")),
            "The global archive should be in the shipped cache: classifying it cold is the slowest part of a first launch");
    }

    [Fact]
    public void SpriteDecoder_ParsesRunaway3RealSprite()
    {
        string path = Path.Combine(R3SteamDir, "Resource", "RESOURCE.A05");
        if (!File.Exists(path)) return;

        using var f = File.OpenRead(path);
        var entries = SceneArchive.ReadEntries(f);
        var e15 = entries[15];
        byte[] bytes = new byte[e15.Size];
        f.Position = e15.Offset;
        f.ReadExactly(bytes);

        var asset = SpriteAsset.Parse(bytes);
        Assert.NotNull(asset);
        Assert.Equal(985, asset.Bounds.X);
        Assert.Equal(362, asset.Bounds.Y);
        Assert.Equal(183, asset.Bounds.Width);
        Assert.Equal(239, asset.Bounds.Height);
        Assert.Equal(38, asset.FrameCount);
        Assert.Null(asset.Verify());
    }

    [Fact]
    public void RealInstall_Runaway3_SceneSpriteAnimations()
    {
        if (!Directory.Exists(R3SteamDir)) return;

        string resDir = Path.Combine(R3SteamDir, "Resource");
        if (!Directory.Exists(resDir)) return;

        // 1. RESOURCE.002: cutscene archive containing 2 character sprite animations (e12, e13)
        string r002Path = Path.Combine(resDir, "RESOURCE.002");
        if (File.Exists(r002Path))
        {
            using var f = File.OpenRead(r002Path);
            var entries = SceneArchive.ReadEntries(f);
            Assert.Equal(14, entries.Count);

            int spriteCount = 0;
            foreach (var entry in entries)
            {
                byte[] bytes = new byte[entry.Size];
                f.Position = entry.Offset;
                f.ReadExactly(bytes);

                var asset = SpriteAsset.Parse(bytes);
                if (asset is null) continue;

                spriteCount++;
                Assert.True(asset.IsRunaway3);
                Assert.True(asset.FrameCount > 0);
                Assert.Null(asset.Verify());

                // Decode every frame
                for (int i = 0; i < asset.FrameCount; i++)
                {
                    SpriteFrame sf = asset.DecodeFrame(i);
                    if (!sf.IsEmpty)
                    {
                        Assert.True(sf.Image!.Width > 0);
                        Assert.True(sf.Image.Height > 0);
                        Assert.True(sf.X >= asset.Bounds.X);
                        Assert.True(sf.Y >= asset.Bounds.Y);
                    }
                }

                // APNG export
                using var apngMs = new MemoryStream();
                ApngWriter.Write(asset, apngMs, 15);
                byte[] apng = apngMs.ToArray();
                Assert.NotEmpty(apng);
            }
            Assert.Equal(2, spriteCount);
        }

        // 2. RESOURCE.D01: chapter 4 autopsy lab scene sprites (8 sprites out of 16 entries)
        string d01Path = Path.Combine(resDir, "RESOURCE.D01");
        if (File.Exists(d01Path))
        {
            using var f = File.OpenRead(d01Path);
            var entries = SceneArchive.ReadEntries(f);

            int spriteCount = 0;
            foreach (var entry in entries)
            {
                if (entry.Size < 16) continue;
                byte[] bytes = new byte[entry.Size];
                f.Position = entry.Offset;
                f.ReadExactly(bytes);

                var asset = SpriteAsset.Parse(bytes);
                if (asset != null)
                {
                    spriteCount++;
                    Assert.True(asset.IsRunaway3);
                    Assert.True(asset.FrameCount > 0);
                    Assert.Null(asset.Verify());

                    // Decode first and last frames
                    var firstFrame = asset.DecodeFrame(0);
                    var lastFrame = asset.DecodeFrame(asset.FrameCount - 1);
                    Assert.True(firstFrame.Image != null || firstFrame.IsEmpty);
                    Assert.True(lastFrame.Image != null || lastFrame.IsEmpty);
                }
            }
            Assert.Equal(8, spriteCount);
        }
    }

    [Fact]
    public void SparseMaskDecoder_DecodesRealRunaway3Mask()
    {
        string path = Path.Combine(R3SteamDir, "Resource", "RESOURCE.C10");
        if (!File.Exists(path)) return;
        using var fs = File.OpenRead(path);
        var ents = SceneArchive.ReadEntries(fs);
        var me = ents[7];
        byte[] data = new byte[me.Size];
        fs.Position = me.Offset;
        fs.ReadExactly(data);

        var decoded = SparseMaskDecoder.Decode(data, 1380, 720, colorSeed: 7);
        Assert.NotNull(decoded);
        Assert.Equal(1380, decoded.Width);
        Assert.Equal(720, decoded.Height);

        int solidPixels = 0;
        int antialiasedPixels = 0;
        int emptyPixels = 0;

        for (int p = 0; p < decoded.Pixels.Length; p += 4)
        {
            byte a = decoded.Pixels[p + 3];
            if (a == 255) solidPixels++;
            else if (a > 0) antialiasedPixels++;
            else emptyPixels++;
        }

        Console.WriteLine($"Decoded e07: solid={solidPixels}, antialiased={antialiasedPixels}, empty={emptyPixels}");
        Assert.True(solidPixels > 10_000, $"Expected solidPixels > 10,000, got {solidPixels}");
        Assert.True(antialiasedPixels > 100, $"Expected antialiasedPixels > 100, got {antialiasedPixels}");
    }

    [Fact]
    public void SceneMask_DecodesRunaway3ContinuousRleMaskAndAttributeTable()
    {
        string path = Path.Combine(R3SteamDir, "Resource", "RESOURCE.C10");
        if (!File.Exists(path)) return;
        using var fs = File.OpenRead(path);
        var ents = SceneArchive.ReadEntries(fs);

        // e01: RLE mask
        var e1 = ents[1];
        byte[] b1 = new byte[e1.Size];
        fs.Position = e1.Offset;
        fs.ReadExactly(b1);

        var maskInfo = RleMaskDecoder.Detect(b1, 1380, 720);
        Assert.NotNull(maskInfo);
        Assert.Equal(1380, maskInfo.Value.Width);
        Assert.Equal(720, maskInfo.Value.Height);

        // e02: 1536-byte attribute table (6 attributes * 256 IDs)
        var e2 = ents[2];
        Assert.Equal(1536, e2.Size);
        byte[] b2 = new byte[e2.Size];
        fs.Position = e2.Offset;
        fs.ReadExactly(b2);

        byte[]? map = RleMaskDecoder.ExtractObjectMapping(b2);
        Assert.NotNull(map);
        Assert.Equal(256, map.Length);
    }

    #endregion
}

/// <summary>
/// Runaway 3 keeps its interface art as whole Windows BMP files in RESOURCE.000 -- the title and pause
/// screens, the panel widgets and the inventory sheets -- rather than as the raw RGB565 rasters and keyed
/// cursor atlas of Runaway 1 and 2.
/// </summary>
public class Runaway3InterfaceTests
{
    [Fact]
    public void RealInstall_GlobalArchiveAlsoHoldsAnimations()
    {
        // Runaway 3 keeps its interface art as BMP, but its global archive carries sprite animations in
        // the scene archives' format alongside them.
        if (!Directory.Exists(Runaway3Tests.R3SteamDir)) return;

        var vfs = VirtualFileSystem.Init(Runaway3Tests.R3SteamDir, cache: ScanCache.Ephemeral(includeShipped: true));
        FsNode global = vfs.Root.Children.Single(c => c.Name == VirtualFileSystem.GlobalFolder)
            .Children.Single(c => c.Name.Equals("RESOURCE.000", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(46, global.Children.Count(c => c.Kind == EntryKind.Animation));
        Assert.Equal(20, global.Children.Count(c => c.FriendlyName?.Contains("  bitmap ", StringComparison.Ordinal) == true));

        foreach (FsNode node in global.Children.Where(c => c.Kind == EntryKind.Animation))
        {
            SpriteAsset sprite = Assert.IsType<SpriteAsset>(SpriteAsset.Parse(vfs.ReadBytes(node)));
            Assert.Null(sprite.Verify());
        }
    }

    [Fact]
    public void RealInstall_FontsUseTheSameWiderGlyphRecordsAsRunaway2()
    {
        if (!Directory.Exists(Runaway3Tests.R3SteamDir)) return;

        var vfs = VirtualFileSystem.Init(Runaway3Tests.R3SteamDir, cache: ScanCache.Ephemeral(includeShipped: true));
        FsNode global = vfs.Root.Children.Single(c => c.Name == VirtualFileSystem.GlobalFolder)
            .Children.Single(c => c.Name.Equals("RESOURCE.000", StringComparison.OrdinalIgnoreCase));

        List<FsNode> fonts = global.Children.Where(c => c.Kind == EntryKind.Font).ToList();
        Assert.Equal([123, 124, 125, 126, 138, 139, 159, 160, 161, 162], fonts.Select(c => c.EntryIndex));

        // 107,900 bytes is why the offset had to grow: it does not fit a 16-bit one.
        byte[] bitmap = vfs.ReadBytes(global.Children.Single(c => c.EntryIndex == 123));
        byte[] table = vfs.ReadBytes(global.Children.Single(c => c.EntryIndex == 124));
        Assert.Equal(107_900, bitmap.Length);
        Assert.True(FontDecoder.IsGlyphTable(table, bitmap.Length, FontDecoder.RecordSizeLater));
    }

    [Fact]
    public void RealInstall_GlobalArchiveHoldsBitmaps()
    {
        string path = Path.Combine(Runaway3Tests.R3SteamDir, "Resource", "RESOURCE.000");
        if (!File.Exists(path)) return;

        using FileStream fs = File.OpenRead(path);
        List<ArchiveEntry> entries = GlobalArchive.ReadEntries(fs, GameVersion.Runaway3);

        var sizes = new List<(int Index, int Width, int Height)>();
        foreach (ArchiveEntry e in entries)
        {
            if (e.Size < BmpDecoder.MinHeaderSize) continue;
            var head = new byte[BmpDecoder.MinHeaderSize];
            fs.Position = e.Offset;
            fs.ReadExactly(head);
            if (BmpDecoder.TryReadHeader(head, out int w, out int h, out long bytes) && bytes <= e.Size)
                sizes.Add((e.Index, w, h));
        }

        Assert.Equal(20, sizes.Count);
        // The title and pause screen is a full 1280x720; the smallest are the panel widgets.
        Assert.Contains((103, 1280, 720), sizes);
        Assert.Contains((136, 48, 40), sizes);

        // Every one of them decodes, header and pixels agreeing.
        foreach ((int index, int w, int h) in sizes)
        {
            ArchiveEntry e = entries.First(x => x.Index == index);
            var data = new byte[e.Size];
            fs.Position = e.Offset;
            fs.ReadExactly(data);
            DecodedImage image = BmpDecoder.Decode(data)!;
            Assert.Equal((w, h), (image.Width, image.Height));
        }
    }

    [Fact]
    public void SyntheticChapterInteractionArchive_ParsesCorrectly()
    {
        uint totalRecords = 3;
        uint chapterCount = 2;
        int headerBytes = 8 + 2 * 12; // 32 bytes

        using var ms = new MemoryStream();
        Span<byte> u32 = stackalloc byte[4];

        BinaryPrimitives.WriteUInt32LittleEndian(u32, totalRecords); ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, chapterCount); ms.Write(u32);

        // Ch 0: 2 records at offset 32
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 0); ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 2); ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, (uint)headerBytes); ms.Write(u32);

        // Ch 1: 1 record at offset 32 + 2 * 220 = 472
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 1); ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 1); ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, (uint)(headerBytes + 2 * ChapterInteractionArchive.RecordSize)); ms.Write(u32);

        // Record 0 (Ch 0, Sc 10, Obj 100, Act 1)
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 0); ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 10); ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 100); ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 1); ms.Write(u32);
        ms.Write(new byte[204]);

        // Record 1 (Ch 0, Sc 10, Obj 101, Act 2)
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 0); ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 10); ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 101); ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 2); ms.Write(u32);
        ms.Write(new byte[204]);

        // Record 2 (Ch 1, Sc 20, Obj 200, Act 3)
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 1); ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 20); ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 200); ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 3); ms.Write(u32);
        ms.Write(new byte[204]);

        byte[] dbBytes = ms.ToArray();
        Assert.True(ChapterInteractionArchive.IsInteractionDatabase(dbBytes));

        ChapterInteractionDatabase db = ChapterInteractionArchive.Parse(dbBytes);
        Assert.Equal(2, db.Chapters.Count);
        Assert.Equal(3, db.TotalRecords);

        var ch0Recs = db.GetRecordsForChapter(0).ToList();
        Assert.Equal(2, ch0Recs.Count);
        Assert.Equal(10, ch0Recs[0].SceneId);
        Assert.Equal(100, ch0Recs[0].ObjectId);
        Assert.Equal(1, ch0Recs[0].ActionId);

        var ch1Recs = db.GetRecordsForChapter(1).ToList();
        Assert.Single(ch1Recs);
        Assert.Equal(20, ch1Recs[0].SceneId);
        Assert.Equal(200, ch1Recs[0].ObjectId);
        Assert.Equal(3, ch1Recs[0].ActionId);

        string summary = ChapterInteractionArchive.FormatSummary(db);
        Assert.Contains("3 records across 2 chapters", summary);
    }

    [Fact]
    public void RealInstall_Runaway3_ChapterInteractionDatabase()
    {
        if (!Directory.Exists(Runaway3Tests.R3SteamDir)) return;

        string res005 = Path.Combine(Runaway3Tests.R3SteamDir, "Resource", "RESOURCE.005");
        if (!File.Exists(res005)) return;

        byte[] bytes = File.ReadAllBytes(res005);
        Assert.Equal(478_152, bytes.Length);
        Assert.True(ChapterInteractionArchive.IsInteractionDatabase(bytes));

        ChapterInteractionDatabase db = ChapterInteractionArchive.Parse(bytes);
        Assert.Equal(7, db.Chapters.Count);
        Assert.Equal(2173, db.TotalRecords);

        // Verify chapter counts
        Assert.Equal(165, db.Chapters[0].RecordCount);
        Assert.Equal(92, db.Chapters[0].Offset);

        Assert.Equal(538, db.Chapters[1].RecordCount);
        Assert.Equal(36392, db.Chapters[1].Offset);

        Assert.Equal(574, db.Chapters[2].RecordCount);
        Assert.Equal(154752, db.Chapters[2].Offset);

        Assert.Equal(412, db.Chapters[3].RecordCount);
        Assert.Equal(281032, db.Chapters[3].Offset);

        Assert.Equal(311, db.Chapters[4].RecordCount);
        Assert.Equal(371672, db.Chapters[4].Offset);

        Assert.Equal(85, db.Chapters[5].RecordCount);
        Assert.Equal(440092, db.Chapters[5].Offset);

        Assert.Equal(88, db.Chapters[6].RecordCount);
        Assert.Equal(458792, db.Chapters[6].Offset);

        // Test VFS integration
        var vfs = VirtualFileSystem.Init(Runaway3Tests.R3SteamDir, cache: ScanCache.Ephemeral(includeShipped: true));
        FsNode global = Assert.Single(vfs.Root.Children, c => c.Name == VirtualFileSystem.GlobalFolder);
        FsNode? res005Node = global.Children.FirstOrDefault(c => c.Name == "RESOURCE.005");
        Assert.NotNull(res005Node);
        Assert.Equal(7, res005Node.Children.Count);
        Assert.All(res005Node.Children, ch => Assert.True(ch.Children.Count > 0));

        // Test payload decryption on real game objects
        Assert.Equal("URNA", db.Records[165].DecodedLabel);
        Assert.Equal("GUANTE", db.Records[166].DecodedLabel);
        Assert.Equal("LINTERNA", db.Records[167].DecodedLabel);
        Assert.Equal("RADIAL", db.Records[171].DecodedLabel);
        Assert.Equal("CINTA DE CORONA FÚNEBRE", db.Records[172].DecodedLabel);

        // Test scene graph and Mermaid flowchart formatting
        string sceneGraph = db.FormatSceneGraph(1, 0);
        Assert.Contains("URNA", sceneGraph);
        Assert.Contains("GUANTE", sceneGraph);
        Assert.Contains("Mirar", sceneGraph);

        string mermaid = db.GenerateMermaid(1, 0);
        Assert.StartsWith("flowchart TD", mermaid);
        Assert.Contains("URNA", mermaid);
    }

    [Fact]
    public void ChapterInteractionArchive_SyntheticDatabase_DecodesLabelsAndBuildsGraph()
    {
        var syntheticRecords = new List<(int ChapterId, int SceneId, int ObjectId, int ActionId, string Label)>
        {
            (1, 5, 10, 0, "CLAVO"),
            (1, 5, 10, 1, "COGER CLAVO"),
            (1, 5, 20, 2, "PUERTA DE HIERRO"),
            (2, 8, 30, 0, "ESTATUA"),
        };

        byte[] dbBytes = SyntheticInteractionDatabase(syntheticRecords);
        Assert.True(ChapterInteractionArchive.IsInteractionDatabase(dbBytes));

        ChapterInteractionDatabase db = ChapterInteractionArchive.Parse(dbBytes);
        Assert.Equal(2, db.Chapters.Count);
        Assert.Equal(4, db.TotalRecords);

        Assert.Equal("CLAVO", db.Records[0].DecodedLabel);
        Assert.Equal("Examine / Look (Mirar)", db.Records[0].VerbName);
        Assert.Equal("COGER CLAVO", db.Records[1].DecodedLabel);
        Assert.Equal("Take / Pick up (Coger)", db.Records[1].VerbName);
        Assert.Equal("PUERTA DE HIERRO", db.Records[2].DecodedLabel);
        Assert.Equal("Use / Interact (Usar)", db.Records[2].VerbName);

        Assert.Equal([5], db.GetScenesForChapter(1));
        Assert.Equal([8], db.GetScenesForChapter(2));
        Assert.Equal([10, 20], db.GetObjectsForScene(1, 5));

        string sceneGraph = db.FormatSceneGraph(1, 5);
        Assert.Contains("CLAVO", sceneGraph);
        Assert.Contains("PUERTA DE HIERRO", sceneGraph);

        string mermaid = db.GenerateMermaid(1, 5);
        Assert.Contains("flowchart TD", mermaid);
        Assert.Contains("CLAVO", mermaid);
        Assert.Contains("PUERTA DE HIERRO", mermaid);
    }

    [Fact]
    public void SceneCatalog_Runaway3_ResourceA01_DoesNotInheritRunaway1Names()
    {
        // When Runaway 3 RESOURCE.A01 is scanned, entry 0..7 must NOT be named with Runaway 1 titles
        var node = new FsNode
        {
            Name = "e00",
            ArchivePath = "RESOURCE.A01",
            EntryIndex = 0,
            Kind = EntryKind.Background,
            Image = new ImageInfo { Width = 1650, Height = 720 },
            GameVersion = GameVersion.Runaway3,
        };
        string label = SceneCatalog.FormatSceneEntryLabel(node, "en", GameVersion.Runaway3);
        Assert.Equal("background 1650×720 (e00)", label);

        var maskNode = new FsNode
        {
            Name = "e04",
            ArchivePath = "RESOURCE.A01",
            EntryIndex = 4,
            Kind = EntryKind.Mask,
            Image = new ImageInfo { Width = 1650, Height = 720 },
            GameVersion = GameVersion.Runaway3,
        };
        string maskLabel = SceneCatalog.FormatSceneEntryLabel(maskNode, "en", GameVersion.Runaway3);
        Assert.Equal("scene mask 1650×720 (e04)", maskLabel);
    }
}

