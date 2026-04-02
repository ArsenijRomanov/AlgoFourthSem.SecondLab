namespace TSP.Domain.Abstractions;

public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);

    double NextDouble();
}
