using TSP.Domain.Abstractions;

namespace TSP.Domain;

public sealed class PenaltyRouteEvaluator : IRouteEvaluator
{
    private readonly IWeightedGraph _graph;
    private readonly double _missingEdgePenalty;

    public PenaltyRouteEvaluator(IWeightedGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        _graph = graph;
        _missingEdgePenalty = CalculateMissingEdgePenalty(graph);
    }

    public RouteEvaluation Evaluate(Route route)
    {
        ArgumentNullException.ThrowIfNull(route);

        if (route.Count != _graph.VertexCount)
        {
            throw new ArgumentException(
                "Маршрут должен содержать количество вершин, равное числу вершин графа.",
                nameof(route));
        }

        var cost = 0d;
        var isFeasible = true;

        for (var i = 0; i < route.Count; i++)
        {
            var from = route[i];
            var to = route[(i + 1) % route.Count];

            if (_graph.TryGetWeight(from, to, out var weight))
            {
                cost += weight;
                continue;
            }

            cost += _missingEdgePenalty;
            isFeasible = false;
        }

        return new RouteEvaluation(cost, isFeasible);
    }

    private static double CalculateMissingEdgePenalty(IWeightedGraph graph)
    {
        var hasAnyEdge = false;
        var maxWeight = 0d;

        for (var vertex = 0; vertex < graph.VertexCount; vertex++)
        {
            var neighbors = graph.GetNeighbors(vertex);

            for (var i = 0; i < neighbors.Count; i++)
            {
                var weight = neighbors[i].Weight;

                if (hasAnyEdge && !(weight > maxWeight)) continue;
                maxWeight = weight;
                hasAnyEdge = true;
            }
        }

        if (!hasAnyEdge)
            throw new InvalidOperationException("Граф не содержит ни одного ребра.");

        return (graph.VertexCount + 1) * maxWeight;
    }
}
