using System.Buffers.Binary;
using System.Text;

namespace RunawayExplorer.Core.Formats;

/// <summary>
/// A horizontal perspective corridor span at a specific scanline Y.
/// Defines the left coordinate, width, and right boundary along the walk surface.
/// </summary>
/// <param name="Width">The horizontal span width in pixels.</param>
/// <param name="Y">The vertical scanline on the screen (e.g. 483 to 599).</param>
/// <param name="X">The starting horizontal pixel coordinate (left edge).</param>
public readonly record struct CorridorSpan(int Width, int Y, int X)
{
    public int Left => X;
    public int Right => X + Width;
}

/// <summary>
/// Perspective walk corridor table parsed from <i>Runaway: A Road Adventure</i>'s 704-byte scene entries.
/// Contains continuous scanline bounds for navigating ground perspective.
/// </summary>
public sealed class CorridorTable(IReadOnlyList<CorridorSpan> spans)
{
    public const int TotalSize = 704;

    public IReadOnlyList<CorridorSpan> Spans { get; } = spans;

    public int MinY => Spans.Count > 0 ? Spans[0].Y : 0;

    public int MaxY => Spans.Count > 0 ? Spans[^1].Y : 0;
}

/// <summary>
/// Decodes <i>Runaway: A Road Adventure</i>'s 704-byte perspective walk corridor tables.
/// <para>
/// Present in scene archives <c>RESOURCE.F09</c> and <c>RESOURCE.H09</c> (slot 4).
/// Layout:
/// <list type="bullet">
///   <item><b>Header (0..1):</b> <c>u16 SpanCount</c> (117 spans).</item>
///   <item><b>Spans (2..703):</b> 117 records × 6 bytes: <c>{ u16 Width, u16 Y, u16 X }</c>.</item>
/// </list>
/// Exactly: 2 + 117 × 6 = 704 bytes.
/// The Y coordinates run scanline-by-scanline monotonically from 483 to 599 (the bottom of the 1024×600 canvas).
/// </para>
/// </summary>
public static class CorridorSpanDecoder
{
    public const int TotalSize = 704;
    public const int ExpectedSpans = 117;
    public const int RecordSize = 6;

    public static bool IsCorridorTable(ReadOnlySpan<byte> data) =>
        data.Length == TotalSize;

    /// <summary>
    /// Parses a 704-byte corridor table into a <see cref="CorridorTable"/>.
    /// </summary>
    public static CorridorTable Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length != TotalSize)
            throw new ArgumentException($"Expected corridor table of {TotalSize} bytes, got {data.Length}.", nameof(data));

        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0, 2));
        var spans = new List<CorridorSpan>(count);

        for (int i = 0; i < count; i++)
        {
            int off = 2 + i * RecordSize;
            if (off + RecordSize > data.Length)
                break;

            ushort width = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(off, 2));
            ushort y = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(off + 2, 2));
            ushort x = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(off + 4, 2));

            spans.Add(new CorridorSpan(width, y, x));
        }

        return new CorridorTable(spans);
    }

    /// <summary>
    /// Formats a readable table summary of the perspective walk corridor spans.
    /// </summary>
    public static string FormatSummary(CorridorTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Walkway Perspective Corridor Table: {table.Spans.Count} scanline spans.");
        sb.AppendLine($"Scanline Range: Y={table.MinY} to Y={table.MaxY} (Screen Height 600)");
        sb.AppendLine();
        sb.AppendLine("Scanline (Y) | Left (X) | Width (px) | Right (X+W)");
        sb.AppendLine("-------------+----------+------------+------------");

        foreach (CorridorSpan s in table.Spans)
        {
            sb.AppendLine($" {s.Y,11} | {s.Left,8} | {s.Width,10} | {s.Right,10}");
        }

        return sb.ToString();
    }
}
