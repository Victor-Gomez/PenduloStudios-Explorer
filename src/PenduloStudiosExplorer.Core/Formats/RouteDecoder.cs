using System.Buffers.Binary;
using System.Text;

namespace PenduloStudiosExplorer.Core.Formats;

/// <summary>
/// A single 2D waypoint along a scene walk route in pixel coordinates.
/// </summary>
public readonly record struct Waypoint(int X, int Y);

/// <summary>
/// A precomputed walk path between two scene walk zones.
/// </summary>
/// <param name="FromZone">The starting walk region index.</param>
/// <param name="ToZone">The destination walk region index.</param>
/// <param name="Waypoints">The intermediate 2D pixel coordinates the character traverses.</param>
/// <param name="ZoneHops">The sequence of zone transitions/hops traversed along this route.</param>
public sealed class Route(int FromZone, int ToZone, IReadOnlyList<Waypoint> Waypoints, IReadOnlyList<byte> ZoneHops)
{
    public int FromZone { get; } = FromZone;
    public int ToZone { get; } = ToZone;
    public IReadOnlyList<Waypoint> Waypoints { get; } = Waypoints;
    public IReadOnlyList<byte> ZoneHops { get; } = ZoneHops;

    public bool IsActive => Waypoints.Count > 0 || ZoneHops.Count > 0;
}

/// <summary>
/// Complete precomputed route matrix for a scene, indexed by starting zones and target zones.
/// </summary>
public sealed class RouteMatrix(int zoneCount, IReadOnlyList<Route> routes)
{
    public int ZoneCount { get; } = zoneCount;
    public int RouteCount => ZoneCount * ZoneCount;

    public IReadOnlyList<Route> Routes { get; } = routes;

    public int TotalActiveRoutes => Routes.Count(r => r.IsActive);

    public int TotalWaypoints => Routes.Sum(r => r.Waypoints.Count);

    public Route? GetRoute(int fromZone, int toZone)
    {
        if (fromZone < 0 || fromZone >= ZoneCount || toZone < 0 || toZone >= ZoneCount)
            return null;
        int index = fromZone * ZoneCount + toZone;
        return index < Routes.Count ? Routes[index] : null;
    }
}

/// <summary>
/// Decodes precomputed waypoint navigation tables in <i>Runaway: A Road Adventure</i> (43,659 bytes)
/// and <i>Runaway 2: The Dream of the Turtle</i> (335,529 bytes).
/// <para>
/// The format is a split planar matrix for N×N zone pairs:
/// <list type="bullet">
///   <item><b>Runaway 1 (43,659 bytes):</b> 21×21 = 441 routes. Section 1 (0..35,279) holds 441 cells × 80 bytes (up to 20 waypoints). Section 2 (35,280..43,658) holds 441 cells × 19 bytes (hops).</item>
///   <item><b>Runaway 2 (335,529 bytes):</b> 51×51 = 2,601 routes. Section 1 (0..208,079) holds 2,601 cells × 80 bytes (up to 20 waypoints). Section 2 (208,080..335,528) holds 2,601 cells × 49 bytes (hops).</item>
/// </list>
/// </para>
/// </summary>
public static class RouteDecoder
{
    public const int TotalSizeR1 = 43_659;
    public const int TotalSizeR2 = 335_529;
    public const int TotalSizeTnbtWaypoints = 194_560;
    public const int TotalSizeTnbtHops = 589_824;

    public const int TotalSize = TotalSizeR1; // Compatibility alias
    public const int ZoneCount = ZoneCountR1; // Compatibility alias
    public const int RouteCount = RouteCountR1; // Compatibility alias
    public const int HopSlotBytes = HopSlotBytesR1; // Compatibility alias
    public const int WaypointsTableSize = RouteCountR1 * WaypointSlotBytes; // Compatibility alias

    public const int ZoneCountR1 = 21;
    public const int RouteCountR1 = 441;
    public const int HopSlotBytesR1 = 19;

    public const int ZoneCountR2 = 51;
    public const int RouteCountR2 = 2_601;
    public const int HopSlotBytesR2 = 49;

    public const int MaxWaypointsPerRoute = 20;
    public const int WaypointBytes = 4; // 2x u16 (X, Y)
    public const int WaypointSlotBytes = MaxWaypointsPerRoute * WaypointBytes; // 80 bytes

    public static bool IsRouteTable(ReadOnlySpan<byte> data) =>
        data.Length is TotalSizeR1 or TotalSizeR2;

    /// <summary>
    /// Parses a route table (either 43,659 bytes for R1 or 335,529 bytes for R2) into a <see cref="RouteMatrix"/>.
    /// </summary>
    public static RouteMatrix Parse(ReadOnlySpan<byte> data)
    {
        int zoneCount;
        int routeCount;
        int hopSlotBytes;

        if (data.Length == TotalSizeR1)
        {
            zoneCount = ZoneCountR1;
            routeCount = RouteCountR1;
            hopSlotBytes = HopSlotBytesR1;
        }
        else if (data.Length == TotalSizeR2)
        {
            zoneCount = ZoneCountR2;
            routeCount = RouteCountR2;
            hopSlotBytes = HopSlotBytesR2;
        }
        else
        {
            throw new ArgumentException($"Expected route table of {TotalSizeR1} or {TotalSizeR2} bytes, got {data.Length}.", nameof(data));
        }

        int waypointsTableSize = routeCount * WaypointSlotBytes;
        var routes = new List<Route>(routeCount);

        for (int cell = 0; cell < routeCount; cell++)
        {
            int fromZone = cell / zoneCount;
            int toZone = cell % zoneCount;

            // 1. Parse Waypoints from Section 1
            int coordOffset = cell * WaypointSlotBytes;
            var waypoints = new List<Waypoint>();
            for (int w = 0; w < MaxWaypointsPerRoute; w++)
            {
                int p = coordOffset + w * WaypointBytes;
                ushort x = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(p, 2));
                ushort y = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(p + 2, 2));

                // 0xFFFF or (0,0) indicates end of valid waypoints in slot
                if (x == 0xFFFF || y == 0xFFFF || (x == 0 && y == 0))
                    continue;

                waypoints.Add(new Waypoint(x, y));
            }

            // 2. Parse Zone Hops from Section 2
            int hopOffset = waypointsTableSize + cell * hopSlotBytes;
            var hops = new List<byte>();
            for (int h = 0; h < hopSlotBytes; h++)
            {
                byte hopZone = data[hopOffset + h];
                if (hopZone == 0)
                    break;
                hops.Add(hopZone);
            }

            routes.Add(new Route(fromZone, toZone, waypoints, hops));
        }

        return new RouteMatrix(zoneCount, routes);
    }

    /// <summary>
    /// Generates a detailed human-readable summary of the route mesh.
    /// </summary>
    public static string FormatSummary(RouteMatrix matrix)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Scene Walk Route Mesh: {matrix.TotalActiveRoutes} active route(s) across {matrix.ZoneCount} zones ({matrix.RouteCount} total cell matrix).");
        sb.AppendLine($"Total Precomputed Waypoints: {matrix.TotalWaypoints:N0}");
        sb.AppendLine();
        sb.AppendLine("Active Routes (From Zone -> To Zone):");
        sb.AppendLine("From | To  | Hops                 | Waypoint Count | Route Preview");
        sb.AppendLine("-----+-----+----------------------+----------------+--------------------------------------------------");

        foreach (Route r in matrix.Routes)
        {
            if (!r.IsActive)
                continue;

            string hopsStr = r.ZoneHops.Count > 0 ? string.Join(" \u2192 ", r.ZoneHops) : "(direct)";
            string preview = r.Waypoints.Count > 0
                ? string.Join(", ", r.Waypoints.Take(3).Select(w => $"({w.X}, {w.Y})")) + (r.Waypoints.Count > 3 ? "..." : "")
                : "(no waypoints)";

            sb.AppendLine($" {r.FromZone,2}  | {r.ToZone,2}  | {hopsStr,-20} | {r.Waypoints.Count,14} | {preview}");
        }

        return sb.ToString();
    }
}
