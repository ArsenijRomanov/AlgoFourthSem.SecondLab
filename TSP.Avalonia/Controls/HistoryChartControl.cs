using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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

        context.FillRectangle(new SolidColorBrush(Color.Parse("#0F1722")), Bounds);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#223047"))), Bounds, 16, 16);

        var frame = Bounds.Deflate(18);
        if (Points.Count == 0)
        {
            DrawLabel(context, "История пока пустая.", new Point(frame.Left + 12, frame.Top + 12), 14, Brushes.Gray);
            return;
        }

        var minIteration = Points.Min(static point => point.Iteration);
        var maxIteration = Math.Max(minIteration + 1, Points.Max(static point => point.Iteration));
        var minCost = Points.Min(static point => point.BestCost);
        var maxCost = Math.Max(minCost + 1e-6, Points.Max(static point => point.BestCost));

        var plot = new Rect(frame.Left + 74, frame.Top + 28, Math.Max(40, frame.Width - 104), Math.Max(40, frame.Height - 92));
        var axisPen = new Pen(new SolidColorBrush(Color.Parse("#314158")), 1);
        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#1C2A3D")), 1);

        context.DrawLine(axisPen, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));
        context.DrawLine(axisPen, new Point(plot.Left, plot.Top), new Point(plot.Left, plot.Bottom));

        const int tickCount = 5;
        for (var index = 0; index <= tickCount; index++)
        {
            var t = index / (double)tickCount;

            var x = plot.Left + plot.Width * t;
            var y = plot.Bottom - plot.Height * t;

            context.DrawLine(gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            context.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));

            var iterationValue = minIteration + (maxIteration - minIteration) * t;
            var costValue = minCost + (maxCost - minCost) * t;

            DrawCenteredLabel(context, Math.Round(iterationValue).ToString(CultureInfo.InvariantCulture), new Point(x, plot.Bottom + 10), 11, new SolidColorBrush(Color.Parse("#93A7C3")));
            DrawRightAlignedLabel(context, costValue.ToString("G6", CultureInfo.InvariantCulture), new Point(plot.Left - 10, y - 8), 11, new SolidColorBrush(Color.Parse("#93A7C3")));
        }

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
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#7EE787")), null, new Point(x, y), 4, 4);
        }

        DrawLabel(context, "Лучшее значение", new Point(plot.Left, frame.Top - 4), 12, Brushes.White);
        DrawCenteredLabel(context, "Итерация", new Point(plot.Left + plot.Width / 2, frame.Bottom - 26), 12, Brushes.White);
    }

    private static void DrawLabel(DrawingContext context, string text, Point point, double fontSize, IBrush brush)
    {
        var formattedText = CreateText(text, fontSize, brush);
        context.DrawText(formattedText, point);
    }

    private static void DrawCenteredLabel(DrawingContext context, string text, Point point, double fontSize, IBrush brush)
    {
        var formattedText = CreateText(text, fontSize, brush);
        context.DrawText(formattedText, new Point(point.X - formattedText.Width / 2, point.Y));
    }

    private static void DrawRightAlignedLabel(DrawingContext context, string text, Point point, double fontSize, IBrush brush)
    {
        var formattedText = CreateText(text, fontSize, brush);
        context.DrawText(formattedText, new Point(point.X - formattedText.Width, point.Y));
    }

    private static FormattedText CreateText(string text, double fontSize, IBrush brush)
        => new(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default),
            fontSize,
            brush);
}
