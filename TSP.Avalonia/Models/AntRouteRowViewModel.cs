using TSP.ACO;

namespace TSP.Avalonia.Models;

public sealed class AntRouteRowViewModel
{
    public required int Rank { get; init; }

    public required string CostText { get; init; }

    public required string FeasibilityText { get; init; }

    public required string CompletionText { get; init; }

    public required string RouteText { get; init; }

    public required AntRouteBuildResult Source { get; init; }
}
