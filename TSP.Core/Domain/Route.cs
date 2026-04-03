namespace TSP.Domain;

public sealed class Route
{
    private readonly int[] _vertices;
    public IReadOnlyList<int> Vertices => _vertices;
    public int Count => _vertices.Length;
    public int this[int index] => _vertices[index];
    public int[] ToArray() => (int[])_vertices.Clone();

    public Route(int[] vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        if (vertices.Length == 0)
            throw new ArgumentException("Маршрут не может быть пустым.", nameof(vertices));

        ValidatePermutation(vertices, nameof(vertices));

        _vertices = vertices;
    }

    private static void ValidatePermutation(int[] vertices, string paramName)
    {
        var seen = new bool[vertices.Length];

        foreach (var vertex in vertices)
        {
            if (vertex < 0 || vertex >= vertices.Length)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    $"Вершина {vertex} находится вне допустимого диапазона [0, {vertices.Length - 1}].");
            }

            if (seen[vertex])
            {
                throw new ArgumentException(
                    $"Маршрут содержит повторяющуюся вершину {vertex}.",
                    paramName);
            }

            seen[vertex] = true;
        }
    }
}
