using TSP.Domain.Abstractions;

namespace TSP.ACO.Abstractions;

public interface IAntStartVertexSelector
{
    int SelectStartVertex(IWeightedGraph graph, int antIndex, int iteration);
}