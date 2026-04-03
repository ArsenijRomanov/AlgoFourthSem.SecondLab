namespace TSP.SA.Options;

public record InitialTemperatureEstimatorOptions
{
    public int StartsCount { get; }
    public int ChainLength { get; }
    public double FallbackTemperature { get; }
    public double TargetAcceptanceProbability { get; }

    public InitialTemperatureEstimatorOptions(
        int startsCount, 
        int chainLength, 
        double fallbackTemperature,
        double targetAcceptanceProbability)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(startsCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chainLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fallbackTemperature);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetAcceptanceProbability);
        if (targetAcceptanceProbability is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(
                nameof(targetAcceptanceProbability),
                "Целевая вероятность принятия решения должна быть от 0 до 1");

        StartsCount = startsCount;
        ChainLength = chainLength;
        FallbackTemperature = fallbackTemperature;
        TargetAcceptanceProbability = targetAcceptanceProbability;
    }
}
