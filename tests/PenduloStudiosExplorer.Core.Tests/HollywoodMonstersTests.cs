using System.Buffers.Binary;
using PenduloStudiosExplorer.Core.FileSystem;
using PenduloStudiosExplorer.Core.Formats;
using PenduloStudiosExplorer.Core.Metadata;
using PenduloStudiosExplorer.Core.Settings;
using Xunit;
using static PenduloStudiosExplorer.Core.Tests.SyntheticAssets;

namespace PenduloStudiosExplorer.Core.Tests;

/// <summary>
/// A throwaway Hollywood Monsters install: one scene archive holding all three indexed image formats plus
/// its own palette block, the shared palette in RESOURCE.000, and one archive of each audio kind.
/// </summary>
public sealed class FakeHmInstall : IDisposable
{
    public const int SceneColors = 176;
    public const int SharedColors = IndexedPalette.Colors - SceneColors;

    public string Root { get; } = Directory.CreateTempSubdirectory("monsters-install").FullName;

    public byte[] ScenePalette { get; } = Palette(SceneColors);
    public byte[] SharedPalette { get; } = Palette(SharedColors, seed: 7);
    public byte[] ResidentSound { get; } = new byte[5826];
    public byte[] Background { get; } = IndexedRaster();
    public byte[] Overlay { get; } = IndexedOverlay([(300, 40, [10, 11, 12]), (300, 41, [13, 14, 15])]);
    public byte[] Sprite { get; } = IndexedSprite(
    [
        new IndexedFrame([(10, 20, [200, 201]), (10, 21, [202, 203])]),
        new IndexedFrame([(11, 20, [204])]),
    ]);

    /// <summary>The second cast palette of the two-palette archive, as RESOURCE.A03 e08 is in the real game.</summary>
    public byte[] SecondScenePalette { get; } = Palette(SceneColors, seed: 31);

    public byte[] SecondSprite { get; } = IndexedSprite(
    [
        new IndexedFrame([(40, 50, [12, 13, 14])]),
    ]);

    public FakeHmInstall()
    {
        File.WriteAllText(Path.Combine(Root, "Monsters.exe"), "dummy");
        File.WriteAllBytes(Path.Combine(Root, "RESOURCE.A02"), SyntheticArchives.Scene([Background, ScenePalette, Overlay, Sprite]));
        // Two palette blocks: entries 2 take e01, entries after e03 take e03.
        File.WriteAllBytes(Path.Combine(Root, "RESOURCE.A03"),
            SyntheticArchives.Scene([Background, ScenePalette, Sprite, SecondScenePalette, SecondSprite]));
        // The shared palette sits at slot 0; the resident sound effects occupy the tail, from 0x55.
        byte[]?[] globals = new byte[VirtualFileSystem.ResidentSoundLastEntry + 1][];
        globals[0] = SharedPalette;
        globals[VirtualFileSystem.ResidentSoundFirstEntry] = ResidentSound;
        File.WriteAllBytes(Path.Combine(Root, "RESOURCE.000"), SyntheticArchives.HmGlobal(globals));
        File.WriteAllBytes(Path.Combine(Root, "RESOURCE.M01"), SyntheticArchives.HmAudio([new byte[44100], new byte[22050]]));
        File.WriteAllBytes(Path.Combine(Root, "RESOURCE.S01"), SyntheticArchives.HmAudio([new byte[11025]]));
        File.WriteAllBytes(Path.Combine(Root, "RESOURCE.001"), SyntheticArchives.HmAudio([new byte[2205]]));
        File.WriteAllBytes(Path.Combine(Root, "RESOURCE.002"), SyntheticArchives.HmAudio([new byte[8820]]));
        File.WriteAllBytes(Path.Combine(Root, "RESOURCE.004"), SyntheticArchives.HmAudio([new byte[800], new byte[900], new byte[1000]], slots: 4000));
        File.WriteAllBytes(Path.Combine(Root, "RESOURCE.003"), new byte[321]);
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}

public class HollywoodMonstersTests
{
    [Fact]
    public void GameDetector_DetectsHollywoodMonsters()
    {
        string dir = Directory.CreateTempSubdirectory("monsters-detect").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "Monsters.exe"), "dummy");
            Assert.Equal(GameVersion.HollywoodMonsters, GameDetector.Detect(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AppSettings_ManagesTheHollywoodMonstersPath()
    {
        var settings = new AppSettings { ActiveGame = GameVersion.HollywoodMonsters };
        settings.SetGameDir(GameVersion.HollywoodMonsters, @"C:\Games\Monsters");

        Assert.Equal(@"C:\Games\Monsters", settings.HollywoodMonstersDir);
        Assert.Equal(@"C:\Games\Monsters", settings.GetGameDir(GameVersion.HollywoodMonsters));
        Assert.Equal(@"C:\Games\Monsters", settings.ActiveGameDir);
    }

    // --- Palettes ---

    [Fact]
    public void IndexedPalette_AcceptsSixBitBlocksAndExpandsToTheFullRange()
    {
        IndexedPalette? full = IndexedPalette.TryParse(Palette(IndexedPalette.Colors));
        Assert.NotNull(full);
        Assert.Equal(IndexedPalette.Colors, full.DefinedCount);

        // Colour 21 is { r 21, g 42, b 63 }; 63 must expand to 255, not 252.
        var bgra = new byte[4];
        full.ToBgra(21, bgra, 0);
        Assert.Equal([255, (byte)((42 << 2) | (42 >> 4)), (byte)((21 << 2) | (21 >> 4)), 255], bgra);

        Assert.Null(IndexedPalette.TryParse(Palette(10)));                  // too small to be a palette
        Assert.Null(IndexedPalette.TryParse(new byte[528 + 1]));            // not whole triples
        Assert.False(IndexedPalette.IsPaletteBlock([.. Palette(176), 64])); // 64 is past 6 bits
    }

    [Fact]
    public void IndexedPalette_WithTail_PutsTheSharedColoursAtTheTopOfTheTable()
    {
        IndexedPalette scene = IndexedPalette.TryParse(Palette(176))!;
        IndexedPalette shared = IndexedPalette.TryParse(Palette(80, seed: 7))!;
        Assert.Equal(176, scene.DefinedCount);

        IndexedPalette merged = scene.WithTail(shared);
        Assert.Equal(IndexedPalette.Colors, merged.DefinedCount);
        Assert.Equal(scene.Bgra[..(176 * 4)].ToArray(), merged.Bgra[..(176 * 4)].ToArray());
        Assert.Equal(shared.Bgra[..(80 * 4)].ToArray(), merged.Bgra[(176 * 4)..].ToArray());
    }

    // --- Images ---

    [Fact]
    public void RasterDecoder_DetectsIndexedScreensByTheirExactSize()
    {
        byte[] screen = IndexedRaster();
        RasterInfo info = RasterDecoder.DetectIndexed(screen)!.Value;
        Assert.Equal((RasterDecoder.IndexedScreenWidth, RasterDecoder.IndexedScreenHeight), (info.Width, info.Height));

        Assert.Null(RasterDecoder.DetectIndexed(new byte[491_520 - 1]));
        Assert.Null(RasterDecoder.DetectIndexed(new byte[491_520 + 7]));

        IndexedPalette palette = IndexedPalette.TryParse(Palette(IndexedPalette.Colors))!;
        DecodedImage image = RasterDecoder.DecodeIndexed(screen, info.Width, info.Height, palette);
        var expected = new byte[4];
        palette.ToBgra(DefaultIndexedPixel(5, 6), expected, 0);
        Assert.Equal((expected[2], expected[1], expected[0], expected[3]), Pixel(image, 5, 6));
    }

    [Fact]
    public void SpriteDecoder_ParsesIndexedSpritesAndWalksEveryFrameByteExact()
    {
        using var install = new FakeHmInstall();

        SpriteAsset asset = SpriteAsset.Parse(install.Sprite, bytesPerPixel: 1)!;
        Assert.True(asset.IsIndexed);
        Assert.Null(asset.Verify());
        Assert.Equal(2, asset.FrameCount);
        Assert.Equal((10, 20, 2, 2), asset.Bounds); // the union of both frames

        IndexedPalette palette = IndexedPalette.TryParse(Palette(IndexedPalette.Colors))!;
        asset.Palette = palette;
        SpriteFrame frame = asset.DecodeFrame(0);
        Assert.Equal((10, 20), (frame.X, frame.Y));
        Assert.Equal((2, 2), (frame.Image!.Width, frame.Image.Height));

        var expected = new byte[4];
        palette.ToBgra(203, expected, 0);
        Assert.Equal((expected[2], expected[1], expected[0], expected[3]), Pixel(frame.Image, 1, 1));

        // The same bytes are not a valid RGB565 sprite: the pixel width is told, not guessed.
        Assert.Null(SpriteAsset.Parse(install.Sprite));
    }

    [Fact]
    public void OverlayDecoder_ParsesIndexedOverlays()
    {
        byte[] data = IndexedOverlay([(300, 40, [1, 2, 3]), (300, 41, [4, 5, 6])]);

        OverlayInfo info = OverlayDecoder.Parse(data, bytesPerPixel: 1)!;
        Assert.Equal((300, 40, 3, 2), (info.X, info.Y, info.Width, info.Height));
        Assert.Equal(1, info.BytesPerPixel);
        Assert.True(info.IsRectangular);

        // At two bytes per pixel the records no longer consume the entry exactly.
        Assert.Null(OverlayDecoder.Parse(data));

        IndexedPalette palette = IndexedPalette.TryParse(Palette(IndexedPalette.Colors))!;
        DecodedImage image = OverlayDecoder.Decode(data, info, palette);
        var expected = new byte[4];
        palette.ToBgra(5, expected, 0);
        Assert.Equal((expected[2], expected[1], expected[0], expected[3]), Pixel(image, 1, 1));
    }

    [Fact]
    public void EntryClassifier_TestsPaletteBlocksBeforeMasks()
    {
        using var install = new FakeHmInstall();

        static EntryClassification Classify(byte[] data) =>
            EntryClassifier.Classify(data, 1024, 480, GameVersion.HollywoodMonsters);

        // A block of 6-bit triples is also a whole number of 3-byte RLE mask runs, so it must be tested first.
        Assert.Equal(EntryKind.Data, Classify(install.ScenePalette).Kind);
        Assert.Equal(EntryKind.Background, Classify(install.Background).Kind);
        Assert.Equal(EntryKind.Overlay, Classify(install.Overlay).Kind);
        Assert.Equal(EntryKind.Animation, Classify(install.Sprite).Kind);
        Assert.Equal(EntryKind.Data, Classify(new byte[37]).Kind);
    }

    // --- Containers ---

    [Fact]
    public void SceneArchive_NamesAndTheOneTablelessArchive()
    {
        Assert.True(SceneArchive.IsSceneArchiveName("RESOURCE.A02", GameVersion.HollywoodMonsters));
        Assert.True(SceneArchive.IsSceneArchiveName("RESOURCE.I02", GameVersion.HollywoodMonsters));
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.001", GameVersion.HollywoodMonsters));
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.004", GameVersion.HollywoodMonsters));
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.M01", GameVersion.HollywoodMonsters));

        long screen = (long)RasterDecoder.IndexedScreenWidth * RasterDecoder.IndexedScreenHeight;
        Assert.Equal(
            [new ArchiveEntry(0, 0, screen), new ArchiveEntry(1, screen, IndexedPalette.FullBlockBytes)],
            SceneArchive.ReadHeaderlessScreenEntries(screen + IndexedPalette.FullBlockBytes));
        Assert.Empty(SceneArchive.ReadHeaderlessScreenEntries(screen));
    }

    [Fact]
    public void AudioArchive_ReadsTheTableLengthFromSlotOne()
    {
        byte[] archive = SyntheticArchives.HmAudio([new byte[1000], new byte[500], new byte[250]]);

        List<AudioArchiveEntry> entries = AudioArchive.ReadHollywoodMonstersEntries(archive, archive.Length);
        Assert.Equal([1000, 500, 250], entries.Select(e => e.Size));
        Assert.Equal([400, 1400, 1900], entries.Select(e => e.Offset));
        Assert.All(entries, e => Assert.Equal(AudioFormat.RawPcm, e.Format));

        // The stale bytes past the terminator defeat the Runaway 1 reader, which is why this one exists.
        Assert.Empty(AudioArchive.ReadAudioEntries(archive, archive.Length));
    }

    [Fact]
    public void GlobalArchive_ReadsTheSingleByteHollywoodMonstersHeader()
    {
        byte[] block = Palette(80);
        byte[] archive = SyntheticArchives.HmGlobal([block]);

        List<ArchiveEntry> entries = GlobalArchive.ReadEntries(archive, archive.Length, GameVersion.HollywoodMonsters);
        Assert.Equal([new ArchiveEntry(0, GlobalArchive.TableEndHm, block.Length)], entries);

        // The same bytes read as a Runaway 1 archive find nothing: the header sizes do not line up.
        Assert.Empty(GlobalArchive.ReadEntries(archive, archive.Length));
    }

    // --- The tree ---

    [Fact]
    public void VirtualFileSystem_BuildsAHollywoodMonstersTree()
    {
        using var install = new FakeHmInstall();
        VirtualFileSystem vfs = VirtualFileSystem.Init(install.Root);

        Assert.Equal(GameVersion.HollywoodMonsters, vfs.GameVersion);
        Assert.Equal(
            [VirtualFileSystem.ScenesFolder, VirtualFileSystem.MusicFolder, VirtualFileSystem.AmbientFolder,
             VirtualFileSystem.CinematicFolder, VirtualFileSystem.VoiceFolder, VirtualFileSystem.DialogueFolder,
             VirtualFileSystem.GlobalFolder],
            vfs.Root.Children.Select(c => c.Name));

        Assert.Equal(["RESOURCE.A02", "RESOURCE.A03"], vfs.Root.Children[0].Children.Select(c => c.Name));

        FsNode scene = vfs.Root.Children[0].Children[0];
        Assert.Equal(
            [EntryKind.Background, EntryKind.Data, EntryKind.Overlay, EntryKind.Animation],
            scene.Children.Select(c => c.Kind));
        Assert.Equal((2, 2, 2, 3), (vfs.Summary.Backgrounds, vfs.Summary.Overlays, vfs.Summary.Animations, vfs.Summary.DataEntries));
        Assert.Equal(0, vfs.Summary.Videos);
    }

    [Fact]
    public void VirtualFileSystem_CompletesScenePalettesFromTheSharedBlock()
    {
        using var install = new FakeHmInstall();
        VirtualFileSystem vfs = VirtualFileSystem.Init(install.Root);

        FsNode scene = vfs.Root.Children[0].Children[0];
        IndexedPalette palette = vfs.ScenePaletteFor(scene.Children[3])!;

        Assert.Equal(IndexedPalette.Colors, palette.DefinedCount);
        IndexedPalette shared = IndexedPalette.TryParse(install.SharedPalette)!;
        Assert.Equal(
            shared.Bgra[..(FakeHmInstall.SharedColors * 4)].ToArray(),
            palette.Bgra[(FakeHmInstall.SceneColors * 4)..].ToArray());

        // The characters' colours live at the top of the table: without the tail they would all be black.
        var bgra = new byte[4];
        palette.ToBgra(200, bgra, 0);
        Assert.NotEqual([0, 0, 0, 255], bgra);
    }

    [Fact]
    public void VirtualFileSystem_GivesEachEntryTheNearestPrecedingPalette()
    {
        using var install = new FakeHmInstall();
        VirtualFileSystem vfs = VirtualFileSystem.Init(install.Root);

        FsNode scene = vfs.Root.Children[0].Children[1];
        Assert.Equal("RESOURCE.A03", scene.Name);

        FsNode Entry(int index) => scene.Children.First(c => c.EntryIndex == index);

        IndexedPalette first = IndexedPalette.TryParse(install.ScenePalette)!;
        IndexedPalette second = IndexedPalette.TryParse(install.SecondScenePalette)!;
        static byte[] Bottom(IndexedPalette p) => p.Bgra[..(FakeHmInstall.SceneColors * 4)].ToArray();

        // e02 sits between the two blocks and takes the first; e04 follows the second and takes it.
        Assert.Equal(Bottom(first), Bottom(vfs.ScenePaletteFor(Entry(2))!));
        Assert.Equal(Bottom(second), Bottom(vfs.ScenePaletteFor(Entry(4))!));
        Assert.NotEqual(Bottom(first), Bottom(second));

        // The background precedes every block, so it takes the first one; so does the archive node itself.
        Assert.Equal(Bottom(first), Bottom(vfs.ScenePaletteFor(Entry(0))!));
        Assert.Equal(Bottom(first), Bottom(vfs.ScenePaletteFor(scene)!));

        // Both are still completed with the shared tail.
        IndexedPalette shared = IndexedPalette.TryParse(install.SharedPalette)!;
        byte[] tail = shared.Bgra[..(FakeHmInstall.SharedColors * 4)].ToArray();
        Assert.Equal(tail, vfs.ScenePaletteFor(Entry(2))!.Bgra[(FakeHmInstall.SceneColors * 4)..].ToArray());
        Assert.Equal(tail, vfs.ScenePaletteFor(Entry(4))!.Bgra[(FakeHmInstall.SceneColors * 4)..].ToArray());
    }

    [Fact]
    public void VirtualFileSystem_RoutesEachAudioArchiveToItsFolderAndPcmShape()
    {
        using var install = new FakeHmInstall();
        VirtualFileSystem vfs = VirtualFileSystem.Init(install.Root);

        FsNode Folder(string name) => vfs.Root.Children.First(c => c.Name == name);

        FsNode music = Folder(VirtualFileSystem.MusicFolder).Children.Single();
        Assert.Equal("RESOURCE.M01", music.Name);
        Assert.Equal(2, music.Children.Count);
        Assert.Equal(EntryKind.Music, music.Children[0].Kind);
        Assert.Equal(
            (11_025, 1, 16),
            (music.Children[0].Audio!.SampleRate, music.Children[0].Audio!.Channels, music.Children[0].Audio!.BitsPerSample));

        // RESOURCE.001 and RESOURCE.S01 are both ambient: 8-bit at the 11,025 Hz effect rate.
        FsNode ambient = Folder(VirtualFileSystem.AmbientFolder);
        Assert.Equal(["RESOURCE.001", "RESOURCE.S01"], ambient.Children.Select(c => c.Name).Order());
        Assert.Equal(
            (11_025, 8),
            (ambient.Children[0].Children[0].Audio!.SampleRate, ambient.Children[0].Children[0].Audio!.BitsPerSample));

        Assert.Single(Folder(VirtualFileSystem.CinematicFolder).Children.Single().Children);

        // RESOURCE.004 is the voice bank, not the lip-sync table it is in Runaway 1.
        FsNode voice = Folder(VirtualFileSystem.VoiceFolder);
        Assert.Equal(3, vfs.Summary.VoiceClips);

        // Voice runs at twice the effect rate, so it carries its own PCM shape rather than sharing one.
        Assert.Equal(
            (22_050, 8),
            (voice.Children[0].Children[0].Audio!.SampleRate, voice.Children[0].Children[0].Audio!.BitsPerSample));

        // RESOURCE.003 is the script, and it belongs under Dialogue rather than with the global data.
        Assert.Contains(Folder(VirtualFileSystem.DialogueFolder).Children, c => c.Name == "RESOURCE.003");
        Assert.DoesNotContain(Folder(VirtualFileSystem.GlobalFolder).Children, c => c.Name == "RESOURCE.003");
        Assert.Equal(5, vfs.Summary.AudioClips); // 2 music + 1 sfx + 1 ambient + 1 cinematic
    }

    [Fact]
    public void VirtualFileSystem_PlaysTheResidentSoundEffectsInTheTailOfResource000()
    {
        using var install = new FakeHmInstall();
        VirtualFileSystem vfs = VirtualFileSystem.Init(install.Root);

        FsNode archive = vfs.Root.Children.Single(c => c.Name == VirtualFileSystem.GlobalFolder)
                                 .Children.Single(c => c.Name == "RESOURCE.000");

        // The palette block at slot 0 stays data; the tail slots are playable at the sound-effect shape.
        FsNode palette = archive.Children.Single(c => c.EntryIndex == 0);
        Assert.Equal(EntryKind.GlobalData, palette.Kind);
        Assert.Null(palette.Audio);

        FsNode effect = archive.Children.Single(c => c.EntryIndex == VirtualFileSystem.ResidentSoundFirstEntry);
        Assert.Equal(EntryKind.Ambient, effect.Kind);
        Assert.Equal((11_025, 1, 8), (effect.Audio!.SampleRate, effect.Audio.Channels, effect.Audio.BitsPerSample));
    }

    private static byte[] ScriptFile() => SyntheticArchives.HmScript(new Dictionary<int, (string[], (string, int)[])>
    {
        [101] = (["  llamador", " puerta"],
                 [("Bien, ya estamos en la mansión Hannover.", 2227),
                  ("Conducen a la mansión.", 2228),
                  ("Está vacía, no sé dónde andará el perro.", 0)]),
        [702] = ([], [("Una sala sin etiquetas.", 91)]),
    });

    [Fact]
    public void HollywoodScript_DeciphersRowsAndPairsLinesWithTheirVoiceClips()
    {
        using var stream = new MemoryStream(ScriptFile());
        List<HollywoodScript.Stage> stages = HollywoodScript.ReadStages(stream);

        Assert.Equal([101, 702], stages.Select(s => s.Index));
        HollywoodScript.Stage first = stages[0];
        Assert.Equal(1010, first.SceneNumber);

        // Labels keep their leading spaces: the game stores them padded and the explorer shows them as stored.
        Assert.Equal(["  llamador", " puerta"], first.Labels.Select(l => l.Text));
        Assert.Equal([1, 2], first.Labels.Select(l => l.Id));

        // Line ids start at 500, and the accented text proves CP850 rather than Windows-1252 decoding.
        Assert.Equal([500, 501, 502], first.Lines.Select(l => l.Id));
        Assert.Equal("Bien, ya estamos en la mansión Hannover.", first.Lines[0].Text);
        Assert.Equal("Está vacía, no sé dónde andará el perro.", first.Lines[2].Text);

        // A cued line carries its clip; an uncued one says so rather than guessing a neighbour's.
        Assert.Equal([2227, 2228, -1], first.Lines.Select(l => l.VoiceClipId));
    }

    [Fact]
    public void HollywoodScript_AcceptsAStageThatCarriesNoLabels()
    {
        // Scene 7020 ships 12 lines and no labels at all, so labels cannot be what qualifies a stage.
        using var stream = new MemoryStream(ScriptFile());
        HollywoodScript.Stage stage = HollywoodScript.ReadStages(stream).Single(s => s.Index == 702);

        Assert.Empty(stage.Labels);
        Assert.Equal("Una sala sin etiquetas.", Assert.Single(stage.Lines).Text);
        Assert.Equal(91, stage.Lines[0].VoiceClipId);
    }

    [Fact]
    public void HollywoodScript_SkipsStageSlotsThatDoNotDecipherToText()
    {
        // Most of the 1,021 slots are zero or stale. A stale one points at real bytes that decipher to
        // noise, so pointing somewhere plausible must not be enough to be listed as a stage.
        byte[] data = ScriptFile();
        int slot = HollywoodScript.KeySize + 300 * 4;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(slot), (uint)(HollywoodScript.KeySize + 8));

        using var stream = new MemoryStream(data);
        Assert.Equal([101, 702], HollywoodScript.ReadStages(stream).Select(s => s.Index));
    }

    [Fact]
    public void VirtualFileSystem_ShowsTheScriptAsScenesOfLabelsAndLines()
    {
        using var install = new FakeHmInstall();
        File.WriteAllBytes(Path.Combine(install.Root, "RESOURCE.003"), ScriptFile());

        VirtualFileSystem vfs = VirtualFileSystem.Init(install.Root);

        FsNode script = vfs.Root.Children.Single(c => c.Name == VirtualFileSystem.DialogueFolder)
                                .Children.Single(c => c.Name == "RESOURCE.003");

        Assert.Equal(["scene 1010", "scene 7020"], script.Children.Select(c => c.Name));

        FsNode scene = script.Children[0];
        Assert.Equal(5, scene.Children.Count); // 2 labels + 3 lines
        Assert.All(scene.Children, c => Assert.Equal(EntryKind.Dialogue, c.Kind));

        FsNode line = scene.Children.Single(c => c.Name == "line 0500");
        Assert.Equal("Bien, ya estamos en la mansión Hannover.", line.Subtitle);
        Assert.Equal(2227, line.VoiceClipIndex);

        // The uncued line still shows, just without a clip to point at.
        Assert.Equal(-1, scene.Children.Single(c => c.Name == "line 0502").VoiceClipIndex);
        Assert.Equal(6, vfs.Summary.Phrases);
    }
}

/// <summary>
/// Hollywood Monsters' menu panel -- the save-slot list, the preview window and the four setting sliders
/// -- is the one screen-sized entry in its RESOURCE.000, at 1024x480 indexed pixels rather than the
/// Runaway games' RGB565.
/// </summary>
public class HollywoodMonstersInterfaceTests
{
    public const string HmDir = @"E:\Games\Juegos\Holywood Monsters\MONSTERS\Monsters";

    [Fact]
    public void RealInstall_TheMenuPanelIsTheOnlyScreenSizedGlobalEntry()
    {
        string path = Path.Combine(HmDir, "RESOURCE.000");
        if (!File.Exists(path)) return;

        using FileStream fs = File.OpenRead(path);
        List<ArchiveEntry> entries = GlobalArchive.ReadEntries(fs, GameVersion.HollywoodMonsters);

        const int width = 1024, height = 480;
        List<ArchiveEntry> screens = entries.Where(e => e.Size == (long)width * height).ToList();
        ArchiveEntry menu = Assert.Single(screens);
        Assert.Equal(42, menu.Index);

        var data = new byte[menu.Size];
        fs.Position = menu.Offset;
        fs.ReadExactly(data);
        RasterInfo geometry = Assert.IsType<RasterInfo>(RasterDecoder.DetectIndexed(data));
        Assert.Equal((width, height), (geometry.Width, geometry.Height));
    }

    [Fact]
    public void RealInstall_Hm_InventoryItemIcons()
    {
        string path = Path.Combine(HmDir, "RESOURCE.000");
        if (!File.Exists(path)) return;

        using FileStream fs = File.OpenRead(path);
        List<ArchiveEntry> entries = GlobalArchive.ReadEntries(fs, GameVersion.HollywoodMonsters);
        ArchiveEntry e43 = entries.Single(e => e.Index == 43);
        Assert.Equal(516_096, e43.Size);
        ArchiveEntry e49 = entries.Single(e => e.Index == 49);
        Assert.Equal(240, e49.Size);

        byte[] paletteBytes = new byte[e49.Size];
        fs.Position = e49.Offset;
        fs.ReadExactly(paletteBytes);
        IndexedPalette? pal = IndexedPalette.TryParse(paletteBytes);
        Assert.NotNull(pal);
        Assert.Equal(80, pal.DefinedCount);

        byte[] iconData = new byte[e43.Size];
        fs.Position = e43.Offset;
        fs.ReadExactly(iconData);
        Assert.Equal(126 * 64 * 64, iconData.Length);
        
        // Check palette distribution
        int under176 = 0, overOrEqual176 = 0;
        foreach (byte b in iconData)
        {
            if (b == 0) continue; // transparent / background
            if (b < 176) under176++;
            else overOrEqual176++;
        }
        Assert.True(overOrEqual176 > 0 || under176 > 0);
        Assert.True(under176 == 0, $"under176={under176}, overOrEqual176={overOrEqual176}");

        var asset = new HollywoodItemIconsAsset(iconData)
        {
            Palette = IndexedPalette.Grayscale.WithTail(pal)
        };
        Assert.Equal(126, asset.FrameCount);
        Assert.Equal((0, 0, 64, 64), asset.Bounds);
        Assert.Null(asset.Verify());
        SpriteFrame frame0 = asset.DecodeFrame(0);
        Assert.NotNull(frame0.Image);
        Assert.Equal(64, frame0.Image.Width);
        Assert.Equal(64, frame0.Image.Height);

        EntryClassification c = EntryClassifier.Classify(iconData, null, null, GameVersion.HollywoodMonsters);
        Assert.Equal(EntryKind.Animation, c.Kind);
        Assert.Equal(126, c.Image?.Frames);
    }

    [Fact]
    public void Synthetic_Hm_InventoryItemIcons_DecodesAllFrames()
    {
        byte[] fakeData = new byte[HollywoodItemIconsAsset.TotalBytes];
        // Populate frame 0 with some pixels
        fakeData[0] = 180;
        var asset = new HollywoodItemIconsAsset(fakeData);
        Assert.Equal(126, asset.FrameCount);
        Assert.Equal((0, 0, 64, 64), asset.Bounds);
        Assert.Null(asset.Verify());

        SpriteFrame f0 = asset.DecodeFrame(0);
        Assert.NotNull(f0.Image);
        Assert.Equal(64, f0.Image.Width);
        Assert.Equal(64, f0.Image.Height);
        Assert.Equal(4096 * 4, f0.Image.Pixels.Length);
    }

    [Fact]
    public void RealInstall_IsCoveredByTheShippedScanCache()
    {
        if (!Directory.Exists(HmDir)) return;

        // Scanning adds whatever the shipped cache did not already answer for. Scanning again must add
        // nothing: that is what "the install is fully covered" means, and it needs no list of archives
        // here that could drift from the one the scan actually walks.
        ShippedCacheBuilder.AddInstall(HmDir);
        Assert.Equal(0, ShippedCacheBuilder.AddInstall(HmDir));
        Assert.True(ShippedCacheBuilder.Covers(Path.Combine(HmDir, "RESOURCE.000")),
            "The global archive should be in the shipped cache: classifying it cold is the slowest part of a first launch");
    }

    [Fact]
    public void RealInstall_HmScript_LoadsAllStagesAndLines()
    {
        string path = Path.Combine(HmDir, "RESOURCE.003");
        if (!File.Exists(path)) return;

        using FileStream f = File.OpenRead(path);
        List<HollywoodScript.Stage> stages = HollywoodScript.ReadStages(f, GameVersion.HollywoodMonsters);
        Assert.Equal(80, stages.Count);
        Assert.Equal(589, stages.Sum(s => s.Labels.Count));
        Assert.Equal(3430, stages.Sum(s => s.Lines.Count));
    }

    [Fact]
    public void RealInstall_Hm_ResourceI04_WorldMapMask()
    {
        string path = Path.Combine(HmDir, "RESOURCE.I04");
        if (!File.Exists(path)) return;

        byte[] fileBytes = File.ReadAllBytes(path);
        List<ArchiveEntry> entries = SceneArchive.ReadEntries(fileBytes, fileBytes.Length);
        Assert.True(entries.Count > 7);
        ArchiveEntry chunk7 = entries[7];
        Assert.Equal(3909, chunk7.Size);
        byte[] data = fileBytes.AsSpan((int)chunk7.Offset, (int)chunk7.Size).ToArray();
        MaskInfo? info = RleMaskDecoder.Detect(data, 1024, 480);
        Assert.NotNull(info);
        Assert.Equal(1024, info.Value.Width);
        Assert.Equal(480, info.Value.Height);

        MaskInfo? autoInfo = RleMaskDecoder.Detect(data);
        Assert.NotNull(autoInfo);
        Assert.Equal(1024, autoInfo.Value.Width);
        Assert.Equal(480, autoInfo.Value.Height);

        EntryClassification classification = EntryClassifier.Classify(data, null, null, GameVersion.HollywoodMonsters);
        Assert.Equal(EntryKind.Mask, classification.Kind);
    }

    [Fact]
    public void TravelScreenTiles_DetectAndClassifyAs200x93()
    {
        var tileBytes = new byte[200 * 93];
        RasterInfo? info = RasterDecoder.DetectIndexed(tileBytes);
        Assert.NotNull(info);
        Assert.Equal(200, info.Value.Width);
        Assert.Equal(93, info.Value.Height);

        EntryClassification c = EntryClassifier.Classify(tileBytes, null, null, GameVersion.HollywoodMonsters);
        Assert.Equal(EntryKind.Background, c.Kind);
        Assert.Equal(200, c.Image!.Width);
        Assert.Equal(93, c.Image.Height);

        // Verify label formatting
        var node = new FsNode
        {
            Name = "e02",
            EntryIndex = 2,
            Kind = EntryKind.Background,
            ArchivePath = "RESOURCE.I04",
            Image = new ImageInfo { Width = 200, Height = 93 },
        };
        Assert.Equal("travel destination tile 1 200×93 (e02)", SceneCatalog.FormatSceneEntryLabel(node, "en"));
        Assert.Equal("miniatura de destino 1 200×93 (e02)", SceneCatalog.FormatSceneEntryLabel(node, "es"));
    }

    [Fact]
    public void RealInstall_Hm_ResourceI04_TravelTiles()
    {
        string path = Path.Combine(HmDir, "RESOURCE.I04");
        if (!File.Exists(path)) return;

        byte[] fileBytes = File.ReadAllBytes(path);
        List<ArchiveEntry> entries = SceneArchive.ReadEntries(fileBytes, fileBytes.Length);
        Assert.True(entries.Count >= 7);

        for (int i = 2; i <= 6; i++)
        {
            ArchiveEntry tileEntry = entries[i];
            Assert.Equal(200 * 93, tileEntry.Size);
            byte[] data = fileBytes.AsSpan((int)tileEntry.Offset, (int)tileEntry.Size).ToArray();
            RasterInfo? info = RasterDecoder.DetectIndexed(data);
            Assert.NotNull(info);
            Assert.Equal(200, info.Value.Width);
            Assert.Equal(93, info.Value.Height);
        }
    }

    [Fact]
    public void BottomActionPanel_DetectAndClassifyAs1024x189()
    {
        var panelBytes = new byte[1024 * 189];
        RasterInfo? info = RasterDecoder.DetectIndexed(panelBytes);
        Assert.NotNull(info);
        Assert.Equal(1024, info.Value.Width);
        Assert.Equal(189, info.Value.Height);

        EntryClassification c = EntryClassifier.Classify(panelBytes, null, null, GameVersion.HollywoodMonsters);
        Assert.Equal(EntryKind.Background, c.Kind);
        Assert.Equal(1024, c.Image!.Width);
        Assert.Equal(189, c.Image.Height);
    }

    [Fact]
    public void RealInstall_Hm_Resource000_Entries47And48()
    {
        string path = Path.Combine(HmDir, "RESOURCE.000");
        if (!File.Exists(path)) return;

        byte[] fileBytes = File.ReadAllBytes(path);
        List<ArchiveEntry> entries = GlobalArchive.ReadEntries(fileBytes, fileBytes.Length, GameVersion.HollywoodMonsters);

        ArchiveEntry e47 = entries.Single(e => e.Index == 47);
        Assert.Equal(193_536, e47.Size); // 1024 * 189
        byte[] d47 = fileBytes.AsSpan((int)e47.Offset, (int)e47.Size).ToArray();
        RasterInfo? info47 = RasterDecoder.DetectIndexed(d47);
        Assert.NotNull(info47);
        Assert.Equal(1024, info47.Value.Width);
        Assert.Equal(189, info47.Value.Height);

        ArchiveEntry e48 = entries.Single(e => e.Index == 48);
        Assert.Equal(28_800, e48.Size); // 640 * 45
        byte[] d48 = fileBytes.AsSpan((int)e48.Offset, (int)e48.Size).ToArray();
        RasterInfo? info48 = RasterDecoder.DetectIndexed(d48);
        Assert.NotNull(info48);
        Assert.Equal(640, info48.Value.Width);
        Assert.Equal(45, info48.Value.Height);
    }

    [Fact]
    public void AttributeTable1792_Parses7PagesCorrectly()
    {
        byte[] walk = new byte[256];
        walk[10] = 1; walk[20] = 2;
        byte[] items = new byte[256];
        items[10] = 3; items[30] = 5;
        byte[] depth = new byte[256];
        depth[10] = 4;
        byte[] delta = new byte[256];
        delta[10] = 1;
        byte[] adjust = new byte[256];
        adjust[10] = 2;
        byte[] footstep = new byte[256];
        footstep[10] = 7;
        byte[] remap = new byte[256];
        remap[10] = 15;

        byte[] tableData = SyntheticAssets.HollywoodAttributeTable(walk, items, depth, delta, adjust, footstep, remap);
        Assert.Equal(HollywoodAttributeTableDecoder.TotalBytes, tableData.Length);
        Assert.True(HollywoodAttributeTableDecoder.IsAttributeTable(tableData));

        HollywoodAttributeTable table = HollywoodAttributeTableDecoder.Parse(tableData);
        Assert.Equal(1, table.GetWalkRegion(10));
        Assert.Equal(2, table.GetWalkRegion(20));
        Assert.Equal(3, table.GetSceneItem(10));
        Assert.Equal(5, table.GetSceneItem(30));
        Assert.Equal(4, table.GetActorDepthClass(10));
        Assert.Equal(1, table.GetPaletteDeltaClass(10));
        Assert.Equal(2, table.GetPaletteAdjustmentClass(10));
        Assert.Equal(7, table.GetFootstepSound(10));
        Assert.Equal(15, table.GetPresentationRemap(10));

        var counts = table.NonZeroCount();
        Assert.Equal(2, counts.Walk);
        Assert.Equal(2, counts.Items);
        Assert.Equal(1, counts.Depth);
        Assert.Equal(1, counts.Light);
        Assert.Equal(1, counts.Adjust);
        Assert.Equal(1, counts.Footstep);
        Assert.Equal(1, counts.Remap);

        // Verify label formatting
        var node = new FsNode { Name = "e03", Size = 1792, Kind = EntryKind.Data };
        Assert.Equal("scene attribute lookup pages (7 pages) (e03)", SceneCatalog.FormatSceneEntryLabel(node, "en"));
        Assert.Equal("páginas de atributos de escena (7 páginas) (e03)", SceneCatalog.FormatSceneEntryLabel(node, "es"));
    }

    [Fact]
    public void SceneMetadata_ParsesItemsAndActionHandlers()
    {
        var items = new List<(short InterX, short InterY, short AppX, short AppY, byte Facing, ushort ActionHandlerId)>
        {
            (100, 200, 110, 210, 2, 401),
            (300, 400, 310, 410, 4, 402),
        };

        byte[] metaData = SyntheticAssets.HollywoodSceneMetadata(items);
        Assert.Equal(Formats.HollywoodSceneMetadata.MinSize, metaData.Length);
        Assert.True(Formats.HollywoodSceneMetadata.IsSceneMetadata(metaData));

        Formats.HollywoodSceneMetadata parsed = Formats.HollywoodSceneMetadata.Parse(metaData);
        Assert.Equal(21, parsed.Items.Count);

        HollywoodSceneItem item0 = parsed.Items[0];
        Assert.Equal(100, item0.InteractionPoint.X);
        Assert.Equal(200, item0.InteractionPoint.Y);
        Assert.Equal(110, item0.ApproachPoint.X);
        Assert.Equal(210, item0.ApproachPoint.Y);
        Assert.Equal(2, item0.Facing);
        Assert.Equal(8, item0.VerbActions.Count);
        Assert.True(item0.VerbActions[0].HasHandler);
        Assert.Equal(401, item0.VerbActions[0].ActionHandlerId);

        HollywoodSceneItem item1 = parsed.Items[1];
        Assert.Equal(300, item1.InteractionPoint.X);
        Assert.Equal(400, item1.InteractionPoint.Y);
        Assert.Equal(402, item1.VerbActions[0].ActionHandlerId);

        // Verify label formatting
        var node = new FsNode { Name = "e04", EntryIndex = 4, Size = 15000, Kind = EntryKind.Data };
        Assert.Equal("scene metadata & interactions (e04)", SceneCatalog.FormatSceneEntryLabel(node, "en"));
        Assert.Equal("metadatos y acciones de escena (e04)", SceneCatalog.FormatSceneEntryLabel(node, "es"));
    }

    [Fact]
    public void FrankensteinReward_ValidatesFrameSequenceAndParts()
    {
        Assert.Equal(36, HollywoodFrankensteinReward.FrameCount);
        Assert.Equal(3, HollywoodFrankensteinReward.PartCount);

        Assert.Equal(0, HollywoodFrankensteinReward.GetFrameAtStep(0));
        Assert.Equal(18, HollywoodFrankensteinReward.GetFrameAtStep(24));
        Assert.Equal(0, HollywoodFrankensteinReward.GetFrameAtStep(35));

        Assert.True(HollywoodFrankensteinReward.IsFrankensteinPart(0x30));
        Assert.True(HollywoodFrankensteinReward.IsFrankensteinPart(0x42));
        Assert.True(HollywoodFrankensteinReward.IsFrankensteinPart(0x4c));
        Assert.False(HollywoodFrankensteinReward.IsFrankensteinPart(0x01));
    }

    [Fact]
    public void RealInstall_ResidentSoundEffects()
    {
        string path = @"E:\Games\Juegos\Holywood Monsters\MONSTERS\Monsters\RESOURCE.000";
        if (!File.Exists(path)) return;

        byte[] bytes = File.ReadAllBytes(path);
        List<ArchiveEntry> entries = GlobalArchive.ReadEntries(bytes, bytes.Length, GameVersion.HollywoodMonsters);
        var resident = entries.Where(e => e.Index >= VirtualFileSystem.ResidentSoundFirstEntry && e.Index <= VirtualFileSystem.ResidentSoundLastEntry).ToList();
        Assert.Equal(14, resident.Count);

        // Check content equality
        var sound0x55 = bytes.AsSpan((int)resident.First(e => e.Index == 0x55).Offset, (int)resident.First(e => e.Index == 0x55).Size).ToArray();
        var sound0x59 = bytes.AsSpan((int)resident.First(e => e.Index == 0x59).Offset, (int)resident.First(e => e.Index == 0x59).Size).ToArray();
        var sound0x5d = bytes.AsSpan((int)resident.First(e => e.Index == 0x5d).Offset, (int)resident.First(e => e.Index == 0x5d).Size).ToArray();
        Assert.Equal(sound0x55, sound0x59);
        Assert.Equal(sound0x55, sound0x5d);

        var sound0x57 = bytes.AsSpan((int)resident.First(e => e.Index == 0x57).Offset, (int)resident.First(e => e.Index == 0x57).Size).ToArray();
        var sound0x5a = bytes.AsSpan((int)resident.First(e => e.Index == 0x5a).Offset, (int)resident.First(e => e.Index == 0x5a).Size).ToArray();
        var sound0x5e = bytes.AsSpan((int)resident.First(e => e.Index == 0x5e).Offset, (int)resident.First(e => e.Index == 0x5e).Size).ToArray();
        var sound0x61 = bytes.AsSpan((int)resident.First(e => e.Index == 0x61).Offset, (int)resident.First(e => e.Index == 0x61).Size).ToArray();
        Assert.Equal(sound0x57, sound0x5a);
        Assert.Equal(sound0x57, sound0x5e);
        Assert.Equal(sound0x57, sound0x61);

        // Check sizes:
        var sizes = resident.ToDictionary(e => e.Index, e => e.Size);
        Assert.Equal(14, sizes.Count);
        // Let's verify sizes of groups
        Assert.Equal(sizes[0x55], sizes[0x59]);
        Assert.Equal(sizes[0x55], sizes[0x5d]);
        Assert.Equal(sizes[0x57], sizes[0x5a]);
        Assert.Equal(sizes[0x57], sizes[0x5e]);
        Assert.Equal(sizes[0x57], sizes[0x61]);
    }

    [Fact]
    public void HollywoodCursorDecoder_DecodesFallbackDibsAndAnimates()
    {
        Assert.Equal(6, HollywoodCursorDecoder.FallbackDibs.Count);

        for (int i = 0; i < HollywoodCursorDecoder.FallbackDibs.Count; i++)
        {
            byte[] dib = HollywoodCursorDecoder.FallbackDibs[i];
            Assert.Equal(HollywoodCursorDecoder.DibByteSize, dib.Length);

            DecodedImage img = HollywoodCursorDecoder.DecodeDib(dib, out int hx, out int hy);
            Assert.Equal(32, img.Width);
            Assert.Equal(32, img.Height);
            Assert.Equal(16, hx);
            Assert.Equal(16, hy);
            Assert.Equal(32 * 32 * 4, img.Pixels.Length);

            // Verify mix of transparent and opaque pixels
            bool hasTransparent = false;
            bool hasOpaque = false;
            for (int p = 0; p < img.Pixels.Length; p += 4)
            {
                if (img.Pixels[p + 3] == 0) hasTransparent = true;
                if (img.Pixels[p + 3] == 255) hasOpaque = true;
            }
            Assert.True(hasTransparent, $"Frame {i} should have transparent pixels");
            Assert.True(hasOpaque, $"Frame {i} should have opaque pixels");
        }

        // Animated asset
        HollywoodCursorAsset asset = HollywoodCursorDecoder.CreateFallbackAsset();
        Assert.Equal(10, asset.FrameCount);
        Assert.Equal((0, 0, 32, 32), asset.Bounds);
        Assert.Equal(16, asset.HotspotX);
        Assert.Equal(16, asset.HotspotY);
        Assert.Null(asset.Verify());

        for (int f = 0; f < 10; f++)
        {
            SpriteFrame frame = asset.DecodeFrame(f);
            Assert.NotNull(frame.Image);
            Assert.Equal(32, frame.Image.Width);
            Assert.Equal(32, frame.Image.Height);
        }

        // APNG export test
        using var ms = new MemoryStream();
        ApngWriter.Write(asset, ms, 13.33);
        Assert.True(ms.Length > 0);
        byte[] apngBytes = ms.ToArray();
        Assert.True(PngDecoder.IsPng(apngBytes));
    }

    [Fact]
    public void HollywoodResidentSound_LabelsAndCanonicalNames()
    {
        Assert.Equal("Footstep (Wood A)", SceneCatalog.GetHollywoodResidentSoundName(0x55, "en"));
        Assert.Equal("Pisada (Madera A)", SceneCatalog.GetHollywoodResidentSoundName(0x55, "es"));

        Assert.Equal("Footstep (Stone A)", SceneCatalog.GetHollywoodResidentSoundName(0x57, "en"));
        Assert.Equal("Pisada (Piedra A)", SceneCatalog.GetHollywoodResidentSoundName(0x57, "es"));

        Assert.Equal("Door Latch / Click", SceneCatalog.GetHollywoodResidentSoundName(0x58, "en"));
        Assert.Equal("Cerrojo / Clic", SceneCatalog.GetHollywoodResidentSoundName(0x58, "es"));

        Assert.Equal("Inventory Open", SceneCatalog.GetHollywoodResidentSoundName(0x60, "en"));
        Assert.Equal("Abrir inventario", SceneCatalog.GetHollywoodResidentSoundName(0x60, "es"));
    }

    [Fact]
    public void SmackerVideo_ParsesHeader_Correctly()
    {
        byte[] smk4 = SyntheticSmackerVideo(width: 640, height: 480, frames: 150, fps: 15, smk4: true, audioSampleRate: 22050, is16Bit: true, stereo: false);
        Assert.True(SmackerVideo.IsSmacker(smk4));

        SmackerVideo? video = SmackerVideo.TryParse(smk4);
        Assert.NotNull(video);
        Assert.Equal("SMK4", video.Signature);
        Assert.Equal(640, video.Width);
        Assert.Equal(480, video.Height);
        Assert.Equal(150, video.TotalFrames);
        Assert.Equal(15.0, video.FramesPerSecond, 0.5);
        Assert.Equal(10.0, video.DurationSeconds, 0.5);
        Assert.Single(video.AudioTracks);
        Assert.Equal(22050, video.AudioTracks[0].SampleRate);
        Assert.Equal(16, video.AudioTracks[0].BitsPerSample);
        Assert.Equal(1, video.AudioTracks[0].Channels);

        string summary = video.FormatSummary();
        Assert.Contains("640×480", summary);
        Assert.Contains("22050 Hz", summary);

        // Test SMK2
        byte[] smk2 = SyntheticSmackerVideo(width: 320, height: 240, frames: 60, fps: 12, smk4: false);
        Assert.True(SmackerVideo.IsSmacker(smk2));
        SmackerVideo? v2 = SmackerVideo.TryParse(smk2);
        Assert.NotNull(v2);
        Assert.Equal("SMK2", v2.Signature);
        Assert.Equal(320, v2.Width);
        Assert.Equal(240, v2.Height);
    }

    [Fact]
    public void VirtualFileSystem_DiscoversAndRestoresHollywoodMonstersSmackerVideo()
    {
        using var install = new FakeHmInstall();
        // Add a Smacker video to the install root
        byte[] smkBytes = SyntheticSmackerVideo(640, 480, 45, 15);
        string smkPath = Path.Combine(install.Root, "Intro.smk");
        File.WriteAllBytes(smkPath, smkBytes);

        VirtualFileSystem vfs = VirtualFileSystem.Init(install.Root);
        Assert.Equal(1, vfs.Summary.Videos);

        FsNode videoFolder = vfs.Root.Children.Single(c => c.Name == VirtualFileSystem.VideoFolder);
        FsNode videoNode = Assert.Single(videoFolder.Children, c => c.Name == "Intro.smk");
        Assert.Equal(EntryKind.Video, videoNode.Kind);
        Assert.NotNull(videoNode.Image);
        Assert.Equal(640, videoNode.Image.Width);
        Assert.Equal(480, videoNode.Image.Height);

        using var ms = new MemoryStream();
        Assert.True(vfs.RestoreVideo(videoNode, ms));
        Assert.Equal(smkBytes.Length, ms.Length);
        Assert.Equal(smkBytes, ms.ToArray());
    }
}
