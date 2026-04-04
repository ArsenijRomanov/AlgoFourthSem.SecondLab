using Avalonia.Controls;

namespace TSP.Avalonia.Views;

public partial class AdvancedSettingsWindow : Window
{
    public AdvancedSettingsWindow()
    {
        InitializeComponent();
    }

    private void CloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close();
}
