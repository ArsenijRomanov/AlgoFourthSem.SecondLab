using TSP.SA.Abstractions;

namespace TSP.SA.Modules;

public sealed class GeometricCoolingSchedule : ICoolingSchedule
{
    private readonly double _alpha;

    public GeometricCoolingSchedule(double alpha)
    {
        if (alpha is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(alpha), "Коэффициент охлаждения должен быть в интервале (0, 1).");

        _alpha = alpha;
    }

    public double GetNextTemperature(
        double currentTemperature,
        double initialTemperature,
        int iteration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentTemperature);
        ArgumentOutOfRangeException.ThrowIfNegative(initialTemperature);
        ArgumentOutOfRangeException.ThrowIfNegative(iteration);

        return currentTemperature * _alpha;
    }
}
