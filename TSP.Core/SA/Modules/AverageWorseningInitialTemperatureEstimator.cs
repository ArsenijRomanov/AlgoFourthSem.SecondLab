using TSP.Domain.Abstractions;
using TSP.SA.Abstractions;
using TSP.SA.Contexts;
using TSP.SA.Options;
using TSP.SA.Results;

namespace TSP.SA.Modules;

public sealed class AverageWorseningInitialTemperatureEstimator(
    IInitialRouteGenerator initialRouteGenerator,
    IRouteNeighborGenerator routeNeighborGenerator,
    IRouteEvaluator routeEvaluator,
    InitialTemperatureEstimatorOptions options)
    : IInitialTemperatureEstimator
{
    private readonly IInitialRouteGenerator _initialRouteGenerator = initialRouteGenerator
        ?? throw new ArgumentNullException(nameof(initialRouteGenerator));

    private readonly IRouteNeighborGenerator _routeNeighborGenerator = routeNeighborGenerator
        ?? throw new ArgumentNullException(nameof(routeNeighborGenerator));

    private readonly IRouteEvaluator _routeEvaluator = routeEvaluator
        ?? throw new ArgumentNullException(nameof(routeEvaluator));

    private readonly InitialTemperatureEstimatorOptions _options = options
        ?? throw new ArgumentNullException(nameof(options));

    public InitialTemperatureEstimationResult Estimate(IWeightedGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var objectiveEvaluations = 0;
        var worseningSum = 0d;
        var worseningCount = 0;

        for (var startIndex = 0; startIndex < _options.StartsCount; startIndex++)
        {
            var currentRoute = _initialRouteGenerator.Create(graph);
            var currentEvaluation = _routeEvaluator.Evaluate(currentRoute);
            objectiveEvaluations++;

            for (var stepIndex = 0; stepIndex < _options.ChainLength; stepIndex++)
            {
                var context = new RouteNeighborGeneratorContext(
                    currentRoute,
                    graph,
                    _options.FallbackTemperature,
                    stepIndex);

                var neighborRoute = _routeNeighborGenerator.GenerateNeighbor(context);
                var neighborEvaluation = _routeEvaluator.Evaluate(neighborRoute);
                objectiveEvaluations++;

                var delta = neighborEvaluation.Cost - currentEvaluation.Cost;
                if (delta > 0)
                {
                    worseningSum += delta;
                    worseningCount++;
                }

                currentRoute = neighborRoute;
                currentEvaluation = neighborEvaluation;
            }
        }

        if (worseningCount == 0)
        {
            return new InitialTemperatureEstimationResult(
                _options.FallbackTemperature,
                objectiveEvaluations);
        }

        var worseningDelta = worseningSum / worseningCount;
        if (worseningDelta <= 0 || !double.IsFinite(worseningDelta))
        {
            return new InitialTemperatureEstimationResult(
                _options.FallbackTemperature,
                objectiveEvaluations);
        }

        var temperature = AnnealingAcceptanceCalculator.CalculateTemperatureForTargetAcceptanceProbability(
            worseningDelta,
            _options.TargetAcceptanceProbability);

        if (temperature <= 0 || !double.IsFinite(temperature))
        {
            return new InitialTemperatureEstimationResult(
                _options.FallbackTemperature,
                objectiveEvaluations);
        }

        return new InitialTemperatureEstimationResult(
            temperature,
            objectiveEvaluations);
    }
}
