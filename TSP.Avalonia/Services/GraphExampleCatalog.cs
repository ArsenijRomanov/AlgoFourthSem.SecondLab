using TSP.Avalonia.Models;

namespace TSP.Avalonia.Services;

public sealed class GraphExampleCatalog
{
    public IReadOnlyList<NamedOption<string>> GetExampleOptions()
        =>
        [
            new NamedOption<string>("SMALL6", "small6.stp"),
            new NamedOption<string>("BERLIN52", "berlin52.stp"),
            new NamedOption<string>("WORLD666", "world666.stp")
        ];

    public string? ResolveExamplePath(string fileName)
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "Examples", fileName),
            Path.Combine(baseDirectory, "TSP.Core", "Examples", fileName),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "TSP.Core", "Examples", fileName)),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "TSP.Core", "Examples", fileName)),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "TSP.Core", "Examples", fileName)),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "TSP.Core", "Examples", fileName))
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
