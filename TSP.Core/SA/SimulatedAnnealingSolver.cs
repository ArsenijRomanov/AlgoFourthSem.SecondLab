using TSP.Domain;
using TSP.Domain.Abstractions;
using TSP.SA.Abstractions;
using TSP.SA.Contexts;
using TSP.SA.Options;

namespace TSP.SA;

public sealed class SimulatedAnnealingSolver(
    SaOptions options,
    IWeightedGraph graph,
    IInitialRouteGenerator initialRouteGenerator,
    IRouteNeighborGenerator routeNeighborGenerator,
    IRouteEvaluator routeEvaluator,
    IInitialTemperatureEstimator initialTemperatureEstimator,
    ICoolingSchedule coolingSchedule,
    IRandomSource random)
    : ITspSolver
{
    private readonly SaOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IWeightedGraph _graph = graph ?? throw new ArgumentNullException(nameof(graph));
    private readonly IInitialRouteGenerator _initialRouteGenerator = initialRouteGenerator
        ?? throw new ArgumentNullException(nameof(initialRouteGenerator));
    private readonly IRouteNeighborGenerator _routeNeighborGenerator = routeNeighborGenerator
        ?? throw new ArgumentNullException(nameof(routeNeighborGenerator));
    private readonly IRouteEvaluator _routeEvaluator = routeEvaluator
        ?? throw new ArgumentNullException(nameof(routeEvaluator));
    private readonly IInitialTemperatureEstimator _initialTemperatureEstimator = initialTemperatureEstimator
        ?? throw new ArgumentNullException(nameof(initialTemperatureEstimator));
    private readonly ICoolingSchedule _coolingSchedule = coolingSchedule
        ?? throw new ArgumentNullException(nameof(coolingSchedule));
    private readonly IRandomSource _random = random
        ?? throw new ArgumentNullException(nameof(random));

    private readonly SaState _state = new();

    private Route? _currentRoute;
    private RouteEvaluation _currentEvaluation;
    private double _initialTemperature;
    private double _currentTemperature;
    private int _iterationsWithoutImprovement;
    private bool _isInitialized;

    public ISolverState State => _state;
    public ISaState AnnealingState => _state;

    public void Initialize()
    {
        Reset();

        var initialRoute = _initialRouteGenerator.Create(_graph);
        var initialEvaluation = _routeEvaluator.Evaluate(initialRoute);

        _currentRoute = initialRoute;
        _currentEvaluation = initialEvaluation;
        _state.ObjectiveEvaluations++;

        if (_options.InitialTemperature.HasValue)
        {
            _initialTemperature = _options.InitialTemperature.Value;
        }
        else
        {
            var estimationResult = _initialTemperatureEstimator.Estimate(_graph);
            _initialTemperature = estimationResult.Temperature;
            _state.ObjectiveEvaluations += estimationResult.ObjectiveEvaluations;
        }

        _currentTemperature = _initialTemperature;

        _state.CurrentRoute = _currentRoute;
        _state.CurrentEvaluation = _currentEvaluation;
        _state.CurrentTemperature = _currentTemperature;

        if (initialEvaluation.IsFeasible)
        {
            _state.BestRoute = initialRoute;
            _state.BestEvaluation = initialEvaluation;
            _state.HasFeasibleSolution = true;
        }

        _isInitialized = true;
    }

    public void Step()
    {
        EnsureInitialized();

        var context = new RouteNeighborGeneratorContext(
            _currentRoute!,
            _graph,
            _state.IterationCount);

        var neighborRoute = _routeNeighborGenerator.GenerateNeighbor(context);
        var neighborEvaluation = _routeEvaluator.Evaluate(neighborRoute);

        _state.ObjectiveEvaluations++;

        var delta = neighborEvaluation.Cost - _currentEvaluation.Cost;
        if (ShouldAccept(delta, _currentTemperature))
        {
            _currentRoute = neighborRoute;
            _currentEvaluation = neighborEvaluation;
        }

        var hasImprovedBest = TryUpdateBest(_currentRoute!, _currentEvaluation);
        if (hasImprovedBest)
            _iterationsWithoutImprovement = 0;
        else
            _iterationsWithoutImprovement++;

        _state.IterationCount++;

        _currentTemperature = _coolingSchedule.GetNextTemperature(
            _currentTemperature,
            _initialTemperature,
            _state.IterationCount);

        _state.CurrentRoute = _currentRoute;
        _state.CurrentEvaluation = _currentEvaluation;
        _state.CurrentTemperature = _currentTemperature;
    }

    public void Step(int steps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(steps);

        for (var i = 0; i < steps; i++)
            Step();
    }

    public SolverResult Run()
    {
        EnsureInitialized();

        while (!ShouldStop())
            Step();

        return new SolverResult(
            _state.BestRoute,
            _state.BestEvaluation,
            _state.HasFeasibleSolution,
            _state.IterationCount,
            _state.ObjectiveEvaluations);
    }

    public void Reset()
    {
        _currentRoute = null;
        _currentEvaluation = default;
        _initialTemperature = 0d;
        _currentTemperature = 0d;
        _iterationsWithoutImprovement = 0;
        _isInitialized = false;

        _state.CurrentRoute = null;
        _state.CurrentEvaluation = null;
        _state.CurrentTemperature = null;

        _state.BestRoute = null;
        _state.BestEvaluation = null;
        _state.HasFeasibleSolution = false;
        _state.IterationCount = 0;
        _state.ObjectiveEvaluations = 0;
    }

    private bool ShouldStop()
    {
        if (_state.IterationCount >= _options.MaxIterations)
            return true;

        if (_options.MaxIterationsWithoutImprovement.HasValue &&
            _iterationsWithoutImprovement >= _options.MaxIterationsWithoutImprovement.Value)
        {
            return true;
        }

        return false;
    }

    private bool ShouldAccept(double delta, double temperature)
    {
        if (delta <= 0)
            return true;

        var acceptanceProbability = AnnealingAcceptanceCalculator.CalculateAcceptanceProbability(
            delta,
            temperature);

        return _random.NextDouble() < acceptanceProbability;
    }

    private bool TryUpdateBest(Route route, RouteEvaluation evaluation)
    {
        if (!evaluation.IsFeasible)
            return false;

        if (_state is { HasFeasibleSolution: true, BestEvaluation: not null } &&
            !(evaluation.Cost < _state.BestEvaluation.Value.Cost))
        {
            return false;
        }

        _state.BestRoute = route;
        _state.BestEvaluation = evaluation;
        _state.HasFeasibleSolution = true;

        return true;
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Решатель не инициализирован. Сначала вызовите Initialize().");
    }
}
