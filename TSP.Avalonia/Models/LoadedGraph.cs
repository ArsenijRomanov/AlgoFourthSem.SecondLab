using Avalonia;
using TSP.Domain;
using TSP.Domain.Abstractions;

namespace TSP.Avalonia.Models;

public sealed class LoadedGraph
{
    public required string Name { get; init; }

    public string? SourcePath { get; init; }

    public required IWeightedGraph Graph { get; init; }

    public required IReadOnlyList<VertexPosition> Positions { get; init; }

    public required bool IsDirected { get; init; }

    public required int EdgeCount { get; init; }

    public Rect GetBounds()
    {
        if (Positions.Count == 0)
        {
            return new Rect(0, 0, 1, 1);
        }

        var minX = Positions.Min(static position => position.Point.X);
        var minY = Positions.Min(static position => position.Point.Y);
        var maxX = Positions.Max(static position => position.Point.X);
        var maxY = Positions.Max(static position => position.Point.Y);
        var width = Math.Max(1, maxX - minX);
        var height = Math.Max(1, maxY - minY);

        return new Rect(minX, minY, width, height);
    }
}
