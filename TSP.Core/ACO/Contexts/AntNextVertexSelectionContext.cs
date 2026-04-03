using TSP.Domain.Abstractions;

namespace TSP.ACO.Contexts;

public sealed class AntNextVertexSelectionContext
{
    public IWeightedGraph Graph { get; }
    public double[,] Pheromones { get; }
    public IReadOnlySet<int> VisitedVertices { get; }
    public int CurrentVertex { get; }
    public int StartVertex { get; }
    public double Alpha { get; }
    public double Beta { get; }

    public AntNextVertexSelectionContext(
        IWeightedGraph graph,
        double[,] pheromones,
        IReadOnlySet<int> visitedVertices,
        int currentVertex,
        int startVertex,
        double alpha,
        double beta)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(pheromones);
        ArgumentNullException.ThrowIfNull(visitedVertices);
        ArgumentOutOfRangeException.ThrowIfNegative(currentVertex);
        ArgumentOutOfRangeException.ThrowIfNegative(startVertex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alpha);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(beta);

        Graph = graph;
        Pheromones = pheromones;
        VisitedVertices = visitedVertices;
        CurrentVertex = currentVertex;
        StartVertex = startVertex;
        Alpha = alpha;
        Beta = beta;
    }
}
