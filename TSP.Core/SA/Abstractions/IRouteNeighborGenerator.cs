using TSP.Domain;
using TSP.Domain.Abstractions;
using TSP.SA.Contexts;

namespace TSP.SA.Abstractions;

public interface IRouteNeighborGenerator
{
    Route GenerateNeighbor(RouteNeighborGeneratorContext context);
}
