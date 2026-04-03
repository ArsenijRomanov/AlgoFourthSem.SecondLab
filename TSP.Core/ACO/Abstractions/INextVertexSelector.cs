using TSP.ACO.Contexts;

namespace TSP.ACO.Abstractions;

public interface INextVertexSelector
{
    int? SelectNextVertex(AntNextVertexSelectionContext context);
}
