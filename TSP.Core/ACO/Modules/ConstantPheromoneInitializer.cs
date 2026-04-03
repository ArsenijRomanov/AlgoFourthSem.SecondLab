using TSP.ACO.Abstractions;
using TSP.Domain.Abstractions;

namespace TSP.ACO.Modules;

public sealed class ConstantPheromoneInitializer(double initialPheromone) : IPheromoneInitializer
{
    private readonly double _initialPheromone = initialPheromone > 0
        ? initialPheromone
        : throw new ArgumentOutOfRangeException(nameof(initialPheromone));

    public double[,] CreateInitialMatrix(IWeightedGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var pheromones = new double[graph.VertexCount, graph.VertexCount];

        for (var from = 0; from < graph.VertexCount; from++)
        {
            for (var to = 0; to < graph.VertexCount; to++)
            {
                pheromones[from, to] = graph.HasEdge(from, to)
                    ? _initialPheromone
                    : 0d;
            }
        }

        return pheromones;
    }
}
