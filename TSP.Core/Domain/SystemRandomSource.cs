using TSP.Domain.Abstractions;

namespace TSP.Domain;

public sealed class SystemRandomSource(Random random) : IRandomSource
{
    private readonly Random _random = random ?? throw new ArgumentNullException(nameof(random));

    public SystemRandomSource()
        : this(new Random()) { }

    public SystemRandomSource(int seed)
        : this(new Random(seed)) { }

    public int NextInt(int minInclusive, int maxExclusive)
        => _random.Next(minInclusive, maxExclusive);

    public double NextDouble()
        => _random.NextDouble();
}
