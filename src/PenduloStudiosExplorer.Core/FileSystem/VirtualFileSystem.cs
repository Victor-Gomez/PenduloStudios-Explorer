using System.Buffers.Binary;
using System.Text;
using PenduloStudiosExplorer.Core.Formats;
using PenduloStudiosExplorer.Core.Metadata;

namespace PenduloStudiosExplorer.Core.FileSystem;

/// <summary>
/// The whole install as one tree. Runaway keeps its assets in nameless offset-table archives spread
/// over three folders, so the tree is organised by what the entries are rather than by where they sit:
/// <code>
/// &lt;install&gt;
///   Scenes            one folder per RESOURCE.&lt;L&gt;&lt;nn&gt;, entries classified as background / overlay / animation / data
///   Music             RESOURCE.M*
///   Ambient &amp; SFX     RESOURCE.S*
///   Cinematic Audio   RESOURCE.002
///   Voice             the DATAACA shards merged, grouped in blocks of 500 clips
///   Lip-sync          RESOURCE.004
///   Video             Datav/*, headers restored from DATAVC00
///   Characters        RESOURCE.001, Runaway 1's character sprite library
///   Global Data       RESOURCE.000 entries, plus the archives no decoder claims
/// </code>
/// Building it means classifying every scene-archive entry, which reads ~900 MB; see <see cref="ScanCache"/>.
/// </summary>
public sealed class VirtualFileSystem
{
    public const string ScenesFolder = "Scenes";
    public const string MusicFolder = "Music";
    public const string AmbientFolder = "Ambient & SFX";
    public const string CinematicFolder = "Cinematic Audio";
    public const string VoiceFolder = "Voice";
    public const string LipSyncFolder = "Lip-sync";
    public const string VideoFolder = "Video";
    public const string DialogueFolder = "Dialogue";
    public const string CharactersFolder = "Characters";
    public const string GlobalFolder = "Global Data";
    public const string SavegamesFolder = "Saves";

    public const int VoiceGroupSize = 500;
    public const int DialogueGroupSize = 500;

    public static readonly AudioInfo MusicPcm = new() { SampleRate = 16_000, Channels = 2, BitsPerSample = 16 };
    public static readonly AudioInfo AmbientPcm = new() { SampleRate = 22_050, Channels = 2, BitsPerSample = 16 };
    public static readonly AudioInfo CinematicPcm = new() { SampleRate = 22_050, Channels = 2, BitsPerSample = 16 };

    /// <summary>The files carry no rate; 16,000 Hz is what the lines sound right at (22,050 makes them rushed and high).</summary>
    public static readonly AudioInfo VoicePcm = new() { SampleRate = 16_000, Channels = 1, BitsPerSample = 8 };

    /// <summary>
    /// Hollywood Monsters' music and cinematic tracks (<c>RESOURCE.M*</c>, <c>RESOURCE.002</c>): 16-bit signed
    /// mono at 11,025 Hz. The autocorrelation of the samples decays smoothly from lag 1 -- which it would not
    /// do if the stream were interleaved stereo. No rate is stored, so the rate was settled by listening:
    /// 22,050 plays the tracks at double tempo.
    /// </summary>
    public static readonly AudioInfo HollywoodMonstersMusicPcm = new() { SampleRate = 11_025, Channels = 1, BitsPerSample = 16 };

    /// <summary>
    /// Hollywood Monsters' sound effects (<c>RESOURCE.S*</c>, <c>RESOURCE.001</c>): 8-bit unsigned mono at
    /// 11,025 Hz. One slot of <c>RESOURCE.S01</c> was shipped as a whole RIFF/WAVE file instead of a bare
    /// stream, and its <c>fmt </c> chunk states exactly this format.
    /// </summary>
    public static readonly AudioInfo HollywoodMonstersSoundPcm = new() { SampleRate = 11_025, Channels = 1, BitsPerSample = 8 };

    /// <summary>First <c>RESOURCE.000</c> slot holding a resident sound effect in <em>Hollywood Monsters</em>.</summary>
    public const int ResidentSoundFirstEntry = 0x55;

    /// <summary>Last such slot; these 14 run to the end of the archive.</summary>
    public const int ResidentSoundLastEntry = 0x62;

    /// <summary>
    /// Hollywood Monsters' voice bank (<c>RESOURCE.004</c>): 8-bit unsigned mono at 22,050 Hz -- twice the
    /// sound-effect rate, so it needs its own entry. Settled by listening; this is the Spanish release.
    /// </summary>
    public static readonly AudioInfo HollywoodMonstersVoicePcm = new() { SampleRate = 22_050, Channels = 1, BitsPerSample = 8 };

    private readonly Dictionary<string, DecodedImage?> _backgroundCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _backgroundGate = new();
    private readonly Dictionary<string, byte[]?> _attributeTableCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _attributeTableGate = new();
    private readonly Dictionary<string, List<(int Index, IndexedPalette Palette)>> _archivePalettes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _paletteGate = new();
    private readonly Dictionary<int, IndexedPalette?> _characterPalettes = [];
    private Dictionary<int, IndexedPalette>? _sharedPalettes;

    public VirtualFileSystem(string baseDir, FsNode root, VideoKeyfile? keyfile = null, GameVersion gameVersion = GameVersion.Runaway1)
    {
        BaseDir = baseDir;
        Root = root;
        Keyfile = keyfile;
        GameVersion = gameVersion;
    }

    public string BaseDir { get; }

    public FsNode Root { get; }

    /// <summary>The detected or configured game release.</summary>
    public GameVersion GameVersion { get; }

    /// <summary>Active metadata and display language (e.g. "en", "es").</summary>
    public string Language { get; private set; } = SceneCatalog.DefaultLanguage;

    /// <summary>The parsed <c>DATAVC00</c> keyfile, or <see langword="null"/> when the install has none.</summary>
    public VideoKeyfile? Keyfile { get; }

    /// <summary>Counts gathered while scanning, for the status line.</summary>
    public ScanSummary Summary { get; private set; } = new();

    public sealed class ScanSummary
    {
        public int SceneArchives { get; set; }
        public int Backgrounds { get; set; }
        public int Masks { get; set; }
        public int Overlays { get; set; }
        public int Animations { get; set; }
        public int DataEntries { get; set; }
        public int AudioClips { get; set; }
        public int VoiceClips { get; set; }
        public int Videos { get; set; }
        public int Visemes { get; set; }
        public int Phrases { get; set; }
        public int CharacterAnimations { get; set; }
        public int ActorAnimations { get; set; }
        public int Savegames { get; set; }
        public bool FromCache { get; set; }

        public override string ToString() =>
            $"{SceneArchives} scenes: {Backgrounds} backgrounds, {Masks} masks, {Overlays} overlays, {Animations} animations; " +
            $"{AudioClips} audio clips, {VoiceClips} voice lines, {Videos} videos, {Visemes} lip-sync tracks, {Phrases} dialogue phrases" +
            (CharacterAnimations > 0 ? $", {CharacterAnimations} character animations" : "") +
            (ActorAnimations > 0 ? $", {ActorAnimations} actor animations" : "") +
            (Savegames > 0 ? $", {Savegames} saves" : "");
    }

    // ---------------------------------------------------------------------------------------------
    // Building
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Scans <paramref name="baseDir"/> (the folder containing <c>Resource\</c>) and builds the tree.
    /// Throws <see cref="DirectoryNotFoundException"/> when no <c>Resource</c> folder is found.
    /// </summary>
    public static VirtualFileSystem Init(
        string baseDir,
        Action<string>? progress = null,
        ScanCache? cache = null,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseDir);
        baseDir = Path.GetFullPath(baseDir);

        GameVersion gameVersion = GameDetector.Detect(baseDir);

        string? resourceDir = FindDir(baseDir, "Resource");
        if (resourceDir is null && gameVersion == GameVersion.HollywoodMonsters)
        {
            // Hollywood Monsters has no Resource folder: the archives sit next to Monsters.exe, either in a
            // Monsters subfolder of the CD/install root or directly in the folder the user picked.
            resourceDir = FindDir(baseDir, "Monsters") ?? baseDir;
        }
        if (resourceDir is null)
            throw new DirectoryNotFoundException($"No 'Resource' folder under {baseDir}. Pick the game's install folder (the one containing Runaway.exe or RunawayTDOTT.exe).");
        cache ??= ScanCache.Ephemeral();
        string activeLanguage = language ?? SceneCatalog.DefaultLanguage;

        var root = new FsNode { NodeType = FsNodeType.Root | FsNodeType.Directory, Name = Path.GetFileName(baseDir.TrimEnd(Path.DirectorySeparatorChar)) };
        var summary = new ScanSummary();

        string? dataaDir = FindDir(baseDir, "Dataa");
        string? datavDir = FindDir(baseDir, "Datav");

        VideoKeyfile? keyfile = null;
        if (datavDir is not null && gameVersion is GameVersion.Runaway1 or GameVersion.Runaway2)
        {
            string? keyPath = Directory.EnumerateFiles(datavDir).FirstOrDefault(f => VideoKeyfile.IsKeyfileName(Path.GetFileName(f)));
            if (keyPath is not null)
            {
                try { keyfile = VideoKeyfile.Load(keyPath); }
                catch (IOException) { keyfile = null; }
            }
        }

        var vfs = new VirtualFileSystem(baseDir, root, keyfile, gameVersion)
        {
            Language = activeLanguage
        };

        string[] resourceFiles = gameVersion == GameVersion.HollywoodMonsters
            ? Directory.GetFiles(resourceDir, "RESOURCE.*", SearchOption.TopDirectoryOnly)
            : Directory.GetFiles(resourceDir, "*", SearchOption.AllDirectories);
        Array.Sort(resourceFiles, StringComparer.OrdinalIgnoreCase);

        FsNode scenes = Folder(root, ScenesFolder);
        scenes.FriendlyName = SceneCatalog.GetCategoryTitle(ScenesFolder, activeLanguage);
        FsNode music = Folder(root, MusicFolder);
        music.FriendlyName = SceneCatalog.GetCategoryTitle(MusicFolder, activeLanguage);
        FsNode ambient = Folder(root, AmbientFolder);
        ambient.FriendlyName = SceneCatalog.GetCategoryTitle(AmbientFolder, activeLanguage);
        FsNode cinematic = Folder(root, CinematicFolder);
        cinematic.FriendlyName = SceneCatalog.GetCategoryTitle(CinematicFolder, activeLanguage);
        FsNode voice = Folder(root, VoiceFolder);
        voice.FriendlyName = SceneCatalog.GetCategoryTitle(VoiceFolder, activeLanguage);
        FsNode lipSync = Folder(root, LipSyncFolder);
        lipSync.FriendlyName = SceneCatalog.GetCategoryTitle(LipSyncFolder, activeLanguage);
        FsNode video = Folder(root, VideoFolder);
        video.FriendlyName = SceneCatalog.GetCategoryTitle(VideoFolder, activeLanguage);
        FsNode dialogue = Folder(root, DialogueFolder);
        dialogue.FriendlyName = SceneCatalog.GetCategoryTitle(DialogueFolder, activeLanguage);
        FsNode characters = Folder(root, CharactersFolder);
        characters.FriendlyName = SceneCatalog.GetCategoryTitle(CharactersFolder, activeLanguage);
        FsNode global = Folder(root, GlobalFolder);
        global.FriendlyName = SceneCatalog.GetCategoryTitle(GlobalFolder, activeLanguage);

        // Scene archives are the expensive part; classify them in parallel, then attach in name order.
        List<string> sceneArchives = resourceFiles.Where(f => SceneArchive.IsSceneArchiveName(Path.GetFileName(f), gameVersion)).ToList();
        var sceneNodes = new FsNode?[sceneArchives.Count];
        int done = 0;
        bool anyFromCache = false;

        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 2, 6),
        };
        Parallel.For(0, sceneArchives.Count, options, i =>
        {
            string path = sceneArchives[i];
            progress?.Invoke($"Scanning {Path.GetFileName(path)}  ({Interlocked.Increment(ref done)}/{sceneArchives.Count})");
            (FsNode node, bool fromCache) = BuildSceneArchive(path, cache, activeLanguage, gameVersion, cancellationToken);
            sceneNodes[i] = node;
            if (fromCache)
                anyFromCache = true;
        });

        foreach (FsNode? node in sceneNodes)
        {
            if (node is null)
                continue;
            if (node.Children.Count == 0 && gameVersion == GameVersion.HollywoodMonsters)
                continue;
            Attach(scenes, node);
            summary.SceneArchives++;
            static void AccumulateSceneEntries(FsNode parent, ScanSummary sum)
            {
                foreach (FsNode child in parent.Children)
                {
                    if (child.Kind == EntryKind.Folder && child.Children.Count > 0)
                    {
                        AccumulateSceneEntries(child, sum);
                    }
                    else
                    {
                        switch (child.Kind)
                        {
                            case EntryKind.Background: sum.Backgrounds++; break;
                            case EntryKind.Mask: sum.Masks++; break;
                            case EntryKind.Overlay: sum.Overlays++; break;
                            case EntryKind.Animation: sum.Animations++; break;
                            case EntryKind.CharacterAnimation: sum.CharacterAnimations++; break;
                            default: sum.DataEntries++; break;
                        }
                    }
                }
            }
            AccumulateSceneEntries(node, summary);
        }
        cache.Save();
        summary.FromCache = anyFromCache;

        Dictionary<int, string>? transcripts = null;
        foreach (string path in resourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = Path.GetFileName(path);
            string upper = name.ToUpperInvariant();

            if (SceneArchive.IsSceneArchiveName(name, gameVersion))
                continue;

            bool hollywood = gameVersion == GameVersion.HollywoodMonsters;

            if (hollywood && upper == "RESOURCE.004")
            {
                // Not lip-sync here: RESOURCE.004 is the 265 MB voice bank, one clip per slot.
                progress?.Invoke($"Reading {name}");
                summary.VoiceClips += Attach(voice, BuildAudioArchive(path, EntryKind.Voice, HollywoodMonstersVoicePcm, dedupe: false, activeLanguage, gameVersion)).Children.Count;
            }
            else if (hollywood && upper is "RESOURCE.001" or "RESOURCE.002" or "RESOURCE.003")
            {
                if (upper == "RESOURCE.001")
                {
                    progress?.Invoke($"Reading {name}");
                    summary.AudioClips += Attach(ambient, BuildAudioArchive(path, EntryKind.Ambient, HollywoodMonstersSoundPcm, dedupe: false, activeLanguage, gameVersion)).Children.Count;
                }
                else if (upper == "RESOURCE.002")
                {
                    progress?.Invoke($"Reading {name}");
                    summary.AudioClips += Attach(cinematic, BuildAudioArchive(path, EntryKind.Cinematic, HollywoodMonstersMusicPcm, dedupe: true, activeLanguage, gameVersion)).Children.Count;
                }
                else
                {
                    // RESOURCE.003 is the script: obfuscated rows indexed by scene, not the Runaway 2
                    // phrase table (see docs/formats/global-data.md).
                    progress?.Invoke($"Reading {name}");
                    var (script, hmTranscripts) = BuildHollywoodScriptArchive(path, gameVersion, activeLanguage);
                    Attach(dialogue, script);
                    summary.Phrases = script.Children.Sum(stage => stage.Children.Count);
                    transcripts = hmTranscripts;
                }
            }
            else if (upper.StartsWith("RESOURCE.M", StringComparison.Ordinal))
            {
                progress?.Invoke($"Reading {name}");
                AudioInfo musicPcm = hollywood ? HollywoodMonstersMusicPcm : MusicPcm;
                summary.AudioClips += Attach(music, BuildAudioArchive(path, EntryKind.Music, musicPcm, dedupe: false, activeLanguage, gameVersion)).Children.Count;
            }
            else if (upper.StartsWith("RESOURCE.S", StringComparison.Ordinal))
            {
                progress?.Invoke($"Reading {name}");
                AudioInfo ambientPcm = hollywood ? HollywoodMonstersSoundPcm : AmbientPcm;
                summary.AudioClips += Attach(ambient, BuildAudioArchive(path, EntryKind.Ambient, ambientPcm, dedupe: false, activeLanguage, gameVersion)).Children.Count;
            }
            else if (upper == "RESOURCE.002" && gameVersion is GameVersion.Runaway1 or GameVersion.Runaway2)
            {
                progress?.Invoke($"Reading {name}");
                summary.AudioClips += Attach(cinematic, BuildAudioArchive(path, EntryKind.Cinematic, CinematicPcm, dedupe: true, activeLanguage, gameVersion)).Children.Count;
            }
            else if (upper == "RESOURCE.004")
            {
                progress?.Invoke($"Reading {name}");
                summary.Visemes += Attach(lipSync, BuildVisemeArchive(path)).Children.Count;
            }
            else if (upper == "RESOURCE.003")
            {
                progress?.Invoke($"Reading {name}");
                if (gameVersion == GameVersion.Runaway1)
                {
                    var (script, r1Transcripts) = BuildHollywoodScriptArchive(path, gameVersion, activeLanguage);
                    if (script.Children.Count > 0)
                    {
                        Attach(dialogue, script);
                        summary.Phrases = script.Children.Sum(stage => stage.Children.Count);
                        transcripts = r1Transcripts;
                    }
                    else
                    {
                        Attach(global, LooseFile(path));
                    }
                }
                else
                {
                    var (dNode, dTranscripts) = BuildPhraseArchive(path, gameVersion);
                    Attach(dialogue, dNode);
                    summary.Phrases = dTranscripts.Count;
                    transcripts = dTranscripts;
                }
            }
            else if (CharacterSpriteArchive.IsLibraryName(name, gameVersion))
            {
                progress?.Invoke($"Reading {name}");
                summary.CharacterAnimations += Attach(characters, BuildCharacterLibrary(path, activeLanguage)).Children.Count;
            }
            else if (upper == "RESOURCE.000")
            {
                progress?.Invoke($"Reading {name}");
                FsNode gNode = BuildGlobalArchive(path, gameVersion, cache, activeLanguage);
                gNode.FriendlyName = SceneCatalog.GetGlobalTitle(name, activeLanguage, gameVersion);
                Attach(global, gNode);

                // The player character's animations are slots of this same archive, but they span two
                // slots each and are not entries the classifier can recognise one at a time, so they get
                // their own folder beside the rest of the cast. The raw slots stay in Global Data: this
                // adds a reading of them, it does not replace one, which is also why nothing here
                // touches the scan cache.
                if (ActorArchive.IsActorArchive(name, gameVersion)
                    && BuildActorLibrary(path, activeLanguage) is { } actor)
                {
                    summary.ActorAnimations += Attach(characters, actor).Children.Count;
                }
            }
            else if (upper == "RESOURCE.IFZ" || upper == "RESOURCE.PSE")
            {
                // The Next BIG Thing and Yesterday interface and pause screen art
                progress?.Invoke($"Reading {name}");
                FsNode ifzNode = BuildInterfaceArchive(path);
                ifzNode.FriendlyName = SceneCatalog.GetGlobalTitle(name, activeLanguage, gameVersion);
                Attach(global, ifzNode);
            }
            else if (upper == "RESOURCE.TAB")
            {
                // The Next BIG Thing and Yesterday scene camera and waypoint geometry table
                progress?.Invoke($"Reading {name}");
                FsNode tabNode = BuildSceneTableArchive(path, gameVersion, activeLanguage);
                tabNode.FriendlyName = SceneCatalog.GetGlobalTitle(name, activeLanguage, gameVersion);
                Attach(global, tabNode);
            }
            else if (upper == "RESOURCE.FNT" && gameVersion == GameVersion.Yesterday)
            {
                // Yesterday 24-bit outlined font library
                progress?.Invoke($"Reading {name}");
                FsNode fntNode = BuildYesterdayFontArchive(path);
                fntNode.FriendlyName = SceneCatalog.GetGlobalTitle(name, activeLanguage, gameVersion);
                Attach(global, fntNode);
            }
            else if (upper == "RESOURCE.005" && gameVersion == GameVersion.Runaway3)
            {
                // Runaway 3 chapter interaction and event database
                progress?.Invoke($"Reading {name}");
                FsNode intNode = BuildChapterInteractionArchive(path);
                intNode.FriendlyName = SceneCatalog.GetGlobalTitle(name, activeLanguage, gameVersion);
                Attach(global, intNode);
            }
            else if (gameVersion == GameVersion.Yesterday && upper is "RESOURCE.CRD" or "RESOURCE.B31")
            {
                // Yesterday's Credits and B31 cutscene Bink videos in Resource/uk
                progress?.Invoke($"Reading {name}");
                var fi = new FileInfo(path);
                Attach(video, new FsNode
                {
                    NodeType = FsNodeType.File,
                    Kind = EntryKind.Video,
                    Name = name,
                    FriendlyName = $"{name}  {FormatSize(fi.Length)}",
                    Size = fi.Length,
                    ArchivePath = path,
                });
                summary.Videos++;
            }
            else
            {
                Attach(global, LooseFile(path));
            }
        }

        if (dataaDir is not null)
        {
            progress?.Invoke("Reading voice shards");
            summary.VoiceClips = BuildVoice(voice, dataaDir, gameVersion, transcripts);
        }

        string? videoDir = datavDir
            ?? FindDir(baseDir, "Video")
            ?? FindDir(baseDir, "Videos")
            ?? FindDir(baseDir, "Movies")
            ?? FindDir(baseDir, "Cinematics")
            ?? FindDir(baseDir, "Smk");

        if (videoDir is not null)
        {
            progress?.Invoke("Reading videos");
            summary.Videos = BuildVideos(video, videoDir, keyfile, gameVersion);
        }

        if (gameVersion == GameVersion.HollywoodMonsters)
        {
            summary.Videos += BuildHollywoodLooseVideos(video, baseDir, resourceDir);
        }

        string? saveDir = FindDir(baseDir, "SAVEGAME")
            ?? (Path.GetFileName(baseDir).Equals("SAVEGAME", StringComparison.OrdinalIgnoreCase) ? baseDir : null);
        if (saveDir is not null && gameVersion == GameVersion.Runaway1)
        {
            List<string> savePaths = SavegameArchive.FindSaves(saveDir);
            if (savePaths.Count > 0)
            {
                progress?.Invoke("Reading savegames");
                FsNode saves = Folder(root, SavegamesFolder);
                saves.FriendlyName = SceneCatalog.GetCategoryTitle(SavegamesFolder, activeLanguage);
                foreach (string savePath in savePaths)
                {
                    int slot = SavegameArchive.GetSlotNumber(savePath);
                    byte[] head = new byte[Math.Min(SavegameArchive.FileSize, 0x1B715)];
                    using (FileStream fs = File.OpenRead(savePath))
                    {
                        fs.ReadExactly(head, 0, head.Length);
                    }
                    SavegameInfo meta = SavegameArchive.ReadMetadata(head, slot, gameVersion);
                    string sceneTitle = meta.SceneArchiveName is not null
                        ? SceneCatalog.GetSceneTitle(meta.SceneArchiveName, activeLanguage, gameVersion)
                        : $"Scene {meta.SceneId}";
                    string label = meta.Name.Length > 0
                        ? $"Slot {slot:000} — \"{meta.Name}\" ({sceneTitle})"
                        : $"Slot {slot:000} ({sceneTitle})";

                    Attach(saves, new FsNode
                    {
                        NodeType = FsNodeType.File,
                        Kind = EntryKind.Savegame,
                        Name = Path.GetFileName(savePath),
                        FriendlyName = label,
                        Size = new FileInfo(savePath).Length,
                        ArchivePath = savePath,
                        Image = new ImageInfo { Width = SavegameArchive.ThumbnailWidth, Height = SavegameArchive.ThumbnailHeight },
                    });
                    summary.Savegames++;
                }
            }
        }

        // Empty categories only add noise.
        root.Children.RemoveAll(c => c.Children.Count == 0);

        vfs.Summary = summary;
        return vfs;
    }

    /// <summary>Updates display names of all nodes in memory to match the specified language.</summary>
    public void ApplyLanguage(string language)
    {
        Language = language;
        UpdateLanguageRecursive(Root, language);
    }

    private void UpdateLanguageRecursive(FsNode node, string language)
    {
        if (node == Root)
        {
            foreach (FsNode child in node.Children)
                UpdateLanguageRecursive(child, language);
            return;
        }

        if (node.Parent == Root && node.IsDirectory)
        {
            node.FriendlyName = SceneCatalog.GetCategoryTitle(node.Name, language);
        }
        else if (node.IsDirectory && node.Parent?.Parent == Root)
        {
            if (node.Parent.Name == ScenesFolder)
                node.FriendlyName = SceneCatalog.GetSceneTitle(node.Name, language, GameVersion);
            else if (node.Parent.Name is MusicFolder or AmbientFolder or CinematicFolder)
                node.FriendlyName = SceneCatalog.GetAudioTitle(node.Name, language, GameVersion);
            else if (node.Parent.Name == GlobalFolder)
                node.FriendlyName = SceneCatalog.GetGlobalTitle(node.Name, language, GameVersion);
        }
        else if (IsInsideScenes(node))
        {
            node.FriendlyName = SceneCatalog.FormatSceneEntryLabel(node, language, GameVersion);
        }
        else if (node.IsFile && node.Parent?.Parent?.Name == GlobalFolder)
        {
            if (GameVersion == GameVersion.HollywoodMonsters && node.Kind == EntryKind.Ambient)
            {
                string sfxName = SceneCatalog.GetHollywoodResidentSoundName(node.EntryIndex, language);
                string dur = FormatDuration(HollywoodMonstersSoundPcm.DurationSeconds(node.Size));
                node.FriendlyName = $"e{node.EntryIndex:000}  {sfxName} ({dur})";
            }
            else if (SceneCatalog.GetKnownGlobalAssetName(GameVersion, node.EntryIndex, language) is { } known)
            {
                if (GameVersion == GameVersion.Runaway1 && node.EntryIndex == 150 && node.Kind == EntryKind.Ambient)
                {
                    string dur = FormatDuration(VoicePcm.DurationSeconds(node.Size));
                    node.FriendlyName = $"e{node.EntryIndex:000}  {known} ({dur})";
                }
                else
                {
                    node.FriendlyName = $"e{node.EntryIndex:000}  {known}";
                }
            }
        }

        foreach (FsNode child in node.Children)
            UpdateLanguageRecursive(child, language);
    }

    private static bool IsInsideScenes(FsNode node)
    {
        for (FsNode? p = node.Parent; p is not null; p = p.Parent)
        {
            if (p.Name == ScenesFolder)
                return true;
        }
        return false;
    }

    internal static (FsNode Node, bool FromCache) BuildSceneArchive(string path, ScanCache cache, string language, GameVersion gameVersion, CancellationToken cancellationToken = default)
    {
        var node = new FsNode
        {
            NodeType = FsNodeType.Directory,
            Kind = EntryKind.Folder,
            Name = Path.GetFileName(path),
            FriendlyName = SceneCatalog.GetSceneTitle(Path.GetFileName(path), language, gameVersion),
            ArchivePath = path,
            Size = new FileInfo(path).Length,
        };

        List<ScanCache.CachedEntry>? cached = cache.TryGet(path);
        bool fromCache = cached is not null;
        if (cached is null)
        {
            cached = [];
            using FileStream f = File.OpenRead(path);
            List<ArchiveEntry> rawEntries = SceneArchive.ReadEntries(f);
            if (rawEntries.Count == 0 && gameVersion == GameVersion.HollywoodMonsters)
                rawEntries = SceneArchive.ReadHeaderlessScreenEntries(f.Length);
            var pairedWithPrev = new HashSet<int>();
            Span<byte> head = stackalloc byte[16];
            Span<byte> charHead = stackalloc byte[CharacterSpriteAsset.RecordSize];

            int? sceneWidth = null;
            int? sceneHeight = null;

            for (int i = 0; i < rawEntries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArchiveEntry entry = rawEntries[i];

                if (pairedWithPrev.Contains(i))
                {
                    cached.Add(ScanCache.CachedEntry.From(entry, EntryClassification.DataEntry));
                    continue;
                }

                bool isPaired = false;
                if (gameVersion == GameVersion.Runaway2 && i + 1 < rawEntries.Count)
                {
                    ArchiveEntry nextEntry = rawEntries[i + 1];
                    if (entry.Offset + entry.Size == nextEntry.Offset)
                    {
                        if (entry.Size == CharacterSpriteAsset.FrameTableSize && nextEntry.Size > 0)
                        {
                            f.Position = entry.Offset;
                            if (f.Read(charHead) == CharacterSpriteAsset.RecordSize
                                && CharacterSpriteAsset.CouldBeEntry(charHead, entry.Size + nextEntry.Size, bytesPerPixel: 2))
                            {
                                long totalSize = entry.Size + nextEntry.Size;
                                if (totalSize <= 50_000_000)
                                {
                                    var combined = new byte[totalSize];
                                    f.Position = entry.Offset;
                                    f.ReadExactly(combined);
                                    if (CharacterSpriteAsset.Parse(combined, bytesPerPixel: 2) is { } charSprite)
                                    {
                                        isPaired = true;
                                        pairedWithPrev.Add(i + 1);
                                        var combinedEntry = new ArchiveEntry(entry.Index, entry.Offset, totalSize);
                                        var classification = new EntryClassification(EntryKind.CharacterAnimation, new ImageInfo
                                        {
                                            X = charSprite.Bounds.X,
                                            Y = charSprite.Bounds.Y,
                                            Width = charSprite.Bounds.Width,
                                            Height = charSprite.Bounds.Height,
                                            Frames = charSprite.FrameCount,
                                        });
                                        cached.Add(ScanCache.CachedEntry.From(combinedEntry, classification));
                                    }
                                }
                            }
                        }
                        else if (entry.Size >= 16)
                        {
                            f.Position = entry.Offset;
                            if (f.Read(head) == 16)
                            {
                                ushort fc = BinaryPrimitives.ReadUInt16LittleEndian(head);
                                uint fo0 = BinaryPrimitives.ReadUInt32LittleEndian(head.Slice(2));
                                if (fc > 0 && fc <= 2000 && fo0 == 0 && entry.Size == 2 + fc * 14)
                                {
                                    long totalSize = entry.Size + nextEntry.Size;
                                    if (totalSize <= 50_000_000)
                                    {
                                        var combined = new byte[totalSize];
                                        f.Position = entry.Offset;
                                        f.ReadExactly(combined);
                                        if (SpriteAsset.Parse(combined) is { } sprite)
                                        {
                                            isPaired = true;
                                            pairedWithPrev.Add(i + 1);
                                            var combinedEntry = new ArchiveEntry(entry.Index, entry.Offset, totalSize);
                                            bool isAtlas = SpriteAtlasDetector.LooksLikeAtlas(sprite);
                                            var classification = new EntryClassification(EntryKind.Animation, new ImageInfo
                                            {
                                                X = sprite.Bounds.X,
                                                Y = sprite.Bounds.Y,
                                                Width = sprite.Bounds.Width,
                                                Height = sprite.Bounds.Height,
                                                Frames = sprite.FrameCount,
                                                IsAtlas = isAtlas,
                                            });
                                            cached.Add(ScanCache.CachedEntry.From(combinedEntry, classification));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (!isPaired)
                {
                    var buffer = new byte[entry.Size];
                    f.Position = entry.Offset;
                    f.ReadExactly(buffer);

                    if (YesterdayImagePackage.Detect(buffer) is { } pkg)
                    {
                        var folderCached = new ScanCache.CachedEntry
                        {
                            Index = entry.Index,
                            Offset = entry.Offset,
                            Size = entry.Size,
                            Kind = EntryKind.Folder,
                            Children = new List<ScanCache.CachedEntry>(pkg.Entries.Count),
                        };
                        for (int subIdx = 0; subIdx < pkg.Entries.Count; subIdx++)
                        {
                            var sub = pkg.Entries[subIdx];
                            folderCached.Children.Add(new ScanCache.CachedEntry
                            {
                                Index = subIdx,
                                Offset = entry.Offset + sub.Offset,
                                Size = sub.Size,
                                Kind = EntryKind.Overlay,
                                Width = sub.Width,
                                Height = sub.Height,
                                X = 0,
                                Y = 0,
                            });
                        }
                        cached.Add(folderCached);
                    }
                    else
                    {
                        EntryClassification c;
                        try
                        {
                            c = EntryClassifier.Classify(buffer, sceneWidth, sceneHeight, gameVersion);
                        }
                        catch (Exception)
                        {
                            // A malformed entry must not take the whole archive down; it is simply "data".
                            c = EntryClassification.DataEntry;
                        }
                        if (c.Kind == EntryKind.Background && sceneWidth is null && c.Image is not null)
                        {
                            sceneWidth = c.Image.Width;
                            sceneHeight = c.Image.Height;
                        }
                        cached.Add(ScanCache.CachedEntry.From(entry, c));
                    }
                }
            }
            cache.Put(path, cached);
        }

        foreach (ScanCache.CachedEntry e in cached)
        {
            if (e.Children is { Count: > 0 } subEntries)
            {
                var folderChild = new FsNode
                {
                    NodeType = FsNodeType.Directory | FsNodeType.InArchive,
                    Kind = EntryKind.Folder,
                    Name = $"e{e.Index:00}",
                    Offset = e.Offset,
                    Size = e.Size,
                    EntryIndex = e.Index,
                    ArchivePath = path,
                    GameVersion = gameVersion,
                };

                foreach (ScanCache.CachedEntry sub in subEntries)
                {
                    var subNode = new FsNode
                    {
                        NodeType = FsNodeType.File | FsNodeType.InArchive,
                        Kind = sub.Kind,
                        Name = $"e{sub.Index:00}",
                        Offset = sub.Offset,
                        Size = sub.Size,
                        EntryIndex = sub.Index,
                        ArchivePath = path,
                        Image = sub.ToImageInfo(),
                        GameVersion = gameVersion,
                    };
                    subNode.FriendlyName = SceneCatalog.FormatSceneEntryLabel(subNode, language, gameVersion);
                    Attach(folderChild, subNode);
                }
                folderChild.FriendlyName = SceneCatalog.FormatSceneEntryLabel(folderChild, language, gameVersion);
                Attach(node, folderChild);
            }
            else
            {
                var child = new FsNode
                {
                    NodeType = FsNodeType.File | FsNodeType.InArchive,
                    Kind = e.Kind,
                    Name = $"e{e.Index:00}",
                    Offset = e.Offset,
                    Size = e.Size,
                    EntryIndex = e.Index,
                    ArchivePath = path,
                    Image = e.ToImageInfo(),
                    GameVersion = gameVersion,
                };
                child.FriendlyName = SceneCatalog.FormatSceneEntryLabel(child, language, gameVersion);
                Attach(node, child);
            }
        }

        return (node, fromCache);
    }

    /// <summary>The tree label for a scene entry: index plus the decoded facts that identify it at a glance.</summary>
    public static string SceneEntryLabel(FsNode entry, GameVersion game = GameVersion.Runaway1) =>
        SceneCatalog.FormatSceneEntryLabel(entry, null, game);

    private static FsNode BuildAudioArchive(string path, EntryKind kind, AudioInfo pcm, bool dedupe, string language, GameVersion gameVersion)
    {
        var node = new FsNode
        {
            NodeType = FsNodeType.Directory,
            Name = Path.GetFileName(path),
            FriendlyName = SceneCatalog.GetAudioTitle(Path.GetFileName(path), language, gameVersion),
            ArchivePath = path,
            Size = new FileInfo(path).Length,
        };

        List<AudioArchiveEntry> entries;
        using (FileStream f = File.OpenRead(path))
        {
            if (gameVersion == GameVersion.HollywoodMonsters)
            {
                var head = new byte[(int)Math.Min(f.Length, 1 << 16)];
                f.ReadExactly(head);
                entries = AudioArchive.ReadHollywoodMonstersEntries(head, f.Length);
            }
            else
            {
                entries = AudioArchive.ReadAudioEntries(f);
            }
        }

        var seen = new HashSet<long>();
        foreach (AudioArchiveEntry e in entries)
        {
            if (dedupe && !seen.Add(e.Offset))
                continue;

            AudioInfo audioInfo;
            if (e.Format == AudioFormat.Mp3)
            {
                audioInfo = new AudioInfo { Format = AudioFormat.Mp3, SampleRate = 44_100, Channels = 2, BitsPerSample = 16 };
            }
            else if (e.Format == AudioFormat.Wav)
            {
                audioInfo = new AudioInfo { Format = AudioFormat.Wav, SampleRate = 44_100, Channels = 2, BitsPerSample = 16 };
            }
            else
            {
                audioInfo = pcm;
            }

            var child = new FsNode
            {
                NodeType = FsNodeType.File | FsNodeType.InArchive,
                Kind = kind,
                Name = $"t{e.Index:000}",
                Offset = e.Offset,
                Size = e.Size,
                EntryIndex = e.Index,
                ArchivePath = path,
                Audio = audioInfo,
                GameVersion = gameVersion,
            };
            child.FriendlyName = $"{child.Name}  {FormatDuration(audioInfo.DurationSeconds(e.Size))}";
            Attach(node, child);
        }
        return node;
    }

    private static FsNode BuildVisemeArchive(string path)
    {
        var node = new FsNode
        {
            NodeType = FsNodeType.Directory,
            Name = Path.GetFileName(path),
            ArchivePath = path,
            Size = new FileInfo(path).Length,
        };

        List<ArchiveEntry> entries;
        using (FileStream f = File.OpenRead(path))
            entries = VisemeArchive.ReadEntries(f);

        foreach (ArchiveEntry e in entries)
        {
            Attach(node, new FsNode
            {
                NodeType = FsNodeType.File | FsNodeType.InArchive,
                Kind = EntryKind.Viseme,
                Name = $"v{e.Index:00000}",
                FriendlyName = $"v{e.Index:00000}  {e.Size} tick{(e.Size == 1 ? "" : "s")}",
                Offset = e.Offset,
                Size = e.Size,
                EntryIndex = e.Index,
                ArchivePath = path,
            });
        }
        return node;
    }

    /// <summary>
    /// <c>RESOURCE.IFZ</c>, the interface archive of The Next BIG Thing and Yesterday: menu screens,
    /// cursors and the rest of the interface, stored as whole PNG and JPEG files. Entries that carry an
    /// image header are classified by it; the rest are the interface's own tables and stay undecoded.
    /// </summary>
    private static FsNode BuildInterfaceArchive(string path)
    {
        var node = new FsNode
        {
            NodeType = FsNodeType.Directory,
            Name = Path.GetFileName(path),
            ArchivePath = path,
            Size = new FileInfo(path).Length,
        };

        using FileStream f = File.OpenRead(path);
        List<ArchiveEntry> entries = InterfaceArchive.ReadEntries(f);
        if (entries.Count == 0)
            entries = SceneArchive.ReadEntries(f);

        var head = new byte[64];
        foreach (ArchiveEntry e in entries)
        {
            f.Position = e.Offset;
            int read = f.Read(head, 0, (int)Math.Min(head.Length, e.Size));
            ReadOnlySpan<byte> probe = head.AsSpan(0, read);

            EntryKind kind = EntryKind.GlobalData;
            ImageInfo? image = null;
            string what = FormatSize(e.Size);

            if (PngDecoder.TryGetDimensions(probe, out int w, out int h))
            {
                kind = EntryKind.Background;
                image = new ImageInfo { Width = w, Height = h };
                what = $"PNG {w}×{h}";
            }
            else if (JpegDecoder.IsJpeg(probe) && JpegDecoder.TryGetDimensions(probe, out w, out h))
            {
                kind = EntryKind.Background;
                image = new ImageInfo { Width = w, Height = h };
                what = $"JPEG {w}×{h}";
            }
            else if (BmpDecoder.TryReadHeader(probe, out w, out h))
            {
                kind = EntryKind.Background;
                image = new ImageInfo { Width = w, Height = h };
                what = $"bitmap {w}×{h}";
            }

            Attach(node, new FsNode
            {
                NodeType = FsNodeType.File | FsNodeType.InArchive,
                Kind = kind,
                Name = $"e{e.Index:000}",
                FriendlyName = $"e{e.Index:000}  {what}",
                Offset = e.Offset,
                Size = e.Size,
                EntryIndex = e.Index,
                ArchivePath = path,
                Image = image,
            });
        }

        return node;
    }

    /// <summary>
    /// <c>RESOURCE.TAB</c>, the scene geometry, camera, and waypoint table of The Next BIG Thing and Yesterday.
    /// Unpacks each defined scene as an entry showing its geometry (e.g. 1920×1080) and archive name.
    /// </summary>
    private static FsNode BuildSceneTableArchive(string path, GameVersion gameVersion, string language)
    {
        var node = new FsNode
        {
            NodeType = FsNodeType.Directory,
            Name = Path.GetFileName(path),
            ArchivePath = path,
            Size = new FileInfo(path).Length,
        };

        byte[] data = File.ReadAllBytes(path);
        List<SceneRecord> scenes = SceneTable.Parse(data);
        foreach (SceneRecord s in scenes)
        {
            string sceneTitle = SceneCatalog.GetSceneTitle(s.ArchiveName, language, gameVersion);
            string desc = s.Width > 0 && s.Height > 0
                ? $"{s.ArchiveName} ({s.Width}×{s.Height})"
                : s.ArchiveName;
            string label = $"s{s.SceneId:000} — {desc}  {FormatSize(s.Size)}";

            Attach(node, new FsNode
            {
                NodeType = FsNodeType.File | FsNodeType.InArchive,
                Kind = EntryKind.SceneTable,
                Name = $"s{s.SceneId:000}",
                FriendlyName = label,
                Offset = s.Offset,
                Size = s.Size,
                EntryIndex = s.Index,
                ArchivePath = path,
                Image = s.Width > 0 ? new ImageInfo { Width = s.Width, Height = s.Height } : null,
            });
        }

        return node;
    }

    /// <summary>
    /// Yesterday's 24-bit outlined font library (RESOURCE.FNT).
    /// </summary>
    private static FsNode BuildYesterdayFontArchive(string path)
    {
        var node = new FsNode
        {
            NodeType = FsNodeType.Directory,
            Name = Path.GetFileName(path),
            FriendlyName = $"{Path.GetFileName(path)}  (2 fonts)",
            Size = new FileInfo(path).Length,
            ArchivePath = path,
        };

        byte[] data = File.ReadAllBytes(path);
        List<YesterdayFont> fonts = YesterdayFontArchive.ReadFonts(data);
        foreach (YesterdayFont f in fonts)
        {
            Attach(node, new FsNode
            {
                NodeType = FsNodeType.File | FsNodeType.InArchive,
                Kind = EntryKind.Font,
                Name = $"font{f.Index}",
                FriendlyName = $"{f.Name}  ({f.Glyphs.Length} glyphs, 24-bit)",
                Size = f.BitmapRgb.Length,
                EntryIndex = f.Index,
                ArchivePath = path,
            });
        }

        return node;
    }

    /// <summary>
    /// Runaway 3's chapter interaction database (RESOURCE.005).
    /// </summary>
    private static FsNode BuildChapterInteractionArchive(string path)
    {
        var node = new FsNode
        {
            NodeType = FsNodeType.Directory,
            Name = Path.GetFileName(path),
            FriendlyName = $"{Path.GetFileName(path)}  (7 chapters)",
            Size = new FileInfo(path).Length,
            ArchivePath = path,
        };

        byte[] data = File.ReadAllBytes(path);
        if (ChapterInteractionArchive.IsInteractionDatabase(data))
        {
            var db = ChapterInteractionArchive.Parse(data);
            foreach (ChapterInfo ch in db.Chapters)
            {
                var chRecs = db.GetRecordsForChapter(ch.ChapterId).ToList();
                int sceneCount = chRecs.Select(r => r.SceneId).Distinct().Count();
                var chNode = Attach(node, new FsNode
                {
                    NodeType = FsNodeType.Directory,
                    Name = $"Chapter {ch.ChapterId}",
                    FriendlyName = $"Chapter {ch.ChapterId}  ({sceneCount} scenes, {ch.RecordCount} triggers)",
                    ArchivePath = path,
                    EntryIndex = ch.ChapterId,
                });

                var scenes = chRecs.GroupBy(r => r.SceneId).OrderBy(g => g.Key);
                foreach (var sceneGroup in scenes)
                {
                    int objCount = sceneGroup.Select(r => r.ObjectId).Distinct().Count();
                    var scNode = Attach(chNode, new FsNode
                    {
                        NodeType = FsNodeType.Directory,
                        Name = $"Scene {sceneGroup.Key:D3}",
                        FriendlyName = $"Scene {sceneGroup.Key:D3}  ({objCount} objects, {sceneGroup.Count()} triggers)",
                        ArchivePath = path,
                        EntryIndex = sceneGroup.Key,
                    });

                    foreach (InteractionRecord rec in sceneGroup.OrderBy(r => r.ObjectId).ThenBy(r => r.ActionId))
                    {
                        string label = !string.IsNullOrWhiteSpace(rec.DecodedLabel)
                            ? $"Obj {rec.ObjectId:D3} [{rec.VerbName}] — \"{rec.DecodedLabel}\""
                            : $"Obj {rec.ObjectId:D3} [{rec.VerbName}]";

                        Attach(scNode, new FsNode
                        {
                            NodeType = FsNodeType.File | FsNodeType.InArchive,
                            Kind = EntryKind.Data,
                            Name = $"trigger_{rec.Index:0000}",
                            FriendlyName = label,
                            Size = ChapterInteractionArchive.RecordSize,
                            EntryIndex = rec.Index,
                            Offset = rec.Offset,
                            ArchivePath = path,
                        });
                    }
                }
            }
        }

        return node;
    }

    /// <summary>
    /// <em>Runaway 1</em>'s character sprite library as one folder of animations.
    /// <para>
    /// Nothing is cached and nothing is probed: the container's own tiling decides which slots are live
    /// (see <see cref="CharacterSpriteArchive"/>), and every live slot is an animation by construction, so
    /// the scan reads 1,728 bytes of table and stops. The frame count needs the entry's 3,200-byte frame
    /// table as well, which is 168 small reads and still costs nothing beside a 108 MB classification.
    /// </para>
    /// </summary>
    private static FsNode BuildCharacterLibrary(string path, string language)
    {
        var node = new FsNode
        {
            NodeType = FsNodeType.Directory,
            Name = Path.GetFileName(path),
            ArchivePath = path,
            Size = new FileInfo(path).Length,
        };

        using FileStream f = File.OpenRead(path);
        List<ArchiveEntry> entries = CharacterSpriteArchive.ReadEntries(f);
        var table = new byte[CharacterSpriteAsset.FrameTableSize];

        foreach (ArchiveEntry e in entries)
        {
            f.Position = e.Offset;
            f.ReadExactly(table);
            int frames = 0;
            while (frames < CharacterSpriteAsset.MaxFrames
                   && table.AsSpan(frames * CharacterSpriteAsset.RecordSize, CharacterSpriteAsset.RecordSize).ContainsAnyExcept((byte)0))
            {
                frames++;
            }

            int width = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(24));
            int height = BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(28));

            var child = new FsNode
            {
                NodeType = FsNodeType.File | FsNodeType.InArchive,
                Kind = EntryKind.CharacterAnimation,
                Name = $"e{e.Index:000}",
                Offset = e.Offset,
                Size = e.Size,
                EntryIndex = e.Index,
                ArchivePath = path,
                Image = new ImageInfo { Width = width, Height = height, Frames = frames },
            };
            child.FriendlyName = SceneCatalog.IsSpanish(language)
                ? $"animación de personaje, {frames} fotogramas {width}×{height} ({child.Name})"
                : $"character animation, {frames} frames {width}×{height} ({child.Name})";
            Attach(node, child);
        }

        return node;
    }

    /// <summary>The eight facings, in the order the walk table stores them.</summary>
    private static readonly string[] FacingNamesEn =
        ["away", "up-right", "right", "down-right", "towards", "down-left", "left", "up-left"];

    private static readonly string[] FacingNamesEs =
        ["de espaldas", "arriba-derecha", "derecha", "abajo-derecha", "de frente", "abajo-izquierda", "izquierda", "arriba-izquierda"];

    /// <summary>
    /// Which facings each head view serves: the renderer picks one with <c>hmap = 0,1,2,2,3,4,4,5</c>,
    /// so heads 2 and 4 cover two facings each -- standing is never in profile, so a facing of 2 becomes
    /// 3 and 6 becomes 5 at the end of a walk (see <c>docs/formats/executable.md</c>).
    /// </summary>
    private static readonly int[][] HeadFacings = [[0], [1], [2, 3], [4], [5, 6], [7]];

    /// <summary>
    /// What a costume group is for, in the game's own terms: which scenes select it, and which side its
    /// key light comes from. The pairs are the same drawing lit from opposite sides, which is the only
    /// thing that distinguishes the second group of a pair from the first.
    /// </summary>
    private static (string Chapters, bool LitFromLeft) ActorGroupLabel(int group) => group switch
    {
        10 => ("chapters 1-2", false),
        27 => ("chapters 1-2", true),
        44 => ("chapter 3", false),
        61 => ("chapter 3", true),
        78 => ("chapters 4-6", false),
        _ => ("chapters 4-6", true),
    };

    private static (string Chapters, bool LitFromLeft) ActorGroupLabelEs(int group) => group switch
    {
        10 => ("capitulos 1-2", false),
        27 => ("capitulos 1-2", true),
        44 => ("capitulo 3", false),
        61 => ("capitulo 3", true),
        78 => ("capitulos 4-6", false),
        _ => ("capitulos 4-6", true),
    };

    /// <summary>
    /// The player character's own animations from <c>RESOURCE.000</c>, as one folder of 84: six costume
    /// groups of eight walk cycles and six talking heads (see <see cref="ActorArchive"/>).
    /// <para>
    /// Nothing is probed. A group is taken only when all seventeen of its slots are there at the sizes
    /// the layout requires, and then every animation in it exists by construction. A walk cycle is
    /// labelled from its frame record, which costs one table read per group; a head has to have frame 0
    /// walked, because a head record carries the box of the <em>body</em> it is painted over rather than
    /// its own -- 36 short reads, against a 110 MB archive.
    /// </para>
    /// <para>
    /// <see langword="null"/> when no group is present, so an install this layout does not fit gets no
    /// folder rather than an empty one.
    /// </para>
    /// </summary>
    private static FsNode? BuildActorLibrary(string path, string language)
    {
        using FileStream f = File.OpenRead(path);
        Dictionary<int, ArchiveEntry> slots = GlobalArchive
            .ReadEntries(f, GameVersion.Runaway1)
            .ToDictionary(e => e.Index);

        bool spanish = SceneCatalog.IsSpanish(language);
        var node = new FsNode
        {
            NodeType = FsNodeType.Directory,
            Name = "Actor",
            FriendlyName = spanish ? "el protagonista (RESOURCE.000)" : "the player character (RESOURCE.000)",
            ArchivePath = path,
            Size = new FileInfo(path).Length,
        };

        foreach (int group in ActorArchive.GroupSlots)
        {
            if (!ActorArchive.HasGroup(slots, group))
                continue;

            ArchiveEntry walkTable = slots[group];
            ArchiveEntry headTable = slots[group + ActorArchive.HeadTableSlot];
            var records = new byte[Math.Max(walkTable.Size, headTable.Size)];

            f.Position = walkTable.Offset;
            f.ReadExactly(records.AsSpan(0, (int)walkTable.Size));

            (string chapters, bool litFromLeft) = spanish ? ActorGroupLabelEs(group) : ActorGroupLabel(group);
            string light = spanish
                ? (litFromLeft ? "luz por la izquierda" : "luz por la derecha")
                : (litFromLeft ? "lit from the left" : "lit from the right");

            for (int dir = 0; dir < ActorArchive.WalkDirections; dir++)
            {
                var animation = new ActorAnimation(group, ActorRole.Walk, dir);
                int at = animation.TableOffset;
                int width = BinaryPrimitives.ReadInt32LittleEndian(records.AsSpan(at + 24));
                int height = BinaryPrimitives.ReadInt32LittleEndian(records.AsSpan(at + 28));
                string facing = (spanish ? FacingNamesEs : FacingNamesEn)[dir];
                Attach(node, ActorNode(
                    path, animation, slots[animation.DataSlot], width, height, ActorArchive.WalkFrames,
                    spanish
                        ? $"caminar {facing}, {chapters}, {light} ({ActorArchive.WalkFrames} fotogramas {width}x{height})"
                        : $"walk {facing}, {chapters}, {light} ({ActorArchive.WalkFrames} frames {width}x{height})"));
            }

            f.Position = headTable.Offset;
            f.ReadExactly(records.AsSpan(0, (int)headTable.Size));

            for (int head = 0; head < ActorArchive.Heads; head++)
            {
                var animation = new ActorAnimation(group, ActorRole.Head, head);
                (int width, int height) = HeadFrameSize(f, slots[animation.DataSlot], records, animation);
                string facings = string.Join("/", HeadFacings[head].Select(d => (spanish ? FacingNamesEs : FacingNamesEn)[d]));
                Attach(node, ActorNode(
                    path, animation, slots[animation.DataSlot], width, height, ActorArchive.HeadFrames,
                    spanish
                        ? $"cabeza {facings}, {chapters}, {light} ({ActorArchive.HeadFrames} fotogramas {width}x{height})"
                        : $"head {facings}, {chapters}, {light} ({ActorArchive.HeadFrames} frames {width}x{height})"));
            }
        }

        return node.Children.Count == 0 ? null : node;
    }

    private static FsNode ActorNode(
        string path, ActorAnimation animation, ArchiveEntry data, int width, int height, int frames, string friendly)
    {
        string name = animation.Role == ActorRole.Walk
            ? $"g{animation.Group:000}-walk{animation.Index}"
            : $"g{animation.Group:000}-head{animation.Index}";

        return new FsNode
        {
            NodeType = FsNodeType.File | FsNodeType.InArchive,
            Kind = EntryKind.ActorAnimation,
            Name = name,
            FriendlyName = $"{friendly} ({name})",
            Offset = data.Offset,
            Size = data.Size,
            EntryIndex = data.Index,
            ArchivePath = path,
            Image = new ImageInfo { Width = width, Height = height, Frames = frames },
        };
    }

    /// <summary>
    /// A talking head's own size, by walking frame 0. Its record states the box of the body frame the
    /// patch is painted over, so reading the size out of the record gives the whole character.
    /// </summary>
    private static (int Width, int Height) HeadFrameSize(
        FileStream f, ArchiveEntry data, byte[] headTable, ActorAnimation animation)
    {
        int at = animation.TableOffset;
        int frame0End = BinaryPrimitives.ReadInt32LittleEndian(headTable.AsSpan(at + CharacterSpriteAsset.RecordSize));
        if (frame0End <= 0 || frame0End > data.Size)
            return (0, 0);

        var bytes = new byte[frame0End];
        f.Position = data.Offset;
        f.ReadExactly(bytes);

        CharacterSpriteAsset? frame0 = CharacterSpriteAsset.Parse(
            headTable.AsSpan(at, CharacterSpriteAsset.RecordSize), 1, bytes, CharacterSpriteGeometry.HeadPatch);
        return frame0 is null ? (0, 0) : (frame0.Bounds.Width, frame0.Bounds.Height);
    }

    private static FsNode BuildGlobalArchive(string path, GameVersion gameVersion = GameVersion.Runaway1, ScanCache? cache = null, string? language = null)
    {
        var node = new FsNode
        {
            NodeType = FsNodeType.Directory,
            Name = Path.GetFileName(path),
            ArchivePath = path,
            Size = new FileInfo(path).Length,
        };

        // Classifying a global archive means reading it: hundreds of megabytes through the sprite walk
        // and the stride sweep. Remember the answer the same way the scene archives do.
        if (cache?.TryGet(path) is { Count: > 0 } remembered)
        {
            foreach (ScanCache.CachedEntry c in WithoutSwallowedSlots(remembered))
                AttachGlobalEntry(node, path, c, gameVersion);
            return node;
        }

        List<ArchiveEntry> entries;
        using (FileStream f = File.OpenRead(path))
            entries = GlobalArchive.ReadEntries(f, gameVersion);

        // Build an index by slot for fast neighbour lookup.
        var byIndex = new Dictionary<int, ArchiveEntry>();
        foreach (ArchiveEntry e in entries)
            byIndex[e.Index] = e;

        // Pre-classify: probe pairs for font glyph tables, and individual entries for the cursor atlas and
        // for whole BMP files. Which probes are worth running is decided by the game, not by the bytes:
        // only Runaway 1 chains its fonts this way, only Runaway 1 and 2 store the cursors as a keyed
        // raster, and only Runaway 3 keeps its interface art as BMP.
        var fontBitmapSlots = new HashSet<int>();
        var fontTableSlots = new HashSet<int>();
        var cursorAtlasSlots = new HashSet<int>();
        var bmpSlots = new Dictionary<int, (int Width, int Height)>();
        var pairedSpriteSlots = new Dictionary<int, (ImageInfo Image, long CombinedSize)>();
        var pairedSecondSlots = new HashSet<int>();

        var rasterSlots = new System.Collections.Concurrent.ConcurrentDictionary<int, (int Width, int Height)>();
        var spriteSlots = new System.Collections.Concurrent.ConcurrentDictionary<int, ImageInfo>();
        var overlaySlots = new System.Collections.Concurrent.ConcurrentDictionary<int, OverlayInfo>();

        // Runaway 2 and 3 keep the same fonts with a wider offset in each glyph record; the rest of the
        // pairing rule is unchanged.
        bool probeFonts = gameVersion is GameVersion.Runaway1 or GameVersion.Runaway2 or GameVersion.Runaway3;
        int fontRecordSize = FontDecoder.RecordSizeFor(gameVersion);
        bool probeCursors = gameVersion is GameVersion.Runaway1 or GameVersion.Runaway2;
        bool probeBmp = gameVersion == GameVersion.Runaway3;
        bool probePairedSprites = gameVersion == GameVersion.Runaway2;
        bool probeOverlays = gameVersion is GameVersion.Runaway1 or GameVersion.Runaway2 or GameVersion.Runaway3;

        // The RGB565 games' rasters are recognised by their exact size, and a size can lie: Runaway 2
        // stores one long data stream in consecutive slots of exactly one screen's worth of bytes
        // (e289-e291), which read as pixels are noise. Confirm the pixels before calling it a picture.
        bool probeRasters = gameVersion is GameVersion.Runaway1 or GameVersion.Runaway2 or GameVersion.Runaway3;

        // The global archives carry animations as well as interface art -- the pointing hands and menu
        // widgets, and in Runaway 2 and 3 whole characters. They are the scene archives' sprite format,
        // self-contained rather than split over a pair of slots, and they walk byte-exact like any other.
        bool probeSprites = gameVersion is GameVersion.Runaway1 or GameVersion.Runaway2 or GameVersion.Runaway3;

        if (probeFonts || probeCursors || probeBmp || probeRasters || probeSprites || probePairedSprites || probeOverlays)
        {
            using FileStream f = File.OpenRead(path);
            Span<byte> pairHead = stackalloc byte[16];

            foreach (ArchiveEntry e in entries)
            {
                // Paired sprite detection for Runaway 2 (e.g. slots 168-263):
                if (probePairedSprites && !pairedSecondSlots.Contains(e.Index)
                    && byIndex.TryGetValue(e.Index + 1, out ArchiveEntry nextSprite)
                    && e.Offset + e.Size == nextSprite.Offset
                    && e.Size >= 16 && e.Size < 50_000_000 && nextSprite.Size > 0 && nextSprite.Size < 50_000_000)
                {
                    f.Position = e.Offset;
                    if (f.Read(pairHead) == 16)
                    {
                        ushort fc = BinaryPrimitives.ReadUInt16LittleEndian(pairHead);
                        uint fo0 = BinaryPrimitives.ReadUInt32LittleEndian(pairHead.Slice(2));
                        if (fc > 0 && fc <= 2000 && fo0 == 0 && e.Size == 2 + fc * 14)
                        {
                            long totalSize = e.Size + nextSprite.Size;
                            var combined = new byte[totalSize];
                            f.Position = e.Offset;
                            f.ReadExactly(combined);
                            if (SpriteAsset.Parse(combined) is { } sprite)
                            {
                                bool isAtlas = SpriteAtlasDetector.LooksLikeAtlas(sprite);
                                pairedSpriteSlots[e.Index] = (new ImageInfo
                                {
                                    X = sprite.Bounds.X,
                                    Y = sprite.Bounds.Y,
                                    Width = sprite.Bounds.Width,
                                    Height = sprite.Bounds.Height,
                                    Frames = sprite.FrameCount,
                                    IsAtlas = isAtlas,
                                }, totalSize);
                                pairedSecondSlots.Add(nextSprite.Index);
                            }
                        }
                    }
                }
                // Font detection: if the next slot exists and its contents validate as a glyph table
                // whose records tile this entry exactly, mark both slots.
                if (probeFonts && byIndex.TryGetValue(e.Index + 1, out ArchiveEntry next))
                {
                    if (next.Size >= fontRecordSize && next.Size <= 10_000 && next.Size % fontRecordSize == 0
                        && e.Size > 0 && e.Size <= 200_000)
                    {
                        var tableData = new byte[next.Size];
                        f.Position = next.Offset;
                        f.ReadExactly(tableData);
                        if (FontDecoder.IsGlyphTable(tableData, e.Size, fontRecordSize))
                        {
                            fontBitmapSlots.Add(e.Index);
                            fontTableSlots.Add(next.Index);
                        }
                    }
                }

                // Cursor atlas detection: 904×540 RGB565 with key colour present.
                if (probeCursors && e.Size == CursorAtlasDecoder.AtlasWidth * CursorAtlasDecoder.AtlasHeight * 2)
                {
                    var head = new byte[Math.Min((int)e.Size, CursorAtlasDecoder.AtlasWidth * 20 * 2)];
                    f.Position = e.Offset;
                    f.ReadExactly(head);
                    if (CursorAtlasDecoder.LooksLikeAtlas(head))
                        cursorAtlasSlots.Add(e.Index);
                }

                // BMP detection: the whole file sits in the slot, header and all.
                if (probeBmp && e.Size >= BmpDecoder.MinHeaderSize)
                {
                    var head = new byte[BmpDecoder.MinHeaderSize];
                    f.Position = e.Offset;
                    f.ReadExactly(head);
                    if (BmpDecoder.TryReadHeader(head, out int bw, out int bh, out long bmpBytes) && bmpBytes <= e.Size)
                        bmpSlots[e.Index] = (bw, bh);
                }
            }
        }

        // The sprite walk and the stride sweep both read and chew through the whole entry, and a global
        // archive runs to hundreds of megabytes, so they go wide -- one file handle per worker -- the way
        // the scene archives are classified.
        if (probeSprites || probeRasters || probeOverlays)
        {
            Parallel.ForEach(
                entries.Where(e => !fontBitmapSlots.Contains(e.Index) && !fontTableSlots.Contains(e.Index)
                    && !cursorAtlasSlots.Contains(e.Index) && !bmpSlots.ContainsKey(e.Index)
                    && !pairedSpriteSlots.ContainsKey(e.Index) && !pairedSecondSlots.Contains(e.Index)),
                () => File.OpenRead(path),
                (e, state, f) =>
                {
                    if (e.Size is < 8 or > int.MaxValue)
                        return f;

                    // A head is enough to rule most entries out; only what survives is read in full.
                    // A global archive runs to hundreds of megabytes and most of it is neither.
                    var head = new byte[Math.Min(e.Size, GlobalProbeHeadBytes)];
                    f.Position = e.Offset;
                    f.ReadExactly(head);

                    bool couldBeSprite = probeSprites && e.Size >= SpriteAsset.RecordSize && SpriteAsset.CouldBeSprite(head, e.Size);
                    bool couldBeOverlay = probeOverlays && OverlayDecoder.CouldBeOverlay(head, e.Size);
                    bool knownGeometry = probeRasters && TryGlobalRaster(gameVersion, e.Size, out _, out _);
                    bool couldBeRaster = probeRasters && (knownGeometry || RasterDecoder.LooksLikeArtwork(head));
                    if (!couldBeSprite && !couldBeOverlay && !couldBeRaster)
                        return f;

                    byte[] data;
                    if (e.Size <= head.Length)
                    {
                        data = head;
                    }
                    else
                    {
                        data = new byte[e.Size];
                        f.Position = e.Offset;
                        f.ReadExactly(data);
                    }

                    // Sprite detection: the record table tiles the entry exactly, so a parse is proof.
                    if (couldBeSprite && SpriteAsset.Parse(data) is { } sprite)
                    {
                        bool isAtlas = SpriteAtlasDetector.LooksLikeAtlas(sprite);
                        spriteSlots[e.Index] = new ImageInfo
                        {
                            X = sprite.Bounds.X,
                            Y = sprite.Bounds.Y,
                            Width = sprite.Bounds.Width,
                            Height = sprite.Bounds.Height,
                            Frames = sprite.FrameCount,
                            IsAtlas = isAtlas,
                        };
                        return f;
                    }

                    // Overlay detection: row records consume the entry byte-exact.
                    if (couldBeOverlay && OverlayDecoder.Parse(data) is { } overlay)
                    {
                        overlaySlots[e.Index] = overlay;
                        return f;
                    }

                    if (!couldBeRaster)
                        return f;

                    // Raster detection: the right size for one of this game's screens, and pixels that
                    // behave like artwork rather than like the bytes of something else.
                    if (TryGlobalRaster(gameVersion, e.Size, out int rw, out int rh))
                    {
                        if (RasterDecoder.LooksLikeArtwork(data, rw, rh))
                            rasterSlots[e.Index] = (rw, rh);
                    }
                    else if (RasterDecoder.Detect(data, gameVersion) is { IsMask: false } detected
                        && RasterDecoder.LooksLikeArtwork(data, detected.Width, detected.Height))
                    {
                        // Not one of the screen geometries: a picture of its own size, its width
                        // recovered by the stride sweep exactly as a scene entry's would be.
                        rasterSlots[e.Index] = (detected.Width, detected.Height);
                    }

                    return f;
                },
                f => f.Dispose());
        }

        if (!probeRasters)
        {
            foreach (ArchiveEntry e in entries)
            {
                if (TryGlobalRaster(gameVersion, e.Size, out int rw, out int rh))
                    rasterSlots[e.Index] = (rw, rh);
            }
        }

        var classifiedEntries = new List<ScanCache.CachedEntry>(entries.Count);
        foreach (ArchiveEntry e in entries)
        {
            bool r1ClickSound = gameVersion == GameVersion.Runaway1 && e.Index == 150 && e.Size == 4978;
            bool residentSound = (gameVersion == GameVersion.HollywoodMonsters
                && e.Index >= ResidentSoundFirstEntry && e.Index <= ResidentSoundLastEntry) || r1ClickSound;

            EntryKind kind;
            ImageInfo? image = null;
            string label;
            ArchiveEntry cacheEntry = e;

            string? knownName = SceneCatalog.GetKnownGlobalAssetName(gameVersion, e.Index, language);

            if (pairedSpriteSlots.TryGetValue(e.Index, out (ImageInfo Image, long CombinedSize) paired))
            {
                kind = paired.Image.Frames == 1 ? EntryKind.Overlay : EntryKind.Animation;
                image = paired.Image;
                string what = knownName ?? (paired.Image.Frames == 1
                    ? $"sprite {image.Width}×{image.Height}"
                    : (image.IsAtlas
                        ? $"sprite atlas, {image.Frames} frames, {image.Width}×{image.Height}"
                        : $"animation, {image.Frames} frames, {image.Width}×{image.Height}"));
                label = $"e{e.Index:000}  {what}";
                cacheEntry = new ArchiveEntry(e.Index, e.Offset, paired.CombinedSize);
            }
            else if (residentSound)
            {
                kind = EntryKind.Ambient;
                if (r1ClickSound)
                {
                    string sfxName = knownName ?? "UI click / select SFX";
                    string dur = FormatDuration(VoicePcm.DurationSeconds(e.Size));
                    label = $"e{e.Index:000}  {sfxName} ({dur})";
                }
                else
                {
                    string sfxName = SceneCatalog.GetHollywoodResidentSoundName(e.Index, language);
                    string dur = FormatDuration(HollywoodMonstersSoundPcm.DurationSeconds(e.Size));
                    label = $"e{e.Index:000}  {sfxName} ({dur})";
                }
            }
            else if (fontBitmapSlots.Contains(e.Index))
            {
                kind = EntryKind.Font;
                string what = knownName ?? $"font bitmap, {FormatSize(e.Size)}";
                label = $"e{e.Index:000}  {what}";
            }
            else if (fontTableSlots.Contains(e.Index))
            {
                int glyphs = (int)(e.Size / fontRecordSize);
                kind = EntryKind.Font;
                string what = knownName is not null ? $"{knownName}, {glyphs} glyphs" : $"glyph table, {glyphs} glyphs";
                label = $"e{e.Index:000}  {what}";
            }
            else if (cursorAtlasSlots.Contains(e.Index))
            {
                kind = EntryKind.Background;
                image = new ImageInfo
                {
                    Width = CursorAtlasDecoder.AtlasWidth,
                    Height = CursorAtlasDecoder.AtlasHeight,
                };
                string what = knownName ?? $"cursor atlas {CursorAtlasDecoder.AtlasWidth}×{CursorAtlasDecoder.AtlasHeight}";
                label = $"e{e.Index:000}  {what}";
            }
            else if (bmpSlots.TryGetValue(e.Index, out (int Width, int Height) bmp))
            {
                kind = EntryKind.Background;
                image = new ImageInfo { Width = bmp.Width, Height = bmp.Height };
                string what = knownName ?? $"bitmap {bmp.Width}×{bmp.Height}";
                label = $"e{e.Index:000}  {what}";
            }
            else if (gameVersion == GameVersion.HollywoodMonsters && e.Index == 43 && e.Size == HollywoodItemIconsAsset.TotalBytes)
            {
                kind = EntryKind.Animation;
                image = new ImageInfo { Width = HollywoodItemIconsAsset.IconSize, Height = HollywoodItemIconsAsset.IconSize, Frames = HollywoodItemIconsAsset.IconCount };
                label = $"e{e.Index:000}  inventory item icons, 126 icons 64×64";
            }
            else if (spriteSlots.TryGetValue(e.Index, out ImageInfo? sprite))
            {
                kind = sprite.Frames == 1 ? EntryKind.Overlay : EntryKind.Animation;
                image = sprite;
                string what = knownName ?? (sprite.Frames == 1
                    ? $"sprite {sprite.Width}×{sprite.Height}"
                    : (sprite.IsAtlas
                        ? $"sprite atlas, {sprite.Frames} frames, {sprite.Width}×{sprite.Height}"
                        : $"animation, {sprite.Frames} frames, {sprite.Width}×{sprite.Height}"));
                label = $"e{e.Index:000}  {what}";
            }
            else if (overlaySlots.TryGetValue(e.Index, out OverlayInfo? overlay))
            {
                kind = EntryKind.Overlay;
                image = new ImageInfo
                {
                    X = overlay.X,
                    Y = overlay.Y,
                    Width = overlay.Width,
                    Height = overlay.Height,
                };
                string what = knownName ?? (overlay.HasAlpha
                    ? $"overlay {overlay.Width}×{overlay.Height} (alpha)"
                    : $"overlay {overlay.Width}×{overlay.Height}");
                label = $"e{e.Index:000}  {what}";
            }
            else if (rasterSlots.TryGetValue(e.Index, out (int Width, int Height) raster))
            {
                (int gw, int gh) = raster;
                kind = EntryKind.Background;
                image = new ImageInfo { Width = gw, Height = gh };
                string what = knownName ?? $"{GlobalRasterName(gameVersion, e.Index, gw)} {gw}×{gh}";
                label = $"e{e.Index:000}  {what}";
            }
            else if (gameVersion == GameVersion.Runaway1 && (e.Index == 118 || e.Index == 157) && e.Size == 48 * 48 * 2)
            {
                kind = EntryKind.Background;
                image = new ImageInfo { Width = 48, Height = 48 };
                string what = knownName ?? $"badge 48×48";
                label = $"e{e.Index:000}  {what}";
            }
            else
            {
                kind = EntryKind.GlobalData;
                string what = knownName ?? FormatSize(e.Size);
                label = $"e{e.Index:000}  {what}";
            }

            var classified = ScanCache.CachedEntry.From(cacheEntry, new EntryClassification(kind, image));
            classified.Label = label;
            classifiedEntries.Add(classified);
        }

        foreach (ScanCache.CachedEntry c in WithoutSwallowedSlots(classifiedEntries))
            AttachGlobalEntry(node, path, c, gameVersion);

        cache?.Put(path, classifiedEntries);
        return node;
    }

    /// <summary>
    /// Drops the entries whose bytes another entry already serves. Runaway 2 splits some sprites over two
    /// slots -- the frame table in one, the pixels in the next -- and the first slot's entry is widened to
    /// cover both, so the second on its own is only an undecodable tail of it (e183 under e182). The rule
    /// is read off the entries themselves rather than off the probe that paired them, so a cached scan
    /// hides the same slots a fresh one does without the cache having to say so. Only unclassified data
    /// is dropped, and only when it starts strictly inside another entry: two slots that alias the same
    /// bytes (Hollywood Monsters' resident sounds) start at the same offset and both stay.
    /// </summary>
    internal static IEnumerable<ScanCache.CachedEntry> WithoutSwallowedSlots(IReadOnlyList<ScanCache.CachedEntry> entries)
    {
        var spans = entries.Where(c => c.Kind != EntryKind.GlobalData)
            .Select(c => (Start: c.Offset, End: c.Offset + c.Size))
            .ToList();

        return entries.Where(c => c.Kind != EntryKind.GlobalData
            || !spans.Any(s => s.Start < c.Offset && c.Offset + c.Size <= s.End));
    }

    /// <summary>Hangs one classified global-archive entry on the archive node.</summary>
    private static void AttachGlobalEntry(FsNode node, string path, ScanCache.CachedEntry c, GameVersion gameVersion)
    {
        AudioInfo? audio = null;
        if (c.Kind == EntryKind.Ambient)
        {
            if (gameVersion == GameVersion.HollywoodMonsters)
                audio = HollywoodMonstersSoundPcm;
            else if (gameVersion == GameVersion.Runaway1 && c.Index == 150)
                audio = VoicePcm;
        }

        Attach(node, new FsNode
        {
            NodeType = FsNodeType.File | FsNodeType.InArchive,
            Kind = c.Kind,
            Name = $"e{c.Index:000}",
            FriendlyName = c.Label ?? $"e{c.Index:000}  {FormatSize(c.Size)}",
            Offset = c.Offset,
            Size = c.Size,
            EntryIndex = c.Index,
            ArchivePath = path,
            Image = c.Kind is EntryKind.GlobalData or EntryKind.Ambient or EntryKind.Font ? null : c.ToImageInfo(),
            Audio = audio,
            GameVersion = gameVersion,
        });
    }

    private static FsNode LooseFile(string path)
    {
        var fi = new FileInfo(path);
        return new FsNode
        {
            NodeType = FsNodeType.File,
            Kind = EntryKind.RawFile,
            Name = fi.Name,
            FriendlyName = $"{fi.Name}  {FormatSize(fi.Length)}",
            Size = fi.Length,
            ArchivePath = path,
        };
    }

    /// <summary>
    /// <summary>
    /// Builds the <em>Hollywood Monsters</em> or <em>Runaway 1</em> script tree from <c>RESOURCE.003</c>:
    /// one folder per scene, holding that scene's hotspot labels and its spoken lines. A line that the scene's
    /// cues pair with a recording carries the voice-bank slot, so the viewer can point at the clip.
    /// </summary>
    private static (FsNode Node, Dictionary<int, string> Transcripts) BuildHollywoodScriptArchive(
        string path, GameVersion gameVersion = GameVersion.HollywoodMonsters, string language = "en")
    {
        var node = new FsNode
        {
            NodeType = FsNodeType.Directory,
            Name = Path.GetFileName(path),
            ArchivePath = path,
            Size = new FileInfo(path).Length,
        };

        List<HollywoodScript.Stage> stages;
        using (FileStream f = File.OpenRead(path))
            stages = HollywoodScript.ReadStages(f, gameVersion);

        var transcripts = new Dictionary<int, string>();

        foreach (HollywoodScript.Stage stage in stages)
        {
            string groupName;
            string friendlyName;

            if (gameVersion == GameVersion.Runaway1)
            {
                int areaNum = stage.Index / 100;
                int sceneNum = stage.Index % 100;
                string archiveName = areaNum >= 1 && areaNum <= 9
                    ? $"RESOURCE.{(char)('A' + areaNum - 1)}{sceneNum:00}"
                    : $"scene {stage.SceneNumber}";
                string sceneTitle = SceneCatalog.GetSceneTitle(archiveName, language, gameVersion);
                groupName = archiveName;
                friendlyName = !string.IsNullOrEmpty(sceneTitle) && archiveName != sceneTitle
                    ? $"{archiveName} — {sceneTitle}  ({stage.Lines.Count} lines)"
                    : $"{archiveName}  ({stage.Lines.Count} lines)";
            }
            else
            {
                groupName = $"scene {stage.SceneNumber}";
                friendlyName = $"scene {stage.SceneNumber}  ({stage.Lines.Count} lines, {stage.Labels.Count} labels)";
            }

            var group = new FsNode
            {
                NodeType = FsNodeType.Directory,
                Name = groupName,
                FriendlyName = friendlyName,
                ArchivePath = path,
                EntryIndex = stage.Index,
            };
            Attach(node, group);

            foreach (HollywoodScript.Label label in stage.Labels)
            {
                Attach(group, new FsNode
                {
                    NodeType = FsNodeType.File | FsNodeType.InArchive,
                    Kind = EntryKind.Dialogue,
                    Name = $"label {label.Id:000}",
                    FriendlyName = $"label {label.Id:000}  \"{Preview(label.Text)}\"",
                    Size = HollywoodScript.SmallRowSize,
                    EntryIndex = label.Id,
                    ArchivePath = path,
                    Subtitle = label.Text,
                });
            }

            foreach (HollywoodScript.Line line in stage.Lines)
            {
                Attach(group, new FsNode
                {
                    NodeType = FsNodeType.File | FsNodeType.InArchive,
                    Kind = EntryKind.Dialogue,
                    Name = $"line {line.Id:0000}",
                    FriendlyName = $"line {line.Id:0000}  \"{Preview(line.Text)}\"",
                    Size = HollywoodScript.LargeRowSize,
                    EntryIndex = line.Id,
                    ArchivePath = path,
                    Subtitle = line.Text,
                    VoiceClipIndex = line.VoiceClipId,
                });

                if (line.VoiceClipId >= 0 && !string.IsNullOrEmpty(line.Text))
                {
                    transcripts.TryAdd(line.VoiceClipId, line.Text);
                }
            }
        }

        return (node, transcripts);
    }

    private static string Preview(string text) =>
        text.Length > 40 ? text[..37] + "..." : text;

    private static (FsNode Node, Dictionary<int, string> Transcripts) BuildPhraseArchive(string path, GameVersion gameVersion)
    {
        var node = new FsNode
        {
            NodeType = FsNodeType.Directory,
            Name = Path.GetFileName(path),
            ArchivePath = path,
            Size = new FileInfo(path).Length,
        };

        var transcripts = new Dictionary<int, string>();
        List<PhraseArchive.Phrase> phrases;
        using (FileStream f = File.OpenRead(path))
            phrases = PhraseArchive.ReadPhrases(f);

        var groups = new Dictionary<int, FsNode>();
        foreach (PhraseArchive.Phrase phrase in phrases)
        {
            if (!string.IsNullOrEmpty(phrase.Text))
                transcripts[phrase.Index] = phrase.Text;

            int g = phrase.Index / DialogueGroupSize;
            if (!groups.TryGetValue(g, out FsNode? group))
            {
                int lo = g * DialogueGroupSize;
                group = new FsNode
                {
                    NodeType = FsNodeType.Directory,
                    Name = $"{lo:00000}-{lo + DialogueGroupSize - 1:00000}",
                    ArchivePath = path,
                };
                groups[g] = group;
                Attach(node, group);
            }

            long recOffset = 4 + (long)phrases.Count * 4 + (long)phrase.Index * PhraseArchive.RecordSize;
            string preview = phrase.Text.Length > 40 ? phrase.Text[..37] + "..." : phrase.Text;
            var phraseNode = new FsNode
            {
                NodeType = FsNodeType.File | FsNodeType.InArchive,
                Kind = EntryKind.Dialogue,
                Name = $"d{phrase.Index:00000}",
                FriendlyName = string.IsNullOrEmpty(phrase.Text)
                    ? $"d{phrase.Index:00000} (ID {phrase.Id})"
                    : $"d{phrase.Index:00000} (ID {phrase.Id})  \"{preview}\"",
                Offset = recOffset,
                Size = PhraseArchive.RecordSize,
                EntryIndex = phrase.Index,
                ArchivePath = path,
                Subtitle = phrase.Text,
            };
            Attach(group, phraseNode);
        }

        return (node, transcripts);
    }

    private static int BuildVoice(FsNode voice, string dataaDir, GameVersion gameVersion, Dictionary<int, string>? transcripts = null)
    {
        if (gameVersion is GameVersion.TheNextBigThing or GameVersion.Yesterday)
        {
            var dataaFiles = Directory.EnumerateFiles(dataaDir, "DATAA*.000", SearchOption.AllDirectories).OrderBy(f => f).ToList();
            if (dataaFiles.Count == 0)
                return 0;

            var clips = new SortedDictionary<int, VoiceArchive.VoiceClip>();
            foreach (string file in dataaFiles)
            {
                var fileClips = VoiceArchive.ReadNamedArchiveClips(file);
                foreach (var kvp in fileClips)
                {
                    if (!clips.ContainsKey(kvp.Key))
                        clips[kvp.Key] = kvp.Value;
                }
            }

            if (clips.Count == 0)
                return 0;

            var groups = new Dictionary<int, FsNode>();
            var voiceMp3 = new AudioInfo { Format = AudioFormat.Mp3, SampleRate = 44_100, Channels = 1, BitsPerSample = 16 };

            foreach (VoiceArchive.VoiceClip clip in clips.Values)
            {
                int g = clip.Index / VoiceGroupSize;
                if (!groups.TryGetValue(g, out FsNode? group))
                {
                    int lo = g * VoiceGroupSize;
                    group = new FsNode
                    {
                        NodeType = FsNodeType.Directory,
                        Name = $"{lo:00000}-{lo + VoiceGroupSize - 1:00000}",
                    };
                    groups[g] = group;
                    Attach(voice, group);
                }

                var node = new FsNode
                {
                    NodeType = FsNodeType.File | FsNodeType.InArchive,
                    Kind = EntryKind.Voice,
                    Name = $"VOICE_{clip.Index:00000}",
                    Offset = clip.Offset,
                    Size = clip.Size,
                    EntryIndex = clip.Index,
                    ArchivePath = clip.ShardPath,
                    Audio = voiceMp3,
                };
                if (transcripts is not null && transcripts.TryGetValue(clip.Index, out string? sub) && !string.IsNullOrEmpty(sub))
                {
                    node.Subtitle = sub;
                    string preview = sub.Length > 35 ? sub[..32] + "..." : sub;
                    node.FriendlyName = $"{node.Name}  \"{preview}\"";
                }
                else
                {
                    node.FriendlyName = $"{node.Name}  {FormatDuration(VoicePcm.DurationSeconds(clip.Size))}";
                }
                Attach(group, node);
            }
            return clips.Count;
        }

        if (gameVersion is GameVersion.Runaway2 or GameVersion.Runaway3)
        {
            string? dataaa = Directory.EnumerateFiles(dataaDir)
                .FirstOrDefault(f => Path.GetFileName(f).Equals("Dataaa.000", StringComparison.OrdinalIgnoreCase));
            if (dataaa is null)
                return 0;

            SortedDictionary<int, VoiceArchive.VoiceClip> clips = VoiceArchive.ReadSingleArchiveClips(dataaa);
            var groups = new Dictionary<int, FsNode>();
            var voiceMp3 = new AudioInfo { Format = AudioFormat.Mp3, SampleRate = 44_100, Channels = 1, BitsPerSample = 16 };

            foreach (VoiceArchive.VoiceClip clip in clips.Values)
            {
                int g = clip.Index / VoiceGroupSize;
                if (!groups.TryGetValue(g, out FsNode? group))
                {
                    int lo = g * VoiceGroupSize;
                    group = new FsNode
                    {
                        NodeType = FsNodeType.Directory,
                        Name = $"{lo:00000}-{lo + VoiceGroupSize - 1:00000}",
                    };
                    groups[g] = group;
                    Attach(voice, group);
                }

                var node = new FsNode
                {
                    NodeType = FsNodeType.File | FsNodeType.InArchive,
                    Kind = EntryKind.Voice,
                    Name = $"VOICE_{clip.Index:00000}",
                    Offset = clip.Offset,
                    Size = clip.Size,
                    EntryIndex = clip.Index,
                    ArchivePath = clip.ShardPath,
                    Audio = voiceMp3,
                };
                if (transcripts is not null && transcripts.TryGetValue(clip.Index, out string? sub) && !string.IsNullOrEmpty(sub))
                {
                    node.Subtitle = sub;
                    string preview = sub.Length > 35 ? sub[..32] + "..." : sub;
                    node.FriendlyName = $"{node.Name}  \"{preview}\"";
                }
                else
                {
                    node.FriendlyName = $"{node.Name}  {FormatDuration(VoicePcm.DurationSeconds(clip.Size))}";
                }
                Attach(group, node);
            }
            return clips.Count;
        }

        var r1Shards = VoiceArchive.ShardNames()
            .Select(name => Directory.EnumerateFiles(dataaDir).FirstOrDefault(f => Path.GetFileName(f).Equals(name, StringComparison.OrdinalIgnoreCase)))
            .Where(path => path is not null)
            .Cast<string>()
            .ToList();

        if (r1Shards.Count == 0)
            return 0;

        SortedDictionary<int, VoiceArchive.VoiceClip> r1Clips = VoiceArchive.ReadClips(r1Shards);
        var r1Groups = new Dictionary<int, FsNode>();
        foreach (VoiceArchive.VoiceClip clip in r1Clips.Values)
        {
            int g = clip.Index / VoiceGroupSize;
            if (!r1Groups.TryGetValue(g, out FsNode? group))
            {
                int lo = g * VoiceGroupSize;
                group = new FsNode
                {
                    NodeType = FsNodeType.Directory,
                    Name = $"{lo:00000}-{lo + VoiceGroupSize - 1:00000}",
                };
                r1Groups[g] = group;
                Attach(voice, group);
            }

            var node = new FsNode
            {
                NodeType = FsNodeType.File | FsNodeType.InArchive,
                Kind = EntryKind.Voice,
                Name = $"VOICE_{clip.Index:00000}",
                Offset = clip.Offset,
                Size = clip.Size,
                EntryIndex = clip.Index,
                ArchivePath = clip.ShardPath,
                Audio = VoicePcm,
            };
            if (transcripts is not null && transcripts.TryGetValue(clip.Index, out string? sub) && !string.IsNullOrEmpty(sub))
            {
                node.Subtitle = sub;
                string preview = sub.Length > 35 ? sub[..32] + "..." : sub;
                node.FriendlyName = $"{node.Name}  \"{preview}\"";
            }
            else
            {
                node.FriendlyName = $"{node.Name}  {FormatDuration(VoicePcm.DurationSeconds(clip.Size))}";
            }
            Attach(group, node);
        }
        return r1Clips.Count;
    }

    private static int BuildVideos(FsNode video, string datavDir, VideoKeyfile? keyfile, GameVersion gameVersion)
    {
        int count = 0;
        string[] files = Directory.GetFiles(datavDir);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (string path in files)
        {
            string name = Path.GetFileName(path);
            if (gameVersion == GameVersion.Yesterday && name.EndsWith(".001", StringComparison.OrdinalIgnoreCase))
                continue; // Subtitle files in Yesterday are paired with matching .000 videos below

            bool isPlainBink = gameVersion is GameVersion.Runaway3 or GameVersion.TheNextBigThing or GameVersion.Yesterday or GameVersion.HollywoodMonsters;
            if (!isPlainBink && VideoKeyfile.IsKeyfileName(name))
                continue;

            var fi = new FileInfo(path);
            bool isSmk = name.EndsWith(".smk", StringComparison.OrdinalIgnoreCase);
            ImageInfo? imgInfo = null;

            if (isSmk || (gameVersion == GameVersion.HollywoodMonsters && fi.Length >= SmackerVideo.HeaderSize))
            {
                byte[] head = new byte[Math.Min(fi.Length, SmackerVideo.HeaderSize)];
                using (var fs = File.OpenRead(path))
                    fs.ReadExactly(head);
                if (SmackerVideo.TryParse(head) is { } smk)
                {
                    imgInfo = new ImageInfo { Width = smk.Width, Height = smk.Height };
                    isSmk = true;
                }
            }

            bool restorable = isPlainBink || isSmk || keyfile?.HeaderFor(name) is not null;

            string? subTrack = null;
            string? subPreview = null;
            if (gameVersion == GameVersion.Yesterday)
            {
                string subPath = Path.ChangeExtension(path, ".001");
                if (File.Exists(subPath))
                {
                    try
                    {
                        string subContent = File.ReadAllText(subPath, Encoding.Latin1);
                        var cues = CutsceneSubtitles.Parse(subContent);
                        if (cues.Count > 0)
                        {
                            subTrack = CutsceneSubtitles.ToSummary(cues);
                            subPreview = cues[0].Text;
                            if (subPreview.Length > 35) subPreview = subPreview[..32] + "...";
                        }
                    }
                    catch { }
                }
            }

            string friendly;
            if (imgInfo is not null)
                friendly = $"{name}  {FormatSize(fi.Length)}  ({imgInfo.Width}×{imgInfo.Height})";
            else if (subPreview is not null)
                friendly = $"{name}  {FormatSize(fi.Length)}  \"{subPreview}\"";
            else
                friendly = $"{name}  {FormatSize(fi.Length)}{(restorable ? "" : " (no header in keyfile)")}";

            Attach(video, new FsNode
            {
                NodeType = FsNodeType.File,
                Kind = restorable ? EntryKind.Video : EntryKind.RawFile,
                Name = name,
                FriendlyName = friendly,
                Size = fi.Length,
                ArchivePath = path,
                Subtitle = subTrack,
                Image = imgInfo,
            });
            if (restorable)
                count++;
        }
        return count;
    }

    private static int BuildHollywoodLooseVideos(FsNode video, string baseDir, string? resourceDir)
    {
        int count = 0;
        var scannedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { baseDir };
        if (!string.IsNullOrEmpty(resourceDir))
            scannedDirs.Add(resourceDir);

        foreach (string dir in scannedDirs)
        {
            if (!Directory.Exists(dir))
                continue;

            foreach (string path in Directory.GetFiles(dir))
            {
                string name = Path.GetFileName(path);
                if (!name.EndsWith(".smk", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (video.Children.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var fi = new FileInfo(path);
                if (fi.Length < SmackerVideo.HeaderSize)
                    continue;

                byte[] head = new byte[SmackerVideo.HeaderSize];
                using (var fs = File.OpenRead(path))
                    fs.ReadExactly(head);

                if (SmackerVideo.TryParse(head) is { } smk)
                {
                    Attach(video, new FsNode
                    {
                        NodeType = FsNodeType.File,
                        Kind = EntryKind.Video,
                        Name = name,
                        FriendlyName = $"{name}  {FormatSize(fi.Length)}  ({smk.Width}×{smk.Height})",
                        Size = fi.Length,
                        ArchivePath = path,
                        Image = new ImageInfo { Width = smk.Width, Height = smk.Height },
                    });
                    count++;
                }
            }
        }
        return count;
    }

    private static FsNode Folder(FsNode parent, string name) =>
        Attach(parent, new FsNode { NodeType = FsNodeType.Directory, Name = name });

    private static FsNode Attach(FsNode parent, FsNode child)
    {
        child.Parent = parent;
        parent.Children.Add(child);
        return child;
    }

    private static string? FindDir(string root, string name)
    {
        if (!Directory.Exists(root))
            return null;
        return Directory.EnumerateDirectories(root)
            .FirstOrDefault(d => Path.GetFileName(d).Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    // ---------------------------------------------------------------------------------------------
    // Navigation
    // ---------------------------------------------------------------------------------------------

    public IEnumerable<FsNode> GetDirectories(FsNode node) => node.Children.Where(c => c.IsDirectory);

    public IEnumerable<FsNode> GetFiles(FsNode node) =>
        node.Children
            .Where(c => c.IsFile)
            .OrderBy(c => GetTypeOrder(c.Kind))
            .ThenBy(c => c.EntryIndex >= 0 ? c.EntryIndex : int.MaxValue)
            .ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase);

    internal static int GetTypeOrder(EntryKind kind) => kind switch
    {
        EntryKind.Background => 1,
        EntryKind.Mask => 2,
        EntryKind.Overlay => 3,
        EntryKind.Animation => 4,
        EntryKind.CharacterAnimation => 4,
        EntryKind.ActorAnimation => 4,
        EntryKind.Music => 5,
        EntryKind.Ambient => 6,
        EntryKind.Cinematic => 7,
        EntryKind.Voice => 8,
        EntryKind.Video => 9,
        EntryKind.Viseme => 10,
        EntryKind.Data => 99,
        EntryKind.GlobalData => 100,
        EntryKind.RawFile => 101,
        _ => 50,
    };

    /// <summary>Every file node under <paramref name="node"/>, depth-first in tree order.</summary>
    public IEnumerable<FsNode> EnumerateFiles(FsNode node)
    {
        foreach (FsNode child in node.Children)
        {
            if (child.IsFile)
                yield return child;
            if (child.IsDirectory)
            {
                foreach (FsNode desc in EnumerateFiles(child))
                    yield return desc;
            }
        }
    }

    /// <summary>Resolves a <c>\</c>-delimited path produced by <see cref="FsNode.GetPath"/>. Case-insensitive.</summary>
    public FsNode? FindNode(FsNode root, string path)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (string.IsNullOrEmpty(path))
            return null;

        FsNode current = root;
        foreach (string segment in path.Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            FsNode? next = current.Children.FirstOrDefault(c => c.Name.Equals(segment, StringComparison.OrdinalIgnoreCase));
            if (next is null)
                return null;
            current = next;
        }
        return current;
    }

    // ---------------------------------------------------------------------------------------------
    // Bytes
    // ---------------------------------------------------------------------------------------------

    /// <summary>The node's bytes exactly as stored: the archive window for an entry, the whole file for a loose file.</summary>
    public Stream OpenFile(FsNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if ((!node.IsFile && ((node.NodeType & FsNodeType.InArchive) == 0 || node.Size == 0)) || string.IsNullOrEmpty(node.ArchivePath))
            throw new InvalidOperationException($"'{node.GetPath()}' has no bytes to open.");

        var stream = new FileStream(node.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        if ((node.NodeType & FsNodeType.InArchive) == 0)
            return stream;
        return new ArchiveWindowStream(stream, node.Offset, node.Size);
    }

    public byte[] ReadBytes(FsNode node)
    {
        using Stream s = OpenFile(node);
        var buffer = new byte[s.Length];
        s.ReadExactly(buffer);
        return buffer;
    }

    /// <summary>
    /// The scene background (entry 0 of the owning archive) for any node inside a scene archive, decoded
    /// once and cached. <see langword="null"/> when the node is not in a scene archive or entry 0 is not a
    /// raster.
    /// </summary>
    public DecodedImage? SceneBackgroundFor(FsNode node)
    {
        FsNode? archive = node;
        while (archive is not null && archive.Parent?.Name != ScenesFolder)
            archive = archive.Parent;
        if (archive is null || archive.Parent?.Name != ScenesFolder || archive.ArchivePath is null)
            return null;

        lock (_backgroundGate)
        {
            if (_backgroundCache.TryGetValue(archive.ArchivePath, out DecodedImage? cached))
                return cached;
        }

        FsNode? entry0 = archive.Children.FirstOrDefault(c => c.Kind == EntryKind.Background && c.EntryIndex == 0)
                         ?? archive.Children.FirstOrDefault(c => c.Kind == EntryKind.Background);
        DecodedImage? image = null;
        if (entry0?.Image is { } info)
        {
            byte[] bytes = ReadBytes(entry0);
            if (PngDecoder.IsPng(bytes))
                image = PngDecoder.Decode(bytes);
            else if (JpegDecoder.IsJpeg(bytes))
                image = JpegDecoder.Decode(bytes);
            else if (GameVersion == GameVersion.HollywoodMonsters)
                image = RasterDecoder.DecodeIndexed(bytes, info.Width, info.Height, ScenePaletteFor(entry0) ?? IndexedPalette.Grayscale);
            else
                image = RasterDecoder.Decode(bytes, info.Width, info.Height);
        }

        lock (_backgroundGate)
            _backgroundCache[archive.ArchivePath] = image;
        return image;
    }

    /// <summary>
    /// The colour table to decode one node of a Hollywood Monsters scene archive with.
    /// <para>
    /// A palette block applies to the entries that <b>follow</b> it, up to the next block: the table is
    /// loaded as the archive is read, so each block recolours everything after it. An archive usually has
    /// one, at entry 1, but sixteen have more -- either a second cast palette part-way through
    /// (<c>RESOURCE.A03</c> e08) or a whole second scene, background and palette included
    /// (<c>RESOURCE.C07</c> e33/e34). Picking the "best" block for the whole archive instead renders every
    /// entry on the wrong side of the boundary in the wrong colours.
    /// </para>
    /// <para>
    /// Entries before the first block -- entry 0, the background -- take that first block. The chosen block
    /// fills the bottom of the table; when it defines fewer than 256 colours (the usual 176), the rest come
    /// from the matching shared block in <c>RESOURCE.000</c>, which is where the characters' colours live.
    /// <see langword="null"/> for any other game, and for an archive with no palette block.
    /// </para>
    /// </summary>
    public IndexedPalette? ScenePaletteFor(FsNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (GameVersion != GameVersion.HollywoodMonsters)
            return null;

        bool isArchiveNode = node.IsDirectory && node.Parent?.Name is ScenesFolder or GlobalFolder;
        FsNode? archive = isArchiveNode ? node : node.Parent;
        if (archive?.ArchivePath is null || archive.Parent?.Name is not (ScenesFolder or GlobalFolder))
            return null;

        // An archive node itself (its background) is coloured by the archive's first block.
        int entryIndex = isArchiveNode ? int.MinValue : node.EntryIndex;
        List<(int Index, IndexedPalette Palette)> blocks = ArchivePalettes(archive);
        if (blocks.Count == 0)
            return null;

        // The nearest block at or before the entry; entries ahead of the first block take that first one.
        (int Index, IndexedPalette Palette) chosen = blocks[0];
        foreach ((int index, IndexedPalette palette) in blocks)
        {
            if (index > entryIndex)
                break;
            chosen = (index, palette);
        }
        return chosen.Palette;
    }

    /// <summary>
    /// Every valid palette block of a scene archive, in entry order, each already completed with the shared
    /// tail. Read once per archive: an entry that merely has a palette's size but does not parse as one is
    /// skipped, so it cannot displace the block that actually governs the entries around it.
    /// </summary>
    private List<(int Index, IndexedPalette Palette)> ArchivePalettes(FsNode archive)
    {
        string path = archive.ArchivePath!;
        lock (_paletteGate)
        {
            if (_archivePalettes.TryGetValue(path, out List<(int, IndexedPalette)>? cached))
                return cached;
        }

        var blocks = new List<(int Index, IndexedPalette Palette)>();
        foreach (FsNode entry in archive.Children)
        {
            if (TryReadPalette(entry) is not { } palette)
                continue;
            if (palette.DefinedCount < IndexedPalette.Colors &&
                SharedPalette(IndexedPalette.Colors - palette.DefinedCount) is { } tail)
            {
                palette = palette.WithTail(tail);
            }
            blocks.Add((entry.EntryIndex, palette));
        }
        blocks.Sort((a, b) => a.Index.CompareTo(b.Index));

        lock (_paletteGate)
            _archivePalettes[path] = blocks;
        return blocks;
    }

    /// <summary>
    /// The colour table for one entry of the character sprite library, from <c>RESOURCE.000</c>.
    /// <para>
    /// The pairing is not stored in either file; it is the library's own three-block layout, which every
    /// live entry agrees with -- see <see cref="CharacterSpriteArchive.PaletteSlotFor"/>.
    /// <see langword="null"/> for any other node, and when the install has no <c>RESOURCE.000</c>.
    /// </para>
    /// </summary>
    public IndexedPalette? CharacterPaletteFor(FsNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.EntryIndex < 0)
            return null;

        // The actor's own animations name their palette outright -- it is the second slot of their
        // group -- so nothing has to be inferred for them.
        int slot;
        if (node.Kind == EntryKind.ActorAnimation
            || (node.ArchivePath is not null
                && ActorArchive.IsActorArchive(Path.GetFileName(node.ArchivePath), GameVersion)
                && ActorArchive.FromDataSlot(node.EntryIndex) is not null))
        {
            if (ActorArchive.FromDataSlot(node.EntryIndex) is not { } animation)
                return null;
            slot = animation.PaletteSlot;
        }
        else if (node.Kind == EntryKind.CharacterAnimation)
        {
            if (GameVersion != GameVersion.Runaway1)
                return null;
            slot = CharacterSpriteArchive.PaletteSlotFor(node.EntryIndex);
        }
        else
        {
            return null;
        }

        lock (_paletteGate)
        {
            if (_characterPalettes.TryGetValue(slot, out IndexedPalette? cached))
                return cached;
        }

        IndexedPalette? palette = null;
        FsNode? entry = Root.Children
            .FirstOrDefault(c => c.Name == GlobalFolder)?.Children
            .FirstOrDefault(c => c.Name.Equals("RESOURCE.000", StringComparison.OrdinalIgnoreCase))?.Children
            .FirstOrDefault(c => c.EntryIndex == slot);
        if (entry is not null && entry.Size == IndexedPalette.Rgb565BlockBytes)
        {
            try { palette = IndexedPalette.TryParseRgb565(ReadBytes(entry)); }
            catch (IOException) { palette = null; }
        }

        lock (_paletteGate)
            _characterPalettes[slot] = palette;
        return palette;
    }

    /// <summary>The <c>RESOURCE.000</c> block that defines exactly <paramref name="colors"/> colours, if there is one.</summary>
    public IndexedPalette? SharedPalette(int colors)
    {
        lock (_paletteGate)
        {
            if (_sharedPalettes is null)
            {
                _sharedPalettes = [];
                FsNode? globalArchive = Root.Children
                    .FirstOrDefault(c => c.Name == GlobalFolder)?.Children
                    .FirstOrDefault(c => c.Name.Equals("RESOURCE.000", StringComparison.OrdinalIgnoreCase));
                if (globalArchive is not null)
                {
                    foreach (FsNode entry in globalArchive.Children)
                    {
                        if (TryReadPalette(entry) is { } p)
                            _sharedPalettes.TryAdd(p.DefinedCount, p);
                    }
                }
            }
            return _sharedPalettes.GetValueOrDefault(colors);
        }
    }

    private IndexedPalette? TryReadPalette(FsNode entry)
    {
        if (!entry.IsFile ||
            entry.Size < IndexedPalette.MinBlockBytes ||
            entry.Size > IndexedPalette.FullBlockBytes ||
            entry.Size % IndexedPalette.BytesPerColor != 0)
        {
            return null;
        }

        try
        {
            return IndexedPalette.TryParse(ReadBytes(entry));
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// The scene archive's 1536-byte attribute table (Entry 2) for any node inside a scene archive, read once
    /// and cached. <see langword="null"/> when not in a scene archive or no 1536-byte entry exists.
    /// </summary>
    public byte[]? SceneAttributeTableFor(FsNode node)
    {
        FsNode? archive = node;
        while (archive is not null && archive.Parent?.Name != ScenesFolder)
            archive = archive.Parent;
        if (archive is null || archive.Parent?.Name != ScenesFolder || archive.ArchivePath is null)
            return null;

        lock (_attributeTableGate)
        {
            if (_attributeTableCache.TryGetValue(archive.ArchivePath, out byte[]? cached))
                return cached;
        }

        FsNode? tableEntry = archive.Children.FirstOrDefault(c => c.Size is 1536 or 1792);
        byte[]? tableBytes = null;
        if (tableEntry is not null)
        {
            try
            {
                tableBytes = ReadBytes(tableEntry);
            }
            catch
            {
                // Ignore missing/corrupt table and return null
            }
        }

        lock (_attributeTableGate)
            _attributeTableCache[archive.ArchivePath] = tableBytes;
        return tableBytes;
    }

    /// <summary>Writes the restored Bink stream for a <see cref="EntryKind.Video"/> node.</summary>
    public bool RestoreVideo(FsNode node, Stream output)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Kind != EntryKind.Video || node.ArchivePath is null)
            return false;

        if (GameVersion is GameVersion.Runaway3 or GameVersion.TheNextBigThing or GameVersion.Yesterday or GameVersion.HollywoodMonsters || Keyfile is null)
        {
            using FileStream src = File.OpenRead(node.ArchivePath);
            src.CopyTo(output);
            return true;
        }

        return Keyfile.Restore(node.ArchivePath, output);
    }

    // ---------------------------------------------------------------------------------------------
    // Formatting helpers shared by the labels
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The geometries the raw RGB565 rasters of a game's <c>RESOURCE.000</c> come in. Nothing in the
    /// archive records a width, so an entry is recognised by its exact size -- see
    /// <c>docs/formats/global-data.md</c>. Runaway 3 keeps the same art as BMP, header and all, so it has
    /// no row here.
    /// </summary>
    private static (int Width, int Height)[] GlobalRasterGeometries(GameVersion game) => game switch
    {
        GameVersion.Runaway1 =>
        [
            (Rgb565.ScreenWidth, Rgb565.ScreenHeight), // menu panel, inventory screen, item close-ups
            (904, 480),                                // inventory item icon sheets
            (700, 74),                                 // UI sprite strips
            (80, 1840),                                // Chapter 1 inventory strip
            (80, 2079),                                // Chapter 2 inventory strip
            (80, 3360),                                // Chapter 3 inventory strip
            (80, 3760),                                // Chapter 4 inventory strip
            (80, 1360),                                // Chapter 5 inventory strip
            (80, 1920),                                // Chapter 6 inventory strip
        ],
        GameVersion.Runaway2 =>
        [
            (Rgb565.ScreenWidth, Rgb565.ScreenHeight), // menu and inventory screens, item close-ups
            (700, 74),                                 // UI sprite strips
        ],
        _ => [],
    };

    private static bool TryGlobalRaster(GameVersion game, long size, out int width, out int height)
    {
        // Hollywood Monsters is one byte per pixel:
        // Screen-sized (1024x480): menu panel (entry 42)
        // Bottom action panel (1024x189 = 193,536 bytes): entry 47
        // Dialogue menu frame (640x45 = 28,800 bytes): entry 48
        if (game == GameVersion.HollywoodMonsters)
        {
            if (size == (long)Rgb565.ScreenWidth * HollywoodMonstersScreenHeight)
            {
                (width, height) = (Rgb565.ScreenWidth, HollywoodMonstersScreenHeight);
                return true;
            }
            if (size == 1024 * 189)
            {
                (width, height) = (1024, 189);
                return true;
            }
            if (size == 640 * 45)
            {
                (width, height) = (640, 45);
                return true;
            }
            (width, height) = (0, 0);
            return false;
        }

        foreach ((int w, int h) in GlobalRasterGeometries(game))
        {
            if (size == (long)w * h * 2)
            {
                (width, height) = (w, h);
                return true;
            }
        }

        (width, height) = (0, 0);
        return false;
    }

    /// <summary>How much of a global entry is read to decide whether the rest is worth reading.</summary>
    private const int GlobalProbeHeadBytes = 64 * 1024;

    /// <summary>Hollywood Monsters draws at 1024x480, not the 1024x600 the Runaway games use.</summary>
    private const int HollywoodMonstersScreenHeight = 480;

    /// <summary>What a recognised <c>RESOURCE.000</c> raster is, for the tree label.</summary>
    private static string GlobalRasterName(GameVersion game, int index, int width) => (game, index) switch
    {
        (GameVersion.HollywoodMonsters, 47) => "bottom action panel",
        (GameVersion.HollywoodMonsters, 48) => "dialogue menu panel",
        (GameVersion.HollywoodMonsters, _) => "menu panel",
        // Easy to get backwards: 131 looks like a title backdrop, but 132 is the one carrying the volume
        // knob, the brightness lever and the setting holes the menu draws its entries over. Those slots are
        // Runaway 1's, and Runaway 2 puts different art at the same indices, so it only gets the shape.
        (GameVersion.Runaway1, 131) => "inventory screen",
        (GameVersion.Runaway1, 132) => "menu panel",
        _ when width == 904 => "item icons",
        _ when width == 700 => "UI strip",
        // Only a picture the size of the game's own screen is a screen; the rest are sheets and panels
        // of their own size, recovered by the stride sweep, and nothing says what they are for.
        _ when width == Rgb565.ScreenWidth || width == 1280 => "screen",
        _ => "raster",
    };

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024)
            return $"{kb:0.#} KB";
        double mb = kb / 1024.0;
        if (mb < 1024)
            return $"{mb:0.##} MB";
        return $"{mb / 1024.0:0.##} GB";
    }

    public static string FormatDuration(double seconds)
    {
        if (seconds < 0 || double.IsNaN(seconds))
            seconds = 0;
        var t = TimeSpan.FromSeconds(seconds);
        if (t.TotalHours >= 1)
            return $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}";
        return $"{t.Minutes}:{t.Seconds:00}";
    }

    /// <summary>Finds a voice clip node across the file system by its clip index.</summary>
    public FsNode? FindVoiceClip(int clipIndex)
    {
        if (clipIndex < 0) return null;
        FsNode? voiceFolder = Root.Children.FirstOrDefault(c => c.Name == VoiceFolder);
        if (voiceFolder is not null)
        {
            return EnumerateFiles(voiceFolder).FirstOrDefault(n => n.Kind == EntryKind.Voice && n.EntryIndex == clipIndex);
        }
        return EnumerateFiles(Root).FirstOrDefault(n => n.Kind == EntryKind.Voice && n.EntryIndex == clipIndex);
    }
}
