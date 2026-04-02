using TSP.SA.Abstractions;

namespace TSP.SA.Modules;

public sealed class CauchyCoolingSchedule : ICoolingSchedule
{
    public double GetNextTemperature(
        double currentTemperature,
        double initialTemperature,
        int iteration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentTemperature);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialTemperature);
        ArgumentOutOfRangeException.ThrowIfNegative(iteration);

        return initialTemperature / (1d + iteration);
    }
}
