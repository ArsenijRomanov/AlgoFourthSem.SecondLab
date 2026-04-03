using TSP.Domain;

namespace TSP.ACO;

public sealed record AntRouteBuildResult(
    Route? Route,
    RouteEvaluation? Evaluation,
    bool IsComplete);
    