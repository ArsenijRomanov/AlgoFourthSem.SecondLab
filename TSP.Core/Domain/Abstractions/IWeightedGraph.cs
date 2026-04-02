namespace TSP.Domain.Abstractions;

public interface IWeightedGraph
{
    int VertexCount { get; }

    bool HasEdge(int from, int to);

    double GetWeight(int from, int to);

    bool TryGetWeight(int from, int to, out double weight);

    IReadOnlyList<(int Vertex, double Weight)> GetNeighbors(int vertex);
}
