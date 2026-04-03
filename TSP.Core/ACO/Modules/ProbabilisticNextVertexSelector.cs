using TSP.ACO.Abstractions;
using TSP.ACO.Contexts;
using TSP.Domain.Abstractions;

namespace TSP.ACO.Modules;

public sealed class ProbabilisticNextVertexSelector(IRandomSource random) : INextVertexSelector
{
    private readonly IRandomSource _random = random ?? throw new ArgumentNullException(nameof(random));

    public int? SelectNextVertex(AntNextVertexSelectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var candidates = new List<(int Vertex, double Attractiveness)>();

        foreach (var (vertex, weight) in context.Graph.GetNeighbors(context.CurrentVertex))
        {
            if (context.VisitedVertices.Contains(vertex))
                continue;

            var pheromone = context.Pheromones[context.CurrentVertex, vertex];
            if (pheromone <= 0 || weight <= 0)
                continue;

            var heuristic = 1d / weight;
            var attractiveness =
                Math.Pow(pheromone, context.Alpha) *
                Math.Pow(heuristic, context.Beta);

            if (attractiveness > 0 && double.IsFinite(attractiveness))
                candidates.Add((vertex, attractiveness));
        }

        if (candidates.Count == 0)
            return null;

        var attractivenessSum = candidates.Sum(candidate => candidate.Attractiveness);

        if (attractivenessSum <= 0 || !double.IsFinite(attractivenessSum))
            return candidates[_random.NextInt(0, candidates.Count)].Vertex;

        var threshold = _random.NextDouble() * attractivenessSum;
        var cumulative = 0d;

        foreach (var candidate in candidates)
        {
            cumulative += candidate.Attractiveness;
            if (threshold <= cumulative)
                return candidate.Vertex;
        }

        return candidates[^1].Vertex;
    }
}
