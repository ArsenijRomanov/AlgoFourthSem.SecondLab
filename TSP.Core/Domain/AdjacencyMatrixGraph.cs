using TSP.Domain.Abstractions;

namespace TSP.Domain;

public sealed class AdjacencyMatrixGraph : IWeightedGraph
{
    private readonly double?[,] _weights;
    private readonly (int Vertex, double Weight)[]?[] _neighborsCache;

    public int VertexCount { get; }

    public AdjacencyMatrixGraph(double?[,] weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        var rows = weights.GetLength(0);
        var columns = weights.GetLength(1);

        if (rows == 0 || columns == 0)
            throw new ArgumentException("Матрица весов не может быть пустой.", nameof(weights));

        if (rows != columns)
            throw new ArgumentException("Матрица весов должна быть квадратной.", nameof(weights));

        _weights = weights;
        VertexCount = rows;
        _neighborsCache = new (int Vertex, double Weight)[VertexCount][];
    }

    public bool HasEdge(int from, int to)
    {
        ValidateVertex(from);
        ValidateVertex(to);

        return _weights[from, to].HasValue;
    }

    public double GetWeight(int from, int to)
    {
        ValidateVertex(from);
        ValidateVertex(to);

        var weight = _weights[from, to];
        return weight ?? throw new InvalidOperationException($"Ребро {from} -> {to} не существует.");
    }

    public bool TryGetWeight(int from, int to, out double weight)
    {
        ValidateVertex(from);
        ValidateVertex(to);

        var value = _weights[from, to];
        if (value.HasValue)
        {
            weight = value.Value;
            return true;
        }

        weight = 0;
        return false;
    }

    public IReadOnlyList<(int Vertex, double Weight)> GetNeighbors(int vertex)
    {
        ValidateVertex(vertex);

        var neighbors = _neighborsCache[vertex];
        if (neighbors is not null)
            return neighbors;

        neighbors = BuildNeighbors(vertex);
        _neighborsCache[vertex] = neighbors;

        return neighbors;
    }

    private (int Vertex, double Weight)[] BuildNeighbors(int vertex)
    {
        var neighbors = new List<(int Vertex, double Weight)>();

        for (var to = 0; to < VertexCount; to++)
        {
            var weight = _weights[vertex, to];
            if (weight.HasValue)
                neighbors.Add((to, weight.Value));
        }

        return neighbors.ToArray();
    }

    private void ValidateVertex(int vertex)
    {
        if (vertex < 0 || vertex >= VertexCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(vertex),
                $"Вершина {vertex} находится вне диапазона [0, {VertexCount - 1}].");
        }
    }
}
