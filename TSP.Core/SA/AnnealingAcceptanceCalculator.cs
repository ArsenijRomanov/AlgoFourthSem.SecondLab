namespace TSP.SA;

public static class AnnealingAcceptanceCalculator
{
    public static double CalculateAcceptanceProbability(
        double worseningDelta,
        double temperature)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(worseningDelta);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(temperature);

        return Math.Exp(-worseningDelta / temperature);
    }

    public static double CalculateTemperatureForTargetAcceptanceProbability(
        double worseningDelta,
        double targetAcceptanceProbability)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(worseningDelta);

        if (targetAcceptanceProbability is <= 0 or >= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetAcceptanceProbability),
                "Вероятность должна быть в интервале (0, 1).");
        }

        return -worseningDelta / Math.Log(targetAcceptanceProbability);
    }
}
