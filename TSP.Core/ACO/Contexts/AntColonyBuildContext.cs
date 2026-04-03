using TSP.Domain.Abstractions;

namespace TSP.ACO.Contexts;

public sealed class AntColonyBuildContext
{
    public IWeightedGraph Graph { get; }
    public double[,] Pheromones { get; }
    public int AntCount { get; }
    public int Iteration { get; }

    public AntColonyBuildContext(
        IWeightedGraph graph,
        double[,] pheromones,
        int antCount,
        int iteration)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(pheromones);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(antCount);
        ArgumentOutOfRangeException.ThrowIfNegative(iteration);

        Graph = graph;
        Pheromones = pheromones;
        AntCount = antCount;
        Iteration = iteration;
    }
}
