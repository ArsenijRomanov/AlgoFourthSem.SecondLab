namespace TSP.Factory;

public record AntColonyAlgorithmConfig
{
    public int? Seed { get; init; }

    public int MaxIterations { get; init; } = 1000;
    public int? MaxIterationsWithoutImprovement { get; init; }

    public int AntCount { get; init; } = 30;
    public double Alpha { get; init; } = 1.0;
    public double Beta { get; init; } = 3.0;
    public double EvaporationRate { get; init; } = 0.5;
    public double Q { get; init; } = 100.0;
    public double InitialPheromone { get; init; } = 1.0;

    public bool UseEliteAnts { get; init; }
    public int EliteAntCount { get; init; } = 5;
}
