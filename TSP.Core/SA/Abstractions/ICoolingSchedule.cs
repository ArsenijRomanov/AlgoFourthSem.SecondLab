namespace TSP.SA.Abstractions;

public interface ICoolingSchedule
{
    double GetNextTemperature(
        double currentTemperature,
        double initialTemperature,
        int iteration);
}
