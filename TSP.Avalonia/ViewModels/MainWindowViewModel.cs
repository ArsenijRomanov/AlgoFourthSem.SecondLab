using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using TSP.ACO;
using TSP.Avalonia.Infrastructure;
using TSP.Avalonia.Models;
using TSP.Avalonia.Services;
using TSP.Domain;
using TSP.Factory;

namespace TSP.Avalonia.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
        : this(new TspExperimentSession(), new StpGraphParser(), new GraphLayoutService(), new GraphExampleCatalog(), new RouteExportService())
    {
    }

    private readonly TspExperimentSession _session;
    private readonly StpGraphParser _parser;
    private readonly GraphLayoutService _layoutService;
    private readonly GraphExampleCatalog _examples;
    private readonly RouteExportService _routeExporter;

    private LoadedGraph? _loadedGraph;
    private NamedOption<SolverFamily> _selectedAlgorithm;
    private NamedOption<string>? _selectedExample;
    private NamedOption<SimulatedAnnealingCoolingKind> _selectedCoolingKind;
    private NamedOption<AntOverlayMode> _selectedAntOverlay;
    private bool _useAutomaticInitialTemperature = true;
    private double? _manualInitialTemperature = 25;
    private double _targetAcceptanceProbability = 0.8;
    private double _geometricAlpha = 0.95;
    private int _antCount = 30;
    private bool _useEliteAnts;
    private double _alpha = 1.0;
    private double _beta = 3.0;
    private double _evaporationRate = 0.5;
    private double _q = 100;
    private double _initialPheromone = 1.0;
    private int _eliteAntCount = 5;
    private int _maxIterations = 1000;
    private bool _useMaxIterationsWithoutImprovement = true;
    private int _maxIterationsWithoutImprovement = 200;
    private int _stepBatchSize = 10;
    private bool _showWeights;
    private bool _showVertexLabels = true;
    private bool _showDirections = true;
    private bool _showBestRoutePinned = true;
    private bool _showBestRoutePeek;
    private bool _isBusy;
    private bool _isInitialized;
    private string _statusMessage = "";
    private string _bestCostText = "—";
    private string _bestRouteText = "—";
    private string _currentRouteCostText = "—";
    private string _iterationBestCostText = "—";
    private string _feasibleText = "Не найден";
    private int _iterationCount;
    private int _objectiveEvaluations;
    private int _successfulAnts;
    private int _completeAnts;
    private Route? _bestRoute;
    private Route? _currentRoute;
    private Route? _selectedAntRoute;
    private double[,]? _pheromones;
    private IReadOnlyList<AntRouteBuildResult> _lastBuiltRoutes = [];
    private AntRouteRowViewModel? _selectedAntRouteRow;

    public MainWindowViewModel(
        TspExperimentSession session,
        StpGraphParser parser,
        GraphLayoutService layoutService,
        GraphExampleCatalog examples,
        RouteExportService routeExporter)
    {
        _session = session;
        _parser = parser;
        _layoutService = layoutService;
        _examples = examples;
        _routeExporter = routeExporter;

        AlgorithmOptions =
        [
            new NamedOption<SolverFamily>("Имитация отжига", SolverFamily.SimulatedAnnealing),
            new NamedOption<SolverFamily>("Муравьиный алгоритм", SolverFamily.AntColony)
        ];

        CoolingKindOptions =
        [
            new NamedOption<SimulatedAnnealingCoolingKind>("База: геометрическое охлаждение", SimulatedAnnealingCoolingKind.Geometric),
            new NamedOption<SimulatedAnnealingCoolingKind>("Оптимизация: отжиг Коши", SimulatedAnnealingCoolingKind.Cauchy)
        ];

        AntOverlayOptions =
        [
            new NamedOption<AntOverlayMode>("Текущие пути колонии", AntOverlayMode.ColonyRoutes),
            new NamedOption<AntOverlayMode>("Феромоны", AntOverlayMode.Pheromones),
            new NamedOption<AntOverlayMode>("Без наложения", AntOverlayMode.None)
        ];

        ExampleOptions = _examples.GetExampleOptions();
        _selectedAlgorithm = AlgorithmOptions[0];
        _selectedCoolingKind = CoolingKindOptions[0];
        _selectedAntOverlay = AntOverlayOptions[0];
        _selectedExample = ExampleOptions[0];

        InitializeCommand = new RelayCommand(InitializeSolver, CanInitializeSolver);
        StepCommand = new RelayCommand(() => ExecuteSteps(Math.Max(1, StepBatchSize)), CanStep);
        RunCommand = new AsyncRelayCommand(RunAsync, CanStep);
        ResetCommand = new RelayCommand(ResetSession, () => !IsBusy);
        LoadExampleCommand = new AsyncRelayCommand(LoadSelectedExampleAsync, () => !IsBusy && SelectedExample is not null);
        ResetAdvancedSettingsCommand = new RelayCommand(ResetAdvancedSettings, () => !IsBusy);
    }

    public IReadOnlyList<NamedOption<SolverFamily>> AlgorithmOptions { get; }

    public IReadOnlyList<NamedOption<SimulatedAnnealingCoolingKind>> CoolingKindOptions { get; }

    public IReadOnlyList<NamedOption<AntOverlayMode>> AntOverlayOptions { get; }

    public IReadOnlyList<NamedOption<string>> ExampleOptions { get; }

    public ObservableCollection<HistoryPoint> History { get; } = [];

    public ObservableCollection<AntRouteRowViewModel> AntRouteRows { get; } = [];

    public ICommand InitializeCommand { get; }

    public ICommand StepCommand { get; }

    public ICommand RunCommand { get; }

    public ICommand ResetCommand { get; }

    public ICommand LoadExampleCommand { get; }

    public ICommand ResetAdvancedSettingsCommand { get; }

    public NamedOption<SolverFamily> SelectedAlgorithm
    {
        get => _selectedAlgorithm;
        set
        {
            if (!SetProperty(ref _selectedAlgorithm, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsSimulatedAnnealing));
            RaisePropertyChanged(nameof(IsAntColony));
            RaisePropertyChanged(nameof(AlgorithmTitle));
            RaisePropertyChanged(nameof(ShouldShowBestRoute));
            RaiseCommandStates();
        }
    }

    public NamedOption<string>? SelectedExample
    {
        get => _selectedExample;
        set
        {
            if (!SetProperty(ref _selectedExample, value))
            {
                return;
            }

            RaiseCommandStates();
        }
    }

    public NamedOption<SimulatedAnnealingCoolingKind> SelectedCoolingKind
    {
        get => _selectedCoolingKind;
        set => SetProperty(ref _selectedCoolingKind, value);
    }

    public NamedOption<AntOverlayMode> SelectedAntOverlay
    {
        get => _selectedAntOverlay;
        set => SetProperty(ref _selectedAntOverlay, value);
    }

    public bool UseAutomaticInitialTemperature
    {
        get => _useAutomaticInitialTemperature;
        set
        {
            if (!SetProperty(ref _useAutomaticInitialTemperature, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsManualInitialTemperature));
        }
    }

    public bool IsManualInitialTemperature => !UseAutomaticInitialTemperature;

    public double? ManualInitialTemperature
    {
        get => _manualInitialTemperature;
        set => SetProperty(ref _manualInitialTemperature, value);
    }

    public double TargetAcceptanceProbability
    {
        get => _targetAcceptanceProbability;
        set => SetProperty(ref _targetAcceptanceProbability, value);
    }

    public double GeometricAlpha
    {
        get => _geometricAlpha;
        set => SetProperty(ref _geometricAlpha, value);
    }

    public int AntCount
    {
        get => _antCount;
        set => SetProperty(ref _antCount, value);
    }

    public bool UseEliteAnts
    {
        get => _useEliteAnts;
        set => SetProperty(ref _useEliteAnts, value);
    }

    public double Alpha
    {
        get => _alpha;
        set => SetProperty(ref _alpha, value);
    }

    public double Beta
    {
        get => _beta;
        set => SetProperty(ref _beta, value);
    }

    public double EvaporationRate
    {
        get => _evaporationRate;
        set => SetProperty(ref _evaporationRate, value);
    }

    public double Q
    {
        get => _q;
        set => SetProperty(ref _q, value);
    }

    public double InitialPheromone
    {
        get => _initialPheromone;
        set => SetProperty(ref _initialPheromone, value);
    }

    public int EliteAntCount
    {
        get => _eliteAntCount;
        set => SetProperty(ref _eliteAntCount, value);
    }

    public int MaxIterations
    {
        get => _maxIterations;
        set => SetProperty(ref _maxIterations, value);
    }

    public bool UseMaxIterationsWithoutImprovement
    {
        get => _useMaxIterationsWithoutImprovement;
        set => SetProperty(ref _useMaxIterationsWithoutImprovement, value);
    }

    public int MaxIterationsWithoutImprovement
    {
        get => _maxIterationsWithoutImprovement;
        set => SetProperty(ref _maxIterationsWithoutImprovement, value);
    }


    public int StepBatchSize
    {
        get => _stepBatchSize;
        set => SetProperty(ref _stepBatchSize, value);
    }

    public bool ShowWeights
    {
        get => _showWeights;
        set => SetProperty(ref _showWeights, value);
    }

    public bool ShowVertexLabels
    {
        get => _showVertexLabels;
        set => SetProperty(ref _showVertexLabels, value);
    }

    public bool ShowDirections
    {
        get => _showDirections;
        set => SetProperty(ref _showDirections, value);
    }

    public bool ShowBestRoutePinned
    {
        get => _showBestRoutePinned;
        set
        {
            if (!SetProperty(ref _showBestRoutePinned, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(ShouldShowBestRoute));
        }
    }

    public bool ShowBestRoutePeek
    {
        get => _showBestRoutePeek;
        set
        {
            if (!SetProperty(ref _showBestRoutePeek, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(ShouldShowBestRoute));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            RaiseCommandStates();
        }
    }

    public bool IsInitialized
    {
        get => _isInitialized;
        private set
        {
            if (!SetProperty(ref _isInitialized, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(CanShowHistory));
            RaiseCommandStates();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string BestCostText
    {
        get => _bestCostText;
        private set => SetProperty(ref _bestCostText, value);
    }

    public string BestRouteText
    {
        get => _bestRouteText;
        private set => SetProperty(ref _bestRouteText, value);
    }

    public string CurrentRouteCostText
    {
        get => _currentRouteCostText;
        private set => SetProperty(ref _currentRouteCostText, value);
    }

    public string IterationBestCostText
    {
        get => _iterationBestCostText;
        private set => SetProperty(ref _iterationBestCostText, value);
    }

    public string FeasibleText
    {
        get => _feasibleText;
        private set => SetProperty(ref _feasibleText, value);
    }

    public int IterationCount
    {
        get => _iterationCount;
        private set => SetProperty(ref _iterationCount, value);
    }

    public int ObjectiveEvaluations
    {
        get => _objectiveEvaluations;
        private set => SetProperty(ref _objectiveEvaluations, value);
    }

    public int SuccessfulAnts
    {
        get => _successfulAnts;
        private set => SetProperty(ref _successfulAnts, value);
    }

    public int CompleteAnts
    {
        get => _completeAnts;
        private set => SetProperty(ref _completeAnts, value);
    }

    public LoadedGraph? LoadedGraph
    {
        get => _loadedGraph;
        private set
        {
            if (!SetProperty(ref _loadedGraph, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(GraphTitle));
            RaisePropertyChanged(nameof(GraphSummary));
            RaiseCommandStates();
        }
    }

    public Route? BestRoute
    {
        get => _bestRoute;
        private set
        {
            if (!SetProperty(ref _bestRoute, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(HasBestRoute));
            RaisePropertyChanged(nameof(ShouldShowBestRoute));
        }
    }

    public Route? CurrentRoute
    {
        get => _currentRoute;
        private set => SetProperty(ref _currentRoute, value);
    }

    public Route? SelectedAntRoute
    {
        get => _selectedAntRoute;
        private set => SetProperty(ref _selectedAntRoute, value);
    }

    public double[,]? Pheromones
    {
        get => _pheromones;
        private set => SetProperty(ref _pheromones, value);
    }

    public IReadOnlyList<AntRouteBuildResult> LastBuiltRoutes
    {
        get => _lastBuiltRoutes;
        private set => SetProperty(ref _lastBuiltRoutes, value);
    }

    public AntRouteRowViewModel? SelectedAntRouteRow
    {
        get => _selectedAntRouteRow;
        set
        {
            if (!SetProperty(ref _selectedAntRouteRow, value))
            {
                return;
            }

            SelectedAntRoute = value?.Source.Route;
        }
    }

    public bool IsSimulatedAnnealing => SelectedAlgorithm.Value == SolverFamily.SimulatedAnnealing;

    public bool IsAntColony => SelectedAlgorithm.Value == SolverFamily.AntColony;

    public string AlgorithmTitle
        => IsSimulatedAnnealing
            ? SelectedCoolingKind.Title
            : UseEliteAnts
                ? "Оптимизация: элитные муравьи"
                : "База: стандартное отложение феромона";

    public string GraphTitle => LoadedGraph?.Name ?? "Граф не загружен";

    public string GraphSummary
        => LoadedGraph is null
            ? "Выберите встроенный пример или STP-файл."
            : $"{LoadedGraph.Graph.VertexCount} вершин · {LoadedGraph.EdgeCount} рёбер · {(LoadedGraph.IsDirected ? "ориентированный" : "неориентированный")}";

    public bool HasBestRoute => BestRoute is not null;

    public bool ShouldShowBestRoute => HasBestRoute && (ShowBestRoutePinned || ShowBestRoutePeek);

    public bool CanShowHistory => History.Count > 0;

    public async Task LoadGraphFromPathAsync(string path)
    {
        try
        {
            IsBusy = true;
            var graph = await Task.Run(() => _parser.Parse(path, _layoutService));
            LoadedGraph = graph;
            ResetVisualState();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExportRouteAsync(string path)
    {
        if (BestRoute is null || LoadedGraph is null || _session.GetSnapshot().BestEvaluation is not { } evaluation)
        {
            throw new InvalidOperationException("Пока нет лучшего маршрута для экспорта.");
        }

        await _routeExporter.ExportAsync(path, LoadedGraph.Name, AlgorithmTitle, BestRoute, evaluation);
    }

    private async Task LoadSelectedExampleAsync()
    {
        if (SelectedExample is null)
        {
            return;
        }

        var path = _examples.ResolveExamplePath(SelectedExample.Value);
        if (path is null)
        {
            return;
        }

        await LoadGraphFromPathAsync(path);
    }

    private void InitializeSolver()
    {
        try
        {
            if (LoadedGraph is null)
            {
                throw new InvalidOperationException("Сначала загрузите граф.");
            }

            History.Clear();
            AntRouteRows.Clear();
            SelectedAntRouteRow = null;
            SelectedAntRoute = null;

            if (IsSimulatedAnnealing)
            {
                _session.Initialize(LoadedGraph, BuildSaConfig());
            }
            else
            {
                _session.Initialize(LoadedGraph, BuildAcoConfig());
            }

            ApplySnapshot(_session.GetSnapshot(), addToHistory: true);
            IsInitialized = true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void ExecuteSteps(int steps)
    {
        try
        {
            var snapshots = _session.Step(Math.Max(1, steps));
            foreach (var snapshot in snapshots)
            {
                ApplySnapshot(snapshot, addToHistory: true);
            }

        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task RunAsync()
    {
        try
        {
            IsBusy = true;
            var snapshots = await Task.Run(() => _session.RunToCompletion());
            foreach (var snapshot in snapshots)
            {
                await Dispatcher.UIThread.InvokeAsync(() => ApplySnapshot(snapshot, addToHistory: true));
            }

        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetSession()
    {
        _session.Reset();
        ResetVisualState();
        LoadedGraph = LoadedGraph;
    }
    
    private void ResetAdvancedSettings()
    {
        if (IsSimulatedAnnealing)
        {
            TargetAcceptanceProbability = 0.8;
            GeometricAlpha = 0.95;
            return;
        }

        Alpha = 1.0;
        Beta = 3.0;
        EvaporationRate = 0.5;
        Q = 100;
        InitialPheromone = 1.0;
        EliteAntCount = 5;
    }

    private bool CanInitializeSolver()
        => !IsBusy && LoadedGraph is not null;

    private bool CanStep()
        => !IsBusy && IsInitialized;

    private SimulatedAnnealingAlgorithmConfig BuildSaConfig()
    {
        ValidateCommonRanges();
        if (TargetAcceptanceProbability is <= 0.01 or >= 0.95)
        {
            throw new InvalidOperationException("Начальная вероятность принятия должна быть в диапазоне (0.01; 0.95).");
        }

        if (GeometricAlpha is <= 0.8 or >= 0.99)
        {
            throw new InvalidOperationException("Geometric alpha должен быть в диапазоне (0.8; 0.99).");
        }

        if (!UseAutomaticInitialTemperature && (!ManualInitialTemperature.HasValue || ManualInitialTemperature.Value <= 0.1 || ManualInitialTemperature.Value >= 1000))
        {
            throw new InvalidOperationException("Ручная температура должна быть в диапазоне (0.1; 1000).");
        }

        return new SimulatedAnnealingAlgorithmConfig
        {
            Seed = null,
            MaxIterations = MaxIterations,
            MaxIterationsWithoutImprovement = UseMaxIterationsWithoutImprovement ? MaxIterationsWithoutImprovement : null,
            InitialTemperature = UseAutomaticInitialTemperature ? null : ManualInitialTemperature,
            TemperatureEstimationTargetAcceptanceProbability = TargetAcceptanceProbability,
            CoolingKind = SelectedCoolingKind.Value,
            GeometricAlpha = GeometricAlpha
        };
    }

    private AntColonyAlgorithmConfig BuildAcoConfig()
    {
        ValidateCommonRanges();

        if (AntCount <= 0)
        {
            throw new InvalidOperationException("Количество муравьёв должно быть положительным.");
        }

        if (Alpha <= 0.1 || Alpha >= 10 || Beta <= 0.1 || Beta >= 10)
        {
            throw new InvalidOperationException("Alpha и Beta должны лежать в диапазоне (0.1; 10).");
        }

        if (EvaporationRate <= 0.1 || EvaporationRate >= 0.99)
        {
            throw new InvalidOperationException("Коэффициент испарения должен быть в диапазоне (0.1; 0.99).");
        }

        if (Q <= 0.1 || Q >= 10000 || InitialPheromone <= 0.01 || InitialPheromone >= 100)
        {
            throw new InvalidOperationException("Q и начальный феромон вышли за допустимые диапазоны.");
        }

        if (UseEliteAnts && (EliteAntCount < 1 || EliteAntCount > 10))
        {
            throw new InvalidOperationException("Коэффициент элитного усиления должен быть в диапазоне [1; 10].");
        }

        return new AntColonyAlgorithmConfig
        {
            Seed = null,
            MaxIterations = MaxIterations,
            MaxIterationsWithoutImprovement = UseMaxIterationsWithoutImprovement ? MaxIterationsWithoutImprovement : null,
            AntCount = AntCount,
            Alpha = Alpha,
            Beta = Beta,
            EvaporationRate = EvaporationRate,
            Q = Q,
            InitialPheromone = InitialPheromone,
            UseEliteAnts = UseEliteAnts,
            EliteAntCount = EliteAntCount
        };
    }

    private void ValidateCommonRanges()
    {
        if (MaxIterations < 100 || MaxIterations > 100000)
        {
            throw new InvalidOperationException("MaxIterations должен быть в диапазоне [100; 100000].");
        }

        if (UseMaxIterationsWithoutImprovement && (MaxIterationsWithoutImprovement < 20 || MaxIterationsWithoutImprovement > 10000))
        {
            throw new InvalidOperationException("MaxIterationsWithoutImprovement должен быть в диапазоне [20; 10000].");
        }
    }

    private void ApplySnapshot(IterationSnapshot snapshot, bool addToHistory)
    {
        IterationCount = snapshot.Iteration;
        ObjectiveEvaluations = snapshot.ObjectiveEvaluations;
        FeasibleText = snapshot.HasFeasibleSolution ? "Найден" : "Не найден";
        BestRoute = snapshot.BestRoute;
        CurrentRoute = snapshot.CurrentRoute;
        Pheromones = snapshot.Pheromones;
        LastBuiltRoutes = snapshot.LastBuiltRoutes;
        SuccessfulAnts = snapshot.SuccessfulAnts;
        CompleteAnts = snapshot.CompleteAnts;
        IterationBestCostText = snapshot.IterationBestCost?.ToString("G8") ?? "—";
        CurrentRouteCostText = snapshot.CurrentEvaluation?.Cost.ToString("G8") ?? "—";
        BestCostText = snapshot.BestEvaluation?.Cost.ToString("G8") ?? "—";
        BestRouteText = _routeExporter.FormatRoute(snapshot.BestRoute);

        if (addToHistory && snapshot.BestEvaluation is { } bestEvaluation)
        {
            if (History.Count == 0 || History[^1].Iteration != snapshot.Iteration)
            {
                History.Add(new HistoryPoint(snapshot.Iteration, bestEvaluation.Cost));
            }
            else
            {
                History[^1] = new HistoryPoint(snapshot.Iteration, bestEvaluation.Cost);
            }
        }

        AntRouteRows.Clear();
        var orderedRoutes = snapshot.LastBuiltRoutes
            .Select((route, index) => new { Route = route, Index = index })
            .OrderBy(static item => item.Route.Evaluation?.Cost ?? double.MaxValue)
            .ThenByDescending(static item => item.Route.IsComplete)
            .ToList();

        for (var index = 0; index < orderedRoutes.Count; index++)
        {
            var item = orderedRoutes[index].Route;
            AntRouteRows.Add(new AntRouteRowViewModel
            {
                Rank = index + 1,
                CostText = item.Evaluation?.Cost.ToString("G8") ?? "—",
                FeasibilityText = item.Evaluation?.IsFeasible == true ? "Допустим" : "Со штрафом",
                CompletionText = item.IsComplete ? "Полный" : "Не завершён",
                RouteText = _routeExporter.FormatRoute(item.Route),
                Source = item
            });
        }

        if (SelectedAntRouteRow is not null)
        {
            SelectedAntRoute = SelectedAntRouteRow.Source.Route;
        }

        RaisePropertyChanged(nameof(CanShowHistory));
        RaisePropertyChanged(nameof(ShouldShowBestRoute));
    }

    private void ResetVisualState()
    {
        _session.Reset();
        History.Clear();
        AntRouteRows.Clear();
        BestRoute = null;
        CurrentRoute = null;
        SelectedAntRoute = null;
        SelectedAntRouteRow = null;
        Pheromones = null;
        LastBuiltRoutes = [];
        IterationCount = 0;
        ObjectiveEvaluations = 0;
        SuccessfulAnts = 0;
        CompleteAnts = 0;
        BestCostText = "—";
        BestRouteText = "—";
        CurrentRouteCostText = "—";
        IterationBestCostText = "—";
        FeasibleText = "Не найден";
        IsInitialized = false;
        RaisePropertyChanged(nameof(CanShowHistory));
        RaisePropertyChanged(nameof(ShouldShowBestRoute));
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        (InitializeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (StepCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RunCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ResetCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LoadExampleCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ResetAdvancedSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
