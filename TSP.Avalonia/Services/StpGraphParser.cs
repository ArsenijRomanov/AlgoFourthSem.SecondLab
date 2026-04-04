using System.Globalization;
using Avalonia;
using TSP.Avalonia.Models;
using TSP.Domain;

namespace TSP.Avalonia.Services;

public sealed class StpGraphParser
{
    public LoadedGraph Parse(string path, GraphLayoutService layoutService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(layoutService);

        var lines = File.ReadAllLines(path);
        if (lines.Length == 0 || !lines[0].Contains("STP Format Version", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Файл не похож на STP 1.00.");
        }

        var name = Path.GetFileNameWithoutExtension(path);
        var declaredNodes = 0;
        var isDirected = false;
        var weights = Array.Empty<(int From, int To, double Weight)>();
        var coordinates = new List<Point>();
        var edgeBuffer = new List<(int From, int To, double Weight)>();
        var parserState = ParserState.None;

        foreach (var rawLine in lines.Select(static value => value.Trim()))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            if (rawLine.StartsWith("Name", StringComparison.OrdinalIgnoreCase))
            {
                name = ExtractQuotedValue(rawLine) ?? name;
                continue;
            }

            if (rawLine.Equals("Section Graph", StringComparison.OrdinalIgnoreCase))
            {
                parserState = ParserState.Graph;
                continue;
            }

            if (rawLine.Equals("Section Coordinates", StringComparison.OrdinalIgnoreCase))
            {
                parserState = ParserState.Coordinates;
                continue;
            }

            if (rawLine.Equals("Section Comment", StringComparison.OrdinalIgnoreCase))
            {
                parserState = ParserState.Comment;
                continue;
            }

            if (rawLine.Equals("END", StringComparison.OrdinalIgnoreCase) || rawLine.Equals("End", StringComparison.OrdinalIgnoreCase))
            {
                parserState = ParserState.None;
                continue;
            }

            if (rawLine.Equals("EOF", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            switch (parserState)
            {
                case ParserState.Graph:
                    if (rawLine.StartsWith("Nodes", StringComparison.OrdinalIgnoreCase))
                    {
                        declaredNodes = ParseInt(rawLine[5..]);
                        continue;
                    }

                    if (rawLine.StartsWith("Arcs", StringComparison.OrdinalIgnoreCase))
                    {
                        isDirected = true;
                        continue;
                    }

                    if (rawLine.StartsWith("Edges", StringComparison.OrdinalIgnoreCase))
                    {
                        isDirected = false;
                        continue;
                    }

                    if (rawLine.StartsWith("A ", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = Split(rawLine);
                        edgeBuffer.Add((ParseInt(parts[1]) - 1, ParseInt(parts[2]) - 1, ParseDouble(parts[3])));
                        isDirected = true;
                        continue;
                    }

                    if (rawLine.StartsWith("E ", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = Split(rawLine);
                        var from = ParseInt(parts[1]) - 1;
                        var to = ParseInt(parts[2]) - 1;
                        var weight = ParseDouble(parts[3]);
                        edgeBuffer.Add((from, to, weight));
                        edgeBuffer.Add((to, from, weight));
                        continue;
                    }

                    break;

                case ParserState.Coordinates:
                    if (rawLine.StartsWith("DD ", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = Split(rawLine);
                        if (parts.Length == 3)
                        {
                            coordinates.Add(new Point(ParseDouble(parts[1]), ParseDouble(parts[2])));
                        }
                        else if (parts.Length >= 4)
                        {
                            coordinates.Add(new Point(ParseDouble(parts[^2]), ParseDouble(parts[^1])));
                        }

                        continue;
                    }

                    break;
            }
        }

        if (declaredNodes <= 0)
        {
            throw new InvalidOperationException("В файле не указано число вершин.");
        }

        weights = edgeBuffer.ToArray();
        var matrix = new double?[declaredNodes, declaredNodes];
        foreach (var edge in weights)
        {
            matrix[edge.From, edge.To] = edge.Weight;
        }

        var graph = new AdjacencyMatrixGraph(matrix);
        var positions = coordinates.Count == declaredNodes
            ? coordinates.Select((point, index) => new VertexPosition(index, point)).ToArray()
            : layoutService.CreateCircularLayout(declaredNodes);

        return new LoadedGraph
        {
            Name = name,
            SourcePath = path,
            Graph = graph,
            Positions = positions,
            IsDirected = isDirected,
            EdgeCount = isDirected ? edgeBuffer.Count : edgeBuffer.Count / 2
        };
    }

    private static string[] Split(string rawLine)
        => rawLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int ParseInt(string value)
        => int.Parse(value.Trim(), CultureInfo.InvariantCulture);

    private static double ParseDouble(string value)
        => double.Parse(value.Trim(), CultureInfo.InvariantCulture);

    private static string? ExtractQuotedValue(string rawLine)
    {
        var first = rawLine.IndexOf('"');
        var last = rawLine.LastIndexOf('"');
        if (first < 0 || last <= first)
        {
            return null;
        }

        return rawLine[(first + 1)..last];
    }

    private enum ParserState
    {
        None,
        Comment,
        Graph,
        Coordinates
    }
}
