using TSP.ACO;
using TSP.ACO.Abstractions;
using TSP.ACO.Modules;
using TSP.ACO.Options;
using TSP.Domain;
using TSP.Domain.Abstractions;
using TSP.SA;
using TSP.SA.Abstractions;
using TSP.SA.Modules;
using TSP.SA.Options;

namespace TSP.Factory;

public static class TspSolverFactory
{
    public static SimulatedAnnealingSolver CreateSimulatedAnnealing(
        IWeightedGraph graph,
        SimulatedAnnealingAlgorithmConfig config)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(config);

        var random = CreateRandomSource(config.Seed);
        var routeEvaluator = new PenaltyRouteEvaluator(graph);

        var solverOptions = new SaOptions(
            maxIterations: config.MaxIterations,
            initialTemperature: config.InitialTemperature,
            maxIterationsWithoutImprovement: config.MaxIterationsWithoutImprovement);

        var estimatorOptions = new InitialTemperatureEstimatorOptions(
            startsCount: config.TemperatureEstimationStartsCount,
            chainLength: config.TemperatureEstimationChainLength,
            fallbackTemperature: config.TemperatureEstimationFallbackTemperature,
            targetAcceptanceProbability: config.TemperatureEstimationTargetAcceptanceProbability);

        var initialRouteGenerator = new RandomInitialRouteGenerator(random);
        var neighborGenerator = new TwoOptRouteNeighborGenerator(random);
        var initialTemperatureEstimator = new AverageWorseningInitialTemperatureEstimator(
            initialRouteGenerator,
            neighborGenerator,
            routeEvaluator,
            estimatorOptions);

        ICoolingSchedule coolingSchedule = config.CoolingKind switch
        {
            SimulatedAnnealingCoolingKind.Geometric => new GeometricCoolingSchedule(config.GeometricAlpha),
            SimulatedAnnealingCoolingKind.Cauchy => new CauchyCoolingSchedule(),
            _ => throw new ArgumentOutOfRangeException(nameof(config.CoolingKind))
        };

        return new SimulatedAnnealingSolver(
            solverOptions,
            graph,
            initialRouteGenerator,
            neighborGenerator,
            routeEvaluator,
            initialTemperatureEstimator,
            coolingSchedule,
            random);
    }

    public static AntColonySolver CreateAntColony(
        IWeightedGraph graph,
        AntColonyAlgorithmConfig config)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(config);

        var random = CreateRandomSource(config.Seed);
        var routeEvaluator = new PenaltyRouteEvaluator(graph);

        var solverOptions = new AcoOptions(
            maxIterations: config.MaxIterations,
            antCount: config.AntCount,
            alpha: config.Alpha,
            beta: config.Beta,
            evaporationRate: config.EvaporationRate,
            q: config.Q,
            initialPheromone: config.InitialPheromone,
            maxIterationsWithoutImprovement: config.MaxIterationsWithoutImprovement);

        var routeConstructionOptions = new AntRouteConstructionOptions(
            alpha: config.Alpha,
            beta: config.Beta);

        var pheromoneInitializer = new ConstantPheromoneInitializer(config.InitialPheromone);
        var startVertexSelector = new RandomAntStartVertexSelector(random);
        var nextVertexSelector = new ProbabilisticNextVertexSelector(random);
        var routeBuilder = new BasicAntRouteBuilder(routeEvaluator);
        var colonyBuilder = new BasicAntColonyBuilder(
            startVertexSelector,
            routeBuilder,
            nextVertexSelector,
            routeConstructionOptions);
        var evaporator = new BasicPheromoneEvaporator();

        IPheromoneDepositor depositor = new BasicPheromoneDepositor();

        if (config.UseEliteAnts)
        {
            var eliteOptions = new EliteAntOptions(config.EliteAntCount);
            depositor = new EliteAntPheromoneDepositor(depositor, eliteOptions);
        }

        return new AntColonySolver(
            solverOptions,
            graph,
            pheromoneInitializer,
            colonyBuilder,
            evaporator,
            depositor);
    }

    private static IRandomSource CreateRandomSource(int? seed)
        => seed.HasValue
            ? new SystemRandomSource(seed.Value)
            : new SystemRandomSource();
}
