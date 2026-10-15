using PenduloStudiosExplorer.Core.FileSystem;
using PenduloStudiosExplorer.Core.Formats;
using PenduloStudiosExplorer.Core.Settings;
using Xunit;

namespace PenduloStudiosExplorer.Core.Tests;

public class TheNextBigThingTests
{
    public const string TnbtSteamDir = @"F:\Games\Steam\steamapps\common\The Next BIG Thing";

    [Fact]
    public void GameDetector_DetectsTheSignature()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "The Next Big Thing.exe"), "dummy");
            Assert.Equal(GameVersion.TheNextBigThing, GameDetector.Detect(tempDir));
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
        if (Directory.Exists(TnbtSteamDir))
            Assert.Equal(GameVersion.TheNextBigThing, GameDetector.Detect(TnbtSteamDir));
    }

    [Fact]
    public void AppSettings_ManagesTheTnbtPath()
    {
        var settings = new AppSettings
        {
            TheNextBigThingDir = @"C:\Games\TNBT",
            ActiveGame = GameVersion.TheNextBigThing
        };

        Assert.Equal(@"C:\Games\TNBT", settings.GetGameDir(GameVersion.TheNextBigThing));
        Assert.Equal(@"C:\Games\TNBT", settings.ActiveGameDir);

        settings.SetGameDir(GameVersion.TheNextBigThing, @"D:\TNBT");
        Assert.Equal(@"D:\TNBT", settings.GetGameDir(GameVersion.TheNextBigThing));
    }

    [Fact]
    public void SceneArchive_RecognizesTnbtNames()
    {
        // Scene archives
        Assert.True(SceneArchive.IsSceneArchiveName("RESOURCE.A00", GameVersion.TheNextBigThing));
        Assert.True(SceneArchive.IsSceneArchiveName("RESOURCE.B04", GameVersion.TheNextBigThing));
        Assert.True(SceneArchive.IsSceneArchiveName("RESOURCE.SP1", GameVersion.TheNextBigThing));

        // Non-scene archives
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.TAB", GameVersion.TheNextBigThing));
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.IFZ", GameVersion.TheNextBigThing));
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.003", GameVersion.TheNextBigThing));
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.004", GameVersion.TheNextBigThing));
        Assert.False(SceneArchive.IsSceneArchiveName("RESOURCE.S00", GameVersion.TheNextBigThing)); // Audio
    }

    [Fact]
    public void RealInstall_CanLoadAndScan()
    {
        if (!Directory.Exists(TnbtSteamDir))
            return;

        var cache = ScanCache.Ephemeral();
        var vfs = VirtualFileSystem.Init(TnbtSteamDir, cache: cache);
        Assert.Equal(GameVersion.TheNextBigThing, vfs.GameVersion);
        Assert.True(vfs.Summary.SceneArchives > 0);
        Assert.True(vfs.Summary.Backgrounds > 0);
        Assert.True(vfs.Summary.Videos > 0);
        Assert.True(vfs.Summary.VoiceClips > 0);

        // Second scan with same cache must hit cache
        var vfs2 = VirtualFileSystem.Init(TnbtSteamDir, cache: cache);
        Assert.True(vfs2.Summary.FromCache);
        Assert.Equal(vfs.Summary.SceneArchives, vfs2.Summary.SceneArchives);
        Assert.Equal(vfs.Summary.Backgrounds, vfs2.Summary.Backgrounds);
    }

    [Fact]
    public void ResourceD02_ClassifiesAndDecodesCorrectly()
    {
        if (!Directory.Exists(TnbtSteamDir))
            return;

        var vfs = VirtualFileSystem.Init(TnbtSteamDir, cache: ScanCache.Ephemeral());
        var d02 = vfs.Root.Children
            .FirstOrDefault(c => c.Name == VirtualFileSystem.ScenesFolder)?
            .Children.FirstOrDefault(c => c.Name.Equals("RESOURCE.D02", StringComparison.OrdinalIgnoreCase));
        if (d02 is null)
            return;

        var e00 = d02.Children.FirstOrDefault(c => c.EntryIndex == 0);
        Assert.NotNull(e00);
        Assert.Equal(EntryKind.Background, e00.Kind);
        Assert.Equal(1920, e00.Image?.Width);
    }
}

/// <summary>
/// The Next BIG Thing has no RESOURCE.000 at all: its interface -- menu screens, cursors and the rest --
/// lives in RESOURCE.IFZ as whole PNG and JPEG files behind a plain offset table.
/// </summary>
public class TheNextBigThingInterfaceTests
{
    [Fact]
    public void RealInstall_InterfaceArchiveHoldsPngArtwork()
    {
        string path = Path.Combine(TheNextBigThingTests.TnbtSteamDir, "Resource", "RESOURCE.IFZ");
        if (!File.Exists(path)) return;

        using FileStream fs = File.OpenRead(path);
        List<ArchiveEntry> entries = InterfaceArchive.ReadEntries(fs);
        Assert.Equal(95, entries.Count);

        int images = 0;
        foreach (ArchiveEntry e in entries)
        {
            var head = new byte[64];
            fs.Position = e.Offset;
            int read = fs.Read(head, 0, (int)Math.Min(head.Length, e.Size));
            if (PngDecoder.TryGetDimensions(head.AsSpan(0, read), out int w, out int h))
            {
                images++;
                Assert.InRange(w, 1, 4096);
                Assert.InRange(h, 1, 16384);
            }
        }

        Assert.Equal(24, images);
    }

    [Fact]
    public void RealInstall_IsCoveredByTheShippedScanCache()
    {
        if (!Directory.Exists(TheNextBigThingTests.TnbtSteamDir)) return;

        // Scanning adds whatever the shipped cache did not already answer for. Scanning again must add
        // nothing: that is what "the install is fully covered" means, and it needs no list of archives
        // here that could drift from the one the scan actually walks.
        ShippedCacheBuilder.AddInstall(TheNextBigThingTests.TnbtSteamDir);
        Assert.Equal(0, ShippedCacheBuilder.AddInstall(TheNextBigThingTests.TnbtSteamDir));
    }

    [Fact]
    public void RealInstall_Tnbt_VisemeLipSyncArchive()
    {
        string path = Path.Combine(TheNextBigThingTests.TnbtSteamDir, "Resource", "RESOURCE.004");
        if (!File.Exists(path)) return;

        byte[] bytes = File.ReadAllBytes(path);
        Assert.Equal(526_654, bytes.Length);

        List<ArchiveEntry> entries = VisemeArchive.ReadEntries(bytes, bytes.Length);
        Assert.True(entries.Count > 0);
        Assert.Equal(92000, (int)entries[0].Offset);
        Assert.Equal(43, (int)entries[0].Size);

        byte[] track0 = bytes.AsSpan((int)entries[0].Offset, (int)entries[0].Size).ToArray();
        string text = VisemeArchive.ToText(track0);
        Assert.NotEmpty(text);
        Assert.True(track0.All(b => b <= 32)); // phoneme IDs

        var vfs = VirtualFileSystem.Init(TheNextBigThingTests.TnbtSteamDir, cache: ScanCache.Ephemeral(includeShipped: true));
        FsNode lipSync = Assert.Single(vfs.Root.Children, c => c.Name == VirtualFileSystem.LipSyncFolder);
        FsNode? r004 = lipSync.Children.FirstOrDefault(c => c.Name == "RESOURCE.004");
        Assert.NotNull(r004);
        Assert.Equal(entries.Count, r004.Children.Count);
    }

    [Fact]
    public void SyntheticTnbt_VisemeLipSyncArchive()
    {
        byte[] track0 = [0, 2, 4, 8, 10, 12, 14, 16];
        byte[] track1 = [0, 4, 8, 16, 0];
        byte[] archive = SyntheticArchives.VisemesR3([track0, track1]);
        List<ArchiveEntry> entries = VisemeArchive.ReadEntries(archive, archive.Length);
        Assert.Equal(2, entries.Count);
        Assert.Equal(92000, (int)entries[0].Offset);
        Assert.Equal(track0.Length, (int)entries[0].Size);
        Assert.Equal(92000 + track0.Length, (int)entries[1].Offset);
        Assert.Equal(track1.Length, (int)entries[1].Size);

        string text = VisemeArchive.ToText(archive.AsSpan((int)entries[0].Offset, (int)entries[0].Size));
        Assert.Equal("0\n2\n4\n8\n10\n12\n14\n16\n", text);
    }

    [Fact]
    public void RealInstall_Tnbt_ResourceIfz()
    {
        string path = Path.Combine(TheNextBigThingTests.TnbtSteamDir, "Resource", "RESOURCE.IFZ");
        if (!File.Exists(path)) return;

        using FileStream fs = File.OpenRead(path);
        List<ArchiveEntry> entries = InterfaceArchive.ReadEntries(fs);
        Assert.NotEmpty(entries);
        Assert.Equal(95, entries.Count);

        var vfs = VirtualFileSystem.Init(TheNextBigThingTests.TnbtSteamDir, cache: ScanCache.Ephemeral(includeShipped: true));
        FsNode global = Assert.Single(vfs.Root.Children, c => c.Name == VirtualFileSystem.GlobalFolder);
        FsNode? ifz = global.Children.FirstOrDefault(c => c.Name == "RESOURCE.IFZ");
        Assert.NotNull(ifz);
        Assert.Equal("User Interface & HUD (RESOURCE.IFZ)", ifz.FriendlyName);
        Assert.Equal(95, ifz.Children.Count);

        var backgrounds = ifz.Children.Where(c => c.Kind == EntryKind.Background).ToList();
        Assert.NotEmpty(backgrounds);
        Assert.All(backgrounds, b => Assert.True(b.Image?.Width > 0 && b.Image?.Height > 0));

        vfs.ApplyLanguage("es");
        Assert.Equal("Interfaz de usuario y menús (RESOURCE.IFZ)", ifz.FriendlyName);
    }
}
