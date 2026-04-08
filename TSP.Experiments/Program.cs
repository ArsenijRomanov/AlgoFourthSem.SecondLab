using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TSP.ACO;
using TSP.Domain;
using TSP.Domain.Abstractions;
using TSP.Factory;
using TSP.SA;

namespace TSP.Experiments;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static int Main(string[] args)
    {
        try
        {
            var configPath = ResolveConfigPath(args);
            var config = LoadConfig(configPath);
            ValidateConfig(config);

            var configDirectory = Path.GetDirectoryName(configPath)
                ?? throw new InvalidOperationException("Не удалось определить каталог конфига.");

            var resultsRoot = ResolvePath(config.ResultsDirectory, configDirectory);
            var runName = string.IsNullOrWhiteSpace(config.RunName)
                ? DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                : config.RunName.Trim();

            var runDirectory = Path.Combine(resultsRoot, runName);
            var historiesDirectory = Path.Combine(runDirectory, "histories");

            Directory.CreateDirectory(runDirectory);
            Directory.CreateDirectory(historiesDirectory);

            File.Copy(configPath, Path.Combine(runDirectory, Path.GetFileName(configPath)), true);

            Console.WriteLine($"Конфиг: {configPath}");
            Console.WriteLine($"Папка результатов: {runDirectory}");

            var graphs = LoadGraphs(config.Graphs, configDirectory);
            var enabledAlgorithms = config.Algorithms.Where(static x => x.Enabled).ToList();

            Console.WriteLine($"Графов: {graphs.Count}");
            Console.WriteLine($"Алгоритмов: {enabledAlgorithms.Count}");
            Console.WriteLine($"Сидов: {config.Seeds.Count}");

            var runs = new List<RunRow>();

            foreach (var algorithm in enabledAlgorithms)
            {
                Console.WriteLine();
                Console.WriteLine($"=== {algorithm.Name} ===");

                foreach (var graph in graphs)
                {
                    foreach (var seed in config.Seeds)
                    {
                        var run = ExecuteRun(
                            config,
                            algorithm,
                            graph,
                            seed,
                            historiesDirectory);

                        runs.Add(run);

                        var bestCostText = run.BestCost.HasValue
                            ? run.BestCost.Value.ToString("G17", CultureInfo.InvariantCulture)
                            : "null";

                        Console.WriteLine(
                            $"[{graph.Name}] seed={seed} best={bestCostText} timeMs={run.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)} evals={run.ObjectiveEvaluations.ToString(CultureInfo.InvariantCulture)}");
                    }
                }
            }

            var summary = BuildSummary(runs);

            WriteRunsCsv(Path.Combine(runDirectory, "runs.csv"), runs);
            WriteSummaryCsv(Path.Combine(runDirectory, "summary.csv"), summary);
            File.WriteAllText(Path.Combine(runDirectory, "runs.json"), JsonSerializer.Serialize(runs, JsonOptions));
            File.WriteAllText(Path.Combine(runDirectory, "summary.json"), JsonSerializer.Serialize(summary, JsonOptions));

            Console.WriteLine();
            Console.WriteLine("Готово.");
            Console.WriteLine($"runs.csv: {Path.Combine(runDirectory, "runs.csv")}");
            Console.WriteLine($"summary.csv: {Path.Combine(runDirectory, "summary.csv")}");
            Console.WriteLine($"histories: {historiesDirectory}");

            return 0;
        }
        catch (Exception exception)
        {
            Console.WriteLine("Ошибка запуска экспериментов:");
            Console.WriteLine(exception);
            return 1;
        }
    }

    private static ExperimentConfig LoadConfig(string path)
    {
        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<ExperimentConfig>(json, JsonOptions)
            ?? throw new InvalidOperationException("Не удалось прочитать конфиг экспериментов.");

        return config;
    }

    private static void ValidateConfig(ExperimentConfig config)
    {
        if (config.Graphs.Count == 0)
            throw new InvalidOperationException("В конфиге должен быть хотя бы один граф.");

        if (config.Seeds.Count == 0)
            throw new InvalidOperationException("В конфиге должен быть хотя бы один seed.");

        if (config.Algorithms.Count == 0)
            throw new InvalidOperationException("В конфиге должен быть хотя бы один алгоритм.");

        if (config.Algorithms.All(static x => !x.Enabled))
            throw new InvalidOperationException("Все алгоритмы отключены. Нечего запускать.");

        if (config.HistorySamplingStep <= 0)
            throw new InvalidOperationException("HistorySamplingStep должен быть > 0.");

        foreach (var algorithm in config.Algorithms.Where(static x => x.Enabled))
        {
            if (string.IsNullOrWhiteSpace(algorithm.Name))
                throw new InvalidOperationException("У алгоритма Name не должен быть пустым.");

            if (string.IsNullOrWhiteSpace(algorithm.Kind))
                throw new InvalidOperationException($"У алгоритма {algorithm.Name} Kind не должен быть пустым.");

            if (algorithm.MaxIterations <= 0)
                throw new InvalidOperationException($"У алгоритма {algorithm.Name} MaxIterations должен быть > 0.");

            if (algorithm.MaxIterationsWithoutImprovement.HasValue && algorithm.MaxIterationsWithoutImprovement.Value <= 0)
            {
                throw new InvalidOperationException(
                    $"У алгоритма {algorithm.Name} MaxIterationsWithoutImprovement должен быть > 0.");
            }

            if (EqualsIgnoreCase(algorithm.Kind, "sa"))
            {
                if (!Enum.TryParse<SimulatedAnnealingCoolingKind>(algorithm.CoolingKind, true, out _))
                {
                    throw new InvalidOperationException(
                        $"У алгоритма {algorithm.Name} неизвестный CoolingKind: {algorithm.CoolingKind}.");
                }

                if (algorithm.GeometricAlpha <= 0d || algorithm.GeometricAlpha >= 1d)
                {
                    throw new InvalidOperationException(
                        $"У алгоритма {algorithm.Name} GeometricAlpha должен быть в интервале (0, 1).");
                }
            }
            else if (EqualsIgnoreCase(algorithm.Kind, "aco"))
            {
                if (algorithm.AntCount <= 0)
                    throw new InvalidOperationException($"У алгоритма {algorithm.Name} AntCount должен быть > 0.");

                if (algorithm.UseEliteAnts && algorithm.EliteAntCount <= 0)
                {
                    throw new InvalidOperationException(
                        $"У алгоритма {algorithm.Name} EliteAntCount должен быть > 0.");
                }
            }
            else
            {
                throw new InvalidOperationException($"Неизвестный вид алгоритма: {algorithm.Kind}.");
            }
        }
    }

    private static List<LoadedGraph> LoadGraphs(IReadOnlyList<GraphConfig> graphConfigs, string configDirectory)
    {
        var graphs = new List<LoadedGraph>();

        foreach (var graphConfig in graphConfigs)
        {
            if (string.IsNullOrWhiteSpace(graphConfig.Name))
                throw new InvalidOperationException("У графа Name не должен быть пустым.");

            if (string.IsNullOrWhiteSpace(graphConfig.Path))
                throw new InvalidOperationException($"У графа {graphConfig.Name} Path не должен быть пустым.");

            var path = ResolvePath(graphConfig.Path, configDirectory);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Не найден граф: {path}");

            Console.WriteLine($"Чтение графа {graphConfig.Name}: {path}");

            graphs.Add(new LoadedGraph(
                graphConfig.Name,
                path,
                ParseStpGraph(path)));
        }

        return graphs;
    }

    private static RunRow ExecuteRun(
        ExperimentConfig config,
        AlgorithmConfig algorithm,
        LoadedGraph graph,
        int seed,
        string historiesDirectory)
    {
        var solver = CreateSolver(graph.Graph, algorithm, seed);
        solver.Initialize();

        var history = new List<HistoryPoint>();
        var bestCost = TryGetBestCost(solver.State);
        var iterationsWithoutImprovement = 0;

        CaptureHistoryIfNeeded(config, history, graph.Name, algorithm.Name, seed, solver.State, force: true);

        var stopwatch = Stopwatch.StartNew();

        while (solver.State.IterationCount < algorithm.MaxIterations)
        {
            solver.Step();

            var newBestCost = TryGetBestCost(solver.State);
            var improved = HasImproved(bestCost, newBestCost);

            if (improved)
            {
                bestCost = newBestCost;
                iterationsWithoutImprovement = 0;
            }
            else
            {
                iterationsWithoutImprovement++;
            }

            CaptureHistoryIfNeeded(config, history, graph.Name, algorithm.Name, seed, solver.State, force: false);

            if (algorithm.MaxIterationsWithoutImprovement.HasValue &&
                iterationsWithoutImprovement >= algorithm.MaxIterationsWithoutImprovement.Value)
            {
                break;
            }
        }

        stopwatch.Stop();

        CaptureHistoryIfNeeded(config, history, graph.Name, algorithm.Name, seed, solver.State, force: true);

        var result = solver.State;

        var historyFileName = $"{SanitizeFileName(graph.Name)}__{SanitizeFileName(algorithm.Name)}__seed_{seed.ToString(CultureInfo.InvariantCulture)}.csv";
        var historyRelativePath = Path.Combine("histories", historyFileName);

        if (config.SaveHistories)
        {
            WriteHistoryCsv(Path.Combine(historiesDirectory, historyFileName), history);
        }

        return new RunRow
        {
            Graph = graph.Name,
            Algorithm = algorithm.Name,
            Kind = algorithm.Kind,
            Seed = seed,
            BestCost = result.HasFeasibleSolution && result.BestEvaluation.HasValue
                ? result.BestEvaluation.Value.Cost
                : null,
            IsFeasible = result.HasFeasibleSolution,
            Iterations = result.IterationCount,
            ObjectiveEvaluations = result.ObjectiveEvaluations,
            ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
            BestRoute = FormatRoute(result.BestRoute),
            HistoryFile = config.SaveHistories ? historyRelativePath.Replace('\\', '/') : null,
            ConfigJson = JsonSerializer.Serialize(BuildUsedConfigSnapshot(algorithm, seed), JsonOptions)
        };
    }

    private static object BuildUsedConfigSnapshot(AlgorithmConfig algorithm, int seed)
    {
        if (EqualsIgnoreCase(algorithm.Kind, "sa"))
        {
            return new SimulatedAnnealingAlgorithmConfig
            {
                Seed = seed,
                MaxIterations = algorithm.MaxIterations,
                MaxIterationsWithoutImprovement = algorithm.MaxIterationsWithoutImprovement,
                InitialTemperature = algorithm.InitialTemperature,
                TemperatureEstimationStartsCount = algorithm.TemperatureEstimationStartsCount,
                TemperatureEstimationChainLength = algorithm.TemperatureEstimationChainLength,
                TemperatureEstimationFallbackTemperature = algorithm.TemperatureEstimationFallbackTemperature,
                TemperatureEstimationTargetAcceptanceProbability = algorithm.TemperatureEstimationTargetAcceptanceProbability,
                CoolingKind = ParseCoolingKind(algorithm.CoolingKind),
                GeometricAlpha = algorithm.GeometricAlpha
            };
        }

        return new AntColonyAlgorithmConfig
        {
            Seed = seed,
            MaxIterations = algorithm.MaxIterations,
            MaxIterationsWithoutImprovement = algorithm.MaxIterationsWithoutImprovement,
            AntCount = algorithm.AntCount,
            Alpha = algorithm.Alpha,
            Beta = algorithm.Beta,
            EvaporationRate = algorithm.EvaporationRate,
            Q = algorithm.Q,
            InitialPheromone = algorithm.InitialPheromone,
            UseEliteAnts = algorithm.UseEliteAnts,
            EliteAntCount = algorithm.EliteAntCount
        };
    }

    private static ITspSolver CreateSolver(IWeightedGraph graph, AlgorithmConfig algorithm, int seed)
    {
        if (EqualsIgnoreCase(algorithm.Kind, "sa"))
        {
            var config = new SimulatedAnnealingAlgorithmConfig
            {
                Seed = seed,
                MaxIterations = algorithm.MaxIterations,
                MaxIterationsWithoutImprovement = algorithm.MaxIterationsWithoutImprovement,
                InitialTemperature = algorithm.InitialTemperature,
                TemperatureEstimationStartsCount = algorithm.TemperatureEstimationStartsCount,
                TemperatureEstimationChainLength = algorithm.TemperatureEstimationChainLength,
                TemperatureEstimationFallbackTemperature = algorithm.TemperatureEstimationFallbackTemperature,
                TemperatureEstimationTargetAcceptanceProbability = algorithm.TemperatureEstimationTargetAcceptanceProbability,
                CoolingKind = ParseCoolingKind(algorithm.CoolingKind),
                GeometricAlpha = algorithm.GeometricAlpha
            };

            return TspSolverFactory.CreateSimulatedAnnealing(graph, config);
        }

        if (EqualsIgnoreCase(algorithm.Kind, "aco"))
        {
            var config = new AntColonyAlgorithmConfig
            {
                Seed = seed,
                MaxIterations = algorithm.MaxIterations,
                MaxIterationsWithoutImprovement = algorithm.MaxIterationsWithoutImprovement,
                AntCount = algorithm.AntCount,
                Alpha = algorithm.Alpha,
                Beta = algorithm.Beta,
                EvaporationRate = algorithm.EvaporationRate,
                Q = algorithm.Q,
                InitialPheromone = algorithm.InitialPheromone,
                UseEliteAnts = algorithm.UseEliteAnts,
                EliteAntCount = algorithm.EliteAntCount
            };

            return TspSolverFactory.CreateAntColony(graph, config);
        }

        throw new InvalidOperationException($"Неизвестный вид алгоритма: {algorithm.Kind}");
    }

    private static SimulatedAnnealingCoolingKind ParseCoolingKind(string? value)
    {
        if (Enum.TryParse<SimulatedAnnealingCoolingKind>(value, true, out var parsed))
            return parsed;

        throw new InvalidOperationException($"Неизвестный CoolingKind: {value}");
    }

    private static void CaptureHistoryIfNeeded(
        ExperimentConfig config,
        List<HistoryPoint> history,
        string graphName,
        string algorithmName,
        int seed,
        ISolverState state,
        bool force)
    {
        if (!config.SaveHistories)
            return;

        if (!force && state.IterationCount % config.HistorySamplingStep != 0)
            return;

        var currentCost = state switch
        {
            SaState sa when sa.CurrentEvaluation.HasValue => sa.CurrentEvaluation.Value.Cost,
            _ => (double?)null
        };

        var currentTemperature = state switch
        {
            SaState sa => sa.CurrentTemperature,
            _ => null
        };

        history.Add(new HistoryPoint
        {
            Graph = graphName,
            Algorithm = algorithmName,
            Seed = seed,
            Iteration = state.IterationCount,
            BestCost = TryGetBestCost(state),
            CurrentCost = currentCost,
            CurrentTemperature = currentTemperature,
            ObjectiveEvaluations = state.ObjectiveEvaluations,
            IsFeasible = state.HasFeasibleSolution
        });
    }

    private static double? TryGetBestCost(ISolverState state)
    {
        if (!state.HasFeasibleSolution || !state.BestEvaluation.HasValue)
            return null;

        return state.BestEvaluation.Value.Cost;
    }

    private static bool HasImproved(double? previousBest, double? currentBest)
    {
        if (!currentBest.HasValue)
            return false;

        if (!previousBest.HasValue)
            return true;

        return currentBest.Value < previousBest.Value;
    }

    private static List<SummaryRow> BuildSummary(IReadOnlyList<RunRow> runs)
    {
        var result = new List<SummaryRow>();

        var groups = runs
            .GroupBy(static x => new { x.Graph, x.Algorithm, x.Kind })
            .OrderBy(static x => x.Key.Graph, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static x => x.Key.Algorithm, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var feasible = group.Where(static x => x.IsFeasible && x.BestCost.HasValue).ToList();
            var costs = feasible.Select(static x => x.BestCost!.Value).ToList();
            var times = group.Select(static x => x.ElapsedMilliseconds).ToList();
            var iterations = group.Select(static x => x.Iterations).ToList();
            var evaluations = group.Select(static x => x.ObjectiveEvaluations).ToList();

            result.Add(new SummaryRow
            {
                Graph = group.Key.Graph,
                Algorithm = group.Key.Algorithm,
                Kind = group.Key.Kind,
                RunCount = group.Count(),
                FeasibleRunCount = feasible.Count,
                MeanBestCost = costs.Count > 0 ? costs.Average() : null,
                StdBestCost = costs.Count > 1 ? CalculateStdDev(costs) : null,
                MinBestCost = costs.Count > 0 ? costs.Min() : null,
                MaxBestCost = costs.Count > 0 ? costs.Max() : null,
                MeanElapsedMilliseconds = times.Average(),
                MeanIterations = iterations.Average(),
                MeanObjectiveEvaluations = evaluations.Average()
            });
        }

        return result;
    }

    private static double CalculateStdDev(IReadOnlyList<double> values)
    {
        if (values.Count <= 1)
            return 0d;

        var mean = values.Average();
        var sum = 0d;

        foreach (var value in values)
            sum += Math.Pow(value - mean, 2d);

        return Math.Sqrt(sum / (values.Count - 1));
    }

    private static void WriteRunsCsv(string path, IReadOnlyList<RunRow> runs)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));

        writer.WriteLine(
            "graph,algorithm,kind,seed,bestCost,isFeasible,iterations,objectiveEvaluations,elapsedMilliseconds,bestRoute,historyFile,configJson");

        foreach (var run in runs)
        {
            writer.WriteLine(string.Join(',',
                Csv(run.Graph),
                Csv(run.Algorithm),
                Csv(run.Kind),
                Csv(run.Seed.ToString(CultureInfo.InvariantCulture)),
                Csv(FormatDouble(run.BestCost)),
                Csv(run.IsFeasible ? "true" : "false"),
                Csv(run.Iterations.ToString(CultureInfo.InvariantCulture)),
                Csv(run.ObjectiveEvaluations.ToString(CultureInfo.InvariantCulture)),
                Csv(run.ElapsedMilliseconds.ToString("G17", CultureInfo.InvariantCulture)),
                Csv(run.BestRoute),
                Csv(run.HistoryFile),
                Csv(run.ConfigJson)));
        }
    }

    private static void WriteSummaryCsv(string path, IReadOnlyList<SummaryRow> rows)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));

        writer.WriteLine(
            "graph,algorithm,kind,runCount,feasibleRunCount,meanBestCost,stdBestCost,minBestCost,maxBestCost,meanElapsedMilliseconds,meanIterations,meanObjectiveEvaluations");

        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(',',
                Csv(row.Graph),
                Csv(row.Algorithm),
                Csv(row.Kind),
                Csv(row.RunCount.ToString(CultureInfo.InvariantCulture)),
                Csv(row.FeasibleRunCount.ToString(CultureInfo.InvariantCulture)),
                Csv(FormatDouble(row.MeanBestCost)),
                Csv(FormatDouble(row.StdBestCost)),
                Csv(FormatDouble(row.MinBestCost)),
                Csv(FormatDouble(row.MaxBestCost)),
                Csv(row.MeanElapsedMilliseconds.ToString("G17", CultureInfo.InvariantCulture)),
                Csv(row.MeanIterations.ToString("G17", CultureInfo.InvariantCulture)),
                Csv(row.MeanObjectiveEvaluations.ToString("G17", CultureInfo.InvariantCulture))));
        }
    }

    private static void WriteHistoryCsv(string path, IReadOnlyList<HistoryPoint> history)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));

        writer.WriteLine(
            "graph,algorithm,seed,iteration,bestCost,currentCost,currentTemperature,objectiveEvaluations,isFeasible");

        foreach (var point in history)
        {
            writer.WriteLine(string.Join(',',
                Csv(point.Graph),
                Csv(point.Algorithm),
                Csv(point.Seed.ToString(CultureInfo.InvariantCulture)),
                Csv(point.Iteration.ToString(CultureInfo.InvariantCulture)),
                Csv(FormatDouble(point.BestCost)),
                Csv(FormatDouble(point.CurrentCost)),
                Csv(FormatDouble(point.CurrentTemperature)),
                Csv(point.ObjectiveEvaluations.ToString(CultureInfo.InvariantCulture)),
                Csv(point.IsFeasible ? "true" : "false")));
        }
    }

    private static string? FormatRoute(Route? route)
    {
        if (route is null)
            return null;

        var vertices = route
            .ToArray()
            .Select(static x => (x + 1).ToString(CultureInfo.InvariantCulture))
            .ToList();

        if (vertices.Count > 0)
            vertices.Add(vertices[0]);

        return string.Join(" -> ", vertices);
    }

    private static string Csv(string? value)
    {
        if (value is null)
            return "";

        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string? FormatDouble(double? value)
        => value?.ToString("G17", CultureInfo.InvariantCulture);

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }

    private static bool EqualsIgnoreCase(string? left, string? right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string ResolveConfigPath(string[] args)
    {
        var fileName = args.Length > 0 ? args[0] : "experiments.json";

        if (Path.IsPathRooted(fileName))
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            throw new FileNotFoundException($"Не найден конфиг: {fileName}");
        }

        var candidates = new List<string>();

        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), fileName));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, fileName));

        var solutionRoot = TryFindSolutionRoot();
        if (solutionRoot is not null)
        {
            candidates.Add(Path.Combine(solutionRoot, fileName));
            candidates.Add(Path.Combine(solutionRoot, "TSP.Experiments", fileName));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        throw new FileNotFoundException(
            $"Не найден конфиг {fileName}. Искал в current dir, AppContext.BaseDirectory и рядом с TSP.sln.");
    }

    private static string? TryFindSolutionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var slnPath = Path.Combine(current.FullName, "TSP.sln");
            var coreDirectory = Path.Combine(current.FullName, "TSP.Core");

            if (File.Exists(slnPath) || Directory.Exists(coreDirectory))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }

    private static string ResolvePath(string path, string baseDirectory)
    {
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        return Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private static IWeightedGraph ParseStpGraph(string path)
    {
        var lines = File.ReadAllLines(path);
        double?[,]? weights = null;
        var inGraphSection = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("Section Graph", StringComparison.OrdinalIgnoreCase))
            {
                inGraphSection = true;
                continue;
            }

            if (!inGraphSection)
                continue;

            if (line.StartsWith("End", StringComparison.OrdinalIgnoreCase))
                break;

            if (line.StartsWith("Nodes", StringComparison.OrdinalIgnoreCase))
            {
                var parts = SplitNonEmpty(line);
                var vertexCount = int.Parse(parts[1], CultureInfo.InvariantCulture);
                weights = new double?[vertexCount, vertexCount];
                continue;
            }

            if (weights is null)
                throw new InvalidOperationException($"В файле {path} не найдено число вершин перед списком рёбер.");

            if (line.StartsWith("A ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = SplitNonEmpty(line);
                var from = int.Parse(parts[1], CultureInfo.InvariantCulture) - 1;
                var to = int.Parse(parts[2], CultureInfo.InvariantCulture) - 1;
                var weight = double.Parse(parts[3], CultureInfo.InvariantCulture);
                weights[from, to] = weight;
                continue;
            }

            if (line.StartsWith("E ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = SplitNonEmpty(line);
                var from = int.Parse(parts[1], CultureInfo.InvariantCulture) - 1;
                var to = int.Parse(parts[2], CultureInfo.InvariantCulture) - 1;
                var weight = double.Parse(parts[3], CultureInfo.InvariantCulture);
                weights[from, to] = weight;
                weights[to, from] = weight;
            }
        }

        if (weights is null)
            throw new InvalidOperationException($"В файле {path} не удалось прочитать секцию Graph.");

        return new AdjacencyMatrixGraph(weights);
    }

    private static string[] SplitNonEmpty(string line)
        => line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

internal sealed record LoadedGraph(string Name, string Path, IWeightedGraph Graph);

internal sealed class ExperimentConfig
{
    public string? RunName { get; init; }
    public string ResultsDirectory { get; init; } = "./Results";
    public bool SaveHistories { get; init; } = true;
    public int HistorySamplingStep { get; init; } = 1;
    public List<GraphConfig> Graphs { get; init; } = [];
    public List<int> Seeds { get; init; } = [];
    public List<AlgorithmConfig> Algorithms { get; init; } = [];
}

internal sealed class GraphConfig
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
}

internal sealed class AlgorithmConfig
{
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public int MaxIterations { get; init; } = 1000;
    public int? MaxIterationsWithoutImprovement { get; init; }

    public double? InitialTemperature { get; init; }
    public int TemperatureEstimationStartsCount { get; init; } = 10;
    public int TemperatureEstimationChainLength { get; init; } = 20;
    public double TemperatureEstimationFallbackTemperature { get; init; } = 1.0;
    public double TemperatureEstimationTargetAcceptanceProbability { get; init; } = 0.8;
    public string CoolingKind { get; init; } = "Geometric";
    public double GeometricAlpha { get; init; } = 0.95;

    public int AntCount { get; init; } = 30;
    public double Alpha { get; init; } = 1.0;
    public double Beta { get; init; } = 3.0;
    public double EvaporationRate { get; init; } = 0.5;
    public double Q { get; init; } = 100.0;
    public double InitialPheromone { get; init; } = 1.0;
    public bool UseEliteAnts { get; init; }
    public int EliteAntCount { get; init; } = 5;
}

internal sealed class RunRow
{
    public string Graph { get; init; } = "";
    public string Algorithm { get; init; } = "";
    public string Kind { get; init; } = "";
    public int Seed { get; init; }
    public double? BestCost { get; init; }
    public bool IsFeasible { get; init; }
    public int Iterations { get; init; }
    public int ObjectiveEvaluations { get; init; }
    public double ElapsedMilliseconds { get; init; }
    public string? BestRoute { get; init; }
    public string? HistoryFile { get; init; }
    public string ConfigJson { get; init; } = "";
}

internal sealed class SummaryRow
{
    public string Graph { get; init; } = "";
    public string Algorithm { get; init; } = "";
    public string Kind { get; init; } = "";
    public int RunCount { get; init; }
    public int FeasibleRunCount { get; init; }
    public double? MeanBestCost { get; init; }
    public double? StdBestCost { get; init; }
    public double? MinBestCost { get; init; }
    public double? MaxBestCost { get; init; }
    public double MeanElapsedMilliseconds { get; init; }
    public double MeanIterations { get; init; }
    public double MeanObjectiveEvaluations { get; init; }
}

internal sealed class HistoryPoint
{
    public string Graph { get; init; } = "";
    public string Algorithm { get; init; } = "";
    public int Seed { get; init; }
    public int Iteration { get; init; }
    public double? BestCost { get; init; }
    public double? CurrentCost { get; init; }
    public double? CurrentTemperature { get; init; }
    public int ObjectiveEvaluations { get; init; }
    public bool IsFeasible { get; init; }
}
