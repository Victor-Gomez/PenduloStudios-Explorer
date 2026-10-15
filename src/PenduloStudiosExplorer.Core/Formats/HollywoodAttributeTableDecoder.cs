namespace PenduloStudiosExplorer.Core.Formats;

/// <summary>
/// Parsed 1,792-byte attribute table (Entry 3 in Hollywood Monsters scene archives).
/// Contains seven 256-byte pages mapping region IDs (from the scene's RLE mask) to functional attributes.
/// </summary>
public sealed class HollywoodAttributeTable
{
    public const int TotalBytes = 1792;
    public const int PageSize = 256;
    public const int PageCount = 7;

    public byte[] WalkRegions { get; }
    public byte[] SceneItems { get; }
    public byte[] ActorDepthClasses { get; }
    public byte[] PaletteDeltaClasses { get; }
    public byte[] PaletteAdjustmentClasses { get; }
    public byte[] FootstepSoundMap { get; }
    public byte[] PresentationPaletteRemap { get; }

    public HollywoodAttributeTable(
        byte[] walkRegions,
        byte[] sceneItems,
        byte[] actorDepthClasses,
        byte[] paletteDeltaClasses,
        byte[] paletteAdjustmentClasses,
        byte[] footstepSoundMap,
        byte[] presentationPaletteRemap)
    {
        WalkRegions = walkRegions;
        SceneItems = sceneItems;
        ActorDepthClasses = actorDepthClasses;
        PaletteDeltaClasses = paletteDeltaClasses;
        PaletteAdjustmentClasses = paletteAdjustmentClasses;
        FootstepSoundMap = footstepSoundMap;
        PresentationPaletteRemap = presentationPaletteRemap;
    }

    public byte GetWalkRegion(byte zoneId) => WalkRegions[zoneId];
    public byte GetSceneItem(byte zoneId) => SceneItems[zoneId];
    public byte GetActorDepthClass(byte zoneId) => ActorDepthClasses[zoneId];
    public byte GetPaletteDeltaClass(byte zoneId) => PaletteDeltaClasses[zoneId];
    public byte GetPaletteAdjustmentClass(byte zoneId) => PaletteAdjustmentClasses[zoneId];
    public byte GetFootstepSound(byte zoneId) => FootstepSoundMap[zoneId];
    public byte GetPresentationRemap(byte zoneId) => PresentationPaletteRemap[zoneId];

    /// <summary>Counts how many zone IDs (1..255) have non-zero entries in each page.</summary>
    public (int Walk, int Items, int Depth, int Light, int Adjust, int Footstep, int Remap) NonZeroCount()
    {
        int walk = 0, items = 0, depth = 0, light = 0, adjust = 0, footstep = 0, remap = 0;
        for (int i = 1; i < 256; i++)
        {
            if (WalkRegions[i] != 0) walk++;
            if (SceneItems[i] != 0) items++;
            if (ActorDepthClasses[i] != 0) depth++;
            if (PaletteDeltaClasses[i] != 0) light++;
            if (PaletteAdjustmentClasses[i] != 0) adjust++;
            if (FootstepSoundMap[i] != 0) footstep++;
            if (PresentationPaletteRemap[i] != 0) remap++;
        }
        return (walk, items, depth, light, adjust, footstep, remap);
    }
}

/// <summary>
/// Decoder for Hollywood Monsters' 1,792-byte scene lookup tables (7 × 256 pages).
/// Layout:
/// <list type="bullet">
///   <item><b>0x000 (Page 0):</b> Walk region numbers (walkability map).</item>
///   <item><b>0x100 (Page 1):</b> Scene item (hotspot) index under the cursor.</item>
///   <item><b>0x200 (Page 2):</b> Actor depth class (foreground / background sorting).</item>
///   <item><b>0x300 (Page 3):</b> Actor palette delta class (lighting/brightness delta).</item>
///   <item><b>0x400 (Page 4):</b> Actor palette adjustment class (recolouring profile).</item>
///   <item><b>0x500 (Page 5):</b> Footstep sound map (material underfoot).</item>
///   <item><b>0x600 (Page 6):</b> Presentation palette remap.</item>
/// </list>
/// </summary>
public static class HollywoodAttributeTableDecoder
{
    public const int TotalBytes = HollywoodAttributeTable.TotalBytes;
    public const int PageSize = HollywoodAttributeTable.PageSize;

    public const int PageWalkRegion = 0x000;
    public const int PageSceneItem = 0x100;
    public const int PageActorDepthClass = 0x200;
    public const int PagePaletteDeltaClass = 0x300;
    public const int PagePaletteAdjustmentClass = 0x400;
    public const int PageFootstepSoundMap = 0x500;
    public const int PagePresentationPaletteRemap = 0x600;

    public static bool IsAttributeTable(ReadOnlySpan<byte> data) =>
        data.Length == TotalBytes;

    /// <summary>Parses the 1,792-byte buffer into a <see cref="HollywoodAttributeTable"/>.</summary>
    public static HollywoodAttributeTable Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < TotalBytes)
            throw new ArgumentException($"Expected attribute table of {TotalBytes} bytes, got {data.Length}.", nameof(data));

        byte[] walk = data.Slice(PageWalkRegion, PageSize).ToArray();
        byte[] items = data.Slice(PageSceneItem, PageSize).ToArray();
        byte[] depth = data.Slice(PageActorDepthClass, PageSize).ToArray();
        byte[] delta = data.Slice(PagePaletteDeltaClass, PageSize).ToArray();
        byte[] adjust = data.Slice(PagePaletteAdjustmentClass, PageSize).ToArray();
        byte[] footstep = data.Slice(PageFootstepSoundMap, PageSize).ToArray();
        byte[] remap = data.Slice(PagePresentationPaletteRemap, PageSize).ToArray();

        return new HollywoodAttributeTable(walk, items, depth, delta, adjust, footstep, remap);
    }
}
