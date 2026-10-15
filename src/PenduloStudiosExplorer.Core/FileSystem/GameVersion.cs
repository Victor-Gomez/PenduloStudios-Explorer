using System.IO;

namespace PenduloStudiosExplorer.Core.FileSystem;

/// <summary>Supported game release.</summary>
public enum GameVersion
{
    /// <summary>Runaway: A Road Adventure (2001).</summary>
    Runaway1 = 0,

    /// <summary>Runaway 2: The Dream of the Turtle (2006).</summary>
    Runaway2 = 1,

    /// <summary>Runaway: A Twist of Fate (2009).</summary>
    Runaway3 = 2,

    /// <summary>The Next BIG Thing (2011).</summary>
    TheNextBigThing = 3,

    /// <summary>Yesterday (2012).</summary>
    Yesterday = 4,

    /// <summary>Hollywood Monsters (1997).</summary>
    HollywoodMonsters = 5,
}

/// <summary>Metadata and detection definition for a supported game.</summary>
public sealed class GameDefinition
{
    public GameVersion Version { get; }
    public string Title { get; }
    public IReadOnlyList<string> Signatures { get; }

    public GameDefinition(GameVersion version, string title, IReadOnlyList<string> signatures)
    {
        Version = version;
        Title = title;
        Signatures = signatures;
    }

    /// <summary>
    /// Hollywood Monsters keeps its archives beside the executable in a <c>Monsters</c> folder rather than in
    /// a <c>Resource</c> one, and its <c>RESOURCE.001</c> would otherwise be mistaken for Runaway 1's -- which
    /// is why this definition is consulted first.
    /// </summary>
    public static readonly GameDefinition HollywoodMonsters = new(
        GameVersion.HollywoodMonsters,
        "Hollywood Monsters",
        [
            "Monsters/Monsters.exe",
            "Monsters.exe",
            "Monsters/TraFX.exe",
            "Monsters/RESOURCE.I18",
            "RESOURCE.I18"
        ]);

    public static readonly GameDefinition Yesterday = new(
        GameVersion.Yesterday,
        "Yesterday",
        [
            "Yesterday.exe",
            "PSEngine.dll",
            "PSConfig.exe"
        ]);

    public static readonly GameDefinition TheNextBigThing = new(
        GameVersion.TheNextBigThing,
        "The Next BIG Thing",
        [
            "The Next Big Thing.exe",
            "TNBT_Config.exe",
            "PS_GFXLib.dll",
            "PS_SNDLib.dll"
        ]);

    public static readonly GameDefinition Runaway3 = new(
        GameVersion.Runaway3,
        "Runaway: A Twist of Fate",
        [
            "RATOF.exe",
            "ratof.config",
            "RATOF-Config.exe",
            "Resource/RESOURCE.D01",
            "RESOURCE.D01"
        ]);

    public static readonly GameDefinition Runaway2 = new(
        GameVersion.Runaway2,
        "Runaway: The Dream of the Turtle",
        [
            "RunawayTDOTT.exe",
            "Resource/RESOURCE.B04A",
            "RESOURCE.B04A",
            "Resource/RESOURCE.SP1",
            "RESOURCE.SP1",
            "Dataa/Dataaa.000",
            "DATAA/DATAAA.000"
        ]);

    public static readonly GameDefinition Runaway1 = new(
        GameVersion.Runaway1,
        "Runaway: A Road Adventure",
        [
            "Runaway.exe",
            "Resource/RESOURCE.001",
            "RESOURCE.001"
        ]);

    public static readonly IReadOnlyList<GameDefinition> All = [HollywoodMonsters, Yesterday, TheNextBigThing, Runaway3, Runaway2, Runaway1];

    public static GameDefinition Get(GameVersion version) => version switch
    {
        GameVersion.HollywoodMonsters => HollywoodMonsters,
        GameVersion.Yesterday => Yesterday,
        GameVersion.TheNextBigThing => TheNextBigThing,
        GameVersion.Runaway3 => Runaway3,
        GameVersion.Runaway2 => Runaway2,
        _ => Runaway1,
    };
}

public static class GameVersionExtensions
{
    public static string GetTitle(this GameVersion version) => GameDefinition.Get(version).Title;
}

/// <summary>Detects which game is installed in a given folder.</summary>
public static class GameDetector
{
    /// <summary>
    /// Attempts to detect the game version from the files present in <paramref name="baseDir"/>.
    /// Returns null if no signatures match.
    /// </summary>
    public static GameVersion? TryDetect(string baseDir)
    {
        if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir))
            return null;

        foreach (GameDefinition game in GameDefinition.All)
        {
            foreach (string sig in game.Signatures)
            {
                string fullPath = Path.Combine(baseDir, sig.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fullPath))
                    return game.Version;
            }
        }

        return null;
    }

    /// <summary>
    /// Detects the game version from the files present in <paramref name="baseDir"/>.
    /// Defaults to <see cref="GameVersion.Runaway1"/> if unknown.
    /// </summary>
    public static GameVersion Detect(string baseDir) =>
        TryDetect(baseDir) ?? GameVersion.Runaway1;

    /// <summary>
    /// Searches common Steam and GOG library locations across available drives for installed Pendulo adventures.
    /// </summary>
    public static Dictionary<GameVersion, string> FindInstalledGames()
    {
        var results = new Dictionary<GameVersion, string>();
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Check drive letters and common library directories
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                string root = drive.RootDirectory.FullName;

                string[] commonBases =
                [
                    Path.Combine(root, "Games", "Steam", "steamapps", "common"),
                    Path.Combine(root, "Steam", "steamapps", "common"),
                    Path.Combine(root, "SteamLibrary", "steamapps", "common"),
                    Path.Combine(root, "Program Files (x86)", "Steam", "steamapps", "common"),
                    Path.Combine(root, "Program Files", "Steam", "steamapps", "common"),
                    Path.Combine(root, "GOG Games"),
                    Path.Combine(root, "Games", "GOG"),
                    Path.Combine(root, "Games"),
                ];

                foreach (string cb in commonBases)
                {
                    if (Directory.Exists(cb))
                        candidates.Add(cb);
                }
            }
        }
        catch
        {
            // Ignore drive enumeration errors
        }

        // 2. User profile directories on Linux / macOS
        try
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home))
            {
                string[] userPaths =
                [
                    Path.Combine(home, ".local", "share", "Steam", "steamapps", "common"),
                    Path.Combine(home, ".steam", "steam", "steamapps", "common"),
                    Path.Combine(home, "GOG Games"),
                ];
                foreach (string up in userPaths)
                {
                    if (Directory.Exists(up))
                        candidates.Add(up);
                }
            }
        }
        catch
        {
            // Ignore
        }

        // 3. Inspect candidate parent folders for game installations
        foreach (string parent in candidates)
        {
            try
            {
                foreach (string sub in Directory.EnumerateDirectories(parent))
                {
                    if (TryDetect(sub) is { } version && !results.ContainsKey(version))
                    {
                        results[version] = sub;
                        if (results.Count == GameDefinition.All.Count)
                            return results;
                    }
                }
            }
            catch
            {
                // Access denied or IO error
            }
        }

        return results;
    }
}
