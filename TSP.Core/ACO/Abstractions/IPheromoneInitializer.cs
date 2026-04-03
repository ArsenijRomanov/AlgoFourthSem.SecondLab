using TSP.Domain.Abstractions;

namespace TSP.ACO.Abstractions;

public interface IPheromoneInitializer
{
    double[,] CreateInitialMatrix(IWeightedGraph graph);
}
