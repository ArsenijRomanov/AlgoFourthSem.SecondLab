using TSP.ACO.Abstractions;
using TSP.ACO.Contexts;
using TSP.ACO.Options;
using TSP.Domain;
using TSP.Domain.Abstractions;

namespace TSP.ACO.Modules;

public sealed class BasicAntRouteBuilder(IRouteEvaluator routeEvaluator) : IAntRouteBuilder
{
    private readonly IRouteEvaluator _routeEvaluator = routeEvaluator
                                                       ?? throw new ArgumentNullException(nameof(routeEvaluator));

    public AntRouteBuildResult BuildRoute(AntRouteBuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var routeVertices = new List<int>(context.Graph.VertexCount);
        var visitedVertices = new HashSet<int>();

        routeVertices.Add(context.StartVertex);
        visitedVertices.Add(context.StartVertex);

        var currentVertex = context.StartVertex;

        while (routeVertices.Count < context.Graph.VertexCount)
        {
            var selectionContext = new AntNextVertexSelectionContext(
                context.Graph,
                context.Pheromones,
                visitedVertices,
                currentVertex,
                context.StartVertex,
                context.Alpha,
                context.Beta);

            var nextVertex = context.NextVertexSelector.SelectNextVertex(selectionContext);
            if (!nextVertex.HasValue)
                return new AntRouteBuildResult(null, null, false);

            if (visitedVertices.Contains(nextVertex.Value))
            {
                throw new InvalidOperationException(
                    $"Выбрана уже посещенная вершина {nextVertex.Value} при построении маршрута муравья.");
            }

            routeVertices.Add(nextVertex.Value);
            visitedVertices.Add(nextVertex.Value);
            currentVertex = nextVertex.Value;
        }

        var route = new Route(routeVertices.ToArray());
        var evaluation = _routeEvaluator.Evaluate(route);

        return new AntRouteBuildResult(route, evaluation, true);
    }
}
