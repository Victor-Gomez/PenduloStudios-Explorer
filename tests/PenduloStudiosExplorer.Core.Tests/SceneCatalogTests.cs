using PenduloStudiosExplorer.Core.FileSystem;
using PenduloStudiosExplorer.Core.Metadata;
using Xunit;

namespace PenduloStudiosExplorer.Core.Tests;

public class SceneCatalogTests
{
    [Fact]
    public void OfficialChaptersAreDefinedBilingually()
    {
        Assert.Equal(6, SceneCatalog.Chapters.Count);

        SceneCatalog.ChapterInfo ch1 = SceneCatalog.GetChapter(1)!;
        Assert.NotNull(ch1);
        Assert.Equal("Wake Me Before Dying", ch1.TitleEn);
        Assert.Equal("Despiértame antes de morir", ch1.TitleEs);

        SceneCatalog.ChapterInfo ch2 = SceneCatalog.GetChapter(2)!;
        Assert.Equal("The Mysterious Crucifix", ch2.TitleEn);
        Assert.Equal("El extraño crucifijo", ch2.TitleEs);

        SceneCatalog.ChapterInfo ch3 = SceneCatalog.GetChapter(3)!;
        Assert.Equal("The Great Escape", ch3.TitleEn);
        Assert.Equal("La gran evasión", ch3.TitleEs);

        SceneCatalog.ChapterInfo ch4 = SceneCatalog.GetChapter(4)!;
        Assert.Equal("Close Encounters of the Fourth Kind", ch4.TitleEn);
        Assert.Equal("Encuentros en la cuarta fase", ch4.TitleEs);

        SceneCatalog.ChapterInfo ch5 = SceneCatalog.GetChapter(5)!;
        Assert.Equal("Gifts from the Crypt", ch5.TitleEn);
        Assert.Equal("La cripta sagrada", ch5.TitleEs);

        SceneCatalog.ChapterInfo ch6 = SceneCatalog.GetChapter(6)!;
        Assert.Equal("The Indian, the Nun and the Finger", ch6.TitleEn);
        Assert.Equal("El indio, la monja y el dedo", ch6.TitleEs);
    }

    [Fact]
    public void SceneTitlesContainChapterAndLocation()
    {
        string en = SceneCatalog.GetSceneTitle("RESOURCE.A01", "en");
        Assert.Contains("RESOURCE.A01", en);
        Assert.Contains("Ch. 1", en);
        Assert.Contains("Wake Me Before Dying", en);
        Assert.Contains("Hospital: Gina's Room", en);

        string es = SceneCatalog.GetSceneTitle("RESOURCE.A01", "es");
        Assert.Contains("RESOURCE.A01", es);
        Assert.Contains("Ch. 1", es);
        Assert.Contains("Despiértame antes de morir", es);
        Assert.Contains("Hospital: Habitación de Gina", es);
    }

    [Fact]
    public void AudioTitlesAreBilingual()
    {
        string m01En = SceneCatalog.GetAudioTitle("RESOURCE.M01", "en");
        string m01Es = SceneCatalog.GetAudioTitle("RESOURCE.M01", "es");
        Assert.Equal("Main Theme & Title (RESOURCE.M01)", m01En);
        Assert.Equal("Tema principal y título (RESOURCE.M01)", m01Es);

        string s08En = SceneCatalog.GetAudioTitle("RESOURCE.S08", "en");
        string s08Es = SceneCatalog.GetAudioTitle("RESOURCE.S08", "es");
        Assert.Equal("Military Camp Ambience & SFX (RESOURCE.S08)", s08En);
        Assert.Equal("Campamento militar: Ambiente y efectos (RESOURCE.S08)", s08Es);
    }

    [Fact]
    public void CategoryTitlesAreBilingual()
    {
        Assert.Equal("Scenes", SceneCatalog.GetCategoryTitle(VirtualFileSystem.ScenesFolder, "en"));
        Assert.Equal("Escenas", SceneCatalog.GetCategoryTitle(VirtualFileSystem.ScenesFolder, "es"));

        Assert.Equal("Music", SceneCatalog.GetCategoryTitle(VirtualFileSystem.MusicFolder, "en"));
        Assert.Equal("Música", SceneCatalog.GetCategoryTitle(VirtualFileSystem.MusicFolder, "es"));

        Assert.Equal("Voice", SceneCatalog.GetCategoryTitle(VirtualFileSystem.VoiceFolder, "en"));
        Assert.Equal("Voces", SceneCatalog.GetCategoryTitle(VirtualFileSystem.VoiceFolder, "es"));
    }

    [Fact]
    public void SceneEntryLabelsAreBilingual()
    {
        var bgNode = new FsNode
        {
            Name = "e00",
            Kind = EntryKind.Background,
            EntryIndex = 0,
            Image = new ImageInfo { Width = 1024, Height = 600 }
        };
        Assert.Equal("background 1024×600 (e00)", SceneCatalog.FormatSceneEntryLabel(bgNode, "en"));
        Assert.Equal("fondo de escena 1024×600 (e00)", SceneCatalog.FormatSceneEntryLabel(bgNode, "es"));

        var overlayNode = new FsNode
        {
            Name = "e05",
            Kind = EntryKind.Overlay,
            EntryIndex = 5,
            Image = new ImageInfo { Width = 200, Height = 100, X = 50, Y = 60 }
        };
        Assert.Equal("overlay 200×100 at 50,60 (e05)", SceneCatalog.FormatSceneEntryLabel(overlayNode, "en"));
        Assert.Equal("capa 200×100 en 50,60 (e05)", SceneCatalog.FormatSceneEntryLabel(overlayNode, "es"));

        var animNode = new FsNode
        {
            Name = "e12",
            Kind = EntryKind.Animation,
            EntryIndex = 12,
            Image = new ImageInfo { Width = 64, Height = 64, Frames = 8 }
        };
        Assert.Equal("animation, 8 frames, 64×64 (e12)", SceneCatalog.FormatSceneEntryLabel(animNode, "en"));
        Assert.Equal("animación, 8 fotogramas, 64×64 (e12)", SceneCatalog.FormatSceneEntryLabel(animNode, "es"));
    }

    [Fact]
    public void KnownSceneAssetNames_ProvideInvestigatedNamesBilingually()
    {
        string? d14Btn0En = SceneCatalog.GetKnownSceneAssetName("RESOURCE.D14", 2, "en");
        string? d14Btn0Es = SceneCatalog.GetKnownSceneAssetName("RESOURCE.D14", 2, "es");
        Assert.Equal("keypad numeric button '0'", d14Btn0En);
        Assert.Equal("botón numérico '0' del teclado", d14Btn0Es);

        string? f39DialEn = SceneCatalog.GetKnownSceneAssetName("RESOURCE.F39", 2, "en");
        string? f39DialEs = SceneCatalog.GetKnownSceneAssetName("RESOURCE.F39", 2, "es");
        Assert.Equal("rotating circular combination dial (0-99)", f39DialEn);
        Assert.Equal("dial circular giratorio de combinación (0-99)", f39DialEs);

        var node = new FsNode
        {
            Name = "e02",
            ArchivePath = @"C:\Games\Resource\RESOURCE.D14",
            EntryIndex = 2,
            Kind = EntryKind.Overlay,
            Image = new ImageInfo { Width = 32, Height = 32, X = 120, Y = 80 }
        };
        string formattedEn = SceneCatalog.FormatSceneEntryLabel(node, "en");
        string formattedEs = SceneCatalog.FormatSceneEntryLabel(node, "es");

        Assert.Equal("keypad numeric button '0' 32×32 at 120,80 (e02)", formattedEn);
        Assert.Equal("botón numérico '0' del teclado 32×32 en 120,80 (e02)", formattedEs);
    }

    [Fact]
    public void KnownSceneAssetNames_AreScopedByGameVersion_AndDoNotPolluteOtherGames()
    {
        // Runaway 1 RESOURCE.A01 entry 0 has an investigated name
        Assert.NotNull(SceneCatalog.GetKnownSceneAssetName(GameVersion.Runaway1, "RESOURCE.A01", 0, "en"));
        Assert.Equal("CD-ROM tray drive animation", SceneCatalog.GetKnownSceneAssetName(GameVersion.Runaway1, "RESOURCE.A01", 0, "en"));

        // Runaway 3 RESOURCE.A01 entry 0 must NOT inherit Runaway 1's name
        Assert.Null(SceneCatalog.GetKnownSceneAssetName(GameVersion.Runaway3, "RESOURCE.A01", 0, "en"));

        // FormatSceneEntryLabel for Runaway 3 RESOURCE.A01 entry 0 must format as normal background, not CD-ROM tray
        var r3Node = new FsNode
        {
            Name = "e00",
            ArchivePath = @"RESOURCE.A01",
            EntryIndex = 0,
            Kind = EntryKind.Background,
            Image = new ImageInfo { Width = 1650, Height = 720 },
            GameVersion = GameVersion.Runaway3,
        };
        string labelEn = SceneCatalog.FormatSceneEntryLabel(r3Node, "en");
        Assert.Equal("background 1650×720 (e00)", labelEn);

        // Runaway 3 mask e04 in RESOURCE.A01 must format as scene mask, not chapter select card
        var r3Mask = new FsNode
        {
            Name = "e04",
            ArchivePath = @"RESOURCE.A01",
            EntryIndex = 4,
            Kind = EntryKind.Mask,
            Image = new ImageInfo { Width = 1650, Height = 720 },
            GameVersion = GameVersion.Runaway3,
        };
        Assert.Equal("scene mask 1650×720 (e04)", SceneCatalog.FormatSceneEntryLabel(r3Mask, "en"));
    }
}

