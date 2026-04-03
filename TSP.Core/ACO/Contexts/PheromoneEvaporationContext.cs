using TSP.Domain.Abstractions;

namespace TSP.ACO.Contexts;

public sealed class PheromoneEvaporationContext
{
    public IWeightedGraph Graph { get; }
    public double[,] Pheromones { get; }
    public double EvaporationRate { get; }

    public PheromoneEvaporationContext(
        IWeightedGraph graph,
        double[,] pheromones,
        double evaporationRate)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(pheromones);

        if (evaporationRate is <= 0 or >= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(evaporationRate),
                "Коэффициент испарения должен быть в интервале (0, 1).");
        }

        Graph = graph;
        Pheromones = pheromones;
        EvaporationRate = evaporationRate;
    }
}
