using TSP.Domain;

namespace TSP.Avalonia.Services;

public sealed class RouteExportService
{
    public async Task ExportAsync(
        string path,
        string graphName,
        string algorithmTitle,
        Route route,
        RouteEvaluation evaluation)
    {
        var lines = new List<string>
        {
            $"Graph: {graphName}",
            $"Algorithm: {algorithmTitle}",
            $"Cost: {evaluation.Cost:G10}",
            $"Feasible: {(evaluation.IsFeasible ? "Yes" : "No")}",
            $"Route: {FormatRoute(route)}"
        };

        await File.WriteAllLinesAsync(path, lines);
    }

    public string FormatRoute(Route? route)
    {
        if (route is null)
        {
            return "—";
        }

        var values = route.Vertices.Select(static vertex => (vertex + 1).ToString()).ToList();
        if (values.Count > 0)
        {
            values.Add(values[0]);
        }

        return string.Join(" → ", values);
    }
}
