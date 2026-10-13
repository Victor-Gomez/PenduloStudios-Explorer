using System.Buffers.Binary;

namespace RunawayExplorer.Core.Formats;

/// <summary>
/// A 2D point (interaction point or approach point) within a Hollywood Monsters scene.
/// </summary>
public readonly record struct ScenePoint(short X, short Y);

/// <summary>
/// Action handler record for a verb executed on a scene item: target handler script ID and movement mode.
/// </summary>
public readonly record struct SceneVerbActionRecord(ushort ActionHandlerId, ushort MovementMode)
{
    public bool HasHandler => ActionHandlerId != 0;
}

/// <summary>
/// Runtime properties and verb action bindings for one of Hollywood Monsters' 21 scene items/hotspots.
/// </summary>
public sealed class HollywoodSceneItem
{
    public int ItemIndex { get; }
    public byte DefaultStrip { get; }
    public ScenePoint InteractionPoint { get; }
    public ScenePoint ApproachPoint { get; }
    public byte Facing { get; }
    public IReadOnlyList<SceneVerbActionRecord> VerbActions { get; }

    public HollywoodSceneItem(
        int itemIndex,
        byte defaultStrip,
        ScenePoint interactionPoint,
        ScenePoint approachPoint,
        byte facing,
        IReadOnlyList<SceneVerbActionRecord> verbActions)
    {
        ItemIndex = itemIndex;
        DefaultStrip = defaultStrip;
        InteractionPoint = interactionPoint;
        ApproachPoint = approachPoint;
        Facing = facing;
        VerbActions = verbActions;
    }
}

/// <summary>
/// Decodes Hollywood Monsters' scene metadata and interaction table (Entry 4 across playable scene archives).
/// Contains depth thresholds, palette lighting, route boundary steps, interaction/approach targets,
/// and verb action handlers for all 21 scene items.
/// </summary>
public sealed class HollywoodSceneMetadata
{
    public const int SceneItemCount = 21;
    public const int VerbCount = 8;
    public const int InventoryRelationItemCount = 0x79; // 121 items
    public const int VerbActionRecordSize = 4;

    // Binary offsets within Entry 4 (as defined in scummvm-hollywood scene_data.h)
    public const int OffsetActorDepthThresholds = 0x0000;
    public const int OffsetPaletteDeltaTable = 0x002a;
    public const int OffsetPaletteAdjustTable = 0x003f;
    public const int OffsetRouteBoundaryPoints = 0x007d;
    public const int OffsetRouteBoundarySteps = 0x1529;
    public const int OffsetSceneItemDefaultStrip = 0x35e4;
    public const int OffsetSceneItemInteractionPoints = 0x35f9;
    public const int OffsetSceneItemApproachPoints = 0x364d;
    public const int OffsetSceneItemFacing = 0x36a1;
    public const int OffsetSceneVerbActionRecords = 0x36b6;
    public const int OffsetSceneRelationRecords = 0x3956;
    public const int OffsetSceneMode2RelationOverlay = 0x610a;

    public const int MinSize = OffsetSceneVerbActionRecords + SceneItemCount * VerbCount * VerbActionRecordSize; // 14,678 bytes

    public IReadOnlyList<HollywoodSceneItem> Items { get; }
    public byte[] DepthThresholds { get; }
    public byte[] PaletteDeltaTable { get; }
    public byte[] PaletteAdjustTable { get; }

    public HollywoodSceneMetadata(
        IReadOnlyList<HollywoodSceneItem> items,
        byte[] depthThresholds,
        byte[] paletteDeltaTable,
        byte[] paletteAdjustTable)
    {
        Items = items;
        DepthThresholds = depthThresholds;
        PaletteDeltaTable = paletteDeltaTable;
        PaletteAdjustTable = paletteAdjustTable;
    }

    /// <summary>Checks whether <paramref name="data"/> is large enough to contain scene item metadata.</summary>
    public static bool IsSceneMetadata(ReadOnlySpan<byte> data) =>
        data.Length >= MinSize;

    /// <summary>Parses the scene metadata and verb action tables from Entry 4.</summary>
    public static HollywoodSceneMetadata Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < MinSize)
            throw new ArgumentException($"Data is too small ({data.Length} bytes) for Hollywood scene metadata; requires at least {MinSize} bytes.", nameof(data));

        byte[] depthThresholds = data.Slice(OffsetActorDepthThresholds, Math.Min(42, data.Length)).ToArray();
        byte[] paletteDelta = data.Slice(OffsetPaletteDeltaTable, Math.Min(21, data.Length - OffsetPaletteDeltaTable)).ToArray();
        byte[] paletteAdjust = data.Slice(OffsetPaletteAdjustTable, Math.Min(62, data.Length - OffsetPaletteAdjustTable)).ToArray();

        var items = new List<HollywoodSceneItem>(SceneItemCount);

        for (int i = 0; i < SceneItemCount; i++)
        {
            byte defaultStrip = data[OffsetSceneItemDefaultStrip + i];
            int interOff = OffsetSceneItemInteractionPoints + i * 4;
            short interX = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(interOff, 2));
            short interY = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(interOff + 2, 2));

            int appOff = OffsetSceneItemApproachPoints + i * 4;
            short appX = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(appOff, 2));
            short appY = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(appOff + 2, 2));

            byte facing = data[OffsetSceneItemFacing + i];

            var verbActions = new List<SceneVerbActionRecord>(VerbCount);
            for (int v = 0; v < VerbCount; v++)
            {
                int recordIndex = i * VerbCount + v;
                int verbOff = OffsetSceneVerbActionRecords + recordIndex * VerbActionRecordSize;
                ushort handler = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(verbOff, 2));
                ushort mode = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(verbOff + 2, 2));
                verbActions.Add(new SceneVerbActionRecord(handler, mode));
            }

            items.Add(new HollywoodSceneItem(
                i,
                defaultStrip,
                new ScenePoint(interX, interY),
                new ScenePoint(appX, appY),
                facing,
                verbActions));
        }

        return new HollywoodSceneMetadata(items, depthThresholds, paletteDelta, paletteAdjust);
    }
}
