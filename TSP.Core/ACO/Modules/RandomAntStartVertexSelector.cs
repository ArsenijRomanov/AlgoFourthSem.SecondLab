using TSP.ACO.Abstractions;
using TSP.Domain.Abstractions;

namespace TSP.ACO.Modules;

public sealed class RandomAntStartVertexSelector(IRandomSource random) : IAntStartVertexSelector
{
    private readonly IRandomSource _random = random ?? throw new ArgumentNullException(nameof(random));

    public int SelectStartVertex(IWeightedGraph graph, int antIndex, int iteration)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentOutOfRangeException.ThrowIfNegative(antIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(iteration);

        return _random.NextInt(0, graph.VertexCount);
    }
}