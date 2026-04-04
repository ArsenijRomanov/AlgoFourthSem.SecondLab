using TSP.ACO;
using TSP.Domain;

namespace TSP.Avalonia.Models;

public sealed record IterationSnapshot(
    int Iteration,
    int ObjectiveEvaluations,
    bool HasFeasibleSolution,
    Route? BestRoute,
    RouteEvaluation? BestEvaluation,
    Route? CurrentRoute,
    RouteEvaluation? CurrentEvaluation,
    IReadOnlyList<AntRouteBuildResult> LastBuiltRoutes,
    double[,]? Pheromones,
    int SuccessfulAnts,
    int CompleteAnts,
    double? IterationBestCost);
