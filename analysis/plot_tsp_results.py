from __future__ import annotations

import argparse
from pathlib import Path

import matplotlib.pyplot as plt
import pandas as pd

# python plot_tsp_results.py "/путь/до/Results/compare_*"

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Строит графики по результатам TSP-экспериментов.")
    parser.add_argument(
        "run_dir",
        nargs="?",
        default=None,
        help="Путь до Results/<runName>. Если не указан, будет взят последний запуск из ./Results.")
    args = parser.parse_args()

    run_dir = resolve_run_dir(args.run_dir)
    runs_path = run_dir / "runs.csv"
    summary_path = run_dir / "summary.csv"

    if not runs_path.exists():
        raise FileNotFoundError(f"Не найден файл: {runs_path}")

    runs = pd.read_csv(runs_path)
    summary = pd.read_csv(summary_path) if summary_path.exists() else build_summary(runs)

    plots_dir = run_dir / "plots"
    plots_dir.mkdir(parents=True, exist_ok=True)

    print(f"Каталог запуска: {run_dir}")
    print(f"Каталог графиков: {plots_dir}")

    print("\nСводка:")
    display_columns = [
        "graph",
        "algorithm",
        "feasibleRunCount",
        "meanBestCost",
        "minBestCost",
        "meanElapsedMilliseconds",
    ]
    existing_columns = [column for column in display_columns if column in summary.columns]
    print(summary[existing_columns].to_string(index=False))

    for graph_name, graph_runs in runs.groupby("graph"):
        graph_summary = summary[summary["graph"] == graph_name].copy()

        plot_best_cost_boxplot(graph_name, graph_runs, plots_dir)
        plot_mean_time_bar(graph_name, graph_summary, plots_dir)
        plot_mean_evaluations_bar(graph_name, graph_summary, plots_dir)
        plot_convergence(graph_name, graph_runs, run_dir, plots_dir)
        plot_final_cost_vs_evaluations(graph_name, graph_runs, plots_dir)
        plot_convergence_by_evaluations(graph_name, graph_runs, run_dir, plots_dir)

    plot_global_min_cost(summary, plots_dir)

    print("\nГотово.")


def resolve_run_dir(argument: str | None) -> Path:
    if argument:
        candidate = Path(argument).expanduser().resolve()
        if candidate.is_dir() and (candidate / "runs.csv").exists():
            return candidate
        raise FileNotFoundError(f"Не найден каталог запуска с runs.csv: {candidate}")

    cwd = Path.cwd()
    results_dir = cwd / "Results"
    if results_dir.exists():
        candidates = [path for path in results_dir.iterdir() if path.is_dir() and (path / "runs.csv").exists()]
        if candidates:
            return max(candidates, key=lambda path: path.stat().st_mtime)

    raise FileNotFoundError(
        "Не удалось автоматически найти запуск. Передай путь до Results/<runName> первым аргументом.")


def build_summary(runs: pd.DataFrame) -> pd.DataFrame:
    rows: list[dict[str, object]] = []

    grouped = runs.groupby(["graph", "algorithm", "kind"], dropna=False)
    for (graph, algorithm, kind), group in grouped:
        feasible = group[group["isFeasible"] == True].copy()
        costs = feasible["bestCost"].dropna()

        rows.append(
            {
                "graph": graph,
                "algorithm": algorithm,
                "kind": kind,
                "runCount": len(group),
                "feasibleRunCount": len(feasible),
                "meanBestCost": costs.mean() if not costs.empty else None,
                "minBestCost": costs.min() if not costs.empty else None,
                "maxBestCost": costs.max() if not costs.empty else None,
                "meanElapsedMilliseconds": group["elapsedMilliseconds"].mean(),
                "meanObjectiveEvaluations": group["objectiveEvaluations"].mean(),
            }
        )

    return pd.DataFrame(rows)


def plot_best_cost_boxplot(graph_name: str, runs: pd.DataFrame, plots_dir: Path) -> None:
    feasible = runs[runs["isFeasible"] == True].copy()
    if feasible.empty:
        return

    algorithms = sorted(feasible["algorithm"].dropna().unique().tolist())
    series = []
    labels = []

    for algorithm in algorithms:
        values = feasible.loc[feasible["algorithm"] == algorithm, "bestCost"].dropna()
        if values.empty:
            continue
        series.append(values.tolist())
        labels.append(algorithm)

    if not series:
        return

    figure = plt.figure(figsize=(10, 6))
    plt.boxplot(series, labels=labels)
    plt.title(f"{graph_name}: распределение лучшей стоимости")
    plt.xlabel("Алгоритм")
    plt.ylabel("Лучшая стоимость")
    plt.xticks(rotation=20)
    plt.tight_layout()
    figure.savefig(plots_dir / f"{graph_name}_best_cost_boxplot.png", dpi=160)
    plt.close(figure)


def plot_mean_time_bar(graph_name: str, summary: pd.DataFrame, plots_dir: Path) -> None:
    if summary.empty or "meanElapsedMilliseconds" not in summary.columns:
        return

    ordered = summary.sort_values("algorithm")
    figure = plt.figure(figsize=(10, 6))
    plt.bar(ordered["algorithm"], ordered["meanElapsedMilliseconds"])
    plt.title(f"{graph_name}: среднее время работы")
    plt.xlabel("Алгоритм")
    plt.ylabel("Среднее время, мс")
    plt.xticks(rotation=20)
    plt.tight_layout()
    figure.savefig(plots_dir / f"{graph_name}_mean_time.png", dpi=160)
    plt.close(figure)


def plot_mean_evaluations_bar(graph_name: str, summary: pd.DataFrame, plots_dir: Path) -> None:
    if summary.empty or "meanObjectiveEvaluations" not in summary.columns:
        return

    ordered = summary.sort_values("algorithm")
    figure = plt.figure(figsize=(10, 6))
    plt.bar(ordered["algorithm"], ordered["meanObjectiveEvaluations"])
    plt.title(f"{graph_name}: среднее число вычислений целевой функции")
    plt.xlabel("Алгоритм")
    plt.ylabel("Среднее число вычислений")
    plt.xticks(rotation=20)
    plt.tight_layout()
    figure.savefig(plots_dir / f"{graph_name}_mean_evaluations.png", dpi=160)
    plt.close(figure)


def plot_convergence(graph_name: str, runs: pd.DataFrame, run_dir: Path, plots_dir: Path) -> None:
    graph_runs = runs[runs["graph"] == graph_name].copy()
    if graph_runs.empty or "historyFile" not in graph_runs.columns:
        return

    figure = plt.figure(figsize=(10, 6))
    has_any_line = False

    for algorithm, algorithm_runs in graph_runs.groupby("algorithm"):
        histories = []

        for _, row in algorithm_runs.iterrows():
            history_file = row.get("historyFile")
            if not isinstance(history_file, str) or not history_file.strip():
                continue

            history_path = run_dir / history_file
            if not history_path.exists():
                continue

            history = pd.read_csv(history_path)
            if history.empty or "bestCost" not in history.columns:
                continue

            history = history[["iteration", "bestCost"]].copy()
            history = history.dropna(subset=["bestCost"])
            if history.empty:
                continue

            history = history.rename(columns={"bestCost": f"bestCost_{len(histories)}"})
            histories.append(history)

        if not histories:
            continue

        merged = histories[0]
        for history in histories[1:]:
            merged = merged.merge(history, on="iteration", how="outer")

        merged = merged.sort_values("iteration")
        value_columns = [column for column in merged.columns if column != "iteration"]
        merged["meanBestCost"] = merged[value_columns].mean(axis=1, skipna=True)
        merged["meanBestCost"] = merged["meanBestCost"].ffill()
        merged = merged.dropna(subset=["meanBestCost"])

        if merged.empty:
            continue

        plt.plot(merged["iteration"], merged["meanBestCost"], label=algorithm)
        has_any_line = True

    if not has_any_line:
        plt.close(figure)
        return

    plt.title(f"{graph_name}: средняя сходимость по seed")
    plt.xlabel("Итерация")
    plt.ylabel("Средняя лучшая стоимость")
    plt.legend()
    plt.tight_layout()
    figure.savefig(plots_dir / f"{graph_name}_convergence.png", dpi=160)
    plt.close(figure)


def plot_final_cost_vs_evaluations(graph_name: str, runs: pd.DataFrame, plots_dir: Path) -> None:
    graph_runs = runs[runs["graph"] == graph_name].copy()
    if graph_runs.empty:
        return

    required_columns = {"objectiveEvaluations", "bestCost", "algorithm", "isFeasible"}
    if not required_columns.issubset(graph_runs.columns):
        return

    graph_runs = graph_runs[graph_runs["isFeasible"] == True].copy()
    graph_runs = graph_runs.dropna(subset=["objectiveEvaluations", "bestCost", "algorithm"])
    if graph_runs.empty:
        return

    figure = plt.figure(figsize=(10, 6))
    has_any_points = False

    for algorithm, algorithm_runs in graph_runs.groupby("algorithm"):
        if algorithm_runs.empty:
            continue

        plt.scatter(
            algorithm_runs["objectiveEvaluations"],
            algorithm_runs["bestCost"],
            label=algorithm)

        has_any_points = True

    if not has_any_points:
        plt.close(figure)
        return

    plt.title(f"{graph_name}: лучшая стоимость от числа вычислений функции")
    plt.xlabel("Число вычислений целевой функции")
    plt.ylabel("Лучшая стоимость")
    plt.legend()
    plt.tight_layout()
    figure.savefig(plots_dir / f"{graph_name}_final_cost_vs_evaluations.png", dpi=160)
    plt.close(figure)


def plot_convergence_by_evaluations(graph_name: str, runs: pd.DataFrame, run_dir: Path, plots_dir: Path) -> None:
    graph_runs = runs[runs["graph"] == graph_name].copy()
    if graph_runs.empty or "historyFile" not in graph_runs.columns:
        return

    figure = plt.figure(figsize=(10, 6))
    has_any_line = False

    for algorithm, algorithm_runs in graph_runs.groupby("algorithm"):
        histories = []

        for _, row in algorithm_runs.iterrows():
            history_file = row.get("historyFile")
            if not isinstance(history_file, str) or not history_file.strip():
                continue

            history_path = run_dir / history_file
            if not history_path.exists():
                continue

            history = pd.read_csv(history_path)
            required_columns = {"objectiveEvaluations", "bestCost"}
            if history.empty or not required_columns.issubset(history.columns):
                continue

            history = history[["objectiveEvaluations", "bestCost"]].copy()
            history = history.dropna(subset=["objectiveEvaluations", "bestCost"])
            if history.empty:
                continue

            history = history.rename(columns={"bestCost": f"bestCost_{len(histories)}"})
            histories.append(history)

        if not histories:
            continue

        merged = histories[0]
        for history in histories[1:]:
            merged = merged.merge(history, on="objectiveEvaluations", how="outer")

        merged = merged.sort_values("objectiveEvaluations")
        value_columns = [column for column in merged.columns if column != "objectiveEvaluations"]
        merged["meanBestCost"] = merged[value_columns].mean(axis=1, skipna=True)
        merged["meanBestCost"] = merged["meanBestCost"].ffill()
        merged = merged.dropna(subset=["meanBestCost"])

        if merged.empty:
            continue

        plt.plot(merged["objectiveEvaluations"], merged["meanBestCost"], label=algorithm)
        has_any_line = True

    if not has_any_line:
        plt.close(figure)
        return

    plt.title(f"{graph_name}: средняя лучшая стоимость от числа вычислений функции")
    plt.xlabel("Число вычислений целевой функции")
    plt.ylabel("Средняя лучшая стоимость")
    plt.legend()
    plt.tight_layout()
    figure.savefig(plots_dir / f"{graph_name}_convergence_by_evaluations.png", dpi=160)
    plt.close(figure)


def plot_global_min_cost(summary: pd.DataFrame, plots_dir: Path) -> None:
    if summary.empty or "minBestCost" not in summary.columns:
        return

    pivot = summary.pivot(index="graph", columns="algorithm", values="minBestCost")
    if pivot.empty:
        return

    figure = plt.figure(figsize=(10, 6))
    image = plt.imshow(pivot.values, aspect="auto")
    plt.colorbar(image, label="Минимальная лучшая стоимость")
    plt.xticks(range(len(pivot.columns)), pivot.columns, rotation=20)
    plt.yticks(range(len(pivot.index)), pivot.index)
    plt.title("Минимальная стоимость: граф × алгоритм")
    plt.tight_layout()
    figure.savefig(plots_dir / "global_min_cost_heatmap.png", dpi=160)
    plt.close(figure)


if __name__ == "__main__":
    main()