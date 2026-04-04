using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TSP.ACO;
using TSP.Avalonia.Models;
using TSP.Domain;

namespace TSP.Avalonia.Controls;

public sealed class GraphCanvasControl : Control
{
    public static readonly StyledProperty<LoadedGraph?> GraphDataProperty =
        AvaloniaProperty.Register<GraphCanvasControl, LoadedGraph?>(nameof(GraphData));

    public static readonly StyledProperty<Route?> CurrentRouteProperty =
        AvaloniaProperty.Register<GraphCanvasControl, Route?>(nameof(CurrentRoute));

    public static readonly StyledProperty<Route?> BestRouteProperty =
        AvaloniaProperty.Register<GraphCanvasControl, Route?>(nameof(BestRoute));

    public static readonly StyledProperty<Route?> SelectedAntRouteProperty =
        AvaloniaProperty.Register<GraphCanvasControl, Route?>(nameof(SelectedAntRoute));

    public static readonly StyledProperty<IReadOnlyList<AntRouteBuildResult>> AntRoutesProperty =
        AvaloniaProperty.Register<GraphCanvasControl, IReadOnlyList<AntRouteBuildResult>>(nameof(AntRoutes), []);

    public static readonly StyledProperty<double[,]?> PheromonesProperty =
        AvaloniaProperty.Register<GraphCanvasControl, double[,]?>(nameof(Pheromones));

    public static readonly StyledProperty<bool> ShowWeightsProperty =
        AvaloniaProperty.Register<GraphCanvasControl, bool>(nameof(ShowWeights));

    public static readonly StyledProperty<bool> ShowVertexLabelsProperty =
        AvaloniaProperty.Register<GraphCanvasControl, bool>(nameof(ShowVertexLabels), true);

    public static readonly StyledProperty<bool> ShowDirectionsProperty =
        AvaloniaProperty.Register<GraphCanvasControl, bool>(nameof(ShowDirections), true);

    public static readonly StyledProperty<bool> ShowBestRouteProperty =
        AvaloniaProperty.Register<GraphCanvasControl, bool>(nameof(ShowBestRoute), true);

    public static readonly StyledProperty<bool> HighlightMissingEdgesProperty =
        AvaloniaProperty.Register<GraphCanvasControl, bool>(nameof(HighlightMissingEdges), true);

    public static readonly StyledProperty<AntOverlayMode> AntOverlayModeProperty =
        AvaloniaProperty.Register<GraphCanvasControl, AntOverlayMode>(nameof(AntOverlayMode), Models.AntOverlayMode.ColonyRoutes);

    private bool _isPanning;
    private Point _lastPointer;
    private double _zoom = 1.0;
    private Vector _pan = Vector.Zero;

    public LoadedGraph? GraphData
    {
        get => GetValue(GraphDataProperty);
        set => SetValue(GraphDataProperty, value);
    }

    public Route? CurrentRoute
    {
        get => GetValue(CurrentRouteProperty);
        set => SetValue(CurrentRouteProperty, value);
    }

    public Route? BestRoute
    {
        get => GetValue(BestRouteProperty);
        set => SetValue(BestRouteProperty, value);
    }

    public Route? SelectedAntRoute
    {
        get => GetValue(SelectedAntRouteProperty);
        set => SetValue(SelectedAntRouteProperty, value);
    }

    public IReadOnlyList<AntRouteBuildResult> AntRoutes
    {
        get => GetValue(AntRoutesProperty);
        set => SetValue(AntRoutesProperty, value);
    }

    public double[,]? Pheromones
    {
        get => GetValue(PheromonesProperty);
        set => SetValue(PheromonesProperty, value);
    }

    public bool ShowWeights
    {
        get => GetValue(ShowWeightsProperty);
        set => SetValue(ShowWeightsProperty, value);
    }

    public bool ShowVertexLabels
    {
        get => GetValue(ShowVertexLabelsProperty);
        set => SetValue(ShowVertexLabelsProperty, value);
    }

    public bool ShowDirections
    {
        get => GetValue(ShowDirectionsProperty);
        set => SetValue(ShowDirectionsProperty, value);
    }

    public bool ShowBestRoute
    {
        get => GetValue(ShowBestRouteProperty);
        set => SetValue(ShowBestRouteProperty, value);
    }

    public bool HighlightMissingEdges
    {
        get => GetValue(HighlightMissingEdgesProperty);
        set => SetValue(HighlightMissingEdgesProperty, value);
    }

    public AntOverlayMode AntOverlayMode
    {
        get => GetValue(AntOverlayModeProperty);
        set => SetValue(AntOverlayModeProperty, value);
    }

    static GraphCanvasControl()
    {
        AffectsRender<GraphCanvasControl>(
            GraphDataProperty,
            CurrentRouteProperty,
            BestRouteProperty,
            SelectedAntRouteProperty,
            AntRoutesProperty,
            PheromonesProperty,
            ShowWeightsProperty,
            ShowVertexLabelsProperty,
            ShowDirectionsProperty,
            ShowBestRouteProperty,
            HighlightMissingEdgesProperty,
            AntOverlayModeProperty);
    }

    public GraphCanvasControl()
    {
        ClipToBounds = true;
    }

    public void ZoomIn()
        => ZoomBy(1.15, Bounds.Center);

    public void ZoomOut()
        => ZoomBy(1 / 1.15, Bounds.Center);

    public void ResetView()
    {
        _zoom = 1.0;
        _pan = Vector.Zero;
        InvalidateVisual();
    }

    public async Task ExportPngAsync(string path)
    {
        var width = Math.Max(1, (int)Math.Ceiling(Bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(Bounds.Height));
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(this);

        await using var stream = File.Create(path);
        bitmap.Save(stream);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPanning = true;
        _lastPointer = e.GetPosition(this);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isPanning = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isPanning)
        {
            return;
        }

        var current = e.GetPosition(this);
        var delta = current - _lastPointer;
        _pan += delta;
        _lastPointer = current;
        ClampPan();
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var factor = e.Delta.Y > 0 ? 1.12 : 1 / 1.12;
        ZoomBy(factor, e.GetPosition(this));
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var background = new SolidColorBrush(Color.Parse("#0E141B"));
        var borderPen = new Pen(new SolidColorBrush(Color.Parse("#223047")), 1);
        context.FillRectangle(background, Bounds);
        context.DrawRectangle(null, borderPen, Bounds, 18, 18);

        if (GraphData is null)
        {
            DrawText(context, "Загрузите граф, чтобы начать визуализацию.", new Point(28, 28), 18, new SolidColorBrush(Color.Parse("#8B9AB2")));
            DrawText(context, "Поддерживаются встроенные примеры и STP 1.00 файлы.", new Point(28, 56), 13, new SolidColorBrush(Color.Parse("#718198")));
            return;
        }

        var graph = GraphData.Graph;
        var positions = GraphData.Positions.ToDictionary(static position => position.Vertex, static position => position.Point);
        var fit = CreateViewport();
        var weightDetails = ShowWeights && fit.EffectiveScale >= 1.75;
        var vertexDetails = ShowVertexLabels && fit.EffectiveScale >= 1.0;
        var directionDetails = ShowDirections && GraphData.IsDirected && fit.EffectiveScale >= 1.0;

        var routeCounts = AntOverlayMode == Models.AntOverlayMode.ColonyRoutes
            ? BuildRouteCounts(AntRoutes)
            : new Dictionary<(int From, int To), int>();
        var maxRouteCount = Math.Max(1, routeCounts.Values.DefaultIfEmpty(0).Max());
        var maxPheromone = GetMaxPheromone(graph);

        DrawCoordinateHints(context, fit);

        for (var from = 0; from < graph.VertexCount; from++)
        {
            foreach (var (to, _) in graph.GetNeighbors(from))
            {
                if (!positions.TryGetValue(from, out var source) || !positions.TryGetValue(to, out var target))
                {
                    continue;
                }

                var style = ResolveBaseEdgeStyle((from, to), routeCounts, maxRouteCount, maxPheromone);
                if (style.Opacity <= 0.001)
                {
                    continue;
                }

                DrawEdge(context, fit.Map(source), fit.Map(target), style.Brush, style.Thickness, directionDetails);

                if (weightDetails && graph.TryGetWeight(from, to, out var weight))
                {
                    var midpoint = Midpoint(fit.Map(source), fit.Map(target));
                    DrawText(context, weight.ToString("G6", CultureInfo.InvariantCulture), midpoint + new Vector(4, -4), 11, new SolidColorBrush(Color.Parse("#B7C7DD")));
                }
            }
        }

        if (GraphData.IsDirected && AntOverlayMode == Models.AntOverlayMode.ColonyRoutes && routeCounts.Count > 0)
        {
            DrawText(context, "Режим: текущие пути колонии", new Point(24, 18), 12, new SolidColorBrush(Color.Parse("#8FB6FF")));
        }
        else if (AntOverlayMode == Models.AntOverlayMode.Pheromones && Pheromones is not null)
        {
            DrawText(context, "Режим: карта феромонов", new Point(24, 18), 12, new SolidColorBrush(Color.Parse("#8FB6FF")));
        }

        if (SelectedAntRoute is not null)
        {
            DrawRoute(context, fit, positions, GraphData.Graph, SelectedAntRoute, new SolidColorBrush(Color.Parse("#F5C542")), 4.2, GraphData.IsDirected, false);
        }

        if (CurrentRoute is not null)
        {
            DrawRoute(context, fit, positions, GraphData.Graph, CurrentRoute, new SolidColorBrush(Color.Parse("#FF9B54")), 4.4, GraphData.IsDirected, HighlightMissingEdges);
        }

        if (ShowBestRoute && BestRoute is not null)
        {
            DrawRoute(context, fit, positions, GraphData.Graph, BestRoute, new SolidColorBrush(Color.Parse("#3BD671")), 5.8, GraphData.IsDirected, false);
        }

        var radius = Math.Clamp(5.5 + fit.EffectiveScale * 0.35, 5.5, 13);
        foreach (var position in GraphData.Positions)
        {
            var point = fit.Map(position.Point);
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#D8E6FF")), null, point, radius, radius);
            context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.Parse("#0D1117")), 2), point, radius, radius);

            if (vertexDetails)
            {
                DrawText(
                    context,
                    (position.Vertex + 1).ToString(CultureInfo.InvariantCulture),
                    point + new Vector(radius + 2, -radius - 2),
                    12,
                    new SolidColorBrush(Color.Parse("#E6EDF7")));
            }
        }
    }

    private void ZoomBy(double factor, Point anchor)
    {
        var clamped = Math.Clamp(_zoom * factor, 0.4, 18);
        var actualFactor = clamped / _zoom;
        _zoom = clamped;

        var center = Bounds.Center;
        var anchorDelta = anchor - center;
        _pan = (_pan + anchorDelta) * actualFactor - anchorDelta;
        ClampPan();
        InvalidateVisual();
    }

    private void ClampPan()
    {
        var horizontalLimit = Bounds.Width * 0.35;
        var verticalLimit = Bounds.Height * 0.35;
        _pan = new Vector(
            Math.Clamp(_pan.X, -horizontalLimit, horizontalLimit),
            Math.Clamp(_pan.Y, -verticalLimit, verticalLimit));
    }

    private ViewportTransform CreateViewport()
    {
        var bounds = GraphData!.GetBounds();
        var padding = 56.0;
        var width = Math.Max(1, Bounds.Width - padding * 2);
        var height = Math.Max(1, Bounds.Height - padding * 2);
        var fitScale = Math.Min(width / bounds.Width, height / bounds.Height);
        fitScale = double.IsFinite(fitScale) && fitScale > 0 ? fitScale : 1;

        var scaledWidth = bounds.Width * fitScale;
        var scaledHeight = bounds.Height * fitScale;
        var offsetX = (Bounds.Width - scaledWidth) / 2 + _pan.X;
        var offsetY = (Bounds.Height - scaledHeight) / 2 + _pan.Y;
        var effectiveScale = fitScale * _zoom;

        return new ViewportTransform(bounds, effectiveScale, offsetX, offsetY);
    }

    private EdgeStyle ResolveBaseEdgeStyle(
        (int From, int To) edge,
        IReadOnlyDictionary<(int From, int To), int> routeCounts,
        int maxRouteCount,
        double maxPheromone)
    {
        if (AntOverlayMode == Models.AntOverlayMode.ColonyRoutes && routeCounts.TryGetValue(edge, out var count))
        {
            var intensity = count / (double)maxRouteCount;
            return new EdgeStyle(
                new SolidColorBrush(Color.FromArgb((byte)(70 + intensity * 150), 78, 153, 255)),
                1.0 + intensity * 4.6,
                1);
        }

        if (AntOverlayMode == Models.AntOverlayMode.Pheromones && Pheromones is not null && maxPheromone > 0)
        {
            var pheromone = Pheromones[edge.From, edge.To];
            if (pheromone > 0)
            {
                var intensity = pheromone / maxPheromone;
                return new EdgeStyle(
                    new SolidColorBrush(Color.FromArgb((byte)(20 + intensity * 200), 74, 150, 255)),
                    0.8 + intensity * 4.2,
                    1);
            }
        }

        return new EdgeStyle(new SolidColorBrush(Color.FromArgb(42, 163, 179, 204)), 1.0, 0.18);
    }

    private double GetMaxPheromone(TSP.Domain.Abstractions.IWeightedGraph graph)
    {
        if (Pheromones is null)
        {
            return 0;
        }

        var max = 0.0;
        for (var from = 0; from < graph.VertexCount; from++)
        {
            foreach (var (to, _) in graph.GetNeighbors(from))
            {
                max = Math.Max(max, Pheromones[from, to]);
            }
        }

        return max;
    }

    private static Dictionary<(int From, int To), int> BuildRouteCounts(IReadOnlyList<AntRouteBuildResult> routes)
    {
        var result = new Dictionary<(int From, int To), int>();
        foreach (var item in routes)
        {
            if (item.Route is null)
            {
                continue;
            }

            foreach (var edge in EnumerateEdges(item.Route))
            {
                result.TryGetValue(edge, out var count);
                result[edge] = count + 1;
            }
        }

        return result;
    }

    private void DrawRoute(
        DrawingContext context,
        ViewportTransform fit,
        IReadOnlyDictionary<int, Point> positions,
        TSP.Domain.Abstractions.IWeightedGraph graph,
        Route route,
        IBrush brush,
        double thickness,
        bool drawDirections,
        bool highlightMissingEdges)
    {
        foreach (var (from, to) in EnumerateEdges(route))
        {
            if (!positions.TryGetValue(from, out var source) || !positions.TryGetValue(to, out var target))
            {
                continue;
            }

            var hasEdge = graph.HasEdge(from, to);
            var edgeBrush = hasEdge || !highlightMissingEdges
                ? brush
                : new SolidColorBrush(Color.Parse("#FF4D4D"));
            var edgeThickness = hasEdge || !highlightMissingEdges
                ? thickness
                : thickness + 1.2;

            DrawEdge(context, fit.Map(source), fit.Map(target), edgeBrush, edgeThickness, drawDirections);
        }
    }

    private static IEnumerable<(int From, int To)> EnumerateEdges(Route route)
    {
        if (route.Count == 0)
        {
            yield break;
        }

        for (var index = 0; index < route.Count; index++)
        {
            var from = route[index];
            var to = route[(index + 1) % route.Count];
            yield return (from, to);
        }
    }

    private static void DrawEdge(DrawingContext context, Point source, Point target, IBrush brush, double thickness, bool drawArrow)
    {
        var pen = new Pen(brush, thickness, lineCap: PenLineCap.Round);
        context.DrawLine(pen, source, target);

        if (!drawArrow)
        {
            return;
        }

        var dx = target.X - source.X;
        var dy = target.Y - source.Y;
        var length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        var unit = new Vector(dx / length, dy / length);
        var normal = new Vector(-unit.Y, unit.X);
        var arrowLength = 8 + thickness;
        var arrowWidth = 4 + thickness * 0.4;
        var tip = target - unit * 10;
        var left = tip - unit * arrowLength + normal * arrowWidth;
        var right = tip - unit * arrowLength - normal * arrowWidth;

        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            stream.BeginFigure(tip, true);
            stream.LineTo(left);
            stream.LineTo(right);
            stream.EndFigure(true);
        }

        context.DrawGeometry(brush, null, geometry);
    }

    private void DrawCoordinateHints(DrawingContext context, ViewportTransform fit)
    {
        var bounds = GraphData!.GetBounds();
        var left = bounds.Left.ToString("G6", CultureInfo.InvariantCulture);
        var right = bounds.Right.ToString("G6", CultureInfo.InvariantCulture);
        var bottom = bounds.Bottom.ToString("G6", CultureInfo.InvariantCulture);
        var top = bounds.Top.ToString("G6", CultureInfo.InvariantCulture);
        var brush = new SolidColorBrush(Color.Parse("#60708A"));

        DrawText(context, left, new Point(16, Bounds.Bottom - 24), 11, brush);
        DrawText(context, right, new Point(Math.Max(16, Bounds.Right - 90), Bounds.Bottom - 24), 11, brush);
        DrawText(context, top, new Point(12, 14), 11, brush);
        DrawText(context, bottom, new Point(12, Math.Max(18, Bounds.Bottom - 44)), 11, brush);
    }

    private static Point Midpoint(Point source, Point target)
        => new((source.X + target.X) / 2, (source.Y + target.Y) / 2);

    private static void DrawText(DrawingContext context, string text, Point point, double fontSize, IBrush brush)
    {
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default),
            fontSize,
            brush);
        context.DrawText(formattedText, point);
    }

    private readonly record struct EdgeStyle(IBrush Brush, double Thickness, double Opacity);

    private readonly record struct ViewportTransform(Rect WorldBounds, double EffectiveScale, double OffsetX, double OffsetY)
    {
        public Point Map(Point point)
        {
            var x = OffsetX + (point.X - WorldBounds.Left) * EffectiveScale;
            var y = OffsetY + (WorldBounds.Bottom - point.Y) * EffectiveScale;
            return new Point(x, y);
        }
    }
}
