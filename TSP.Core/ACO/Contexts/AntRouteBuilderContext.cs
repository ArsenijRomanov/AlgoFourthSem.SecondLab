using TSP.ACO.Abstractions;
using TSP.Domain.Abstractions;

namespace TSP.ACO.Contexts;

public sealed class AntRouteBuilderContext
{
    public IWeightedGraph Graph { get; }
    public double[,] Pheromones { get; }
    public int StartVertex { get; }
    public double Alpha { get; }
    public double Beta { get; }
    public INextVertexSelector NextVertexSelector { get; }

    public AntRouteBuilderContext(
        IWeightedGraph graph,
        double[,] pheromones,
        int startVertex,
        double alpha,
        double beta,
        INextVertexSelector nextVertexSelector)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(pheromones);
        ArgumentNullException.ThrowIfNull(nextVertexSelector);
        ArgumentOutOfRangeException.ThrowIfNegative(startVertex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alpha);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(beta);

        Graph = graph;
        Pheromones = pheromones;
        StartVertex = startVertex;
        Alpha = alpha;
        Beta = beta;
        NextVertexSelector = nextVertexSelector;
    }
}
