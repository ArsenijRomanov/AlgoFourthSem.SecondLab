using TSP.ACO.Contexts;

namespace TSP.ACO.Abstractions;

public interface IAntColonyBuilder
{
    IReadOnlyList<AntRouteBuildResult> BuildColonyRoutes(AntColonyBuildContext context);
}
