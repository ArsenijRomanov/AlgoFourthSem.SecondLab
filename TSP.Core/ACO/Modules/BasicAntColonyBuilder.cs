using TSP.ACO.Abstractions;
using TSP.ACO.Contexts;
using TSP.ACO.Options;

namespace TSP.ACO.Modules;

public sealed class BasicAntColonyBuilder(
    IAntStartVertexSelector startVertexSelector,
    IAntRouteBuilder routeBuilder,
    INextVertexSelector nextVertexSelector,
    AntRouteConstructionOptions routeConstructionOptions)
    : IAntColonyBuilder
{
    private readonly IAntStartVertexSelector _startVertexSelector = startVertexSelector
                                                                    ?? throw new ArgumentNullException(nameof(startVertexSelector));

    private readonly IAntRouteBuilder _routeBuilder = routeBuilder
                                                      ?? throw new ArgumentNullException(nameof(routeBuilder));

    private readonly INextVertexSelector _nextVertexSelector = nextVertexSelector
                                                               ?? throw new ArgumentNullException(nameof(nextVertexSelector));

    private readonly AntRouteConstructionOptions _routeConstructionOptions = routeConstructionOptions
                                                                             ?? throw new ArgumentNullException(nameof(routeConstructionOptions));

    public IReadOnlyList<AntRouteBuildResult> BuildColonyRoutes(AntColonyBuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var results = new List<AntRouteBuildResult>(context.AntCount);

        for (var antIndex = 0; antIndex < context.AntCount; antIndex++)
        {
            var startVertex = _startVertexSelector.SelectStartVertex(
                context.Graph,
                antIndex,
                context.Iteration);

            var routeBuilderContext = new AntRouteBuilderContext(
                context.Graph,
                context.Pheromones,
                startVertex,
                _routeConstructionOptions.Alpha,
                _routeConstructionOptions.Beta,
                _nextVertexSelector);

            var result = _routeBuilder.BuildRoute(routeBuilderContext);
            results.Add(result);
        }

        return results;
    }
}
