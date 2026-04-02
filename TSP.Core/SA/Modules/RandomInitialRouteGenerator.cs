using TSP.Domain;
using TSP.Domain.Abstractions;
using TSP.SA.Abstractions;

namespace TSP.SA.Modules;

public sealed class RandomInitialRouteGenerator(IRandomSource random) : IInitialRouteGenerator
{
    private readonly IRandomSource _random = random ?? throw new ArgumentNullException(nameof(random));

    public Route Create(IWeightedGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var vertices = new int[graph.VertexCount];
        for (var i = 0; i < vertices.Length; i++)
            vertices[i] = i;

        for (var i = vertices.Length - 1; i > 0; i--)
        {
            var j = _random.NextInt(0, i + 1);
            (vertices[i], vertices[j]) = (vertices[j], vertices[i]);
        }

        return new Route(vertices);
    }
}
