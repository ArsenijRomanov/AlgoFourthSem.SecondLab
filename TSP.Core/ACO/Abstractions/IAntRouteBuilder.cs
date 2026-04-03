using TSP.ACO.Contexts;

namespace TSP.ACO.Abstractions;

public interface IAntRouteBuilder
{
    AntRouteBuildResult BuildRoute(AntRouteBuilderContext context);
}
