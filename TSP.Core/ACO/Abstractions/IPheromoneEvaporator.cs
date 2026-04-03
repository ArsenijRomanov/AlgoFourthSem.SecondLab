using TSP.ACO.Contexts;

namespace TSP.ACO.Abstractions;

public interface IPheromoneEvaporator
{
    void Evaporate(PheromoneEvaporationContext context);
}
