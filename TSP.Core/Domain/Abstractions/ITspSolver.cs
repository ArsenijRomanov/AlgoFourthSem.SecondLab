namespace TSP.Domain.Abstractions;

public interface ITspSolver
{
    ISolverState State { get; }

    void Initialize();

    void Step();

    void Step(int steps);

    SolverResult Run();

    void Reset();
}
