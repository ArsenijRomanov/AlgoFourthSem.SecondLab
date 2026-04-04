namespace TSP.Avalonia.Models;

public sealed class NamedOption<T>
{
    public NamedOption(string title, T value)
    {
        Title = title;
        Value = value;
    }

    public string Title { get; }

    public T Value { get; }

    public override string ToString()
        => Title;
}
