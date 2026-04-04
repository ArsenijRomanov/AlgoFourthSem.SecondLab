using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TSP.Avalonia.Views;

public partial class AdvancedSettingsWindow : Window
{
    public AdvancedSettingsWindow()
    {
        InitializeComponent();
    }

    public void CloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}