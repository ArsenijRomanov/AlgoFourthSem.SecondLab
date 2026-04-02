namespace TSP.SA.Results;

public readonly record struct InitialTemperatureEstimationResult(
    double Temperature,
    int ObjectiveEvaluations
);
