using System.Buffers.Binary;
using System.Text;

namespace PenduloStudiosExplorer.Core.FileSystem;

/// <summary>
/// Metadata for one chapter's interaction block in Runaway 3's <c>RESOURCE.005</c>.
/// </summary>
public readonly record struct ChapterInfo(int ChapterId, int RecordCount, long Offset);

/// <summary>
/// A single 220-byte interaction record describing an in-game object interaction or state trigger.
/// </summary>
public sealed class InteractionRecord
{
    private static readonly Encoding Windows1252;

    static InteractionRecord()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1252 = Encoding.GetEncoding(1252);
    }

    public int Index { get; }
    public int ChapterRecordIndex { get; }
    public int ChapterId { get; }
    public int SceneId { get; }
    public int ObjectId { get; }
    public int ActionId { get; }
    public byte[] Payload { get; }
    public byte[] DecryptedPayload { get; }
    public string DecodedLabel { get; }
    public byte[] StateTrailer { get; }
    public string VerbName => GetVerbName(ActionId);
    public long Offset { get; }

    public InteractionRecord(int index, int chapterRecordIndex, int chapterId, int sceneId, int objectId, int actionId, byte[] payload, long offset = 0)
    {
        Index = index;
        ChapterRecordIndex = chapterRecordIndex;
        ChapterId = chapterId;
        SceneId = sceneId;
        ObjectId = objectId;
        ActionId = actionId;
        Payload = payload;
        Offset = offset;

        DecryptedPayload = DecryptPayload(payload, chapterRecordIndex);

        // Extract null-terminated ASCII/Windows-1252 label
        int term = Array.IndexOf(DecryptedPayload, (byte)0);
        if (term > 0)
        {
            DecodedLabel = Windows1252.GetString(DecryptedPayload, 0, term);
            StateTrailer = DecryptedPayload[(term + 1)..];
        }
        else if (term == 0)
        {
            DecodedLabel = string.Empty;
            StateTrailer = DecryptedPayload[1..];
        }
        else
        {
            DecodedLabel = Windows1252.GetString(DecryptedPayload).TrimEnd('\0');
            StateTrailer = [];
        }
    }

    /// <summary>
    /// Decrypts the 204-byte payload using the XOR-chain cipher keyed on <c>(recordIndex &amp; 0xFF)</c>.
    /// </summary>
    public static byte[] DecryptPayload(ReadOnlySpan<byte> raw, int recordIndex)
    {
        if (raw.Length == 0)
            return [];

        byte[] plain = new byte[raw.Length];
        plain[0] = (byte)(raw[0] ^ (recordIndex & 0xFF));
        for (int j = 1; j < raw.Length; j++)
        {
            plain[j] = (byte)(raw[j] ^ raw[j - 1]);
        }
        return plain;
    }

    /// <summary>
    /// Maps numeric action IDs to Pendulo engine interaction verbs.
    /// </summary>
    public static string GetVerbName(int actionId) => actionId switch
    {
        0 => "Examine / Look (Mirar)",
        1 => "Take / Pick up (Coger)",
        2 => "Use / Interact (Usar)",
        3 => "Talk To (Hablar con)",
        4 => "Go To (Ir a)",
        5 => "Drop / Leave (Dejar)",
        6 => "Show Inventory (Inventario)",
        20 => "Combine / Use With (Usar con)",
        _ => $"Action {actionId}"
    };
}

/// <summary>
/// Parsed database of chapter interactions across <i>Runaway: A Twist of Fate</i>.
/// </summary>
public sealed class ChapterInteractionDatabase(IReadOnlyList<ChapterInfo> chapters, IReadOnlyList<InteractionRecord> records)
{
    public IReadOnlyList<ChapterInfo> Chapters { get; } = chapters;
    public IReadOnlyList<InteractionRecord> Records { get; } = records;
    public int TotalRecords => Records.Count;

    public IEnumerable<InteractionRecord> GetRecordsForChapter(int chapterId) =>
        Records.Where(r => r.ChapterId == chapterId);

    public IReadOnlyList<int> GetScenesForChapter(int chapterId) =>
        GetRecordsForChapter(chapterId).Select(r => r.SceneId).Distinct().OrderBy(s => s).ToList();

    public IReadOnlyList<InteractionRecord> GetRecordsForScene(int chapterId, int sceneId) =>
        Records.Where(r => r.ChapterId == chapterId && r.SceneId == sceneId).ToList();

    public IReadOnlyList<int> GetObjectsForScene(int chapterId, int sceneId) =>
        GetRecordsForScene(chapterId, sceneId).Select(r => r.ObjectId).Distinct().OrderBy(o => o).ToList();

    /// <summary>
    /// Formats a complete ASCII trigger tree / interaction graph for a given scene.
    /// </summary>
    public string FormatSceneGraph(int chapterId, int sceneId)
    {
        var recs = GetRecordsForScene(chapterId, sceneId);
        var sb = new StringBuilder();
        sb.AppendLine($"=== Chapter {chapterId} — Scene {sceneId:D3} Interaction Graph ===");
        sb.AppendLine($"Total Triggers: {recs.Count} | Unique Objects: {recs.Select(r => r.ObjectId).Distinct().Count()}");
        sb.AppendLine();

        var objects = recs.GroupBy(r => r.ObjectId).OrderBy(g => g.Key);
        foreach (var objGroup in objects)
        {
            string primaryName = objGroup.Select(r => r.DecodedLabel).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? $"Object {objGroup.Key:D3}";
            sb.AppendLine($"[Object {objGroup.Key:D3}] \"{primaryName}\"");

            foreach (InteractionRecord r in objGroup.OrderBy(r => r.ActionId))
            {
                string labelDetail = !string.IsNullOrWhiteSpace(r.DecodedLabel) && r.DecodedLabel != primaryName
                    ? $" -> \"{r.DecodedLabel}\""
                    : string.Empty;
                sb.AppendLine($"  └─ Action {r.ActionId,2} [{r.VerbName}]{labelDetail}");
                if (r.StateTrailer.Length > 0)
                {
                    int nonZero = r.StateTrailer.Count(b => b != 0);
                    sb.AppendLine($"     State Flags: {nonZero} active byte(s) in payload trailer");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a Mermaid flowchart diagram (<c>flowchart TD</c>) representing the scene's interaction graph.
    /// </summary>
    public string GenerateMermaid(int chapterId, int sceneId)
    {
        var recs = GetRecordsForScene(chapterId, sceneId);
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        sb.AppendLine($"    scene[\"Scene {sceneId:D3} (Ch {chapterId})\"]");

        var objects = recs.GroupBy(r => r.ObjectId).OrderBy(g => g.Key);
        int nodeIndex = 0;
        foreach (var objGroup in objects)
        {
            string primaryName = objGroup.Select(r => r.DecodedLabel).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? $"Object {objGroup.Key}";
            string cleanObjName = EscapeMermaid(primaryName);
            string objNodeId = $"obj_{chapterId}_{sceneId}_{objGroup.Key}";
            sb.AppendLine($"    scene --> {objNodeId}[\"{cleanObjName} (ID {objGroup.Key})\"]");

            foreach (InteractionRecord r in objGroup.OrderBy(r => r.ActionId))
            {
                string actNodeId = $"act_{nodeIndex++}";
                string cleanVerb = EscapeMermaid(r.VerbName);
                string labelText = !string.IsNullOrWhiteSpace(r.DecodedLabel) && r.DecodedLabel != primaryName
                    ? $"{cleanVerb}<br/><i>{EscapeMermaid(r.DecodedLabel)}</i>"
                    : cleanVerb;
                sb.AppendLine($"    {objNodeId} --> {actNodeId}[\"{labelText}\"]");
            }
        }

        return sb.ToString();
    }

    private static string EscapeMermaid(string text) =>
        text.Replace("\"", "'").Replace("[", "(").Replace("]", ")").Trim();
}

/// <summary>
/// Reader for <i>Runaway 3</i>'s interaction and event database (<c>RESOURCE.005</c>, 478,152 bytes).
/// <para>
/// Divided into 7 chapters containing 2,173 records of exactly 220 bytes each:
/// <list type="bullet">
///   <item>Header (8 bytes): <c>u32 TotalRecords</c> (2,173), <c>u32 ChapterCount</c> (7).</item>
///   <item>Chapter table (7 × 12 bytes): <c>{ u32 ChapterId, u32 RecordCount, u32 Offset }</c>.</item>
///   <item>Record table (2,173 × 220 bytes): <c>{ u32 ChapterId, u32 SceneId, u32 ObjectId, u32 ActionId, u8 Payload[204] }</c>.</item>
/// </list>
/// </para>
/// </summary>
public static class ChapterInteractionArchive
{
    public const int RecordSize = 220;

    public static bool IsInteractionDatabase(ReadOnlySpan<byte> data)
    {
        if (data.Length < 20)
            return false;

        uint totalRecords = BinaryPrimitives.ReadUInt32LittleEndian(data);
        uint chapterCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4));

        if (totalRecords == 0 || chapterCount == 0 || chapterCount > 100)
            return false;

        long expectedSize = 8 + (long)chapterCount * 12 + (long)totalRecords * RecordSize;
        return expectedSize == data.Length;
    }

    public static ChapterInteractionDatabase Parse(ReadOnlySpan<byte> data)
    {
        if (!IsInteractionDatabase(data))
            throw new ArgumentException("Data is not a valid Runaway 3 interaction database.", nameof(data));

        uint totalRecords = BinaryPrimitives.ReadUInt32LittleEndian(data);
        uint chapterCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4));

        var chapters = new List<ChapterInfo>((int)chapterCount);
        for (int i = 0; i < chapterCount; i++)
        {
            int p = 8 + i * 12;
            int chId = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(p, 4));
            int recCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(p + 4, 4));
            long offset = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(p + 8, 4));
            chapters.Add(new ChapterInfo(chId, recCount, offset));
        }

        var records = new List<InteractionRecord>((int)totalRecords);
        foreach (ChapterInfo ch in chapters)
        {
            for (int r = 0; r < ch.RecordCount; r++)
            {
                int p = (int)(ch.Offset + r * RecordSize);
                if (p + RecordSize > data.Length)
                    break;

                int chId = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(p, 4));
                int scId = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(p + 4, 4));
                int obId = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(p + 8, 4));
                int acId = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(p + 12, 4));

                byte[] payload = data.Slice(p + 16, RecordSize - 16).ToArray();
                records.Add(new InteractionRecord(records.Count, r, chId, scId, obId, acId, payload, p));
            }
        }

        return new ChapterInteractionDatabase(chapters, records);
    }

    public static string FormatSummary(ChapterInteractionDatabase db)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Runaway 3 Interaction Database: {db.TotalRecords:N0} records across {db.Chapters.Count} chapters.");
        sb.AppendLine();
        sb.AppendLine("Chapter Breakdown:");
        sb.AppendLine("Chapter | Records | Offset  | Unique Scenes | Unique Objects");
        sb.AppendLine("--------+---------+---------+---------------+---------------");

        foreach (ChapterInfo ch in db.Chapters)
        {
            var recs = db.GetRecordsForChapter(ch.ChapterId).ToList();
            int scenes = recs.Select(r => r.SceneId).Distinct().Count();
            int objs = recs.Select(r => r.ObjectId).Distinct().Count();
            sb.AppendLine($"   {ch.ChapterId,2}   |  {ch.RecordCount,5}  | {ch.Offset,7} |       {scenes,3}     |       {objs,3}");
        }

        return sb.ToString();
    }
}
