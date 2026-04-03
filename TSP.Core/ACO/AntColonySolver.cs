using TSP.ACO.Abstractions;
using TSP.ACO.Contexts;
using TSP.ACO.Options;
using TSP.Domain;
using TSP.Domain.Abstractions;

namespace TSP.ACO;

public sealed class AntColonySolver(
    AcoOptions options,
    IWeightedGraph graph,
    IPheromoneInitializer pheromoneInitializer,
    IAntColonyBuilder colonyBuilder,
    IPheromoneEvaporator pheromoneEvaporator,
    IPheromoneDepositor pheromoneDepositor)
    : ITspSolver
{
    private readonly AcoOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IWeightedGraph _graph = graph ?? throw new ArgumentNullException(nameof(graph));
    private readonly IPheromoneInitializer _pheromoneInitializer = pheromoneInitializer
        ?? throw new ArgumentNullException(nameof(pheromoneInitializer));
    private readonly IAntColonyBuilder _colonyBuilder = colonyBuilder
        ?? throw new ArgumentNullException(nameof(colonyBuilder));
    private readonly IPheromoneEvaporator _pheromoneEvaporator = pheromoneEvaporator
        ?? throw new ArgumentNullException(nameof(pheromoneEvaporator));
    private readonly IPheromoneDepositor _pheromoneDepositor = pheromoneDepositor
        ?? throw new ArgumentNullException(nameof(pheromoneDepositor));

    private readonly AcoState _state = new();

    private double[,]? _pheromones;
    private int _iterationsWithoutImprovement;
    private bool _isInitialized;

    public ISolverState State => _state;
    public IAcoState ColonyState => _state;

    public void Initialize()
    {
        Reset();

        _pheromones = _pheromoneInitializer.CreateInitialMatrix(_graph);
        _state.Pheromones = _pheromones;

        _isInitialized = true;
    }

    public void Step()
    {
        EnsureInitialized();

        if (ShouldStop())
            return;

        var colonyContext = new AntColonyBuildContext(
            _graph,
            _pheromones!,
            _options.AntCount,
            _state.IterationCount);

        var colonyRoutes = _colonyBuilder.BuildColonyRoutes(colonyContext);

        _state.LastBuiltRoutes = colonyRoutes;

        var completedRoutesCount = 0;
        var hasImprovedBest = false;

        foreach (var colonyRoute in colonyRoutes)
        {
            if (!colonyRoute.IsComplete || colonyRoute.Route is null || colonyRoute.Evaluation is null)
                continue;

            completedRoutesCount++;

            if (TryUpdateBest(colonyRoute.Route, colonyRoute.Evaluation.Value))
                hasImprovedBest = true;
        }

        _state.ObjectiveEvaluations += completedRoutesCount;

        if (hasImprovedBest)
            _iterationsWithoutImprovement = 0;
        else
            _iterationsWithoutImprovement++;

        var evaporationContext = new PheromoneEvaporationContext(
            _graph,
            _pheromones!,
            _options.EvaporationRate);

        _pheromoneEvaporator.Evaporate(evaporationContext);

        var depositContext = new PheromoneDepositContext(
            _graph,
            _pheromones!,
            colonyRoutes,
            _state.BestRoute,
            _state.BestEvaluation,
            _options.Q);

        _pheromoneDepositor.Deposit(depositContext);

        _state.Pheromones = _pheromones;
        _state.IterationCount++;
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
        _pheromones = null;
        _iterationsWithoutImprovement = 0;
        _isInitialized = false;

        _state.BestRoute = null;
        _state.BestEvaluation = null;
        _state.HasFeasibleSolution = false;

        _state.IterationCount = 0;
        _state.ObjectiveEvaluations = 0;

        _state.LastBuiltRoutes = [];
        _state.Pheromones = null;
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
            throw new InvalidOperationException(
                "Решатель не инициализирован. Сначала вызовите Initialize().");
    }
}
