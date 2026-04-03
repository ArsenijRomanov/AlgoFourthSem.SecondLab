namespace TSP.ACO.Options;

public record AcoOptions
{
    public int MaxIterations { get; }
    public int AntCount { get; }
    public double Alpha { get; }
    public double Beta { get; }
    public double EvaporationRate { get; }
    public double Q { get; }
    public double InitialPheromone { get; }
    public int? MaxIterationsWithoutImprovement { get; }

    public AcoOptions(
        int maxIterations,
        int antCount,
        double alpha,
        double beta,
        double evaporationRate,
        double q,
        double initialPheromone,
        int? maxIterationsWithoutImprovement = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxIterations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(antCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alpha);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(beta);

        if (evaporationRate is <= 0 or >= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(evaporationRate),
                "Коэффициент испарения должен быть в интервале (0, 1).");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(q);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialPheromone);

        if (maxIterationsWithoutImprovement.HasValue)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxIterationsWithoutImprovement.Value);

        MaxIterations = maxIterations;
        AntCount = antCount;
        Alpha = alpha;
        Beta = beta;
        EvaporationRate = evaporationRate;
        Q = q;
        InitialPheromone = initialPheromone;
        MaxIterationsWithoutImprovement = maxIterationsWithoutImprovement;
    }
}
