namespace TSP.Domain.Abstractions;

public interface IRouteEvaluator
{
    RouteEvaluation Evaluate(Route route);
}
