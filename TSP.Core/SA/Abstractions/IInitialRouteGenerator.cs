using TSP.Domain;
using TSP.Domain.Abstractions;

namespace TSP.SA.Abstractions;

public interface IInitialRouteGenerator
{
    Route Create(IWeightedGraph graph);
}
