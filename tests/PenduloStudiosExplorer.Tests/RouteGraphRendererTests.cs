using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using PenduloStudiosExplorer.Core.Formats;
using PenduloStudiosExplorer.Views;
using Xunit;

namespace PenduloStudiosExplorer.Tests;

public class RouteGraphRendererTests
{
    private static (RouteMatrix Matrix, Dictionary<int, (int X, int Y)> Centroids) CreateTestEnvironment()
    {
        // 3x3 matrix:
        // (0, 1) direct adjacent transition with vertical boundary seam at X=100, Y in [100..200]
        // (1, 2) direct adjacent transition with vertical boundary seam at X=200, Y in [100..200]
        // (0, 2) non-adjacent multi-hop route traversing hops [1, 2] with 0 direct waypoints
        var routes = new Route[9];
        for (int i = 0; i < 9; i++)
        {
            int from = i / 3;
            int to = i % 3;
            routes[i] = new Route(from, to, [], []);
        }

        routes[0 * 3 + 1] = new Route(0, 1, [new Waypoint(100, 100), new Waypoint(100, 200)], [1]);
        routes[1 * 3 + 2] = new Route(1, 2, [new Waypoint(200, 100), new Waypoint(200, 200)], [2]);
        routes[0 * 3 + 2] = new Route(0, 2, [], [1, 2]);

        var centroids = new Dictionary<int, (int X, int Y)>
        {
            [0] = (50, 150),
            [1] = (150, 150),
            [2] = (250, 150)
        };

        return (new RouteMatrix(3, routes), centroids);
    }

    [AvaloniaFact]
    public void Render_AdjacentMode_RendersPortalGatesAndTransitions()
    {
        var canvas = new Canvas();
        var (matrix, centroids) = CreateTestEnvironment();

        RouteGraphRenderer.Render(canvas, matrix, RouteDisplayMode.Adjacent, selectedRoute: null, onRouteClicked: null, centroids);

        Assert.NotEmpty(canvas.Children);

        // 2 portal gate lines + 2 transition routes * 2 segments each = 6 lines
        var lines = canvas.Children.OfType<Line>().ToList();
        Assert.Equal(6, lines.Count);

        // Zone badges for Z0, Z1, Z2
        var badges = canvas.Children.OfType<Border>().ToList();
        Assert.Equal(3, badges.Count);

        // Arrowheads for each adjacent transition entering the destination zone
        var arrows = canvas.Children.OfType<Polygon>().ToList();
        Assert.Equal(2, arrows.Count);
    }

    [AvaloniaFact]
    public void Render_SingleMode_ReconstructsMultiHopPath()
    {
        var canvas = new Canvas();
        var (matrix, centroids) = CreateTestEnvironment();
        Route target = matrix.GetRoute(0, 2)!;

        RouteGraphRenderer.Render(canvas, matrix, RouteDisplayMode.Single, selectedRoute: target, onRouteClicked: null, centroids);

        // 2 portal gate lines (one between 0-1, one between 1-2)
        // Multi-hop path: C0 -> Portal(0,1) -> C1 -> Portal(1,2) -> C2 (4 segments)
        // Total lines = 2 portal lines + 4 path segments = 6 lines
        var lines = canvas.Children.OfType<Line>().ToList();
        Assert.Equal(6, lines.Count);

        // Arrowheads on each path segment indicating direction of travel
        var arrows = canvas.Children.OfType<Polygon>().ToList();
        Assert.Equal(4, arrows.Count);

        // Badges for all 3 traversed zones (Start, Intermediate, End)
        var badges = canvas.Children.OfType<Border>().ToList();
        Assert.Equal(3, badges.Count);
    }

    [AvaloniaFact]
    public void Render_SingleMode_DirectRoute_RendersSingleCrossing()
    {
        var canvas = new Canvas();
        var (matrix, centroids) = CreateTestEnvironment();
        Route target = matrix.GetRoute(0, 1)!;

        RouteGraphRenderer.Render(canvas, matrix, RouteDisplayMode.Single, selectedRoute: target, onRouteClicked: null, centroids);

        // 2 portal gate lines in mesh + direct journey C0 -> Portal(0,1) -> C1 (2 segments) = 4 lines
        var lines = canvas.Children.OfType<Line>().ToList();
        Assert.Equal(4, lines.Count);

        var arrows = canvas.Children.OfType<Polygon>().ToList();
        Assert.Equal(2, arrows.Count);

        // Only start (0) and end (1) zones are part of direct hop sequence
        var badges = canvas.Children.OfType<Border>().ToList();
        Assert.Equal(2, badges.Count);
    }

    [AvaloniaFact]
    public void Render_ClickingLine_FiresCallbackWithRoute()
    {
        var canvas = new Canvas();
        var (matrix, centroids) = CreateTestEnvironment();
        Route target = matrix.GetRoute(0, 1)!;

        Route? clicked = null;
        RouteGraphRenderer.Render(canvas, matrix, RouteDisplayMode.Single, selectedRoute: target, r => clicked = r, centroids);

        // Find a transition path line (not portal dashed line)
        Line pathLine = canvas.Children.OfType<Line>().First(l => l.StrokeThickness > 3.0);

        pathLine.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(
            pathLine,
            new Avalonia.Input.Pointer(0, Avalonia.Input.PointerType.Mouse, true),
            canvas,
            new Avalonia.Point(75, 150),
            0,
            new Avalonia.Input.PointerPointProperties(),
            Avalonia.Input.KeyModifiers.None));

        Assert.NotNull(clicked);
        Assert.Equal((0, 1), (clicked.FromZone, clicked.ToZone));
    }

    [AvaloniaFact]
    public void Render_FallbackCentroids_WhenNoMaskCentroidsProvided()
    {
        var canvas = new Canvas();
        var (matrix, _) = CreateTestEnvironment();

        // Pass centroids = null to exercise fallback resolution from portal midpoints
        RouteGraphRenderer.Render(canvas, matrix, RouteDisplayMode.Adjacent, selectedRoute: null, onRouteClicked: null, centroids: null);

        Assert.NotEmpty(canvas.Children);
        var badges = canvas.Children.OfType<Border>().ToList();
        Assert.Equal(3, badges.Count);
    }
}
