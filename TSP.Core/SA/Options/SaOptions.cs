namespace TSP.SA.Options;

public record SaOptions
{
    public int MaxIterations { get; }
    public double? InitialTemperature { get; }
    public int? MaxIterationsWithoutImprovement { get; }

    public SaOptions(
        int maxIterations = 1000, 
        double? initialTemperature = null, 
        int? maxIterationsWithoutImprovement = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxIterations);
        if (initialTemperature.HasValue)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialTemperature.Value);
        if (maxIterationsWithoutImprovement.HasValue)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxIterationsWithoutImprovement.Value);

        MaxIterations = maxIterations;
        InitialTemperature = initialTemperature;
        MaxIterationsWithoutImprovement = maxIterationsWithoutImprovement;
    }
}
