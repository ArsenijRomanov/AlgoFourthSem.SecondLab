using TSP.Domain;
using TSP.SA.Abstractions;

namespace TSP.SA;

public sealed class SaState : ISaState
{
    public Route? CurrentRoute { get; set; }
    public RouteEvaluation? CurrentEvaluation { get; set; }
    public double? CurrentTemperature { get; set; }

    public Route? BestRoute { get; set; }
    public RouteEvaluation? BestEvaluation { get; set; }
    public bool HasFeasibleSolution { get; set; }

    public int IterationCount { get; set; }
    public int ObjectiveEvaluations { get; set; }
}
