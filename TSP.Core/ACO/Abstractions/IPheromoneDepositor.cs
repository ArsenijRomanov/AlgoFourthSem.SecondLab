using TSP.ACO.Contexts;

namespace TSP.ACO.Abstractions;

public interface IPheromoneDepositor
{
    void Deposit(PheromoneDepositContext context);
}
