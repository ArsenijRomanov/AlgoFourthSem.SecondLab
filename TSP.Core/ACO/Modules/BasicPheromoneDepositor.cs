using TSP.ACO.Abstractions;
using TSP.ACO.Contexts;

namespace TSP.ACO.Modules;

public sealed class BasicPheromoneDepositor : IPheromoneDepositor
{
    public void Deposit(PheromoneDepositContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var routeResult in context.ColonyRoutes)
        {
            if (!routeResult.IsComplete ||
                routeResult.Route is null ||
                routeResult.Evaluation is null ||
                !routeResult.Evaluation.Value.IsFeasible ||
                routeResult.Evaluation.Value.Cost <= 0)
            {
                continue;
            }

            var delta = context.Q / routeResult.Evaluation.Value.Cost;
            var route = routeResult.Route;

            for (var vertexIndex = 0; vertexIndex < route.Count; vertexIndex++)
            {
                var from = route[vertexIndex];
                var to = route[(vertexIndex + 1) % route.Count];

                if (!context.Graph.HasEdge(from, to))
                    continue;

                context.Pheromones[from, to] += delta;
            }
        }
    }
}
