using TSP.Domain.Abstractions;
using TSP.SA.Results;

namespace TSP.SA.Abstractions;

public interface IInitialTemperatureEstimator
{
    InitialTemperatureEstimationResult Estimate(IWeightedGraph graph);
}
