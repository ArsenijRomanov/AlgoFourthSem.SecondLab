using TSP.ACO;
using TSP.ACO.Abstractions;
using TSP.Avalonia.Models;
using TSP.Domain;
using TSP.Domain.Abstractions;
using TSP.Factory;
using TSP.SA;
using TSP.SA.Abstractions;

namespace TSP.Avalonia.Services;

public sealed class TspExperimentSession
{
    private ITspSolver? _solver;
    private double? _lastBestCost;
    private int _iterationsWithoutImprovement;
    private int _maxIterations;
    private int? _maxIterationsWithoutImprovement;

    public void Initialize(LoadedGraph graph, SimulatedAnnealingAlgorithmConfig config)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(config);

        _solver = TspSolverFactory.CreateSimulatedAnnealing(graph.Graph, config);
        _maxIterations = config.MaxIterations;
        _maxIterationsWithoutImprovement = config.MaxIterationsWithoutImprovement;
        ResetCounters();
        _solver.Initialize();
        CaptureBestAfterInitialize();
    }

    public void Initialize(LoadedGraph graph, AntColonyAlgorithmConfig config)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(config);

        _solver = TspSolverFactory.CreateAntColony(graph.Graph, config);
        _maxIterations = config.MaxIterations;
        _maxIterationsWithoutImprovement = config.MaxIterationsWithoutImprovement;
        ResetCounters();
        _solver.Initialize();
        CaptureBestAfterInitialize();
    }

    public IterationSnapshot GetSnapshot()
    {
        if (_solver is null)
        {
            return new IterationSnapshot(0, 0, false, null, null, null, null, [], null, 0, 0, null);
        }

        var state = _solver.State;
        if (state is ISaState saState)
        {
            return new IterationSnapshot(
                saState.IterationCount,
                saState.ObjectiveEvaluations,
                saState.HasFeasibleSolution,
                saState.BestRoute,
                saState.BestEvaluation,
                saState.CurrentRoute,
                saState.CurrentEvaluation,
                [],
                null,
                0,
                0,
                null);
        }

        if (state is IAcoState acoState)
        {
            var successfulAnts = acoState.LastBuiltRoutes.Count(static route => route.Route is not null);
            var completeAnts = acoState.LastBuiltRoutes.Count(static route => route.IsComplete);
            var iterationBestCost = acoState.LastBuiltRoutes
                .Where(static route => route.Evaluation.HasValue)
                .Select(static route => (double?)route.Evaluation!.Value.Cost)
                .OrderBy(static value => value)
                .FirstOrDefault();

            return new IterationSnapshot(
                acoState.IterationCount,
                acoState.ObjectiveEvaluations,
                acoState.HasFeasibleSolution,
                acoState.BestRoute,
                acoState.BestEvaluation,
                null,
                null,
                acoState.LastBuiltRoutes,
                acoState.Pheromones,
                successfulAnts,
                completeAnts,
                iterationBestCost);
        }

        return new IterationSnapshot(
            state.IterationCount,
            state.ObjectiveEvaluations,
            state.HasFeasibleSolution,
            state.BestRoute,
            state.BestEvaluation,
            null,
            null,
            [],
            null,
            0,
            0,
            null);
    }

    public IReadOnlyList<IterationSnapshot> Step(int steps)
    {
        if (_solver is null)
        {
            throw new InvalidOperationException("Сначала загрузите граф и инициализируйте алгоритм.");
        }

        var snapshots = new List<IterationSnapshot>();
        for (var index = 0; index < steps && ShouldContinue(); index++)
        {
            _solver.Step();
            UpdateImprovementCounters();
            snapshots.Add(GetSnapshot());
        }

        return snapshots;
    }

    public IReadOnlyList<IterationSnapshot> RunToCompletion()
    {
        if (_solver is null)
        {
            throw new InvalidOperationException("Сначала загрузите граф и инициализируйте алгоритм.");
        }

        var snapshots = new List<IterationSnapshot>();
        while (ShouldContinue())
        {
            _solver.Step();
            UpdateImprovementCounters();
            snapshots.Add(GetSnapshot());
        }

        return snapshots;
    }

    public void Reset()
    {
        _solver?.Reset();
        _solver = null;
        ResetCounters();
    }

    private bool ShouldContinue()
    {
        if (_solver is null)
        {
            return false;
        }

        if (_solver.State.IterationCount >= _maxIterations)
        {
            return false;
        }

        if (_maxIterationsWithoutImprovement.HasValue && _iterationsWithoutImprovement >= _maxIterationsWithoutImprovement.Value)
        {
            return false;
        }

        return true;
    }

    private void ResetCounters()
    {
        _lastBestCost = null;
        _iterationsWithoutImprovement = 0;
    }

    private void CaptureBestAfterInitialize()
    {
        var bestEvaluation = _solver?.State.BestEvaluation;
        _lastBestCost = bestEvaluation?.Cost;
        _iterationsWithoutImprovement = 0;
    }

    private void UpdateImprovementCounters()
    {
        var bestCost = _solver?.State.BestEvaluation?.Cost;
        if (!bestCost.HasValue)
        {
            _iterationsWithoutImprovement++;
            return;
        }

        if (!_lastBestCost.HasValue || bestCost.Value < _lastBestCost.Value - 1e-9)
        {
            _lastBestCost = bestCost.Value;
            _iterationsWithoutImprovement = 0;
            return;
        }

        _iterationsWithoutImprovement++;
    }
}
