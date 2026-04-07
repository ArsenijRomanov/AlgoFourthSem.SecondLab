using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TSP.Domain;
using TSP.Domain.Abstractions;
using TSP.Factory;

namespace TSP.ParameterStudy;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly object RawWriterLock = new();
    private static long _plannedRuns;
    private static long _completedRuns;

    public static int Main(string[] args)
    {
        try
        {
            var configPath = args.Length > 0
                ? ResolvePath(args[0])
                : ResolvePath("runner-config.json");

            if (!File.Exists(configPath))
            {
                Console.WriteLine($"Не найден конфиг: {configPath}");
                return 1;
            }

            var config = LoadConfig(configPath);
            ValidateConfig(config);

            var outputDirectory = ResolveOutputDirectory(config.Output.BaseDirectory, config.Output.RunName);
            Directory.CreateDirectory(outputDirectory);

            var copiedConfigPath = Path.Combine(outputDirectory, Path.GetFileName(configPath));
            File.Copy(configPath, copiedConfigPath, true);

            Console.WriteLine($"Конфиг: {configPath}");
            Console.WriteLine($"Папка результатов: {outputDirectory}");

            var graphs = LoadGraphs(config);
            _plannedRuns = CountPlannedRuns(config, graphs.Count);
            _completedRuns = 0;

            Console.WriteLine($"Графов: {graphs.Count}");
            Console.WriteLine($"Всего запусков по плану: {_plannedRuns}");

            var rawRunsPath = Path.Combine(outputDirectory, "raw-runs.jsonl");

            using (var rawWriter = new StreamWriter(rawRunsPath, false, new UTF8Encoding(false)))
            {
                foreach (var experimentClass in config.Classes)
                {
                    var parameterSetCount = CountParameterSets(experimentClass);
                    Console.WriteLine();
                    Console.WriteLine($"=== Класс: {experimentClass.Name} ===");
                    Console.WriteLine($"Комбинаций параметров: {parameterSetCount}");

                    var options = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = config.Execution.MaxDegreeOfParallelism > 0
                            ? config.Execution.MaxDegreeOfParallelism
                            : Environment.ProcessorCount
                    };

                    Parallel.ForEach(
                        EnumerateParameterSets(experimentClass),
                        options,
                        parameterSet => RunParameterSet(config, parameterSet, graphs, rawWriter));
                }
            }

            Console.WriteLine();
            Console.WriteLine("Все прогоны завершены. Идет постобработка...");

            var runs = LoadRawRuns(rawRunsPath);
            var references = BuildReferences(runs, graphs, config);
            var summaries = BuildSummaries(runs, references, config);
            var topConfigs = BuildTopConfigs(summaries, config.TopKPerClass);

            if (config.Output.SaveReferences)
            {
                var referencesPath = Path.Combine(outputDirectory, "references.json");
                File.WriteAllText(referencesPath, JsonSerializer.Serialize(references, JsonOptions));
            }

            if (config.Output.SaveSummaries)
            {
                var summariesPath = Path.Combine(outputDirectory, "summaries.json");
                File.WriteAllText(summariesPath, JsonSerializer.Serialize(summaries, JsonOptions));
            }

            if (config.Output.SaveTopConfigs)
            {
                var topConfigsPath = Path.Combine(outputDirectory, "top-configs.json");
                File.WriteAllText(topConfigsPath, JsonSerializer.Serialize(topConfigs, JsonOptions));
            }

            if (!config.Output.SaveRawRuns && File.Exists(rawRunsPath))
                File.Delete(rawRunsPath);

            Console.WriteLine("Готово.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Фатальная ошибка:");
            Console.WriteLine(ex);
            return 1;
        }
    }

    private static RunnerConfig LoadConfig(string path)
    {
        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<RunnerConfig>(json, JsonOptions);

        if (config is null)
            throw new InvalidOperationException("Не удалось десериализовать runner-config.json.");

        return config;
    }

    private static void ValidateConfig(RunnerConfig config)
    {
        if (config.Graphs is null || config.Graphs.Count == 0)
            throw new InvalidOperationException("В конфиге должен быть хотя бы один граф.");

        if (config.Classes is null || config.Classes.Count == 0)
            throw new InvalidOperationException("В конфиге должен быть хотя бы один класс экспериментов.");

        if (config.SeedCount <= 0)
            throw new InvalidOperationException("SeedCount должен быть > 0.");

        if (config.TopKPerClass <= 0)
            throw new InvalidOperationException("TopKPerClass должен быть > 0.");

        if (config.RunLimits.MaxIterations <= 0)
            throw new InvalidOperationException("RunLimits.MaxIterations должен быть > 0.");

        if (string.IsNullOrWhiteSpace(config.Output.BaseDirectory))
            throw new InvalidOperationException("Output.BaseDirectory не должен быть пустым.");

        if (string.IsNullOrWhiteSpace(config.Output.RunName))
            throw new InvalidOperationException("Output.RunName не должен быть пустым.");
    }

    private static List<LoadedGraph> LoadGraphs(RunnerConfig config)
    {
        var graphs = new List<LoadedGraph>();

        foreach (var graph in config.Graphs)
        {
            var fullPath = ResolvePath(graph.Path);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Не найден граф: {fullPath}");

            Console.WriteLine($"Чтение графа {graph.Name}: {fullPath}");
            var weightedGraph = ParseStpGraph(fullPath);

            graphs.Add(new LoadedGraph
            {
                Name = graph.Name,
                Path = fullPath,
                Graph = weightedGraph
            });
        }

        return graphs;
    }

    private static IWeightedGraph ParseStpGraph(string path)
    {
        var lines = File.ReadAllLines(path);
        double?[,]? weights = null;
        var inGraphSection = false;
        var vertexCount = 0;

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

            if (line.Equals("END", StringComparison.OrdinalIgnoreCase) ||
                line.Equals("End", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (line.StartsWith("Nodes ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = SplitNonEmpty(line);
                vertexCount = int.Parse(parts[1], CultureInfo.InvariantCulture);
                weights = new double?[vertexCount, vertexCount];
                continue;
            }

            if (weights is null)
                throw new InvalidOperationException($"В файле {path} не найдено число вершин перед списком ребер.");

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
                continue;
            }
        }

        if (weights is null)
            throw new InvalidOperationException($"В файле {path} не удалось прочитать секцию Graph.");

        return new AdjacencyMatrixGraph(weights);
    }

    private static string[] SplitNonEmpty(string line)
        => line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static long CountPlannedRuns(RunnerConfig config, int graphCount)
    {
        var totalParameterSets = config.Classes.Sum(CountParameterSets);
        return totalParameterSets * graphCount * config.SeedCount;
    }

    private static long CountParameterSets(ExperimentClassConfig experimentClass)
    {
        if (EqualsIgnoreCase(experimentClass.Algorithm, "SA"))
        {
            var temperatureCount = EqualsIgnoreCase(experimentClass.TemperatureMode, "Manual")
                ? Count(experimentClass.InitialTemperatureValues)
                : Count(experimentClass.TargetAcceptanceProbabilityValues);

            var alphaCount = EqualsIgnoreCase(experimentClass.CoolingKind, "Geometric")
                ? Count(experimentClass.GeometricAlphaValues)
                : 1;

            return temperatureCount * alphaCount;
        }

        if (EqualsIgnoreCase(experimentClass.Algorithm, "ACO"))
        {
            var eliteCount = experimentClass.UseEliteAnts == true
                ? Count(experimentClass.EliteAntCountValues)
                : 1;

            return Count(experimentClass.AntCountValues)
                   * Count(experimentClass.AlphaValues)
                   * Count(experimentClass.BetaValues)
                   * Count(experimentClass.EvaporationRateValues)
                   * Count(experimentClass.QValues)
                   * Count(experimentClass.InitialPheromoneValues)
                   * eliteCount;
        }

        throw new InvalidOperationException($"Неизвестный алгоритм: {experimentClass.Algorithm}");
    }

    private static long Count<T>(IReadOnlyCollection<T>? values)
    {
        if (values is null || values.Count == 0)
            throw new InvalidOperationException("В конфиге обнаружен пустой список значений для обязательного параметра.");

        return values.Count;
    }

    private static IEnumerable<ParameterSet> EnumerateParameterSets(ExperimentClassConfig experimentClass)
    {
        if (EqualsIgnoreCase(experimentClass.Algorithm, "SA"))
        {
            var coolingKind = experimentClass.CoolingKind ?? throw new InvalidOperationException("Для SA обязателен CoolingKind.");
            var temperatureMode = experimentClass.TemperatureMode ?? throw new InvalidOperationException("Для SA обязателен TemperatureMode.");

            var geometricAlphas = EqualsIgnoreCase(coolingKind, "Geometric")
                ? experimentClass.GeometricAlphaValues ?? throw new InvalidOperationException("Для Geometric SA обязателен список GeometricAlphaValues.")
                : [0.95];

            if (EqualsIgnoreCase(temperatureMode, "Manual"))
            {
                var initialTemperatures = experimentClass.InitialTemperatureValues
                    ?? throw new InvalidOperationException("Для Manual SA обязателен список InitialTemperatureValues.");

                foreach (var initialTemperature in initialTemperatures)
                {
                    foreach (var geometricAlpha in geometricAlphas)
                    {
                        var parameterSet = new ParameterSet
                        {
                            ClassName = experimentClass.Name,
                            Algorithm = "SA",
                            CoolingKind = coolingKind,
                            TemperatureMode = "Manual",
                            InitialTemperature = initialTemperature,
                            GeometricAlpha = EqualsIgnoreCase(coolingKind, "Geometric") ? geometricAlpha : null,
                            UseEliteAnts = false
                        };

                        parameterSet.Id = BuildParameterSetId(parameterSet);
                        yield return parameterSet;
                    }
                }

                yield break;
            }

            if (EqualsIgnoreCase(temperatureMode, "Auto"))
            {
                var targetAcceptanceProbabilities = experimentClass.TargetAcceptanceProbabilityValues
                    ?? throw new InvalidOperationException("Для Auto SA обязателен список TargetAcceptanceProbabilityValues.");

                foreach (var targetAcceptanceProbability in targetAcceptanceProbabilities)
                {
                    foreach (var geometricAlpha in geometricAlphas)
                    {
                        var parameterSet = new ParameterSet
                        {
                            ClassName = experimentClass.Name,
                            Algorithm = "SA",
                            CoolingKind = coolingKind,
                            TemperatureMode = "Auto",
                            TargetAcceptanceProbability = targetAcceptanceProbability,
                            GeometricAlpha = EqualsIgnoreCase(coolingKind, "Geometric") ? geometricAlpha : null,
                            UseEliteAnts = false
                        };

                        parameterSet.Id = BuildParameterSetId(parameterSet);
                        yield return parameterSet;
                    }
                }

                yield break;
            }

            throw new InvalidOperationException($"Неизвестный режим температуры SA: {temperatureMode}");
        }

        if (EqualsIgnoreCase(experimentClass.Algorithm, "ACO"))
        {
            var useEliteAnts = experimentClass.UseEliteAnts == true;
            var antCounts = experimentClass.AntCountValues ?? throw new InvalidOperationException("Для ACO обязателен список AntCountValues.");
            var alphas = experimentClass.AlphaValues ?? throw new InvalidOperationException("Для ACO обязателен список AlphaValues.");
            var betas = experimentClass.BetaValues ?? throw new InvalidOperationException("Для ACO обязателен список BetaValues.");
            var evaporationRates = experimentClass.EvaporationRateValues ?? throw new InvalidOperationException("Для ACO обязателен список EvaporationRateValues.");
            var qs = experimentClass.QValues ?? throw new InvalidOperationException("Для ACO обязателен список QValues.");
            var initialPheromones = experimentClass.InitialPheromoneValues ?? throw new InvalidOperationException("Для ACO обязателен список InitialPheromoneValues.");
            var eliteAntCounts = useEliteAnts
                ? experimentClass.EliteAntCountValues ?? throw new InvalidOperationException("Для elite ACO обязателен список EliteAntCountValues.")
                : [0];

            foreach (var antCount in antCounts)
            {
                foreach (var alpha in alphas)
                {
                    foreach (var beta in betas)
                    {
                        foreach (var evaporationRate in evaporationRates)
                        {
                            foreach (var q in qs)
                            {
                                foreach (var initialPheromone in initialPheromones)
                                {
                                    foreach (var eliteAntCount in eliteAntCounts)
                                    {
                                        var parameterSet = new ParameterSet
                                        {
                                            ClassName = experimentClass.Name,
                                            Algorithm = "ACO",
                                            UseEliteAnts = useEliteAnts,
                                            AntCount = antCount,
                                            Alpha = alpha,
                                            Beta = beta,
                                            EvaporationRate = evaporationRate,
                                            Q = q,
                                            InitialPheromone = initialPheromone,
                                            EliteAntCount = useEliteAnts ? eliteAntCount : null
                                        };

                                        parameterSet.Id = BuildParameterSetId(parameterSet);
                                        yield return parameterSet;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            yield break;
        }

        throw new InvalidOperationException($"Неизвестный алгоритм: {experimentClass.Algorithm}");
    }

    private static void RunParameterSet(
        RunnerConfig config,
        ParameterSet parameterSet,
        IReadOnlyList<LoadedGraph> graphs,
        StreamWriter rawWriter)
    {
        foreach (var graph in graphs)
        {
            for (var seed = config.SeedStart; seed < config.SeedStart + config.SeedCount; seed++)
            {
                var result = RunSingleExperiment(config, parameterSet, graph, seed);
                var jsonLine = JsonSerializer.Serialize(result, JsonOptions);

                lock (RawWriterLock)
                {
                    rawWriter.WriteLine(jsonLine);
                }

                var completedRuns = Interlocked.Increment(ref _completedRuns);
                if (completedRuns % 100 == 0 || completedRuns == _plannedRuns)
                {
                    Console.WriteLine($"Выполнено запусков: {completedRuns}/{_plannedRuns}");
                }
            }
        }
    }

    private static RawRunRecord RunSingleExperiment(
        RunnerConfig config,
        ParameterSet parameterSet,
        LoadedGraph graph,
        int seed)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            SolverResult solverResult;

            if (EqualsIgnoreCase(parameterSet.Algorithm, "SA"))
            {
                var saConfig = new SimulatedAnnealingAlgorithmConfig
                {
                    Seed = seed,
                    MaxIterations = config.RunLimits.MaxIterations,
                    MaxIterationsWithoutImprovement = config.RunLimits.MaxIterationsWithoutImprovement,
                    InitialTemperature = EqualsIgnoreCase(parameterSet.TemperatureMode, "Manual")
                        ? parameterSet.InitialTemperature
                        : null,
                    TemperatureEstimationStartsCount = config.TemperatureEstimation.StartsCount,
                    TemperatureEstimationChainLength = config.TemperatureEstimation.ChainLength,
                    TemperatureEstimationFallbackTemperature = config.TemperatureEstimation.FallbackTemperature,
                    TemperatureEstimationTargetAcceptanceProbability = parameterSet.TargetAcceptanceProbability ?? 0.8,
                    CoolingKind = ParseCoolingKind(parameterSet.CoolingKind),
                    GeometricAlpha = parameterSet.GeometricAlpha ?? 0.95
                };

                var solver = TspSolverFactory.CreateSimulatedAnnealing(graph.Graph, saConfig);
                solver.Initialize();
                solverResult = solver.Run();
            }
            else if (EqualsIgnoreCase(parameterSet.Algorithm, "ACO"))
            {
                var acoConfig = new AntColonyAlgorithmConfig
                {
                    Seed = seed,
                    MaxIterations = config.RunLimits.MaxIterations,
                    MaxIterationsWithoutImprovement = config.RunLimits.MaxIterationsWithoutImprovement,
                    AntCount = parameterSet.AntCount ?? throw new InvalidOperationException("AntCount не задан."),
                    Alpha = parameterSet.Alpha ?? throw new InvalidOperationException("Alpha не задан."),
                    Beta = parameterSet.Beta ?? throw new InvalidOperationException("Beta не задан."),
                    EvaporationRate = parameterSet.EvaporationRate ?? throw new InvalidOperationException("EvaporationRate не задан."),
                    Q = parameterSet.Q ?? throw new InvalidOperationException("Q не задан."),
                    InitialPheromone = parameterSet.InitialPheromone ?? throw new InvalidOperationException("InitialPheromone не задан."),
                    UseEliteAnts = parameterSet.UseEliteAnts,
                    EliteAntCount = parameterSet.EliteAntCount ?? 1
                };

                var solver = TspSolverFactory.CreateAntColony(graph.Graph, acoConfig);
                solver.Initialize();
                solverResult = solver.Run();
            }
            else
            {
                throw new InvalidOperationException($"Неизвестный алгоритм: {parameterSet.Algorithm}");
            }

            stopwatch.Stop();

            return new RawRunRecord
            {
                ClassName = parameterSet.ClassName,
                ParameterSetId = parameterSet.Id,
                ParameterSet = parameterSet,
                GraphName = graph.Name,
                GraphPath = graph.Path,
                Seed = seed,
                HasFeasibleSolution = solverResult.HasFeasibleSolution,
                BestCost = solverResult.BestEvaluation?.Cost,
                Iterations = solverResult.Iterations,
                ObjectiveEvaluations = solverResult.ObjectiveEvaluations,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new RawRunRecord
            {
                ClassName = parameterSet.ClassName,
                ParameterSetId = parameterSet.Id,
                ParameterSet = parameterSet,
                GraphName = graph.Name,
                GraphPath = graph.Path,
                Seed = seed,
                HasFeasibleSolution = false,
                BestCost = null,
                Iterations = 0,
                ObjectiveEvaluations = 0,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Error = ex.ToString()
            };
        }
    }

    private static SimulatedAnnealingCoolingKind ParseCoolingKind(string? coolingKind)
    {
        if (EqualsIgnoreCase(coolingKind, "Geometric"))
            return SimulatedAnnealingCoolingKind.Geometric;

        if (EqualsIgnoreCase(coolingKind, "Cauchy"))
            return SimulatedAnnealingCoolingKind.Cauchy;

        throw new InvalidOperationException($"Неизвестный CoolingKind: {coolingKind}");
    }

    private static List<RawRunRecord> LoadRawRuns(string rawRunsPath)
    {
        var runs = new List<RawRunRecord>();

        foreach (var line in File.ReadLines(rawRunsPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var run = JsonSerializer.Deserialize<RawRunRecord>(line, JsonOptions);
            if (run is null)
                continue;

            runs.Add(run);
        }

        return runs;
    }

    private static List<GraphReferenceRecord> BuildReferences(
        IReadOnlyList<RawRunRecord> runs,
        IReadOnlyList<LoadedGraph> graphs,
        RunnerConfig config)
    {
        var references = new List<GraphReferenceRecord>();

        foreach (var graph in graphs)
        {
            var feasibleRuns = runs
                .Where(run => run.GraphName == graph.Name)
                .Where(run => string.IsNullOrWhiteSpace(run.Error))
                .Where(run => run.HasFeasibleSolution)
                .Where(run => run.BestCost.HasValue)
                .OrderBy(run => run.BestCost!.Value)
                .ThenBy(run => run.ElapsedMilliseconds)
                .ToList();

            if (feasibleRuns.Count == 0)
            {
                references.Add(new GraphReferenceRecord
                {
                    GraphName = graph.Name,
                    GraphPath = graph.Path,
                    ReferenceMode = config.Scoring.ReferenceMode,
                    ReferenceCost = null,
                    Error = "Для графа не найдено ни одного допустимого решения."
                });

                continue;
            }

            var bestRun = feasibleRuns[0];

            references.Add(new GraphReferenceRecord
            {
                GraphName = graph.Name,
                GraphPath = graph.Path,
                ReferenceMode = config.Scoring.ReferenceMode,
                ReferenceCost = bestRun.BestCost,
                SourceClassName = bestRun.ClassName,
                SourceParameterSetId = bestRun.ParameterSetId,
                SourceSeed = bestRun.Seed,
                SourceElapsedMilliseconds = bestRun.ElapsedMilliseconds,
                SourceIterations = bestRun.Iterations,
                SourceObjectiveEvaluations = bestRun.ObjectiveEvaluations
            });
        }

        return references;
    }

    private static List<ParameterSetSummary> BuildSummaries(
        IReadOnlyList<RawRunRecord> runs,
        IReadOnlyList<GraphReferenceRecord> references,
        RunnerConfig config)
    {
        var referencesByGraph = references.ToDictionary(reference => reference.GraphName, reference => reference);
        var summaries = new List<ParameterSetSummary>();

        foreach (var group in runs.GroupBy(run => run.ParameterSetId).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var groupRuns = group.ToList();
            var firstRun = groupRuns[0];
            var parameterSet = firstRun.ParameterSet ?? throw new InvalidOperationException("В raw result отсутствует ParameterSet.");
            var scoredRuns = groupRuns.Select(run => BuildScoredRun(run, referencesByGraph, config)).ToList();
            var feasibleRuns = groupRuns.Where(run => string.IsNullOrWhiteSpace(run.Error) && run.HasFeasibleSolution && run.BestCost.HasValue).ToList();
            var graphSummaries = new List<GraphSummary>();

            foreach (var graphGroup in groupRuns.GroupBy(run => run.GraphName).OrderBy(grouping => grouping.Key, StringComparer.Ordinal))
            {
                var graphRuns = graphGroup.ToList();
                var graphScoredRuns = graphRuns.Select(run => BuildScoredRun(run, referencesByGraph, config)).ToList();
                var graphFeasibleRuns = graphRuns.Where(run => string.IsNullOrWhiteSpace(run.Error) && run.HasFeasibleSolution && run.BestCost.HasValue).ToList();
                referencesByGraph.TryGetValue(graphGroup.Key, out var graphReference);

                graphSummaries.Add(new GraphSummary
                {
                    GraphName = graphGroup.Key,
                    Runs = graphRuns.Count,
                    FeasibleRuns = graphFeasibleRuns.Count,
                    FeasibilityRate = SafeDivide(graphFeasibleRuns.Count, graphRuns.Count),
                    ReferenceCost = graphReference?.ReferenceCost,
                    BestCost = graphFeasibleRuns.Count == 0 ? null : graphFeasibleRuns.Min(run => run.BestCost),
                    MeanBestCost = graphFeasibleRuns.Count == 0 ? null : graphFeasibleRuns.Average(run => run.BestCost!.Value),
                    MeanRelativeError = graphScoredRuns.Average(run => run.RelativeError),
                    MeanElapsedMilliseconds = graphRuns.Average(run => (double)run.ElapsedMilliseconds),
                    MeanIterations = graphRuns.Average(run => (double)run.Iterations),
                    MeanObjectiveEvaluations = graphRuns.Average(run => (double)run.ObjectiveEvaluations)
                });
            }

            summaries.Add(new ParameterSetSummary
            {
                ClassName = firstRun.ClassName,
                ParameterSetId = firstRun.ParameterSetId,
                ParameterSet = parameterSet,
                TotalRuns = groupRuns.Count,
                FeasibleRuns = feasibleRuns.Count,
                FeasibilityRate = SafeDivide(feasibleRuns.Count, groupRuns.Count),
                MeanScore = scoredRuns.Average(run => run.RelativeError),
                BestCostOverall = feasibleRuns.Count == 0 ? null : feasibleRuns.Min(run => run.BestCost),
                MeanBestCostOverall = feasibleRuns.Count == 0 ? null : feasibleRuns.Average(run => run.BestCost!.Value),
                MeanElapsedMilliseconds = groupRuns.Average(run => (double)run.ElapsedMilliseconds),
                MeanIterations = groupRuns.Average(run => (double)run.Iterations),
                MeanObjectiveEvaluations = groupRuns.Average(run => (double)run.ObjectiveEvaluations),
                Graphs = graphSummaries
            });
        }

        return summaries
            .OrderBy(summary => summary.ClassName, StringComparer.Ordinal)
            .ThenBy(summary => summary.MeanScore)
            .ThenBy(summary => summary.MeanElapsedMilliseconds)
            .ToList();
    }

    private static List<ClassTopConfigs> BuildTopConfigs(
        IReadOnlyList<ParameterSetSummary> summaries,
        int topKPerClass)
    {
        var topConfigs = new List<ClassTopConfigs>();

        foreach (var classGroup in summaries.GroupBy(summary => summary.ClassName).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var top = classGroup
                .OrderBy(summary => summary.MeanScore)
                .ThenByDescending(summary => summary.FeasibilityRate)
                .ThenBy(summary => summary.MeanElapsedMilliseconds)
                .Take(topKPerClass)
                .ToList();

            topConfigs.Add(new ClassTopConfigs
            {
                ClassName = classGroup.Key,
                Top = top
            });
        }

        return topConfigs;
    }

    private static ScoredRun BuildScoredRun(
        RawRunRecord run,
        IReadOnlyDictionary<string, GraphReferenceRecord> referencesByGraph,
        RunnerConfig config)
    {
        var relativeError = config.Scoring.InfeasiblePenalty;

        if (referencesByGraph.TryGetValue(run.GraphName, out var reference) &&
            reference.ReferenceCost.HasValue &&
            reference.ReferenceCost.Value > 0 &&
            string.IsNullOrWhiteSpace(run.Error) &&
            run.HasFeasibleSolution &&
            run.BestCost.HasValue)
        {
            relativeError = (run.BestCost.Value - reference.ReferenceCost.Value) / reference.ReferenceCost.Value;
            if (relativeError < 0)
                relativeError = 0;
        }

        return new ScoredRun
        {
            GraphName = run.GraphName,
            RelativeError = relativeError
        };
    }

    private static double SafeDivide(double numerator, double denominator)
        => denominator == 0 ? 0 : numerator / denominator;

    private static bool EqualsIgnoreCase(string? left, string? right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string BuildParameterSetId(ParameterSet parameterSet)
    {
        if (EqualsIgnoreCase(parameterSet.Algorithm, "SA"))
        {
            var parts = new List<string>
            {
                parameterSet.ClassName,
                parameterSet.Algorithm,
                parameterSet.CoolingKind ?? "",
                parameterSet.TemperatureMode ?? ""
            };

            if (parameterSet.InitialTemperature.HasValue)
                parts.Add($"t={FormatDouble(parameterSet.InitialTemperature.Value)}");

            if (parameterSet.TargetAcceptanceProbability.HasValue)
                parts.Add($"p={FormatDouble(parameterSet.TargetAcceptanceProbability.Value)}");

            if (parameterSet.GeometricAlpha.HasValue)
                parts.Add($"a={FormatDouble(parameterSet.GeometricAlpha.Value)}");

            return string.Join('|', parts);
        }

        if (EqualsIgnoreCase(parameterSet.Algorithm, "ACO"))
        {
            var parts = new List<string>
            {
                parameterSet.ClassName,
                parameterSet.Algorithm,
                $"ants={parameterSet.AntCount}",
                $"alpha={FormatDouble(parameterSet.Alpha ?? 0)}",
                $"beta={FormatDouble(parameterSet.Beta ?? 0)}",
                $"rho={FormatDouble(parameterSet.EvaporationRate ?? 0)}",
                $"q={FormatDouble(parameterSet.Q ?? 0)}",
                $"tau0={FormatDouble(parameterSet.InitialPheromone ?? 0)}",
                $"elite={parameterSet.UseEliteAnts}"
            };

            if (parameterSet.EliteAntCount.HasValue)
                parts.Add($"eliteCount={parameterSet.EliteAntCount.Value}");

            return string.Join('|', parts);
        }

        throw new InvalidOperationException($"Неизвестный алгоритм: {parameterSet.Algorithm}");
    }

    private static string FormatDouble(double value)
        => value.ToString("G17", CultureInfo.InvariantCulture);

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    private static string ResolveOutputDirectory(string baseDirectory, string runName)
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, baseDirectory, runName));
}

internal sealed class RunnerConfig
{
    public List<GraphConfig> Graphs { get; set; } = [];
    public int SeedStart { get; set; }
    public int SeedCount { get; set; }
    public int TopKPerClass { get; set; }
    public RunLimitsConfig RunLimits { get; set; } = new();
    public ScoringConfig Scoring { get; set; } = new();
    public TemperatureEstimationConfig TemperatureEstimation { get; set; } = new();
    public ExecutionConfig Execution { get; set; } = new();
    public OutputConfig Output { get; set; } = new();
    public List<ExperimentClassConfig> Classes { get; set; } = [];
}

internal sealed class GraphConfig
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

internal sealed class RunLimitsConfig
{
    public int MaxIterations { get; set; }
    public int? MaxIterationsWithoutImprovement { get; set; }
}

internal sealed class ScoringConfig
{
    public string ReferenceMode { get; set; } = "ExperimentalBest";
    public double InfeasiblePenalty { get; set; } = 1.0;
}

internal sealed class TemperatureEstimationConfig
{
    public int StartsCount { get; set; }
    public int ChainLength { get; set; }
    public double FallbackTemperature { get; set; }
}

internal sealed class ExecutionConfig
{
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
}

internal sealed class OutputConfig
{
    public string BaseDirectory { get; set; } = "../../../RunnerResults";
    public string RunName { get; set; } = "run-01";
    public bool SaveRawRuns { get; set; } = true;
    public bool SaveSummaries { get; set; } = true;
    public bool SaveTopConfigs { get; set; } = true;
    public bool SaveReferences { get; set; } = true;
}

internal sealed class ExperimentClassConfig
{
    public string Name { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public string? CoolingKind { get; set; }
    public string? TemperatureMode { get; set; }
    public bool? UseEliteAnts { get; set; }
    public List<double>? InitialTemperatureValues { get; set; }
    public List<double>? TargetAcceptanceProbabilityValues { get; set; }
    public List<double>? GeometricAlphaValues { get; set; }
    public List<int>? AntCountValues { get; set; }
    public List<double>? AlphaValues { get; set; }
    public List<double>? BetaValues { get; set; }
    public List<double>? EvaporationRateValues { get; set; }
    public List<double>? QValues { get; set; }
    public List<double>? InitialPheromoneValues { get; set; }
    public List<int>? EliteAntCountValues { get; set; }
}

internal sealed class LoadedGraph
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public IWeightedGraph Graph { get; set; } = default!;
}

internal sealed class ParameterSet
{
    public string Id { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public string? CoolingKind { get; set; }
    public string? TemperatureMode { get; set; }
    public double? InitialTemperature { get; set; }
    public double? TargetAcceptanceProbability { get; set; }
    public double? GeometricAlpha { get; set; }
    public bool UseEliteAnts { get; set; }
    public int? AntCount { get; set; }
    public double? Alpha { get; set; }
    public double? Beta { get; set; }
    public double? EvaporationRate { get; set; }
    public double? Q { get; set; }
    public double? InitialPheromone { get; set; }
    public int? EliteAntCount { get; set; }
}

internal sealed class RawRunRecord
{
    public string ClassName { get; set; } = string.Empty;
    public string ParameterSetId { get; set; } = string.Empty;
    public ParameterSet? ParameterSet { get; set; }
    public string GraphName { get; set; } = string.Empty;
    public string GraphPath { get; set; } = string.Empty;
    public int Seed { get; set; }
    public bool HasFeasibleSolution { get; set; }
    public double? BestCost { get; set; }
    public int Iterations { get; set; }
    public int ObjectiveEvaluations { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public string? Error { get; set; }
}

internal sealed class GraphReferenceRecord
{
    public string GraphName { get; set; } = string.Empty;
    public string GraphPath { get; set; } = string.Empty;
    public string ReferenceMode { get; set; } = string.Empty;
    public double? ReferenceCost { get; set; }
    public string? SourceClassName { get; set; }
    public string? SourceParameterSetId { get; set; }
    public int? SourceSeed { get; set; }
    public long? SourceElapsedMilliseconds { get; set; }
    public int? SourceIterations { get; set; }
    public int? SourceObjectiveEvaluations { get; set; }
    public string? Error { get; set; }
}

internal sealed class ParameterSetSummary
{
    public string ClassName { get; set; } = string.Empty;
    public string ParameterSetId { get; set; } = string.Empty;
    public ParameterSet ParameterSet { get; set; } = new();
    public int TotalRuns { get; set; }
    public int FeasibleRuns { get; set; }
    public double FeasibilityRate { get; set; }
    public double MeanScore { get; set; }
    public double? BestCostOverall { get; set; }
    public double? MeanBestCostOverall { get; set; }
    public double MeanElapsedMilliseconds { get; set; }
    public double MeanIterations { get; set; }
    public double MeanObjectiveEvaluations { get; set; }
    public List<GraphSummary> Graphs { get; set; } = [];
}

internal sealed class GraphSummary
{
    public string GraphName { get; set; } = string.Empty;
    public int Runs { get; set; }
    public int FeasibleRuns { get; set; }
    public double FeasibilityRate { get; set; }
    public double? ReferenceCost { get; set; }
    public double? BestCost { get; set; }
    public double? MeanBestCost { get; set; }
    public double MeanRelativeError { get; set; }
    public double MeanElapsedMilliseconds { get; set; }
    public double MeanIterations { get; set; }
    public double MeanObjectiveEvaluations { get; set; }
}

internal sealed class ClassTopConfigs
{
    public string ClassName { get; set; } = string.Empty;
    public List<ParameterSetSummary> Top { get; set; } = [];
}

internal sealed class ScoredRun
{
    public string GraphName { get; set; } = string.Empty;
    public double RelativeError { get; set; }
}
