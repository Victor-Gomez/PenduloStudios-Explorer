namespace PenduloStudiosExplorer.Core.Formats;

/// <summary>
/// Mini-game sequence data for Hollywood Monsters' Frankenstein Reward sequence (from frankenstein_reward.cpp).
/// Governs the 36-frame reward animation choreography and the three body part inventory items awarded.
/// </summary>
public static class HollywoodFrankensteinReward
{
    public const int FrameCount = 36;
    public const int PartCount = 3;

    /// <summary>
    /// The 36-frame animation sequence mapping of frame indices:
    /// ascends through opening frames, loops through excitement poses (8..10), walks through presentation frames (11..18),
    /// re-loops through excitement poses, and winds down to resting pose 0.
    /// </summary>
    public static readonly byte[] FrameMap =
    [
        0, 1, 2, 3, 4, 8, 9, 10, 9, 8, 4, 5, 6, 7, 8, 9, 10, 11,
        12, 13, 14, 15, 16, 17, 18, 4, 8, 9, 10, 9, 8, 4, 3, 2, 1, 0
    ];

    /// <summary>
    /// The three Frankenstein body part item IDs (0x30, 0x42, 0x4c) in Sue / Ron's inventory:
    /// 48 (Head / Brain), 66 (Torso / Heart), 76 (Legs / Limbs).
    /// </summary>
    public static readonly byte[] PartItemIds =
    [
        0x30, 0x42, 0x4c
    ];

    /// <summary>Returns true if the specified inventory item ID is one of the three Frankenstein parts.</summary>
    public static bool IsFrankensteinPart(byte itemId) =>
        itemId is 0x30 or 0x42 or 0x4c;

    /// <summary>Returns the mapped frame index at the given step (0..35).</summary>
    public static byte GetFrameAtStep(int step)
    {
        if (step < 0 || step >= FrameCount)
            throw new ArgumentOutOfRangeException(nameof(step), $"Step must be between 0 and {FrameCount - 1}.");
        return FrameMap[step];
    }
}
