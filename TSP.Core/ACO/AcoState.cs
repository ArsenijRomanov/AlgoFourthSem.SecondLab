using TSP.ACO.Abstractions;
using TSP.Domain;

namespace TSP.ACO;

public sealed class AcoState : IAcoState
{
    public Route? BestRoute { get; set; }
    public RouteEvaluation? BestEvaluation { get; set; }
    public bool HasFeasibleSolution { get; set; }

    public int IterationCount { get; set; }
    public int ObjectiveEvaluations { get; set; }

    public IReadOnlyList<AntRouteBuildResult> LastBuiltRoutes { get; set; } = [];
    public double[,]? Pheromones { get; set; }
}
