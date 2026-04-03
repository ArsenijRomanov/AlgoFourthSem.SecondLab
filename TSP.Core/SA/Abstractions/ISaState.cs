using TSP.Domain;
using TSP.Domain.Abstractions;

namespace TSP.SA.Abstractions;

public interface ISaState : ISolverState
{
    Route? CurrentRoute { get; }
    RouteEvaluation? CurrentEvaluation { get; }
    double? CurrentTemperature { get; }
}
