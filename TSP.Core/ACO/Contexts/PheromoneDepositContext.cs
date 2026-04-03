using TSP.Domain;
using TSP.Domain.Abstractions;

namespace TSP.ACO.Contexts;

public sealed class PheromoneDepositContext
{
    public IWeightedGraph Graph { get; }
    public double[,] Pheromones { get; }
    public IReadOnlyList<AntRouteBuildResult> ColonyRoutes { get; }
    public Route? GlobalBestRoute { get; }
    public RouteEvaluation? GlobalBestEvaluation { get; }
    public double Q { get; }

    public PheromoneDepositContext(
        IWeightedGraph graph,
        double[,] pheromones,
        IReadOnlyList<AntRouteBuildResult> colonyRoutes,
        Route? globalBestRoute,
        RouteEvaluation? globalBestEvaluation,
        double q)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(pheromones);
        ArgumentNullException.ThrowIfNull(colonyRoutes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(q);

        Graph = graph;
        Pheromones = pheromones;
        ColonyRoutes = colonyRoutes;
        GlobalBestRoute = globalBestRoute;
        GlobalBestEvaluation = globalBestEvaluation;
        Q = q;
    }
}
