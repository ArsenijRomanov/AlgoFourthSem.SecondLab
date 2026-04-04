using Avalonia;
using TSP.Avalonia.Models;

namespace TSP.Avalonia.Services;

public sealed class GraphLayoutService
{
    public IReadOnlyList<VertexPosition> CreateCircularLayout(int vertexCount)
    {
        if (vertexCount <= 0)
        {
            return [];
        }

        var positions = new List<VertexPosition>(vertexCount);
        var radius = Math.Max(60, vertexCount * 7);
        var center = new Point(radius + 24, radius + 24);

        for (var vertex = 0; vertex < vertexCount; vertex++)
        {
            var angle = 2 * Math.PI * vertex / vertexCount - Math.PI / 2;
            var point = new Point(
                center.X + radius * Math.Cos(angle),
                center.Y + radius * Math.Sin(angle));

            positions.Add(new VertexPosition(vertex, point));
        }

        return positions;
    }
}
