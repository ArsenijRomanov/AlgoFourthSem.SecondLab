namespace TSP.Domain;

public sealed record SolverResult(
    Route? BestRoute,
    RouteEvaluation? BestEvaluation,
    bool HasFeasibleSolution,
    int Iterations,
    int ObjectiveEvaluations
);
