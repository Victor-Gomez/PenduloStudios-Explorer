using System.Text;
using PenduloStudiosExplorer.Core.FileSystem;

namespace PenduloStudiosExplorer.Core.Formats;

/// <summary>
/// Renders 256-colour (or partial) <see cref="IndexedPalette"/> blocks as a 16×16 visual colour swatch grid,
/// and formats human-readable palette breakdowns.
/// </summary>
public static class PaletteDecoder
{
    public const int GridColumns = 16;
    public const int GridRows = 16;
    public const int DefaultCellSize = 32;

    /// <summary>
    /// Renders <paramref name="palette"/> as a 16×16 grid of colour swatches.
    /// Undefined slots past <paramref name="definedCount"/> are rendered with a distinct dark checkerboard pattern.
    /// An optional <paramref name="dividerIndex"/> (e.g. 176 or 208 for the scene/shared boundary) highlights
    /// where the shared tail starts.
    /// </summary>
    public static DecodedImage RenderGrid(
        IndexedPalette palette,
        int cellSize = DefaultCellSize,
        int? definedCount = null,
        int? dividerIndex = null)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (cellSize < 4) throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be at least 4 pixels.");

        int width = GridColumns * cellSize;
        int height = GridRows * cellSize;
        var pixels = new byte[width * height * 4];

        int limit = Math.Clamp(definedCount ?? palette.DefinedCount, 0, IndexedPalette.Colors);
        ReadOnlySpan<byte> bgra = palette.Bgra;

        for (int r = 0; r < GridRows; r++)
        {
            for (int c = 0; c < GridColumns; c++)
            {
                int index = r * GridColumns + c;
                bool isDefined = index < limit;
                bool isDivider = dividerIndex.HasValue && index == dividerIndex.Value;

                byte b = 0, g = 0, red = 0;
                if (isDefined)
                {
                    int p = index * 4;
                    b = bgra[p];
                    g = bgra[p + 1];
                    red = bgra[p + 2];
                }

                int startX = c * cellSize;
                int startY = r * cellSize;

                for (int dy = 0; dy < cellSize; dy++)
                {
                    int py = startY + dy;
                    int rowOffset = py * width * 4;

                    for (int dx = 0; dx < cellSize; dx++)
                    {
                        int px = startX + dx;
                        int dst = rowOffset + px * 4;

                        bool isBorder = dx == 0 || dy == 0 || px == width - 1 || py == height - 1;

                        if (isBorder)
                        {
                            if (isDivider && (dx == 0 || dy == 0))
                            {
                                // Golden/amber accent line indicating the shared character tail boundary
                                pixels[dst] = 40;       // B
                                pixels[dst + 1] = 195;  // G
                                pixels[dst + 2] = 255;  // R
                                pixels[dst + 3] = 255;  // A
                            }
                            else
                            {
                                // Subtle cell border (visible against both pure black and bright white)
                                pixels[dst] = 38;
                                pixels[dst + 1] = 38;
                                pixels[dst + 2] = 38;
                                pixels[dst + 3] = 255;
                            }
                        }
                        else
                        {
                            if (isDefined)
                            {
                                pixels[dst] = b;
                                pixels[dst + 1] = g;
                                pixels[dst + 2] = red;
                                pixels[dst + 3] = 255;
                            }
                            else
                            {
                                // Undefined slot pattern: 4x4 subtle checkerboard
                                bool check = ((dx / 4) + (dy / 4)) % 2 == 0;
                                byte pat = check ? (byte)22 : (byte)36;
                                pixels[dst] = pat;
                                pixels[dst + 1] = pat;
                                pixels[dst + 2] = pat;
                                pixels[dst + 3] = 255;
                            }
                        }
                    }
                }
            }
        }

        return new DecodedImage(width, height, pixels);
    }

    /// <summary>
    /// Formats a complete human-readable breakdown of the palette block and assembled table.
    /// </summary>
    public static string FormatSummary(
        IndexedPalette palette,
        IndexedPalette? assembled,
        FsNode node,
        bool isVga6Bit,
        byte[]? rawBytes = null)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(node);

        var sb = new StringBuilder();
        int defined = palette.DefinedCount;
        int byteLen = rawBytes?.Length ?? (isVga6Bit ? defined * 3 : defined * 2);

        sb.AppendLine($"=== Palette Block: {node.GetPath()} ===");
        sb.AppendLine($"Format: {(isVga6Bit ? "6-bit VGA triples (0..63)" : "16-bit RGB565 words")}");
        sb.AppendLine($"Defined Colours: {defined} ({byteLen:N0} bytes)");

        if (assembled is not null && assembled.DefinedCount > defined)
        {
            int sharedCount = assembled.DefinedCount - defined;
            sb.AppendLine($"Assembled Palette: {assembled.DefinedCount} colours ({defined} from scene block + {sharedCount} shared from RESOURCE.000)");
        }
        else
        {
            sb.AppendLine($"Assembled Palette: {defined} colours");
        }

        sb.AppendLine();
        sb.AppendLine("Colour Table:");
        sb.AppendLine(isVga6Bit
            ? "Slot | Hex     | RGB (8-bit)     | VGA (6-bit)  | Source"
            : "Slot | Hex     | RGB (8-bit)     | Source");
        sb.AppendLine(isVga6Bit
            ? "-----+---------+-----------------+--------------+--------------------------"
            : "-----+---------+-----------------+-----------------------------------------");

        IndexedPalette active = assembled ?? palette;
        int maxSlots = Math.Max(defined, active.DefinedCount);
        ReadOnlySpan<byte> bgra = active.Bgra;

        for (int i = 0; i < maxSlots; i++)
        {
            int p = i * 4;
            byte b = bgra[p];
            byte g = bgra[p + 1];
            byte r = bgra[p + 2];
            string hex = $"#{r:X2}{g:X2}{b:X2}";
            string source = i < defined ? "Scene block" : "Shared tail (RESOURCE.000)";

            if (isVga6Bit)
            {
                int vgaR = r >> 2;
                int vgaG = g >> 2;
                int vgaB = b >> 2;
                sb.AppendLine($"{i,4} | {hex} | ({r,3}, {g,3}, {b,3}) | ({vgaR,2}, {vgaG,2}, {vgaB,2}) | {source}");
            }
            else
            {
                sb.AppendLine($"{i,4} | {hex} | ({r,3}, {g,3}, {b,3}) | {source}");
            }
        }

        if (rawBytes is not null && rawBytes.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Raw Hex Dump:");
            sb.Append(RawFormat.ToHexDump(rawBytes.AsSpan(0, Math.Min(rawBytes.Length, 1024))));
            if (rawBytes.Length > 1024)
                sb.AppendLine($"\n... {rawBytes.Length - 1024:N0} more byte(s) not shown.");
        }

        return sb.ToString();
    }
}
