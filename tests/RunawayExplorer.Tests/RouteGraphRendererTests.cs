using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using RunawayExplorer.Core.Formats;
using RunawayExplorer.Views;
using Xunit;

namespace RunawayExplorer.Tests;

public class RouteGraphRendererTests
{
    private static RouteMatrix CreateTestMatrix()
    {
        // 3x3 matrix:
        // (0, 1) direct crossing with 1 waypoint, hops: [1]
        // (1, 2) adjacent route with 2 waypoints, hops: [2]
        // (0, 2) multi-hop route with 3 waypoints, hops: [1, 2]
        var routes = new Route[9];
        for (int i = 0; i < 9; i++)
        {
            int from = i / 3;
            int to = i % 3;
            routes[i] = new Route(from, to, [], []);
        }

        routes[0 * 3 + 1] = new Route(0, 1, [new Waypoint(100, 100)], [1]);
        routes[1 * 3 + 2] = new Route(1, 2, [new Waypoint(100, 100), new Waypoint(200, 200)], [2]);
        routes[0 * 3 + 2] = new Route(0, 2, [new Waypoint(50, 50), new Waypoint(150, 150), new Waypoint(250, 250)], [1, 2]);

        return new RouteMatrix(3, routes);
    }

    [AvaloniaFact]
    public void Render_AdjacentMode_FiltersDirectTransitions()
    {
        var canvas = new Canvas();
        RouteMatrix matrix = CreateTestMatrix();

        RouteGraphRenderer.Render(canvas, matrix, RouteDisplayMode.Adjacent, selectedRoute: null, onRouteClicked: null);

        // Should include route (0,1) and (1,2) whose hops.Count <= 1, but exclude (0,2) whose hops.Count == 2
        Assert.NotEmpty(canvas.Children);
        var lines = canvas.Children.OfType<Line>().ToList();
        // Route (1,2) has 2 waypoints -> 1 line. (0,2) would have 2 lines, but is excluded in adjacent mode.
        Assert.Single(lines);

        var ellipses = canvas.Children.OfType<Ellipse>().ToList();
        // Route (0,1) single waypoint circle + route (1,2) terminal markers (start and end)
        Assert.NotEmpty(ellipses);
    }

    [AvaloniaFact]
    public void Render_AllMode_RendersAllActiveRoutes()
    {
        var canvas = new Canvas();
        RouteMatrix matrix = CreateTestMatrix();

        RouteGraphRenderer.Render(canvas, matrix, RouteDisplayMode.All, selectedRoute: null, onRouteClicked: null);

        var lines = canvas.Children.OfType<Line>().ToList();
        // Route (1,2) has 1 line segment. Route (0,2) has 2 line segments. Total = 3 lines.
        Assert.Equal(3, lines.Count);

        var arrows = canvas.Children.OfType<Polygon>().ToList();
        Assert.Equal(3, arrows.Count);
    }

    [AvaloniaFact]
    public void Render_SingleMode_RendersOnlySpecifiedRoute()
    {
        var canvas = new Canvas();
        RouteMatrix matrix = CreateTestMatrix();
        Route target = matrix.GetRoute(0, 2)!;

        RouteGraphRenderer.Render(canvas, matrix, RouteDisplayMode.Single, selectedRoute: target, onRouteClicked: null);

        var lines = canvas.Children.OfType<Line>().ToList();
        // Route (0,2) has 3 waypoints -> 2 lines
        Assert.Equal(2, lines.Count);
    }

    [AvaloniaFact]
    public void Render_ClickingLine_FiresCallbackWithRoute()
    {
        var canvas = new Canvas();
        RouteMatrix matrix = CreateTestMatrix();
        Route target = matrix.GetRoute(1, 2)!;

        Route? clicked = null;
        RouteGraphRenderer.Render(canvas, matrix, RouteDisplayMode.Single, selectedRoute: target, r => clicked = r);

        Line line = canvas.Children.OfType<Line>().First();
        // Raise pointer press or invoke handler via reflection/simulated input
        line.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(
            line,
            new Avalonia.Input.Pointer(0, Avalonia.Input.PointerType.Mouse, true),
            canvas,
            new Avalonia.Point(150, 150),
            0,
            new Avalonia.Input.PointerPointProperties(),
            Avalonia.Input.KeyModifiers.None));

        Assert.NotNull(clicked);
        Assert.Equal((1, 2), (clicked.FromZone, clicked.ToZone));
    }
}
