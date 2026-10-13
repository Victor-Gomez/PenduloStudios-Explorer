using System.Buffers.Binary;
using RunawayExplorer.Core.FileSystem;
using RunawayExplorer.Core.Formats;
using Xunit;

namespace RunawayExplorer.Core.Tests;

/// <summary>
/// <em>Runaway: A Road Adventure</em>'s character sprite library (<c>Resource.001</c>) -- the one format
/// only this game has. Everything else Runaway 1 does is the family default and is tested with the
/// decoder that implements it.
/// </summary>
public class Runaway1CharacterLibraryTests
{
    private const string R1SteamDir = @"F:\Games\Steam\steamapps\common\Runaway A Road Adventure";

    private static string LibraryPath => Path.Combine(R1SteamDir, "Resource", "RESOURCE.001");

    private static string GlobalPath => Path.Combine(R1SteamDir, "Resource", "RESOURCE.000");

    private static byte[] OneEntry(int frames = 3) =>
        SyntheticAssets.CharacterEntry(
            [.. Enumerable.Range(0, frames).Select(i => SyntheticAssets.SolidCharacterFrame(40 + i, 30, 12, 9, (byte)(7 + i)))]);

    // ------------------------------------------------------------------------------------------
    // The container
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void EntriesSpanTheirFrameTableAsWellAsTheirData()
    {
        byte[] entry = OneEntry();
        byte[] file = SyntheticAssets.CharacterLibrary(new Dictionary<int, byte[]> { [0] = entry, [5] = entry });

        List<ArchiveEntry> entries = CharacterSpriteArchive.ReadEntries(file, file.Length);

        Assert.Equal(2, entries.Count);
        Assert.Equal([0, 5], entries.Select(e => e.Index));
        // The slot's size counts frame data only; the entry occupies 3,200 bytes more than that.
        Assert.All(entries, e => Assert.Equal(entry.Length, e.Size));
        Assert.Equal(CharacterSpriteArchive.TableEnd, entries[0].Offset);
    }

    [Fact]
    public void AFileWhoseEntriesDoNotTileItIsNotTheLibrary()
    {
        byte[] entry = OneEntry();
        byte[] file = SyntheticAssets.CharacterLibrary(new Dictionary<int, byte[]> { [0] = entry });

        // One byte of slack at the end and the tiling no longer closes on the last byte.
        Assert.Empty(CharacterSpriteArchive.ReadEntries(file, file.Length + 1));
        // Sizing the entries without their frame tables leaves a gap the size of one table.
        byte[] shrunk = (byte[])file.Clone();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            shrunk.AsSpan(CharacterSpriteArchive.SlotCount * 4),
            (uint)(entry.Length - CharacterSpriteAsset.FrameTableSize - 1));
        Assert.Empty(CharacterSpriteArchive.ReadEntries(shrunk, shrunk.Length));
    }

    [Fact]
    public void TheLibraryIsThreeBlocksOfSeventyTwoSlotsOnePerPalette()
    {
        Assert.Equal(11, CharacterSpriteArchive.PaletteSlotFor(0));
        Assert.Equal(11, CharacterSpriteArchive.PaletteSlotFor(71));
        Assert.Equal(45, CharacterSpriteArchive.PaletteSlotFor(72));
        Assert.Equal(45, CharacterSpriteArchive.PaletteSlotFor(143));
        Assert.Equal(79, CharacterSpriteArchive.PaletteSlotFor(144));
        Assert.Equal(79, CharacterSpriteArchive.PaletteSlotFor(215));
    }

    [Fact]
    public void OnlyRunaway1HasALibraryUnderThisName()
    {
        Assert.True(CharacterSpriteArchive.IsLibraryName("RESOURCE.001", GameVersion.Runaway1));
        Assert.True(CharacterSpriteArchive.IsLibraryName("Resource.001", GameVersion.Runaway1));
        // Hollywood Monsters' RESOURCE.001 is ambient audio, and Runaway 2's is a scene archive.
        Assert.False(CharacterSpriteArchive.IsLibraryName("RESOURCE.001", GameVersion.HollywoodMonsters));
        Assert.False(CharacterSpriteArchive.IsLibraryName("RESOURCE.001", GameVersion.Runaway2));
    }

    // ------------------------------------------------------------------------------------------
    // The frames
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void AnEntryParsesToItsFramesAndVerifiesClean()
    {
        CharacterSpriteAsset asset = Assert.IsType<CharacterSpriteAsset>(CharacterSpriteAsset.Parse(OneEntry(4)));

        Assert.Equal(4, asset.FrameCount);
        Assert.Null(asset.Verify());
        Assert.Equal(0, asset.Records[0].DataOffset);
        Assert.All(asset.Records, r => Assert.Equal(r.EdgeCountA + r.EdgeCountB, r.EdgeRunCount));
    }

    [Fact]
    public void RunCoordinatesArePositionsOnACanvasNotInsideTheBox()
    {
        CharacterSpriteAsset asset = CharacterSpriteAsset.Parse(OneEntry(3))!;

        // Frame i's runs start at x = 40 + i, and that is where the frame goes -- not at 0.
        for (int i = 0; i < asset.FrameCount; i++)
        {
            Assert.Equal((40 + i, 30), asset.OriginOf(i));
            Assert.Equal(40 + i, asset.DecodeFrame(i).X);
            Assert.Equal(30, asset.DecodeFrame(i).Y);
        }

        // The canvas is the union of the frames' extents, so it is wider than any single frame.
        Assert.Equal((40, 30, 14, 9), asset.Bounds);
    }

    [Fact]
    public void TheFeetLandOnOneCanvasPointThroughoutAnAnimation()
    {
        CharacterSpriteAsset asset = CharacterSpriteAsset.Parse(OneEntry(5))!;

        for (int i = 0; i < asset.FrameCount; i++)
        {
            (int x, int y) = asset.OriginOf(i);
            Assert.Equal(SyntheticAssets.CharacterFeetX, x + asset.Records[i].AnchorX);
            Assert.Equal(SyntheticAssets.CharacterFeetY, y + asset.Records[i].AnchorY);
        }
    }

    [Fact]
    public void TheBodyGivesTheColourAndTheCoverageStreamGivesTheAlpha()
    {
        byte[] entry = SyntheticAssets.CharacterEntry([SyntheticAssets.SolidCharacterFrame(10, 20, 6, 4, index: 3)]);
        CharacterSpriteAsset asset = CharacterSpriteAsset.Parse(entry)!;
        asset.Palette = IndexedPalette.TryParseRgb565(SyntheticAssets.Rgb565Palette());

        DecodedImage image = asset.DecodeFrame(0).Image!;

        // Rows 0..2 are body, fully covered by kind-0 runs: the palette colour at full alpha.
        (byte r, byte g, byte b, byte a) = SyntheticAssets.Pixel(image, 0, 0);
        Assert.Equal(255, a);
        Assert.True(r != 0 || g != 0 || b != 0);
        // The last row is coverage with no body behind it: the shadow, black and translucent.
        (byte sr, byte sg, byte sb, byte sa) = SyntheticAssets.Pixel(image, 0, 3);
        Assert.Equal((0, 0, 0), (sr, sg, sb));
        Assert.Equal(128 * CharacterSpriteAsset.ShadowAlpha / 255, sa);
    }

    [Fact]
    public void PuttingTheCoverageBytesThroughThePaletteIsNotWhatHappens()
    {
        // Coverage 128 against a palette whose entry 128 is a bright colour: if the byte were an index
        // the shadow row would take that colour. It stays black, at 128 scaled by the shadow strength.
        byte[] entry = SyntheticAssets.CharacterEntry([SyntheticAssets.SolidCharacterFrame(0, 0, 4, 3, index: 1)]);
        CharacterSpriteAsset asset = CharacterSpriteAsset.Parse(entry)!;
        asset.Palette = IndexedPalette.TryParseRgb565(SyntheticAssets.Rgb565Palette());

        (byte r, byte g, byte b, _) = SyntheticAssets.Pixel(asset.DecodeFrame(0).Image!, 1, 2);
        Assert.Equal((0, 0, 0), (r, g, b));
    }

    [Fact]
    public void AnEntryWhoseStreamsDoNotTileTheFrameIsRejected()
    {
        byte[] entry = OneEntry(2);
        // Claim one body run more than the frame holds: the streams then overrun their space.
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(4), 999);
        Assert.Null(CharacterSpriteAsset.Parse(entry));
    }

    [Fact]
    public void RunawayOnesPalettesAreRgb565NotSixBitTriples()
    {
        byte[] block = SyntheticAssets.Rgb565Palette();

        Assert.NotNull(IndexedPalette.TryParseRgb565(block));
        // The Hollywood Monsters reader must not claim it: 512 bytes is not a whole number of triples.
        Assert.Null(IndexedPalette.TryParse(block));
        Assert.Null(IndexedPalette.TryParseRgb565(SyntheticAssets.Palette(256)));
    }

    // ------------------------------------------------------------------------------------------
    // Against the real install
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void RealInstall_EveryEntryTilesTheFileAndEveryFrameWalksClean()
    {
        if (!File.Exists(LibraryPath)) return;

        using FileStream f = File.OpenRead(LibraryPath);
        List<ArchiveEntry> entries = CharacterSpriteArchive.ReadEntries(f);

        Assert.Equal(168, entries.Count);
        Assert.Equal(108_761_688, f.Length);

        int frames = 0;
        foreach (ArchiveEntry e in entries)
        {
            var data = new byte[e.Size];
            f.Position = e.Offset;
            f.ReadExactly(data);

            CharacterSpriteAsset asset = Assert.IsType<CharacterSpriteAsset>(CharacterSpriteAsset.Parse(data));
            Assert.Null(asset.Verify());
            frames += asset.FrameCount;
        }
        Assert.Equal(2166, frames);
    }

    [Fact]
    public void RealInstall_TheThreePalettesAreWhereTheLibraryExpectsThem()
    {
        if (!File.Exists(GlobalPath)) return;

        using FileStream f = File.OpenRead(GlobalPath);
        List<ArchiveEntry> entries = GlobalArchive.ReadEntries(f);
        var bySlot = entries.ToDictionary(e => e.Index);

        var tables = new Dictionary<int, IndexedPalette>();
        foreach (int slot in (int[])[11, 28, 45, 62, 79, 96])
        {
            ArchiveEntry e = bySlot[slot];
            Assert.Equal(IndexedPalette.Rgb565BlockBytes, e.Size);
            var block = new byte[e.Size];
            f.Position = e.Offset;
            f.ReadExactly(block);
            tables[slot] = Assert.IsType<IndexedPalette>(IndexedPalette.TryParseRgb565(block));
        }

        // Six slots, three tables: 11 = 28, 45 = 62, 79 = 96, and no two of the three are alike.
        static byte[] Bytes(IndexedPalette p) => p.Bgra.ToArray();
        Assert.Equal(Bytes(tables[11]), Bytes(tables[28]));
        Assert.Equal(Bytes(tables[45]), Bytes(tables[62]));
        Assert.Equal(Bytes(tables[79]), Bytes(tables[96]));
        Assert.NotEqual(Bytes(tables[11]), Bytes(tables[45]));
        Assert.NotEqual(Bytes(tables[11]), Bytes(tables[79]));
        Assert.NotEqual(Bytes(tables[45]), Bytes(tables[79]));
    }

    [Fact]
    public void RealInstall_EveryEntryMatchesItsOwnBlocksPaletteAndNoOther()
    {
        if (!File.Exists(LibraryPath) || !File.Exists(GlobalPath)) return;

        // The palette an entry wants is not stored, so the claim under test is the three-block layout.
        // Score each entry's index histogram against the three candidate palettes' own histograms: the
        // art drawn for one costume uses that costume's colours and barely touches the others'.
        var reference = new Dictionary<int, double[]>();
        var libraryHistograms = new Dictionary<int, double[]>();

        using (FileStream f = File.OpenRead(LibraryPath))
        {
            foreach (ArchiveEntry e in CharacterSpriteArchive.ReadEntries(f))
            {
                var data = new byte[e.Size];
                f.Position = e.Offset;
                f.ReadExactly(data);
                libraryHistograms[e.Index] = FirstFrameHistogram(CharacterSpriteAsset.Parse(data)!, data);
            }
        }

        foreach (int slot in (int[])[11, 45, 79])
            reference[slot] = libraryHistograms.First(kv => CharacterSpriteArchive.PaletteSlotFor(kv.Key) == slot).Value;

        Assert.Equal(168, libraryHistograms.Count);
        foreach ((int index, double[] histogram) in libraryHistograms)
        {
            int own = CharacterSpriteArchive.PaletteSlotFor(index);
            double mine = Cosine(histogram, reference[own]);
            foreach (int other in (int[])[11, 45, 79])
            {
                if (other == own)
                    continue;
                Assert.True(
                    mine > Cosine(histogram, reference[other]) + 0.5,
                    $"entry {index} is not clearly in block {own}: {mine:F3} against {Cosine(histogram, reference[other]):F3} for {other}");
            }
        }
    }

    [Fact]
    public void RealInstall_TheLibraryIsItsOwnFolderAndItsEntriesDecodeInColour()
    {
        if (!Directory.Exists(R1SteamDir)) return;

        var vfs = VirtualFileSystem.Init(R1SteamDir, cache: ScanCache.Ephemeral(includeShipped: true));

        FsNode characters = vfs.Root.Children.Single(c => c.Name == VirtualFileSystem.CharactersFolder);
        // The actor's own animations share this folder -- see Runaway1ActorTests.
        FsNode library = characters.Children.Single(c => c.Name.Equals("RESOURCE.001", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(168, library.Children.Count);
        Assert.All(library.Children, c => Assert.Equal(EntryKind.CharacterAnimation, c.Kind));
        Assert.Equal(168, vfs.Summary.CharacterAnimations);

        // One entry from each of the three blocks, decoded the way the viewer decodes it.
        foreach (int index in (int[])[0, 100, 200])
        {
            FsNode node = library.Children.Single(c => c.EntryIndex == index);
            IndexedPalette palette = Assert.IsType<IndexedPalette>(vfs.CharacterPaletteFor(node));

            CharacterSpriteAsset asset = Assert.IsType<CharacterSpriteAsset>(CharacterSpriteAsset.Parse(vfs.ReadBytes(node)));
            asset.Palette = palette;
            Assert.Null(asset.Verify());

            DecodedImage image = asset.DecodeFrame(0).Image!;
            // A character, not a silhouette: the body comes out in many colours at full alpha, and the
            // coverage mask reaches past it to leave a translucent shadow.
            var opaque = new HashSet<uint>();
            bool translucent = false;
            for (int i = 0; i < image.Width * image.Height; i++)
            {
                byte a = image.Pixels[i * 4 + 3];
                if (a == 255)
                    opaque.Add(BitConverter.ToUInt32(image.Pixels, i * 4));
                else if (a > 0)
                    translucent = true;
            }
            Assert.True(opaque.Count > 20, $"entry {index} decoded to {opaque.Count} opaque colours");
            Assert.True(translucent, $"entry {index} has no anti-aliased edge or shadow");
        }
    }

    /// <summary>How often each palette index appears in an entry's first frame, as a unit-sum vector.</summary>
    private static double[] FirstFrameHistogram(CharacterSpriteAsset asset, byte[] entry)
    {
        var counts = new double[256];
        int p = CharacterSpriteAsset.FrameTableSize;
        for (int i = 0; i < asset.Records[0].BodyRunCount; i++)
        {
            int n = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(entry.AsSpan(p + 4));
            p += 6;
            for (int k = 0; k < n; k++)
                counts[entry[p + k]]++;
            p += n;
        }
        double total = counts.Sum();
        return total == 0 ? counts : [.. counts.Select(c => c / total)];
    }

    private static double Cosine(double[] a, double[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return na == 0 || nb == 0 ? 0 : dot / Math.Sqrt(na * nb);
    }
}

/// <summary>
/// <em>Runaway 1</em>'s scene masks, against the shipped game. The encoding itself is the family default
/// and is tested synthetically in <see cref="RleMaskDecoderTests"/>; what is checked here is that every
/// scene in the game is recognised at the geometry its own background declares, including the two the
/// scanline and two-zone tests used to reject.
/// </summary>
public class Runaway1SceneMaskTests
{
    private const string R1SteamDir = @"F:\Games\Steam\steamapps\common\Runaway A Road Adventure";

    private static string ResourceDir => Path.Combine(R1SteamDir, "Resource");

    private sealed record Scene(string Name, byte[] Mask, byte[] Table, int Width, int Height);

    /// <summary>
    /// Every scene archive that holds a mask at entry 1 and its attribute table at entry 2, with the
    /// scene size taken from the background at entry 0 (an RGB565 raster, 600 rows in this game).
    /// </summary>
    private static List<Scene> ReadScenes()
    {
        var scenes = new List<Scene>();
        foreach (string path in Directory.GetFiles(ResourceDir).OrderBy(x => x))
        {
            byte[] d;
            try { d = File.ReadAllBytes(path); } catch { continue; }
            if (d.Length < 16)
                continue;

            int tableBytes = (int)BinaryPrimitives.ReadUInt32LittleEndian(d);
            if (tableBytes <= 0 || tableBytes % 8 != 0 || tableBytes > d.Length)
                continue;
            int half = tableBytes / 2;
            if (half / 4 < 3)
                continue;

            int Off(int i) => (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(i * 4, 4));
            int Siz(int i) => (int)BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(half + i * 4, 4));
            if (Off(1) + Siz(1) > d.Length || Siz(1) < 6 || Siz(2) != 1536 || Off(2) + 1536 > d.Length)
                continue;

            int bgPx = Siz(0) / 2;
            if (bgPx % 600 != 0)
                continue;

            byte[] mask = d[Off(1)..(Off(1) + Siz(1))];
            if (mask.Length % 3 != 0)
                continue;

            long total = 0;
            bool zeroRun = false;
            for (int q = 0; q + 3 <= mask.Length; q += 3)
            {
                ushort len = BinaryPrimitives.ReadUInt16LittleEndian(mask.AsSpan(q + 1, 2));
                if (len == 0) zeroRun = true;
                total += len;
            }
            // RESOURCE.F27 is the one entry 1 that is neither: 716,800 px against a 1024x600
            // background, unexplained -- see docs/formats/masks.md.
            if (zeroRun || total != bgPx)
                continue;

            scenes.Add(new Scene(Path.GetFileName(path), mask, d[Off(2)..(Off(2) + 1536)], bgPx / 600, 600));
        }
        return scenes;
    }

    [Fact]
    public void RealInstall_EverySceneMaskIsRecognisedAtItsOwnBackgroundsGeometry()
    {
        if (!Directory.Exists(ResourceDir))
            return;

        List<Scene> scenes = ReadScenes();
        Assert.Equal(58, scenes.Count);

        var wrong = new List<string>();
        foreach (Scene s in scenes)
        {
            MaskInfo? got = RleMaskDecoder.Detect(s.Mask, s.Width, s.Height);
            if (got is null)
                wrong.Add($"{s.Name}: not recognised ({s.Width}x{s.Height})");
            else if (got.Value.Width != s.Width || got.Value.Height != s.Height)
                wrong.Add($"{s.Name}: {got.Value.Width}x{got.Value.Height}, want {s.Width}x{s.Height}");
        }
        Assert.Empty(wrong);
    }

    [Fact]
    public void RealInstall_TheTwoSingleZoneScenesAreMasksAndNotDataEntries()
    {
        if (!Directory.Exists(ResourceDir))
            return;

        foreach (string name in new[] { "RESOURCE.D07", "RESOURCE.E13" })
        {
            Scene s = ReadScenes().Single(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            MaskInfo info = Assert.NotNull(RleMaskDecoder.Detect(s.Mask, s.Width, s.Height));
            Assert.Equal((1024, 600, 10, 1), (info.Width, info.Height, info.RunCount, info.UniqueIdCount));

            // And the classifier, which is what puts it in the tree, agrees.
            EntryClassification c = EntryClassifier.Classify(s.Mask, s.Width, s.Height);
            Assert.Equal(EntryKind.Mask, c.Kind);
        }
    }

    [Fact]
    public void RealInstall_TheDivasBusIsATextbookMask()
    {
        if (!Directory.Exists(ResourceDir))
            return;

        Scene e06 = ReadScenes().Single(x => x.Name.Equals("RESOURCE.E06", StringComparison.OrdinalIgnoreCase));
        Assert.Equal((1380, 600), (e06.Width, e06.Height));

        MaskInfo info = Assert.NotNull(RleMaskDecoder.Detect(e06.Mask, e06.Width, e06.Height));
        Assert.Equal((4898, 16), (info.RunCount, info.UniqueIdCount));

        // Scanline-bounded in the strong sense: every row closes on 1380 and no run crosses a boundary.
        int row = 0, rows = 0;
        for (int q = 0; q + 3 <= e06.Mask.Length; q += 3)
        {
            row += BinaryPrimitives.ReadUInt16LittleEndian(e06.Mask.AsSpan(q + 1, 2));
            Assert.True(row <= 1380, "a run crossed a row boundary");
            if (row == 1380) { row = 0; rows++; }
        }
        Assert.Equal((0, 600), (row, rows));

        // The exit is the one zone in two layers: walk region 1 and hotspot 1 over the same pixels.
        Assert.Equal([(16, MaskLayers.Walk | MaskLayers.Hotspot)], RleMaskDecoder.OverlappingIds(e06.Table));

        // Page 2 is blank although the bus fills the middle of the painting: the walkable track never
        // passes behind it, so there is nothing to sort. That is not a broken table.
        Assert.False(RleMaskDecoder.DetectAttributePresence(e06.Table).HasDepth);
    }

    [Fact]
    public void RealInstall_TheExitKeepsBothItsLayersInACombinedView()
    {
        if (!Directory.Exists(ResourceDir))
            return;

        Scene e06 = ReadScenes().Single(x => x.Name.Equals("RESOURCE.E06", StringComparison.OrdinalIgnoreCase));
        byte[]? idMap = RleMaskDecoder.ExtractObjectMapping(e06.Table);
        (byte B, byte G, byte R, byte A) walk1 = RleMaskDecoder.GenerateWalkboxPalette()[1];

        // Id 16 is an 86 px patch at (1330,577)-(1379,587), inside the right-edge exit strip.
        int WalkColoured(MaskLayers layers)
        {
            DecodedImage img = RleMaskDecoder.Decode(e06.Mask, e06.Width, e06.Height, idMap, e06.Table, layers);
            int n = 0;
            for (int y = 577; y <= 587; y++)
                for (int x = 1330; x <= 1379; x++)
                {
                    int o = (y * e06.Width + x) * 4;
                    if (img.Pixels[o + 3] != 0 && img.Pixels[o] == walk1.B &&
                        img.Pixels[o + 1] == walk1.G && img.Pixels[o + 2] == walk1.R)
                        n++;
                }
            return n;
        }

        Assert.Equal(86, WalkColoured(MaskLayers.Walk));
        // With every layer on it is banded between its two, so neither reading of the zone is lost.
        int combined = WalkColoured(MaskLayers.All);
        Assert.InRange(combined, 1, 85);
    }
}

/// <summary>
/// The player character's own animations, which <em>Runaway 1</em> keeps in <c>RESOURCE.000</c> rather
/// than in the library with the rest of the cast. The codec is the library's; what is under test here is
/// the layout -- records in a slot of their own, a group of seventeen slots per costume -- and the one
/// place the two readings genuinely differ, which is what a talking head's record describes.
/// </summary>
public class Runaway1ActorTests
{
    private const string R1SteamDir = @"F:\Games\Steam\steamapps\common\Runaway A Road Adventure";

    private static string GlobalPath => Path.Combine(R1SteamDir, "Resource", "RESOURCE.000");

    // ------------------------------------------------------------------------------------------
    // The layout
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void AnimationsKnowTheirSlotsFromTheirGroup()
    {
        var walk = new ActorAnimation(10, ActorRole.Walk, 3);
        Assert.Equal((15, 10, 19), (walk.DataSlot, walk.TableSlot, walk.FrameCount));
        Assert.Equal(3 * 19 * CharacterSpriteAsset.RecordSize, walk.TableOffset);
        Assert.Equal(11, walk.PaletteSlot);
        Assert.Equal(CharacterSpriteGeometry.Body, walk.Geometry);

        var head = new ActorAnimation(78, ActorRole.Head, 2);
        Assert.Equal((90, 94, 40), (head.DataSlot, head.TableSlot, head.FrameCount));
        Assert.Equal(2 * 40 * CharacterSpriteAsset.RecordSize, head.TableOffset);
        Assert.Equal(79, head.PaletteSlot);
        Assert.Equal(CharacterSpriteGeometry.HeadPatch, head.Geometry);
    }

    [Fact]
    public void EveryRunSlotOfAGroupNamesItsAnimationAndNoOtherSlotDoes()
    {
        Assert.Equal(6 * (8 + 6), ActorArchive.All().Count());
        Assert.Equal(ActorArchive.All(), ActorArchive.All().Select(a => ActorArchive.FromDataSlot(a.DataSlot)!.Value));

        // The frame tables and the palettes are slots of the group, but they are not animations.
        foreach (int group in ActorArchive.GroupSlots)
        {
            Assert.Null(ActorArchive.FromDataSlot(group));
            Assert.Null(ActorArchive.FromDataSlot(group + ActorArchive.PaletteSlot));
            Assert.Null(ActorArchive.FromDataSlot(group + ActorArchive.HeadTableSlot));
        }
        Assert.Null(ActorArchive.FromDataSlot(9));
        Assert.Null(ActorArchive.FromDataSlot(112));
    }

    [Fact]
    public void AGroupIsTakenOnlyWhenAllSeventeenSlotsAreThereAtTheRightSizes()
    {
        byte[] file = SyntheticAssets.ActorGlobalArchive([10]);
        Dictionary<int, ArchiveEntry> slots = GlobalArchive
            .ReadEntries(file, file.Length).ToDictionary(e => e.Index);

        Assert.True(ActorArchive.HasGroup(slots, 10));
        Assert.False(ActorArchive.HasGroup(slots, 27));

        // A frame table that is not exactly 8x19 or 6x40 records is not this layout.
        Dictionary<int, ArchiveEntry> shortTable = new(slots) { [10] = slots[10] with { Size = 4863 } };
        Assert.False(ActorArchive.HasGroup(shortTable, 10));

        // A missing run slot is not a group with a hole in it, it is not a group.
        Dictionary<int, ArchiveEntry> missingHead = new(slots);
        missingHead.Remove(10 + ActorArchive.HeadDataSlot + 5);
        Assert.False(ActorArchive.HasGroup(missingHead, 10));
    }

    // ------------------------------------------------------------------------------------------
    // The decoder
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void AnAnimationDecodesWithItsRecordsHeldInASeparateSlot()
    {
        var table = new byte[4 * CharacterSpriteAsset.RecordSize];
        List<SyntheticAssets.CharacterFrame> frames = [.. Enumerable.Range(0, 4)
            .Select(f => SyntheticAssets.SolidCharacterFrame(40 + f, 30, 12, 9, (byte)(7 + f)))];
        byte[] data = SyntheticAssets.ActorAnimationData(frames, table, 0);

        CharacterSpriteAsset asset = Assert.IsType<CharacterSpriteAsset>(
            CharacterSpriteAsset.Parse(table, 4, data, CharacterSpriteGeometry.Body));

        Assert.Equal(4, asset.FrameCount);
        Assert.Null(asset.Verify());
        // Same runs, same answer as the library layout, which is the point of sharing the decoder.
        CharacterSpriteAsset library = Assert.IsType<CharacterSpriteAsset>(
            CharacterSpriteAsset.Parse(SyntheticAssets.CharacterEntry(frames)));
        Assert.Equal(library.Bounds, asset.Bounds);
        Assert.Equal(library.DecodeFrame(2).Image!.Pixels, asset.DecodeFrame(2).Image!.Pixels);
    }

    [Fact]
    public void AHeadPatchIsSizedFromItsRunsBecauseItsRecordDescribesTheBody()
    {
        var table = new byte[3 * CharacterSpriteAsset.RecordSize];
        List<SyntheticAssets.CharacterFrame> frames = [.. Enumerable.Range(0, 3)
            .Select(f => SyntheticAssets.SolidCharacterFrame(114, 32, 7, 5, (byte)(20 + f)))];
        byte[] data = SyntheticAssets.ActorAnimationData(frames, table, 0, CharacterSpriteGeometry.HeadPatch);

        CharacterSpriteAsset asset = Assert.IsType<CharacterSpriteAsset>(
            CharacterSpriteAsset.Parse(table, 3, data, CharacterSpriteGeometry.HeadPatch));

        Assert.Null(asset.Verify());
        // The record says 168x418 -- the body it is painted over. The patch is the 7x5 its runs cover.
        Assert.Equal(168, asset.Records[0].Width);
        Assert.Equal((114, 32, 7, 5), asset.BoxOf(0));
        Assert.Equal((114, 32, 7, 5), asset.Bounds);
        Assert.Equal(7 * 5 * 4, asset.DecodeFrame(0).Image!.Pixels.Length);
    }

    [Fact]
    public void ReadingAHeadWithTheBodysRuleFindsTheWrongNumberOfCoverageRuns()
    {
        var table = new byte[2 * CharacterSpriteAsset.RecordSize];
        List<SyntheticAssets.CharacterFrame> frames = [.. Enumerable.Range(0, 2)
            .Select(f => SyntheticAssets.SolidCharacterFrame(114, 32, 7, 5, (byte)(20 + f)))];
        byte[] data = SyntheticAssets.ActorAnimationData(frames, table, 0, CharacterSpriteGeometry.HeadPatch);

        // A head's A is not part of the count, so adding it in overshoots and the frame will not walk.
        Assert.Null(CharacterSpriteAsset.Parse(table, 2, data, CharacterSpriteGeometry.Body));
    }

    [Fact]
    public void RunsThatDoNotFillTheSlotAreNotThisLayout()
    {
        var table = new byte[2 * CharacterSpriteAsset.RecordSize];
        List<SyntheticAssets.CharacterFrame> frames = [.. Enumerable.Range(0, 2)
            .Select(f => SyntheticAssets.SolidCharacterFrame(40, 30, 12, 9, (byte)(7 + f)))];
        byte[] data = SyntheticAssets.ActorAnimationData(frames, table, 0);

        // The last frame runs to the end of the slot, so a slot with a byte spare does not tile.
        Assert.Null(CharacterSpriteAsset.Parse(table, 2, [.. data, (byte)0], CharacterSpriteGeometry.Body));
        // Nor does one that stops short.
        Assert.Null(CharacterSpriteAsset.Parse(table, 2, data[..^1], CharacterSpriteGeometry.Body));
        // Claiming more frames than the table holds is refused rather than read off the end.
        Assert.Null(CharacterSpriteAsset.Parse(table, 3, data, CharacterSpriteGeometry.Body));
    }

    // ------------------------------------------------------------------------------------------
    // The tree
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void TheSyntheticGroupsBecomeAFolderOfWalkCyclesAndHeads()
    {
        using var install = new FakeInstall();
        File.WriteAllBytes(
            Path.Combine(install.Root, "Resource", "RESOURCE.000"),
            SyntheticAssets.ActorGlobalArchive([10, 44]));

        var vfs = VirtualFileSystem.Init(install.Root, cache: ScanCache.Ephemeral(includeShipped: false));
        FsNode characters = vfs.Root.Children.Single(c => c.Name == VirtualFileSystem.CharactersFolder);
        FsNode actor = characters.Children.Single(c => c.Name == "Actor");

        Assert.Equal(2 * (8 + 6), actor.Children.Count);
        Assert.Equal(2 * (8 + 6), vfs.Summary.ActorAnimations);
        Assert.All(actor.Children, c => Assert.Equal(EntryKind.ActorAnimation, c.Kind));
        Assert.Contains(actor.Children, c => c.Name == "g010-walk0");
        Assert.Contains(actor.Children, c => c.Name == "g044-head5");

        // A walk cycle is labelled from its record; a head from its runs, because its record is the
        // body's. Neither may come out as the body's 168x418.
        FsNode head = actor.Children.Single(c => c.Name == "g010-head3");
        Assert.Equal((7, 5, 40), (head.Image!.Width, head.Image.Height, head.Image.Frames));
        FsNode walk = actor.Children.Single(c => c.Name == "g010-walk0");
        Assert.Equal(19, walk.Image!.Frames);
    }

    [Fact]
    public void AnArchiveWithNoCompleteGroupGetsNoActorFolder()
    {
        using var install = new FakeInstall();
        File.WriteAllBytes(
            Path.Combine(install.Root, "Resource", "RESOURCE.000"),
            SyntheticAssets.GlobalArchiveOf(new Dictionary<int, byte[]> { [10] = new byte[4864], [11] = SyntheticAssets.Rgb565Palette() }));

        var vfs = VirtualFileSystem.Init(install.Root, cache: ScanCache.Ephemeral(includeShipped: false));
        FsNode characters = vfs.Root.Children.Single(c => c.Name == VirtualFileSystem.CharactersFolder);
        Assert.DoesNotContain(characters.Children, c => c.Name == "Actor");
        Assert.Equal(0, vfs.Summary.ActorAnimations);
    }

    // ------------------------------------------------------------------------------------------
    // The shipped game
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void RealInstall_AllSixGroupsAreThereAtTheSizesTheLayoutRequires()
    {
        if (!File.Exists(GlobalPath)) return;

        using FileStream f = File.OpenRead(GlobalPath);
        Dictionary<int, ArchiveEntry> slots = GlobalArchive
            .ReadEntries(f, GameVersion.Runaway1).ToDictionary(e => e.Index);

        Assert.All(ActorArchive.GroupSlots, g => Assert.True(ActorArchive.HasGroup(slots, g), $"group {g}"));

        // The groups are exactly slots 10-111, back to back, and slot 112 belongs to something else.
        Assert.Equal(102, ActorArchive.GroupSlots.Count * ActorArchive.SlotsPerGroup);
        Assert.Equal([10, 27, 44, 61, 78, 95], ActorArchive.GroupSlots);
        Assert.All(ActorArchive.GroupSlots, g => Assert.Equal(512, slots[g + ActorArchive.PaletteSlot].Size));
    }

    [Fact]
    public void RealInstall_EveryActorFrameWalksByteExact()
    {
        if (!File.Exists(GlobalPath)) return;

        var vfs = VirtualFileSystem.Init(R1SteamDir, cache: ScanCache.Ephemeral(includeShipped: true));
        FsNode actor = vfs.Root.Children
            .Single(c => c.Name == VirtualFileSystem.CharactersFolder).Children
            .Single(c => c.Name == "Actor");

        Assert.Equal(84, actor.Children.Count);
        Assert.Equal(84, vfs.Summary.ActorAnimations);

        var bad = new List<string>();
        int frames = 0;
        foreach (FsNode node in actor.Children)
        {
            ActorAnimation animation = ActorArchive.FromDataSlot(node.EntryIndex)!.Value;
            byte[] table = Assert.IsType<byte[]>(ActorArchive.ReadFrameTable(GlobalPath, animation));
            CharacterSpriteAsset? asset = CharacterSpriteAsset.Parse(
                table, animation.FrameCount, vfs.ReadBytes(node), animation.Geometry);

            if (asset is null)
            {
                bad.Add($"{node.Name}: does not parse");
                continue;
            }
            if (asset.Verify() is { } why)
                bad.Add($"{node.Name}: {why}");
            frames += asset.FrameCount;
        }

        Assert.Empty(bad);
        Assert.Equal(6 * (8 * 19 + 6 * 40), frames);
    }

    [Fact]
    public void RealInstall_AWalkCycleKeepsTheFeetOnOneCanvasPointAndDecodesInColour()
    {
        if (!File.Exists(GlobalPath)) return;

        var vfs = VirtualFileSystem.Init(R1SteamDir, cache: ScanCache.Ephemeral(includeShipped: true));
        FsNode actor = vfs.Root.Children
            .Single(c => c.Name == VirtualFileSystem.CharactersFolder).Children
            .Single(c => c.Name == "Actor");

        FsNode node = actor.Children.Single(c => c.Name == "g010-walk0");
        var animation = new ActorAnimation(10, ActorRole.Walk, 0);
        CharacterSpriteAsset asset = Assert.IsType<CharacterSpriteAsset>(CharacterSpriteAsset.Parse(
            ActorArchive.ReadFrameTable(GlobalPath, animation)!, animation.FrameCount,
            vfs.ReadBytes(node), animation.Geometry));

        Assert.Equal(19, asset.FrameCount);
        Assert.Null(asset.Verify());

        // Verify already holds the feet fixed; this says where, and that it is the library's own point.
        (int ox, int oy) = asset.OriginOf(0);
        Assert.Equal((168, 434), (ox + asset.Records[0].AnchorX, oy + asset.Records[0].AnchorY));

        // The palette is the group's own second slot, and the frame is not a silhouette without it.
        IndexedPalette palette = Assert.IsType<IndexedPalette>(vfs.CharacterPaletteFor(node));
        asset.Palette = palette;
        byte[] px = asset.DecodeFrame(0).Image!.Pixels;
        var colours = new HashSet<(byte, byte, byte)>();
        for (int i = 0; i < px.Length; i += 4)
            if (px[i + 3] == 255)
                colours.Add((px[i], px[i + 1], px[i + 2]));
        Assert.True(colours.Count > 32, $"only {colours.Count} opaque colours -- the palette did not apply");
    }

    [Fact]
    public void RealInstall_AHeadPatchIsAHeadAndNotTheWholeBody()
    {
        if (!File.Exists(GlobalPath)) return;

        var animation = new ActorAnimation(10, ActorRole.Head, 0);
        byte[] table = Assert.IsType<byte[]>(ActorArchive.ReadFrameTable(GlobalPath, animation));

        using FileStream f = File.OpenRead(GlobalPath);
        ArchiveEntry slot = GlobalArchive.ReadEntries(f, GameVersion.Runaway1)
            .Single(e => e.Index == animation.DataSlot);
        var data = new byte[slot.Size];
        f.Position = slot.Offset;
        f.ReadExactly(data);

        CharacterSpriteAsset asset = Assert.IsType<CharacterSpriteAsset>(
            CharacterSpriteAsset.Parse(table, animation.FrameCount, data, CharacterSpriteGeometry.HeadPatch));

        Assert.Equal(40, asset.FrameCount);
        Assert.Null(asset.Verify());

        // Its record carries walk facing 0's box; the patch itself is a head-sized piece of it, sitting
        // at the top of the same canvas.
        Assert.Equal((168, 418), (asset.Records[0].Width, asset.Records[0].Height));
        (int x, int y, int w, int h) = asset.Bounds;
        Assert.Equal((114, 32), (x, y));
        Assert.True(w < 130 && h < 130, $"the patch measured {w}x{h}, which is not a head");

        // Read with the body's rule -- A+B coverage runs -- the same bytes do not walk at all.
        Assert.Null(CharacterSpriteAsset.Parse(table, animation.FrameCount, data, CharacterSpriteGeometry.Body));
    }

    [Fact]
    public void RealInstall_ResourceD10_Entry0_IsWideScene()
    {
        string path = Path.Combine(R1SteamDir, "Resource", "RESOURCE.D10");
        if (!File.Exists(path)) return;

        using FileStream f = File.OpenRead(path);
        List<ArchiveEntry> entries = SceneArchive.ReadEntries(f);
        Assert.NotEmpty(entries);
        ArchiveEntry e0 = entries[0];
        var data = new byte[e0.Size];
        f.Position = e0.Offset;
        f.ReadExactly(data);

        RasterInfo? info = RasterDecoder.Detect(data, GameVersion.Runaway1);
        Assert.NotNull(info);
        Assert.Equal(1536, info.Value.Width);
        Assert.Equal(600, info.Value.Height);
    }

    [Fact]
    public void SyntheticScript_Runaway1_DecodesStagesAndExpandsContinuationCues()
    {
        byte[] scriptData = SyntheticArchives.R1Script(new Dictionary<int, (string[] Labels, (string Text, int VoiceClip)[] Lines)>
        {
            [100] = ([],
            [
                ("¡Hola!", 467),
                ("Mi nombre es Brian.", 468),
                ("Estoy aquí para contaros algo...", 469),
            ]),
            [201] = (["  pizarra"],
            [
                ("Laboratorio de restauración.", 1200),
            ])
        }, continuationSpan: 3);

        using var ms = new MemoryStream(scriptData);
        List<HollywoodScript.Stage> stages = HollywoodScript.ReadStages(ms, GameVersion.Runaway1);

        Assert.Equal([100, 201], stages.Select(s => s.Index));

        HollywoodScript.Stage intro = stages.Single(s => s.Index == 100);
        Assert.Equal(3, intro.Lines.Count);
        Assert.Equal("¡Hola!", intro.Lines[0].Text);
        Assert.Equal(467, intro.Lines[0].VoiceClipId);
        Assert.Equal("Mi nombre es Brian.", intro.Lines[1].Text);
        Assert.Equal(468, intro.Lines[1].VoiceClipId);
        Assert.Equal("Estoy aquí para contaros algo...", intro.Lines[2].Text);
        Assert.Equal(469, intro.Lines[2].VoiceClipId);

        HollywoodScript.Stage b01 = stages.Single(s => s.Index == 201);
        Assert.Single(b01.Labels);
        Assert.Equal("  pizarra", b01.Labels[0].Text);
        Assert.Single(b01.Lines);
        Assert.Equal("Laboratorio de restauración.", b01.Lines[0].Text);
        Assert.Equal(1200, b01.Lines[0].VoiceClipId);
    }

    [Fact]
    public void RealInstall_Resource003_DecodesScriptAndPairsVoiceClips()
    {
        string path = Path.Combine(R1SteamDir, "Resource", "RESOURCE.003");
        if (!File.Exists(path)) return;

        using FileStream f = File.OpenRead(path);
        List<HollywoodScript.Stage> stages = HollywoodScript.ReadStages(f, GameVersion.Runaway1);

        Assert.Equal(54, stages.Count);
        Assert.Equal(5125, stages.Sum(s => s.Lines.Count));
        Assert.Equal(4635, stages.Sum(s => s.Lines.Count(l => l.VoiceClipId >= 0)));

        HollywoodScript.Stage intro = stages.Single(s => s.Index == 100);
        Assert.Equal("¡Hola!", intro.Lines[0].Text);
        Assert.Equal(467, intro.Lines[0].VoiceClipId);

        Assert.Equal("Mi nombre es Brian.", intro.Lines[1].Text);
        Assert.Equal(468, intro.Lines[1].VoiceClipId);

        Assert.Equal("Estoy aquí para contaros algo que me pasó hace un par de semanas...", intro.Lines[2].Text);
        Assert.Equal(469, intro.Lines[2].VoiceClipId);
    }

    [Fact]
    public void RealInstall_Runaway1_DialogueTreeIsLoadedAndVoiceHasSubtitles()
    {
        if (!Directory.Exists(R1SteamDir)) return;

        var vfs = VirtualFileSystem.Init(R1SteamDir, cache: ScanCache.Ephemeral(includeShipped: true));
        FsNode dialogue = Assert.Single(vfs.Root.Children, c => c.Name == VirtualFileSystem.DialogueFolder);
        Assert.Single(dialogue.Children, c => c.Name == "RESOURCE.003");

        FsNode res003 = dialogue.Children.Single(c => c.Name == "RESOURCE.003");
        Assert.Equal(54, res003.Children.Count);

        // Check intro scene
        FsNode intro = res003.Children.Single(c => c.EntryIndex == 100);
        Assert.StartsWith("RESOURCE.A00", intro.Name);
        Assert.Equal(121, intro.Children.Count);

        // Check voice clips have subtitles populated from the script
        FsNode voiceFolder = Assert.Single(vfs.Root.Children, c => c.Name == VirtualFileSystem.VoiceFolder);
        FsNode clip467 = voiceFolder.Children.SelectMany(c => c.Children).Single(c => c.EntryIndex == 467);
        Assert.Equal("¡Hola!", clip467.Subtitle);
    }

    [Fact]
    public void RealInstall_Runaway1_SavesFolderIsLoaded()
    {
        if (!Directory.Exists(R1SteamDir)) return;

        var vfs = VirtualFileSystem.Init(R1SteamDir, cache: ScanCache.Ephemeral(includeShipped: true));
        FsNode saves = Assert.Single(vfs.Root.Children, c => c.Name == VirtualFileSystem.SavegamesFolder);
        Assert.Equal(10, saves.Children.Count);

        FsNode slot1 = saves.Children[0];
        Assert.Equal("SAVEGAME.001", slot1.Name);
        Assert.Equal(EntryKind.Savegame, slot1.Kind);
        Assert.Contains("Inicial", slot1.FriendlyName);
        Assert.Contains("RESOURCE.B02", slot1.FriendlyName);
    }

    [Fact]
    public void SyntheticRouteTable_ParsesCorrectly()
    {
        var dict = new Dictionary<(int From, int To), (IReadOnlyList<(int X, int Y)> Waypoints, IReadOnlyList<byte> Hops)>
        {
            [(1, 2)] = ([(100, 200), (150, 250)], [2]),
            [(1, 5)] = ([(300, 400), (350, 450), (400, 500)], [3, 5]),
        };

        byte[] data = SyntheticAssets.RouteTable(dict);
        Assert.Equal(RouteDecoder.TotalSize, data.Length);
        Assert.True(RouteDecoder.IsRouteTable(data));

        RouteMatrix matrix = RouteDecoder.Parse(data);
        Assert.Equal(2, matrix.TotalActiveRoutes);
        Assert.Equal(5, matrix.TotalWaypoints);

        Route r12 = matrix.GetRoute(1, 2)!;
        Assert.NotNull(r12);
        Assert.True(r12.IsActive);
        Assert.Equal(2, r12.Waypoints.Count);
        Assert.Equal(100, r12.Waypoints[0].X);
        Assert.Equal(200, r12.Waypoints[0].Y);
        Assert.Equal([2], r12.ZoneHops);

        Route r15 = matrix.GetRoute(1, 5)!;
        Assert.NotNull(r15);
        Assert.Equal(3, r15.Waypoints.Count);
        Assert.Equal([3, 5], r15.ZoneHops);

        string summary = RouteDecoder.FormatSummary(matrix);
        Assert.Contains("2 active route(s)", summary);
        Assert.Contains("(100, 200)", summary);
    }

    [Fact]
    public void RealInstall_Runaway1_SceneRouteMeshParsesCorrectly()
    {
        if (!Directory.Exists(R1SteamDir)) return;

        string scenePath = Path.Combine(R1SteamDir, "Resource", "RESOURCE.B02");
        if (!File.Exists(scenePath)) return;

        byte[] fileBytes = File.ReadAllBytes(scenePath);
        List<ArchiveEntry> entries = SceneArchive.ReadEntries(fileBytes, fileBytes.Length);
        ArchiveEntry routeEntry = Assert.Single(entries, e => e.Size == RouteDecoder.TotalSize);

        byte[] routeData = fileBytes.AsSpan((int)routeEntry.Offset, (int)routeEntry.Size).ToArray();
        RouteMatrix matrix = RouteDecoder.Parse(routeData);

        Assert.True(matrix.TotalActiveRoutes > 0);
        Assert.True(matrix.TotalWaypoints > 0);

        Route? r12 = matrix.GetRoute(1, 2);
        Assert.NotNull(r12);
        Assert.True(r12.IsActive);
        Assert.Equal(12, r12.Waypoints.Count);
        Assert.Equal(856, r12.Waypoints[0].X);
        Assert.Equal(599, r12.Waypoints[0].Y);
        Assert.Equal([2], r12.ZoneHops);

        Route? r14 = matrix.GetRoute(1, 4);
        Assert.NotNull(r14);
        Assert.Equal([3, 4], r14.ZoneHops);
    }

    [Fact]
    public void SyntheticCorridorTable_ParsesCorrectly()
    {
        var spans = new List<(int Width, int Y, int X)>
        {
            (241, 483, 9),
            (239, 484, 14),
            (236, 485, 19),
        };

        byte[] data = SyntheticAssets.CorridorTable(spans);
        Assert.Equal(CorridorSpanDecoder.TotalSize, data.Length);
        Assert.True(CorridorSpanDecoder.IsCorridorTable(data));

        CorridorTable table = CorridorSpanDecoder.Parse(data);
        Assert.Equal(3, table.Spans.Count);
        Assert.Equal(483, table.MinY);
        Assert.Equal(485, table.MaxY);

        Assert.Equal(9, table.Spans[0].Left);
        Assert.Equal(241, table.Spans[0].Width);
        Assert.Equal(250, table.Spans[0].Right);
        Assert.Equal(483, table.Spans[0].Y);

        string summary = CorridorSpanDecoder.FormatSummary(table);
        Assert.Contains("3 scanline spans", summary);
        Assert.Contains("483", summary);
    }

    [Fact]
    public void RealInstall_Runaway1_SceneCorridorTableParsesCorrectly()
    {
        if (!Directory.Exists(R1SteamDir)) return;

        string scenePath = Path.Combine(R1SteamDir, "Resource", "RESOURCE.F09");
        if (!File.Exists(scenePath)) return;

        byte[] fileBytes = File.ReadAllBytes(scenePath);
        List<ArchiveEntry> entries = SceneArchive.ReadEntries(fileBytes, fileBytes.Length);
        ArchiveEntry corridorEntry = Assert.Single(entries, e => e.Size == CorridorSpanDecoder.TotalSize);

        byte[] data = fileBytes.AsSpan((int)corridorEntry.Offset, (int)corridorEntry.Size).ToArray();
        CorridorTable table = CorridorSpanDecoder.Parse(data);

        Assert.Equal(117, table.Spans.Count);
        Assert.Equal(483, table.MinY);
        Assert.Equal(599, table.MaxY);

        // First span
        Assert.Equal(9, table.Spans[0].Left);
        Assert.Equal(241, table.Spans[0].Width);
        Assert.Equal(250, table.Spans[0].Right);
        Assert.Equal(483, table.Spans[0].Y);

        // Last span
        Assert.Equal(200, table.Spans[^1].Left);
        Assert.Equal(114, table.Spans[^1].Width);
        Assert.Equal(314, table.Spans[^1].Right);
        Assert.Equal(599, table.Spans[^1].Y);
    }

    [Fact]
    public void RealInstall_Runaway1_MuseumLabMonitorFeedsAreDecodedAsRasters()
    {
        if (!Directory.Exists(R1SteamDir)) return;

        string scenePath = Path.Combine(R1SteamDir, "Resource", "RESOURCE.B01");
        if (!File.Exists(scenePath)) return;

        byte[] fileBytes = File.ReadAllBytes(scenePath);
        List<ArchiveEntry> entries = SceneArchive.ReadEntries(fileBytes, fileBytes.Length);
        List<ArchiveEntry> monitorEntries = entries.Where(e => e.Size == 106_032).ToList();

        Assert.Equal(16, monitorEntries.Count);
        foreach (ArchiveEntry entry in monitorEntries)
        {
            byte[] rasterBytes = fileBytes.AsSpan((int)entry.Offset, (int)entry.Size).ToArray();
            RasterInfo? info = RasterDecoder.Detect(rasterBytes);
            Assert.NotNull(info);
            Assert.Equal(282, info.Value.Width);
            Assert.Equal(188, info.Value.Height);
            Assert.False(info.Value.IsMask);

            DecodedImage decoded = RasterDecoder.Decode(rasterBytes, info.Value.Width, info.Value.Height);
            Assert.Equal(282, decoded.Width);
            Assert.Equal(188, decoded.Height);
            Assert.Equal(282 * 188 * 4, decoded.Pixels.Length);
        }
    }

    [Fact]
    public void RealInstall_Runaway1_ChapterSelectionThumbnailsAreDecodedAsRasters()
    {
        string scenePath = Path.Combine(R1SteamDir, "Resource", "RESOURCE.A03");
        if (!File.Exists(scenePath)) return;

        byte[] fileBytes = File.ReadAllBytes(scenePath);
        List<ArchiveEntry> entries = SceneArchive.ReadEntries(fileBytes, fileBytes.Length);
        Assert.Equal(7, entries.Count);

        for (int i = 0; i < entries.Count; i++)
        {
            ArchiveEntry entry = entries[i];
            Assert.Equal(48_960, entry.Size);
            byte[] rasterBytes = fileBytes.AsSpan((int)entry.Offset, (int)entry.Size).ToArray();
            RasterInfo? info = RasterDecoder.Detect(rasterBytes);
            Assert.NotNull(info);
            Assert.Equal(204, info.Value.Width);
            Assert.Equal(120, info.Value.Height);
            Assert.False(info.Value.IsMask);

            DecodedImage decoded = RasterDecoder.Decode(rasterBytes, info.Value.Width, info.Value.Height);
            Assert.Equal(204, decoded.Width);
            Assert.Equal(120, decoded.Height);
            Assert.Equal(204 * 120 * 4, decoded.Pixels.Length);
        }
    }

    [Fact]
    public void Synthetic_ResourceD06_1760x1200_RasterAndMask()
    {
        var rasterBytes = new byte[1760 * 1200 * 2];
        RasterInfo? rInfo = RasterDecoder.Detect(rasterBytes);
        Assert.NotNull(rInfo);
        Assert.Equal(1760, rInfo.Value.Width);
        Assert.Equal(1200, rInfo.Value.Height);

        // Synthetic mask of two runs summing to 1760 * 1200 = 2,112,000 px
        // 2,112,000 = 65,000 * 32 + 32,000
        using var ms = new MemoryStream();
        Span<byte> run = stackalloc byte[3];
        long remaining = 1760 * 1200;
        byte id = 1;
        while (remaining > 0)
        {
            ushort len = (ushort)Math.Min(60000, remaining);
            run[0] = id++;
            BinaryPrimitives.WriteUInt16LittleEndian(run.Slice(1), len);
            ms.Write(run);
            remaining -= len;
        }

        byte[] maskBytes = ms.ToArray();
        MaskInfo? mInfo = RleMaskDecoder.Detect(maskBytes, 1760, 1200);
        Assert.NotNull(mInfo);
        Assert.Equal(1760, mInfo.Value.Width);
        Assert.Equal(1200, mInfo.Value.Height);

        DecodedImage decodedMask = RleMaskDecoder.Decode(maskBytes, 1760, 1200);
        Assert.Equal(1760, decodedMask.Width);
        Assert.Equal(1200, decodedMask.Height);
    }

    [Fact]
    public void RealInstall_Runaway1_ResourceD06_CanvasAndMask()
    {
        if (!Directory.Exists(R1SteamDir)) return;

        string scenePath = Path.Combine(R1SteamDir, "Resource", "RESOURCE.D06");
        if (!File.Exists(scenePath)) return;

        byte[] fileBytes = File.ReadAllBytes(scenePath);
        List<ArchiveEntry> entries = SceneArchive.ReadEntries(fileBytes, fileBytes.Length);
        Assert.True(entries.Count >= 4);

        // Entry 0: 1760x1200 RGB565 background raster (4,224,000 bytes)
        ArchiveEntry e0 = entries[0];
        Assert.Equal(1760 * 1200 * 2, e0.Size);
        byte[] e0Bytes = fileBytes.AsSpan((int)e0.Offset, (int)e0.Size).ToArray();
        RasterInfo? rInfo = RasterDecoder.Detect(e0Bytes);
        Assert.NotNull(rInfo);
        Assert.Equal(1760, rInfo.Value.Width);
        Assert.Equal(1200, rInfo.Value.Height);

        // Entry 1: 1760x1200 3-byte continuous RLE mask (89,475 bytes = 29,825 runs summing to 2,112,000 px)
        ArchiveEntry e1 = entries[1];
        Assert.Equal(89_475, e1.Size);
        byte[] e1Bytes = fileBytes.AsSpan((int)e1.Offset, (int)e1.Size).ToArray();
        MaskInfo? mInfo = RleMaskDecoder.Detect(e1Bytes, 1760, 1200);
        Assert.NotNull(mInfo);
        Assert.Equal(1760, mInfo.Value.Width);
        Assert.Equal(1200, mInfo.Value.Height);

        // Entry 2: 1536-byte attribute table
        Assert.Equal(1536, entries[2].Size);

        // Entry 3: 43,659-byte route table
        Assert.Equal(43_659, entries[3].Size);
    }

    [Fact]
    public void RealInstall_Runaway1_SceneCorridorTable_H09()
    {
        if (!Directory.Exists(R1SteamDir)) return;

        string scenePath = Path.Combine(R1SteamDir, "Resource", "RESOURCE.H09");
        if (!File.Exists(scenePath)) return;

        byte[] fileBytes = File.ReadAllBytes(scenePath);
        List<ArchiveEntry> entries = SceneArchive.ReadEntries(fileBytes, fileBytes.Length);
        ArchiveEntry corridorEntry = Assert.Single(entries, e => e.Size == CorridorSpanDecoder.TotalSize);

        byte[] data = fileBytes.AsSpan((int)corridorEntry.Offset, (int)corridorEntry.Size).ToArray();
        CorridorTable table = CorridorSpanDecoder.Parse(data);

        Assert.Equal(117, table.Spans.Count);
        Assert.Equal(483, table.MinY);
        Assert.Equal(599, table.MaxY);
    }

    [Fact]
    public void RealInstall_SavegameMetadata_InspectAllSaves()
    {
        if (!Directory.Exists(R1SteamDir)) return;
        string saveDir = Path.Combine(R1SteamDir, "SAVEGAME");
        if (!Directory.Exists(saveDir)) return;

        List<string> saves = SavegameArchive.FindSaves(saveDir);
        Assert.NotEmpty(saves);
        foreach (string path in saves)
        {
            byte[] bytes = File.ReadAllBytes(path);
            int slot = SavegameArchive.GetSlotNumber(path);
            SavegameInfo info = SavegameArchive.ReadMetadata(bytes, slot);
            Assert.True(info.SceneId > 0);
            Assert.NotNull(info.SceneArchiveName);
        }
    }

    [Fact]
    public void RealInstall_SavegameThumbnailMatching()
    {
        if (!Directory.Exists(R1SteamDir)) return;
        string save1Path = Path.Combine(R1SteamDir, "SAVEGAME", "SAVEGAME.001");
        string b02Path = Path.Combine(R1SteamDir, "Resource", "RESOURCE.B02");
        string b06Path = Path.Combine(R1SteamDir, "Resource", "RESOURCE.B06");
        string f09Path = Path.Combine(R1SteamDir, "Resource", "RESOURCE.F09");
        if (!File.Exists(save1Path) || !File.Exists(b02Path) || !File.Exists(b06Path) || !File.Exists(f09Path)) return;

        byte[] saveBytes = File.ReadAllBytes(save1Path);
        SavegameInfo info = SavegameArchive.ReadMetadata(saveBytes, 1);
        Assert.Equal("RESOURCE.B02", info.SceneArchiveName);

        byte[] b02Bytes = File.ReadAllBytes(b02Path);
        List<ArchiveEntry> b02Entries = SceneArchive.ReadEntries(b02Bytes, b02Bytes.Length);
        byte[] b02Bg = b02Bytes.AsSpan((int)b02Entries[0].Offset, (int)b02Entries[0].Size).ToArray();

        byte[] b06Bytes = File.ReadAllBytes(b06Path);
        List<ArchiveEntry> b06Entries = SceneArchive.ReadEntries(b06Bytes, b06Bytes.Length);
        byte[] b06Bg = b06Bytes.AsSpan((int)b06Entries[0].Offset, (int)b06Entries[0].Size).ToArray();

        byte[] f09Bytes = File.ReadAllBytes(f09Path);
        List<ArchiveEntry> f09Entries = SceneArchive.ReadEntries(f09Bytes, f09Bytes.Length);
        byte[] f09Bg = f09Bytes.AsSpan((int)f09Entries[0].Offset, (int)f09Entries[0].Size).ToArray();

        ReadOnlySpan<byte> thumb = saveBytes.AsSpan(0, SavegameArchive.ThumbnailBytes);

        double scoreB02 = SavegameArchive.ScoreThumbnailMatch(thumb, info.ViewX, b02Bg, 1024, 600);
        double scoreB06 = SavegameArchive.ScoreThumbnailMatch(thumb, info.ViewX, b06Bg, 1024, 600);
        double scoreF09 = SavegameArchive.ScoreThumbnailMatch(thumb, info.ViewX, f09Bg, 1024, 600);

        Assert.True(scoreB02 < 8.0, $"Expected B02 score < 8.0, got {scoreB02}");
        Assert.True(scoreB02 < scoreB06, $"Expected B02 ({scoreB02}) < B06 ({scoreB06})");
        Assert.True(scoreF09 > 20.0, $"Expected F09 score > 20.0, got {scoreF09}");

        var candidates = new (string ArchiveName, byte[] BackgroundRgb565, int Width, int Height)[]
        {
            ("RESOURCE.F09", f09Bg, 1024, 600),
            ("RESOURCE.B06", b06Bg, 1024, 600),
            ("RESOURCE.B02", b02Bg, 1024, 600),
        };

        var match = SavegameArchive.MatchSceneArchive(thumb, info.ViewX, candidates);
        Assert.NotNull(match);
        Assert.Equal("RESOURCE.B02", match.Value.ArchiveName);
    }
}




