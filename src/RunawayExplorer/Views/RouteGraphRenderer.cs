using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using RunawayExplorer.Core.Formats;

namespace RunawayExplorer.Views;

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
/// </summary>
public static class RouteGraphRenderer
{
    private static readonly IBrush NormalEdgeBrush = new SolidColorBrush(Color.FromArgb(200, 0, 229, 255)); // Cyan / Neon blue
    private static readonly IBrush HighlightEdgeBrush = new SolidColorBrush(Color.FromArgb(255, 255, 215, 0)); // Gold
    private static readonly IBrush DimmedEdgeBrush = new SolidColorBrush(Color.FromArgb(60, 0, 150, 200));

    private static readonly IBrush NormalWaypointBrush = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255));
    private static readonly IBrush HighlightWaypointBrush = new SolidColorBrush(Color.FromArgb(255, 255, 215, 0));

    private static readonly IBrush StartNodeBrush = new SolidColorBrush(Color.FromArgb(240, 46, 204, 113)); // Emerald green
    private static readonly IBrush EndNodeBrush = new SolidColorBrush(Color.FromArgb(240, 231, 76, 60)); // Crimson / Coral red
    private static readonly IBrush BadgeBgBrush = new SolidColorBrush(Color.FromArgb(200, 15, 15, 20));

    public static void Render(
        Canvas canvas,
        RouteMatrix matrix,
        RouteDisplayMode mode,
        Route? selectedRoute,
        Action<Route>? onRouteClicked)
    {
        canvas.Children.Clear();

        IEnumerable<Route> routesToDraw;
        switch (mode)
        {
            case RouteDisplayMode.Adjacent:
                routesToDraw = matrix.Routes.Where(r => r.IsActive && r.ZoneHops.Count <= 1);
                // Fallback to all active routes if no direct adjacent transitions are found
                if (!routesToDraw.Any())
                    routesToDraw = matrix.Routes.Where(r => r.IsActive);
                break;

            case RouteDisplayMode.Single:
                if (selectedRoute is not null && selectedRoute.IsActive)
                    routesToDraw = [selectedRoute];
                else
                    routesToDraw = matrix.Routes.Where(r => r.IsActive).Take(1);
                break;

            case RouteDisplayMode.All:
            default:
                routesToDraw = matrix.Routes.Where(r => r.IsActive);
                break;
        }

        var activeList = routesToDraw.ToList();
        if (activeList.Count == 0)
            return;

        // 1. Draw edge segments and arrowheads
        foreach (Route r in activeList)
        {
            bool isHighlight = selectedRoute is not null &&
                               r.FromZone == selectedRoute.FromZone &&
                               r.ToZone == selectedRoute.ToZone;

            IBrush edgeBrush = mode == RouteDisplayMode.Single || isHighlight
                ? HighlightEdgeBrush
                : (selectedRoute is not null ? DimmedEdgeBrush : NormalEdgeBrush);

            double thickness = isHighlight || mode == RouteDisplayMode.Single ? 3.5 : 2.0;

            if (r.Waypoints.Count >= 2)
            {
                for (int i = 0; i < r.Waypoints.Count - 1; i++)
                {
                    Waypoint p1 = r.Waypoints[i];
                    Waypoint p2 = r.Waypoints[i + 1];

                    var line = new Line
                    {
                        StartPoint = new Point(p1.X, p1.Y),
                        EndPoint = new Point(p2.X, p2.Y),
                        Stroke = edgeBrush,
                        StrokeThickness = thickness,
                        StrokeLineCap = PenLineCap.Round,
                        Cursor = new Cursor(StandardCursorType.Hand),
                    };

                    string hopsText = r.ZoneHops.Count > 0 ? string.Join(" \u2192 ", r.ZoneHops) : "(direct)";
                    ToolTip.SetTip(line, $"Route: Zone {r.FromZone} \u2192 Zone {r.ToZone}\nHops: {hopsText}\nWaypoints: {r.Waypoints.Count}");

                    if (onRouteClicked is not null)
                    {
                        Route captured = r;
                        line.PointerPressed += (_, e) =>
                        {
                            e.Handled = true;
                            onRouteClicked(captured);
                        };
                    }

                    canvas.Children.Add(line);
                    AddArrowhead(canvas, p1, p2, edgeBrush);
                }
            }
            else if (r.Waypoints.Count == 1)
            {
                // Single waypoint boundary crossing
                Waypoint p = r.Waypoints[0];
                var circle = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = edgeBrush,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1.5,
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                Canvas.SetLeft(circle, p.X - 5);
                Canvas.SetTop(circle, p.Y - 5);

                string hopsText = r.ZoneHops.Count > 0 ? string.Join(" \u2192 ", r.ZoneHops) : "(direct)";
                ToolTip.SetTip(circle, $"Crossing: Zone {r.FromZone} \u2192 Zone {r.ToZone}\nHops: {hopsText}\nPoint: ({p.X}, {p.Y})");

                if (onRouteClicked is not null)
                {
                    Route captured = r;
                    circle.PointerPressed += (_, e) =>
                    {
                        e.Handled = true;
                        onRouteClicked(captured);
                    };
                }

                canvas.Children.Add(circle);
            }
        }

        // 2. Draw waypoint dots and zone terminal badges
        var drawnZoneLabels = new HashSet<(int X, int Y, int Zone)>();

        foreach (Route r in activeList)
        {
            bool isHighlight = selectedRoute is not null &&
                               r.FromZone == selectedRoute.FromZone &&
                               r.ToZone == selectedRoute.ToZone;

            for (int w = 0; w < r.Waypoints.Count; w++)
            {
                Waypoint wp = r.Waypoints[w];
                bool isStart = w == 0;
                bool isEnd = w == r.Waypoints.Count - 1 && r.Waypoints.Count > 1;

                if (isStart || isEnd)
                {
                    int zoneId = isStart ? r.FromZone : r.ToZone;
                    IBrush markerBrush = isStart ? StartNodeBrush : EndNodeBrush;

                    var marker = new Ellipse
                    {
                        Width = 12,
                        Height = 12,
                        Fill = markerBrush,
                        Stroke = Brushes.White,
                        StrokeThickness = 1.8,
                        Cursor = new Cursor(StandardCursorType.Hand),
                    };
                    Canvas.SetLeft(marker, wp.X - 6);
                    Canvas.SetTop(marker, wp.Y - 6);

                    ToolTip.SetTip(marker, $"{(isStart ? "Start" : "Target")} Zone {zoneId} at ({wp.X}, {wp.Y})");

                    if (onRouteClicked is not null)
                    {
                        Route captured = r;
                        marker.PointerPressed += (_, e) =>
                        {
                            e.Handled = true;
                            onRouteClicked(captured);
                        };
                    }

                    canvas.Children.Add(marker);

                    if (drawnZoneLabels.Add((wp.X, wp.Y, zoneId)))
                    {
                        var badge = new Border
                        {
                            Background = BadgeBgBrush,
                            BorderBrush = markerBrush,
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(3, 1),
                            Child = new TextBlock
                            {
                                Text = $"Z{zoneId}",
                                FontSize = 10,
                                FontWeight = FontWeight.Bold,
                                Foreground = Brushes.White,
                            },
                            IsHitTestVisible = false,
                        };
                        Canvas.SetLeft(badge, wp.X + 7);
                        Canvas.SetTop(badge, wp.Y - 8);
                        canvas.Children.Add(badge);
                    }
                }
                else
                {
                    IBrush wpBrush = isHighlight || mode == RouteDisplayMode.Single ? HighlightWaypointBrush : NormalWaypointBrush;
                    var dot = new Ellipse
                    {
                        Width = 6,
                        Height = 6,
                        Fill = wpBrush,
                        Stroke = Brushes.Black,
                        StrokeThickness = 1.0,
                        Cursor = new Cursor(StandardCursorType.Hand),
                    };
                    Canvas.SetLeft(dot, wp.X - 3);
                    Canvas.SetTop(dot, wp.Y - 3);

                    ToolTip.SetTip(dot, $"Waypoint #{w + 1} at ({wp.X}, {wp.Y}) [Zone {r.FromZone} \u2192 {r.ToZone}]");

                    if (onRouteClicked is not null)
                    {
                        Route captured = r;
                        dot.PointerPressed += (_, e) =>
                        {
                            e.Handled = true;
                            onRouteClicked(captured);
                        };
                    }

                    canvas.Children.Add(dot);
                }
            }
        }
    }

    private static void AddArrowhead(Canvas canvas, Waypoint p1, Waypoint p2, IBrush brush)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 14)
            return;

        double midX = p1.X + dx * 0.6;
        double midY = p1.Y + dy * 0.6;

        double ux = dx / len;
        double uy = dy / len;
        double nx = -uy;
        double ny = ux;

        const double arrowLen = 7.0;
        const double arrowWidth = 4.0;

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
}
