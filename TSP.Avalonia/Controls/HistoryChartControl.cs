using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TSP.Avalonia.Models;

namespace TSP.Avalonia.Controls;

public sealed class HistoryChartControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<HistoryPoint>> PointsProperty =
        AvaloniaProperty.Register<HistoryChartControl, IReadOnlyList<HistoryPoint>>(nameof(Points), []);

    public IReadOnlyList<HistoryPoint> Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    static HistoryChartControl()
    {
        AffectsRender<HistoryChartControl>(PointsProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds.Deflate(16);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#0F1722")), Bounds);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#223047"))), Bounds, 16, 16);

        if (Points.Count == 0)
        {
            DrawLabel(context, "История пока пустая.", new Point(bounds.X + 12, bounds.Y + 12), 14, Brushes.Gray);
            return;
        }

        var minIteration = Points.Min(static point => point.Iteration);
        var maxIteration = Math.Max(minIteration + 1, Points.Max(static point => point.Iteration));
        var minCost = Points.Min(static point => point.BestCost);
        var maxCost = Math.Max(minCost + 1e-6, Points.Max(static point => point.BestCost));

        var plot = bounds.Deflate(18);
        var axisPen = new Pen(new SolidColorBrush(Color.Parse("#314158")), 1);
        context.DrawLine(axisPen, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));
        context.DrawLine(axisPen, new Point(plot.Left, plot.Top), new Point(plot.Left, plot.Bottom));

        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            var first = true;
            foreach (var point in Points)
            {
                var x = plot.Left + (point.Iteration - minIteration) * plot.Width / (maxIteration - minIteration);
                var y = plot.Bottom - (point.BestCost - minCost) * plot.Height / (maxCost - minCost);
                var screen = new Point(x, y);

                if (first)
                {
                    stream.BeginFigure(screen, false);
                    first = false;
                }
                else
                {
                    stream.LineTo(screen);
                }
            }
        }

        context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#2F81F7")), 3, lineCap: PenLineCap.Round), geometry);

        foreach (var point in Points)
        {
            var x = plot.Left + (point.Iteration - minIteration) * plot.Width / (maxIteration - minIteration);
            var y = plot.Bottom - (point.BestCost - minCost) * plot.Height / (maxCost - minCost);
            context.FillEllipse(new SolidColorBrush(Color.Parse("#7EE787")), new Point(x, y), 4, 4);
        }

        DrawLabel(context, $"Итерации: {minIteration} – {maxIteration}", new Point(bounds.Left + 8, bounds.Top + 8), 12, Brushes.White);
        DrawLabel(context, $"Лучшее значение: {minCost:G8} – {maxCost:G8}", new Point(bounds.Left + 8, bounds.Top + 30), 12, Brushes.White);
    }

    private static void DrawLabel(DrawingContext context, string text, Point point, double fontSize, IBrush brush)
    {
        var layout = new TextLayout(
            text,
            new Typeface(FontFamily.Default),
            fontSize,
            brush);

        context.DrawText(layout, point);
    }
}
