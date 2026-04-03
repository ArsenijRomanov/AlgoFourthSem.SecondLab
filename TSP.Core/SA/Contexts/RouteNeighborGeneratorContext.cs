using TSP.Domain;
using TSP.Domain.Abstractions;

namespace TSP.SA.Contexts;

public class RouteNeighborGeneratorContext
{
    public Route CurrentRoute { get; }
    public IWeightedGraph Graph { get; } 
    public int Iteration { get; }

    public RouteNeighborGeneratorContext(
        Route currentRoute, 
        IWeightedGraph graph, 
        int iteration)
    {
        ArgumentNullException.ThrowIfNull(currentRoute);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentOutOfRangeException.ThrowIfNegative(iteration);

        CurrentRoute = currentRoute;
        Graph = graph;
        Iteration = iteration;
    }
}
