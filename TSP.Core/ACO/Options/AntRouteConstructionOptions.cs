namespace TSP.ACO.Options;

public record AntRouteConstructionOptions
{
    public double Alpha { get; }
    public double Beta { get; }

    public AntRouteConstructionOptions(double alpha, double beta)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alpha);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(beta);

        Alpha = alpha;
        Beta = beta;
    }
}
