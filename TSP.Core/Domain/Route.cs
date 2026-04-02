namespace TSP.Domain;

public sealed class Route
{
    private readonly int[] _vertices;

    public Route(int[] vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        if (vertices.Length == 0)
            throw new ArgumentException("Маршрут не может быть пустым.", nameof(vertices));
        
        _vertices = vertices;
    }

    public IReadOnlyList<int> Vertices => _vertices;

    public int Count => _vertices.Length;

    public int this[int index] => _vertices[index];

    public int[] ToArray() => (int[])_vertices.Clone();
}
