namespace TSP.Domain;

public record struct RouteEvaluation(
    double Cost,
    bool IsFeasible
);
