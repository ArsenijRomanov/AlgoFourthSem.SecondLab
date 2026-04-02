namespace TSP.Domain.Abstractions;

public interface ISolverState
{
    Route? BestRoute { get; }
    RouteEvaluation? BestEvaluation { get; }
    bool HasFeasibleSolution { get; }

    int IterationCount { get; }
    int ObjectiveEvaluations { get; }
}
