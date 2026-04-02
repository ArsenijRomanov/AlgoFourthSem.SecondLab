using TSP.Domain;
using TSP.Domain.Abstractions;
using TSP.SA.Abstractions;
using TSP.SA.Contexts;

namespace TSP.SA.Modules;

public sealed class TwoOptRouteNeighborGenerator(IRandomSource random) : IRouteNeighborGenerator
{
    private readonly IRandomSource _random = random ?? throw new ArgumentNullException(nameof(random));

    public Route GenerateNeighbor(RouteNeighborGeneratorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.CurrentRoute.Count < 2)
            return new Route(context.CurrentRoute.ToArray());

        var vertices = context.CurrentRoute.ToArray();

        var firstIndex = _random.NextInt(0, vertices.Length);
        var secondIndex = _random.NextInt(0, vertices.Length - 1);

        if (secondIndex >= firstIndex)
            secondIndex++;

        if (firstIndex > secondIndex)
            (firstIndex, secondIndex) = (secondIndex, firstIndex);

        Array.Reverse(vertices, firstIndex, secondIndex - firstIndex + 1);

        return new Route(vertices);
    }
}
