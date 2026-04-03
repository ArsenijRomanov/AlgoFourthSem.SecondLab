namespace TSP.Factory;

public record SimulatedAnnealingAlgorithmConfig
{
    public int? Seed { get; init; }

    public int MaxIterations { get; init; } = 1000;
    public int? MaxIterationsWithoutImprovement { get; init; }

    public double? InitialTemperature { get; init; }

    public int TemperatureEstimationStartsCount { get; init; } = 10;
    public int TemperatureEstimationChainLength { get; init; } = 20;
    public double TemperatureEstimationFallbackTemperature { get; init; } = 1.0;
    public double TemperatureEstimationTargetAcceptanceProbability { get; init; } = 0.8;

    public SimulatedAnnealingCoolingKind CoolingKind { get; init; } = SimulatedAnnealingCoolingKind.Geometric;
    public double GeometricAlpha { get; init; } = 0.95;
}
