using TSP.Domain.Abstractions;

namespace TSP.ACO.Abstractions;

public interface IAcoState : ISolverState
{
    IReadOnlyList<AntRouteBuildResult> LastBuiltRoutes { get; }
    double[,]? Pheromones { get; }
}
