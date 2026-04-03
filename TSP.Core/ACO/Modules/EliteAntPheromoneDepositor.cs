using TSP.ACO.Abstractions;
using TSP.ACO.Contexts;
using TSP.ACO.Options;

namespace TSP.ACO.Modules;

public sealed class EliteAntPheromoneDepositor(
    IPheromoneDepositor innerDepositor,
    EliteAntOptions options)
    : IPheromoneDepositor
{
    private readonly IPheromoneDepositor _innerDepositor = innerDepositor
                                                           ?? throw new ArgumentNullException(nameof(innerDepositor));

    private readonly EliteAntOptions _options = options
                                                ?? throw new ArgumentNullException(nameof(options));

    public void Deposit(PheromoneDepositContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _innerDepositor.Deposit(context);

        if (context.GlobalBestRoute is null ||
            context.GlobalBestEvaluation is null ||
            !context.GlobalBestEvaluation.Value.IsFeasible ||
            context.GlobalBestEvaluation.Value.Cost <= 0)
        {
            return;
        }

        var eliteDelta = _options.EliteAntCount * context.Q / context.GlobalBestEvaluation.Value.Cost;
        var route = context.GlobalBestRoute;

        for (var vertexIndex = 0; vertexIndex < route.Count; vertexIndex++)
        {
            var from = route[vertexIndex];
            var to = route[(vertexIndex + 1) % route.Count];

            if (!context.Graph.HasEdge(from, to))
                continue;

            context.Pheromones[from, to] += eliteDelta;
        }
    }
}
