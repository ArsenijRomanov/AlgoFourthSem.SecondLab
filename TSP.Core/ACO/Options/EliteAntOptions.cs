namespace TSP.ACO.Options;

public record EliteAntOptions
{
    public int EliteAntCount { get; }

    public EliteAntOptions(int eliteAntCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eliteAntCount);

        EliteAntCount = eliteAntCount;
    }
}
