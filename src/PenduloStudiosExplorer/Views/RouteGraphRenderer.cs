using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PenduloStudiosExplorer.Core.Formats;

namespace PenduloStudiosExplorer.Views;

/// <summary>
/// Display mode for filtering routes on the vector overlay canvas.
/// </summary>
public enum RouteDisplayMode
{
    Adjacent,
    All,
    Single
}

/// <summary>
/// Renders the precomputed waypoint navigation mesh as vector elements (<see cref="Line"/>, <see cref="Polygon"/>,
/// <see cref="Ellipse"/>, <see cref="Border"/>) on a stage overlay <see cref="Canvas"/> matching scene pixel dimensions.
/// <para>
/// Correctly visualizes scene walk regions as centroid nodes, shared boundaries as portal gates,
/// and transition paths crossing portal midpoints with multi-hop route reconstruction.
/// </para>
/// </summary>
public static class RouteGraphRenderer
{
    private static readonly IBrush NormalEdgeBrush = new SolidColorBrush(Color.FromArgb(200, 0, 229, 255)); // Cyan / Neon blue
    private static readonly IBrush HighlightEdgeBrush = new SolidColorBrush(Color.FromArgb(255, 255, 215, 0)); // Gold
    private static readonly IBrush DimmedEdgeBrush = new SolidColorBrush(Color.FromArgb(50, 0, 150, 200));

    private static readonly IBrush PortalGateBrush = new SolidColorBrush(Color.FromArgb(170, 210, 235, 255)); // Translucent icy blue
    private static readonly IBrush HighlightPortalGateBrush = new SolidColorBrush(Color.FromArgb(255, 255, 215, 0)); // Gold portal gate
    private static readonly IBrush DimmedPortalGateBrush = new SolidColorBrush(Color.FromArgb(40, 200, 220, 255));

    private static readonly IBrush StartNodeBrush = new SolidColorBrush(Color.FromArgb(240, 46, 204, 113)); // Emerald green
    private static readonly IBrush EndNodeBrush = new SolidColorBrush(Color.FromArgb(240, 231, 76, 60)); // Crimson / Coral red
    private static readonly IBrush IntermediateNodeBrush = new SolidColorBrush(Color.FromArgb(240, 243, 156, 18)); // Amber/Gold
    private static readonly IBrush NormalNodeBgBrush = new SolidColorBrush(Color.FromArgb(220, 24, 28, 38)); // Slate dark
    private static readonly IBrush NormalNodeBorderBrush = new SolidColorBrush(Color.FromArgb(220, 0, 229, 255)); // Neon blue
    private static readonly IBrush DimmedNodeBgBrush = new SolidColorBrush(Color.FromArgb(140, 20, 24, 32));
    private static readonly IBrush DimmedNodeBorderBrush = new SolidColorBrush(Color.FromArgb(80, 0, 150, 200));

    private sealed record PortalInfo(Point Midpoint, Point Start, Point End, int WaypointCount, Route Route);

    public static void Render(
        Canvas canvas,
        RouteMatrix matrix,
        RouteDisplayMode mode,
        Route? selectedRoute,
        Action<Route>? onRouteClicked,
        IReadOnlyDictionary<int, (int X, int Y)>? centroids = null)
    {
        canvas.Children.Clear();

        // 1. Index all boundary portals between adjacent zones
        var directedPortals = new Dictionary<(int From, int To), PortalInfo>();
        var uniquePortals = new Dictionary<(int A, int B), PortalInfo>();

        foreach (Route r in matrix.Routes)
        {
            if (r.FromZone == r.ToZone || r.Waypoints.Count == 0)
                continue;

            var pStart = new Point(r.Waypoints[0].X, r.Waypoints[0].Y);
            var pEnd = new Point(r.Waypoints[^1].X, r.Waypoints[^1].Y);
            var mid = new Point((pStart.X + pEnd.X) / 2.0, (pStart.Y + pEnd.Y) / 2.0);
            var info = new PortalInfo(mid, pStart, pEnd, r.Waypoints.Count, r);

            directedPortals[(r.FromZone, r.ToZone)] = info;

            int u = Math.Min(r.FromZone, r.ToZone);
            int v = Math.Max(r.FromZone, r.ToZone);
            if (!uniquePortals.ContainsKey((u, v)))
                uniquePortals[(u, v)] = info;
        }

        PortalInfo? FindPortal(int fromZone, int toZone)
        {
            if (directedPortals.TryGetValue((fromZone, toZone), out var p))
                return p;
            if (directedPortals.TryGetValue((toZone, fromZone), out var rev))
                return rev;
            return null;
        }

        Point GetCentroid(int zone) => ResolveCentroid(zone, matrix, centroids, directedPortals);

        // 2. Determine routes to display
        Route effectiveSelected = selectedRoute is not null && selectedRoute.IsActive
            ? selectedRoute
            : matrix.Routes.FirstOrDefault(r => r.IsActive) ?? matrix.Routes[0];

        var traversedPortals = new HashSet<(int, int)>();
        var activeHopZones = new HashSet<int>();

        if (mode == RouteDisplayMode.Single)
        {
            var hops = new List<int> { effectiveSelected.FromZone };
            if (effectiveSelected.ZoneHops.Count > 0)
                hops.AddRange(effectiveSelected.ZoneHops.Select(b => (int)b));
            else if (effectiveSelected.FromZone != effectiveSelected.ToZone)
                hops.Add(effectiveSelected.ToZone);

            foreach (int h in hops)
                activeHopZones.Add(h);

            for (int i = 0; i < hops.Count - 1; i++)
            {
                int u = Math.Min(hops[i], hops[i + 1]);
                int v = Math.Max(hops[i], hops[i + 1]);
                traversedPortals.Add((u, v));
            }
        }

        // 3. Render Portal Gates (shared boundary seams between adjacent zones)
        foreach (var (key, portal) in uniquePortals)
        {
            bool isTraversed = traversedPortals.Contains(key);
            bool isDimmed = mode == RouteDisplayMode.Single && !isTraversed;

            IBrush gateBrush = isTraversed
                ? HighlightPortalGateBrush
                : (isDimmed ? DimmedPortalGateBrush : PortalGateBrush);

            double thickness = isTraversed ? 2.8 : 1.8;

            bool isGateLine = portal.Start != portal.End;
            if (isGateLine)
            {
                var portalLine = new Line
                {
                    StartPoint = portal.Start,
                    EndPoint = portal.End,
                    Stroke = gateBrush,
                    StrokeThickness = thickness,
                    StrokeDashArray = new AvaloniaList<double> { 4, 3 },
                    StrokeLineCap = PenLineCap.Round,
                    Cursor = new Cursor(StandardCursorType.Hand),
                };

                double seamLen = Math.Sqrt(Math.Pow(portal.End.X - portal.Start.X, 2) + Math.Pow(portal.End.Y - portal.Start.Y, 2));
                ToolTip.SetTip(portalLine, $"Portal Gate: Zone {key.A} \u2194 Zone {key.B}\nSeam Length: {seamLen:F0} px ({portal.Start.X:F0},{portal.Start.Y:F0}) to ({portal.End.X:F0},{portal.End.Y:F0})\nSamples: {portal.WaypointCount} boundary points");

                if (onRouteClicked is not null)
                {
                    Route captured = portal.Route;
                    portalLine.PointerPressed += (_, e) =>
                    {
                        e.Handled = true;
                        onRouteClicked(captured);
                    };
                }

                canvas.Children.Add(portalLine);

                // Small endpoint dots
                AddPortalDot(canvas, portal.Start, gateBrush);
                AddPortalDot(canvas, portal.End, gateBrush);
            }
            else
            {
                // Single point portal
                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = gateBrush,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1.0,
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                Canvas.SetLeft(dot, portal.Start.X - 4);
                Canvas.SetTop(dot, portal.Start.Y - 4);
                ToolTip.SetTip(dot, $"Portal Point: Zone {key.A} \u2194 Zone {key.B} at ({portal.Start.X:F0}, {portal.Start.Y:F0})");

                if (onRouteClicked is not null)
                {
                    Route captured = portal.Route;
                    dot.PointerPressed += (_, e) =>
                    {
                        e.Handled = true;
                        onRouteClicked(captured);
                    };
                }

                canvas.Children.Add(dot);
            }
        }

        // 4. Render Navigation Edges
        var activeDirectRoutes = matrix.Routes
            .Where(r => r.IsActive && r.ZoneHops.Count <= 1 && r.FromZone != r.ToZone)
            .ToList();

        if (activeDirectRoutes.Count == 0)
        {
            activeDirectRoutes = matrix.Routes
                .Where(r => r.IsActive && r.FromZone != r.ToZone)
                .ToList();
        }

        if (mode == RouteDisplayMode.Single)
        {
            // Reconstruct and draw the complete chained journey
            var hops = new List<int> { effectiveSelected.FromZone };
            if (effectiveSelected.ZoneHops.Count > 0)
                hops.AddRange(effectiveSelected.ZoneHops.Select(b => (int)b));
            else if (effectiveSelected.FromZone != effectiveSelected.ToZone)
                hops.Add(effectiveSelected.ToZone);

            var pathPoints = new List<Point> { GetCentroid(hops[0]) };
            for (int i = 0; i < hops.Count - 1; i++)
            {
                int u = hops[i];
                int v = hops[i + 1];
                var portal = FindPortal(u, v);
                if (portal is not null)
                    pathPoints.Add(portal.Midpoint);
                pathPoints.Add(GetCentroid(v));
            }

            for (int i = 0; i < pathPoints.Count - 1; i++)
            {
                Point p1 = pathPoints[i];
                Point p2 = pathPoints[i + 1];

                var line = new Line
                {
                    StartPoint = p1,
                    EndPoint = p2,
                    Stroke = HighlightEdgeBrush,
                    StrokeThickness = 3.5,
                    StrokeLineCap = PenLineCap.Round,
                    Cursor = new Cursor(StandardCursorType.Hand),
                };

                string hopsDesc = string.Join(" \u2192 ", hops);
                ToolTip.SetTip(line, $"Route: Zone {effectiveSelected.FromZone} \u2192 Zone {effectiveSelected.ToZone}\nPath: {hopsDesc}");

                if (onRouteClicked is not null)
                {
                    Route captured = effectiveSelected;
                    line.PointerPressed += (_, e) =>
                    {
                        e.Handled = true;
                        onRouteClicked(captured);
                    };
                }

                canvas.Children.Add(line);
                AddArrowhead(canvas, p1, p2, HighlightEdgeBrush);
            }
        }
        else
        {
            // Adjacent or All mode: draw direct adjacent transition legs
            foreach (Route r in activeDirectRoutes)
            {
                bool isSelected = selectedRoute is not null &&
                                  r.FromZone == selectedRoute.FromZone &&
                                  r.ToZone == selectedRoute.ToZone;

                IBrush edgeBrush = isSelected
                    ? HighlightEdgeBrush
                    : (selectedRoute is not null ? DimmedEdgeBrush : NormalEdgeBrush);

                double thickness = isSelected ? 3.5 : 2.0;

                Point cFrom = GetCentroid(r.FromZone);
                Point cTo = GetCentroid(r.ToZone);
                var portal = FindPortal(r.FromZone, r.ToZone);

                if (portal is not null)
                {
                    // Path crosses centroid(from) -> portal midpoint -> centroid(to)
                    Point mid = portal.Midpoint;

                    // Leg 1: cFrom -> mid
                    AddTransitionSegment(canvas, cFrom, mid, edgeBrush, thickness, r, onRouteClicked);
                    // Leg 2: mid -> cTo (with arrowhead pointing towards destination)
                    AddTransitionSegment(canvas, mid, cTo, edgeBrush, thickness, r, onRouteClicked);
                    AddArrowhead(canvas, mid, cTo, edgeBrush);
                }
                else
                {
                    // Fallback direct line between centroids
                    AddTransitionSegment(canvas, cFrom, cTo, edgeBrush, thickness, r, onRouteClicked);
                    AddArrowhead(canvas, cFrom, cTo, edgeBrush);
                }
            }
        }

        // 5. Render Zone Centroid Node Badges
        var zonesToDraw = new HashSet<int>();
        if (mode == RouteDisplayMode.Single)
        {
            foreach (int h in activeHopZones)
                zonesToDraw.Add(h);
        }
        else
        {
            foreach (Route r in activeDirectRoutes)
            {
                zonesToDraw.Add(r.FromZone);
                zonesToDraw.Add(r.ToZone);
            }
        }

        foreach (int z in zonesToDraw)
        {
            Point pt = GetCentroid(z);

            IBrush bgBrush;
            IBrush borderBrush;

            if (mode == RouteDisplayMode.Single)
            {
                if (z == effectiveSelected.FromZone)
                {
                    bgBrush = StartNodeBrush;
                    borderBrush = Brushes.White;
                }
                else if (z == effectiveSelected.ToZone)
                {
                    bgBrush = EndNodeBrush;
                    borderBrush = Brushes.White;
                }
                else if (activeHopZones.Contains(z))
                {
                    bgBrush = IntermediateNodeBrush;
                    borderBrush = Brushes.White;
                }
                else
                {
                    bgBrush = DimmedNodeBgBrush;
                    borderBrush = DimmedNodeBorderBrush;
                }
            }
            else
            {
                bool isHighlighted = selectedRoute is not null &&
                                     (z == selectedRoute.FromZone || z == selectedRoute.ToZone);

                if (isHighlighted)
                {
                    bgBrush = z == selectedRoute!.FromZone ? StartNodeBrush : EndNodeBrush;
                    borderBrush = Brushes.White;
                }
                else
                {
                    bgBrush = NormalNodeBgBrush;
                    borderBrush = NormalNodeBorderBrush;
                }
            }

            var badge = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = bgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1.8),
                Child = new TextBlock
                {
                    Text = $"Z{z}",
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                },
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            Canvas.SetLeft(badge, pt.X - 13);
            Canvas.SetTop(badge, pt.Y - 13);

            ToolTip.SetTip(badge, $"Zone {z}\nCentroid: ({pt.X:F0}, {pt.Y:F0})");

            if (onRouteClicked is not null)
            {
                Route? routeFromZone = matrix.Routes.FirstOrDefault(r => r.IsActive && r.FromZone == z);
                if (routeFromZone is not null)
                {
                    badge.PointerPressed += (_, e) =>
                    {
                        e.Handled = true;
                        onRouteClicked(routeFromZone);
                    };
                }
            }

            canvas.Children.Add(badge);
        }
    }

    private static void AddTransitionSegment(
        Canvas canvas,
        Point p1,
        Point p2,
        IBrush brush,
        double thickness,
        Route route,
        Action<Route>? onRouteClicked)
    {
        var line = new Line
        {
            StartPoint = p1,
            EndPoint = p2,
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeLineCap = PenLineCap.Round,
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        string hopsText = route.ZoneHops.Count > 0 ? string.Join(" \u2192 ", route.ZoneHops) : "(direct)";
        ToolTip.SetTip(line, $"Route: Zone {route.FromZone} \u2192 Zone {route.ToZone}\nHops: {hopsText}");

        if (onRouteClicked is not null)
        {
            Route captured = route;
            line.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                onRouteClicked(captured);
            };
        }

        canvas.Children.Add(line);
    }

    private static void AddPortalDot(Canvas canvas, Point p, IBrush brush)
    {
        var dot = new Ellipse
        {
            Width = 4,
            Height = 4,
            Fill = brush,
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(dot, p.X - 2);
        Canvas.SetTop(dot, p.Y - 2);
        canvas.Children.Add(dot);
    }

    private static void AddArrowhead(Canvas canvas, Point p1, Point p2, IBrush brush)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 16)
            return;

        double midX = p1.X + dx * 0.58;
        double midY = p1.Y + dy * 0.58;

        double ux = dx / len;
        double uy = dy / len;
        double nx = -uy;
        double ny = ux;

        const double arrowLen = 9.0;
        const double arrowWidth = 5.0;

        Point tip = new(midX + ux * (arrowLen * 0.5), midY + uy * (arrowLen * 0.5));
        Point baseLeft = new(tip.X - ux * arrowLen + nx * arrowWidth, tip.Y - uy * arrowLen + ny * arrowWidth);
        Point baseRight = new(tip.X - ux * arrowLen - nx * arrowWidth, tip.Y - uy * arrowLen - ny * arrowWidth);

        var arrow = new Polygon
        {
            Points = [tip, baseLeft, baseRight],
            Fill = brush,
            Stroke = Brushes.Black,
            StrokeThickness = 0.8,
            IsHitTestVisible = false,
        };
        canvas.Children.Add(arrow);
    }

    private static Point ResolveCentroid(
        int zone,
        RouteMatrix matrix,
        IReadOnlyDictionary<int, (int X, int Y)>? centroids,
        Dictionary<(int From, int To), PortalInfo> portals)
    {
        if (centroids is not null && centroids.TryGetValue(zone, out var pt))
            return new Point(pt.X, pt.Y);

        // Fallback: average midpoints of all portals touching this zone
        double sumX = 0;
        double sumY = 0;
        int count = 0;
        int lastNeighbor = -1;
        PortalInfo? lastPortal = null;

        foreach (var (key, portal) in portals)
        {
            if (key.From == zone)
            {
                sumX += portal.Midpoint.X;
                sumY += portal.Midpoint.Y;
                count++;
                lastNeighbor = key.To;
                lastPortal = portal;
            }
            else if (key.To == zone)
            {
                sumX += portal.Midpoint.X;
                sumY += portal.Midpoint.Y;
                count++;
                lastNeighbor = key.From;
                lastPortal = portal;
            }
        }

        if (count > 1)
        {
            return new Point(sumX / count, sumY / count);
        }

        if (count == 1 && lastPortal is not null)
        {
            // Offset away from single portal so centroid doesn't collapse onto portal midpoint
            Point mid = lastPortal.Midpoint;
            double dx = zone < lastNeighbor ? -35 : 35;
            double dy = zone < lastNeighbor ? -25 : 25;
            return new Point(Math.Max(20, mid.X + dx), Math.Max(20, mid.Y + dy));
        }

        // Secondary fallback: check waypoints in routes touching this zone
        foreach (Route r in matrix.Routes)
        {
            if ((r.FromZone == zone || r.ToZone == zone) && r.Waypoints.Count > 0)
            {
                foreach (Waypoint wp in r.Waypoints)
                {
                    sumX += wp.X;
                    sumY += wp.Y;
                    count++;
                }
                break;
            }
        }

        if (count > 0)
            return new Point(sumX / count, sumY / count);

        // Grid layout fallback if completely disconnected
        return new Point(80 + (zone % 8) * 90, 80 + (zone / 8) * 80);
    }
}
