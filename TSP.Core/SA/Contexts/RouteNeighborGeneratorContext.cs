using TSP.Domain;
using TSP.Domain.Abstractions;

namespace TSP.SA.Contexts;

public class RouteNeighborGeneratorContext
{
    public Route CurrentRoute { get; }
    public IWeightedGraph Graph { get; } 
    public double CurrentTemperature { get; }
    public int Iteration { get; }

    public RouteNeighborGeneratorContext(
        Route currentRoute, 
        IWeightedGraph graph, 
        double currentTemperature,
        int iteration)
    {
        ArgumentNullException.ThrowIfNull(currentRoute);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentOutOfRangeException.ThrowIfNegative(currentTemperature);
        ArgumentOutOfRangeException.ThrowIfNegative(iteration);

        CurrentRoute = currentRoute;
        Graph = graph;
        CurrentTemperature = currentTemperature;
        Iteration = iteration;
    }
}
