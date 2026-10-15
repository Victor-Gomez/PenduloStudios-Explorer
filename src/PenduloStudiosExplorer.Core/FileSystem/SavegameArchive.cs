using System.Buffers.Binary;
using System.Text;
using PenduloStudiosExplorer.Core.Formats;

namespace PenduloStudiosExplorer.Core.FileSystem;

/// <summary>
/// Metadata extracted from a <em>Runaway 1</em> savegame file (<c>SAVEGAME.NNN</c>, 149,080 bytes).
/// See <c>docs/formats/savegames.md</c>.
/// </summary>
/// <param name="SlotNumber">1-based slot number (e.g. 1 for <c>SAVEGAME.001</c>).</param>
/// <param name="Name">Player-entered save description or timestamp.</param>
/// <param name="Action">Action verb / sentence from the status line (e.g. <c>IR A</c>, <c>EXAMINAR PÓSTER</c>).</param>
/// <param name="SceneId">Engine scene selector byte (7 = B02, 8 = B06, 15 = D05, 23 = F09, 24 = F22).</param>
/// <param name="SceneArchiveName">Mapped scene archive name if known (e.g. <c>RESOURCE.B02</c>).</param>
/// <param name="ViewX">Camera / viewport X scroll offset.</param>
/// <param name="ViewY">Camera / viewport Y scroll offset.</param>
/// <param name="ActorX">Brian's X position in the scene.</param>
/// <param name="ActorY">Brian's Y position in the scene.</param>
/// <param name="InventoryCount">Number of held inventory items.</param>
public sealed record SavegameInfo(
    int SlotNumber,
    string Name,
    string Action,
    byte SceneId,
    string? SceneArchiveName,
    int ViewX,
    int ViewY,
    int ActorX,
    int ActorY,
    int InventoryCount);

/// <summary>
/// Reads and decodes <em>Runaway 1</em> savegame files (<c>SAVEGAME.NNN</c> and <c>SAVEGAME.IJS</c>).
/// Saves carry no header or magic, but have a fixed length of 149,080 bytes starting with a
/// 204×120 RGB565 thumbnail (48,960 bytes).
/// </summary>
public static class SavegameArchive
{
    public const int FileSize = 149080;
    public const int ThumbnailWidth = 204;
    public const int ThumbnailHeight = 120;
    public const int ThumbnailBytes = ThumbnailWidth * ThumbnailHeight * 2; // 48,960

    public const int OffsetThumbnail = 0;
    public const int OffsetSlotName = 48960; // 120 bytes
    public const int OffsetHotspot = 49080;  // 81 bytes
    public const int OffsetAction = 49161;   // 81 bytes
    public const int OffsetSceneId = 0x0C063; // byte
    public const int OffsetViewX = 0x0C06B;   // u16
    public const int OffsetViewY = 0x0C06D;   // u16
    public const int OffsetActorX = 0x0C06F;  // u16
    public const int OffsetActorY = 0x0C071;  // u16
    public const int OffsetInvCount = 0x1B711; // u16

    /// <summary>
    /// Known SceneId values mapped to their corresponding scene archive names.
    /// Derived from executable analysis and confirmed across saves by comparing thumbnails against scene backgrounds.
    /// </summary>
    private static readonly Dictionary<byte, string> KnownSceneIds = new()
    {
        [7] = "RESOURCE.B02",
        [8] = "RESOURCE.B06",
        [15] = "RESOURCE.D05",
        [23] = "RESOURCE.F09",
        [24] = "RESOURCE.F22",
    };

    /// <summary>Resolves a scene archive name from an engine scene ID if known.</summary>
    public static string? SceneArchiveForId(byte sceneId) =>
        KnownSceneIds.GetValueOrDefault(sceneId);

    /// <summary>Registers an engine scene ID mapping dynamically.</summary>
    public static void RegisterSceneId(byte sceneId, string archiveName)
    {
        ArgumentException.ThrowIfNullOrEmpty(archiveName);
        KnownSceneIds[sceneId] = archiveName;
    }

    /// <summary>
    /// Computes a difference score between a 204×120 RGB565 thumbnail and a scene background raster.
    /// <para>
    /// Samples <paramref name="backgroundRgb565"/> at <c>(viewX + 5 * x, 5 * y)</c> and scores
    /// on the better half (median / lowest 50%) of the sample differences so that foreground
    /// sprites and the player character do not distort the score (see <c>docs/formats/savegames.md</c> §3.1).
    /// Lower score represents a closer match (0 is identical).
    /// </para>
    /// </summary>
    public static double ScoreThumbnailMatch(
        ReadOnlySpan<byte> thumbnailRgb565,
        int viewX,
        ReadOnlySpan<byte> backgroundRgb565,
        int bgWidth,
        int bgHeight)
    {
        if (thumbnailRgb565.Length < ThumbnailBytes || backgroundRgb565.Length < bgWidth * bgHeight * 2)
            return double.MaxValue;

        var diffs = new int[ThumbnailWidth * ThumbnailHeight];
        int sampleCount = 0;

        for (int ty = 0; ty < ThumbnailHeight; ty++)
        {
            int by = ty * 5;
            if (by >= bgHeight)
                continue;

            int bgRowOffset = by * bgWidth;
            int thumbRowOffset = ty * ThumbnailWidth;

            for (int tx = 0; tx < ThumbnailWidth; tx++)
            {
                int bx = viewX + tx * 5;
                if (bx < 0 || bx >= bgWidth)
                    continue;

                int thumbPixOffset = (thumbRowOffset + tx) * 2;
                ushort tp = BinaryPrimitives.ReadUInt16LittleEndian(thumbnailRgb565.Slice(thumbPixOffset));

                int bgPixOffset = (bgRowOffset + bx) * 2;
                ushort bp = BinaryPrimitives.ReadUInt16LittleEndian(backgroundRgb565.Slice(bgPixOffset));

                int tr = (tp >> 11) & 0x1F;
                int tg = (tp >> 5) & 0x3F;
                int tb = tp & 0x1F;

                int br = (bp >> 11) & 0x1F;
                int bg = (bp >> 5) & 0x3F;
                int bb = bp & 0x1F;

                int diff = Math.Abs(tr - br) + Math.Abs(tg - bg) + Math.Abs(tb - bb);
                diffs[sampleCount++] = diff;
            }
        }

        if (sampleCount < 1000)
            return double.MaxValue;

        Array.Sort(diffs, 0, sampleCount);
        int halfCount = sampleCount / 2;
        long sum = 0;
        for (int i = 0; i < halfCount; i++)
            sum += diffs[i];

        return (double)sum / halfCount;
    }

    /// <summary>
    /// Identifies the best-matching scene archive from candidate scene backgrounds.
    /// Returns the archive name and score, or null if no scene scores below <paramref name="threshold"/>.
    /// </summary>
    public static (string ArchiveName, double Score)? MatchSceneArchive(
        ReadOnlySpan<byte> thumbnailRgb565,
        int viewX,
        IEnumerable<(string ArchiveName, byte[] BackgroundRgb565, int Width, int Height)> candidates,
        double threshold = 10.0)
    {
        string? bestArchive = null;
        double bestScore = double.MaxValue;

        foreach (var (name, bgBytes, w, h) in candidates)
        {
            double score = ScoreThumbnailMatch(thumbnailRgb565, viewX, bgBytes, w, h);
            if (score < bestScore)
            {
                bestScore = score;
                bestArchive = name;
            }
        }

        if (bestArchive is not null && bestScore <= threshold)
            return (bestArchive, bestScore);

        return null;
    }

    /// <summary>Checks whether <paramref name="fileName"/> matches the <c>SAVEGAME.NNN</c> naming pattern.</summary>
    public static bool IsSaveFileName(string fileName)
    {
        string name = Path.GetFileName(fileName);
        if (!name.StartsWith("SAVEGAME.", StringComparison.OrdinalIgnoreCase))
            return false;
        string ext = name[9..];
        return ext.Length == 3 && int.TryParse(ext, out _);
    }

    /// <summary>Parses the 1-based slot number from <c>SAVEGAME.NNN</c>.</summary>
    public static int GetSlotNumber(string fileName)
    {
        string name = Path.GetFileName(fileName);
        if (name.StartsWith("SAVEGAME.", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(name[9..], out int slot))
        {
            return slot;
        }
        return 0;
    }

    /// <summary>Decodes the embedded 204×120 RGB565 thumbnail into a BGRA32 image.</summary>
    public static DecodedImage DecodeThumbnail(ReadOnlySpan<byte> data)
    {
        if (data.Length < ThumbnailBytes)
            throw new ArgumentException($"Data is too small for a savegame thumbnail ({data.Length} < {ThumbnailBytes} bytes).", nameof(data));

        byte[] bgra = new byte[ThumbnailWidth * ThumbnailHeight * 4];
        Rgb565.CopyRow(data, 0, bgra, 0, ThumbnailWidth * ThumbnailHeight);
        return new DecodedImage(ThumbnailWidth, ThumbnailHeight, bgra);
    }

    /// <summary>Reads metadata fields from the raw save file data.</summary>
    public static SavegameInfo ReadMetadata(ReadOnlySpan<byte> data, int slotNumber, GameVersion game = GameVersion.Runaway1)
    {
        string name = ReadNullTerminatedString(data, OffsetSlotName, 120);
        string action = ReadNullTerminatedString(data, OffsetAction, 81);

        byte sceneId = data.Length > OffsetSceneId ? data[OffsetSceneId] : (byte)0;
        string? sceneName = SceneArchiveForId(sceneId);

        int viewX = data.Length >= OffsetViewX + 2 ? BinaryPrimitives.ReadUInt16LittleEndian(data[OffsetViewX..]) : 0;
        int viewY = data.Length >= OffsetViewY + 2 ? BinaryPrimitives.ReadUInt16LittleEndian(data[OffsetViewY..]) : 0;
        int actorX = data.Length >= OffsetActorX + 2 ? BinaryPrimitives.ReadUInt16LittleEndian(data[OffsetActorX..]) : 0;
        int actorY = data.Length >= OffsetActorY + 2 ? BinaryPrimitives.ReadUInt16LittleEndian(data[OffsetActorY..]) : 0;
        int invCount = data.Length >= OffsetInvCount + 2 ? BinaryPrimitives.ReadUInt16LittleEndian(data[OffsetInvCount..]) : 0;

        return new SavegameInfo(
            slotNumber,
            name,
            action,
            sceneId,
            sceneName,
            viewX,
            viewY,
            actorX,
            actorY,
            invCount);
    }

    /// <summary>Scans <paramref name="saveDir"/> and returns ordered paths of all valid save files.</summary>
    public static List<string> FindSaves(string saveDir)
    {
        if (!Directory.Exists(saveDir))
            return [];

        string ijsPath = Path.Combine(saveDir, "SAVEGAME.IJS");
        var result = new List<string>();

        if (File.Exists(ijsPath))
        {
            try
            {
                byte[] ijs = File.ReadAllBytes(ijsPath);
                if (ijs.Length >= 2)
                {
                    int count = BinaryPrimitives.ReadUInt16LittleEndian(ijs);
                    if (count > 0 && ijs.Length >= 2 + 1000 + count * 2)
                    {
                        ReadOnlySpan<byte> used = ijs.AsSpan(2, 1000);
                        ReadOnlySpan<byte> order = ijs.AsSpan(1002, count * 2);
                        for (int i = 0; i < count; i++)
                        {
                            int raw = BinaryPrimitives.ReadUInt16LittleEndian(order[(i * 2)..]);
                            // The engine stores 0-based slot numbers (0..9) in Order; files are 1-based (SAVEGAME.001..010)
                            int slot = (raw > 0 && raw < 1000 && used[raw] != 0) ? raw : raw + 1;
                            if (slot > 0 && slot < 1000 && used[slot] != 0)
                            {
                                string candidate = Path.Combine(saveDir, $"SAVEGAME.{slot:000}");
                                if (File.Exists(candidate) && new FileInfo(candidate).Length == FileSize && !result.Contains(candidate))
                                    result.Add(candidate);
                            }
                        }
                    }
                }
            }
            catch (IOException) { }
        }

        // Add any additional SAVEGAME.NNN files not listed in IJS
        foreach (string file in Directory.EnumerateFiles(saveDir, "SAVEGAME.*"))
        {
            if (IsSaveFileName(file) && new FileInfo(file).Length == FileSize && !result.Contains(file))
                result.Add(file);
        }

        return result;
    }

    private static string ReadNullTerminatedString(ReadOnlySpan<byte> data, int offset, int maxLength)
    {
        if (offset >= data.Length)
            return string.Empty;

        int available = Math.Min(maxLength, data.Length - offset);
        ReadOnlySpan<byte> slice = data.Slice(offset, available);
        int nul = slice.IndexOf((byte)0);
        if (nul == 0)
            return string.Empty;
        if (nul > 0)
            slice = slice[..nul];

        // Spanish Windows CP1252 / ISO-8859-1 strings
        return Encoding.Latin1.GetString(slice).Trim();
    }
}
