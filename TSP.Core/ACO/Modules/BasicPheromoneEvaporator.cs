using TSP.ACO.Abstractions;
using TSP.ACO.Contexts;

namespace TSP.ACO.Modules;

public sealed class BasicPheromoneEvaporator : IPheromoneEvaporator
{
    public void Evaporate(PheromoneEvaporationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var factor = 1d - context.EvaporationRate;

        for (var from = 0; from < context.Graph.VertexCount; from++)
        {
            for (var to = 0; to < context.Graph.VertexCount; to++)
            {
                if (!context.Graph.HasEdge(from, to))
                    continue;

                context.Pheromones[from, to] *= factor;
            }
        }
    }
}
