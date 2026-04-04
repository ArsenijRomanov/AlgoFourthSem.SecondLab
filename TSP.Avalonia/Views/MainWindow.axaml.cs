using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TSP.Avalonia.Controls;
using TSP.Avalonia.ViewModels;

namespace TSP.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private GraphCanvasControl? GetGraphCanvas()
        => this.FindControl<GraphCanvasControl>("GraphCanvas");

    private MainWindowViewModel? GetViewModel()
        => DataContext as MainWindowViewModel;

    private void ZoomInPlotClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => GetGraphCanvas()?.ZoomIn();

    private void ZoomOutPlotClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => GetGraphCanvas()?.ZoomOut();

    private void ResetPlotViewClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => GetGraphCanvas()?.ResetView();

    private void BestRoutePeekPressed(object? sender, PointerPressedEventArgs e)
    {
        if (GetViewModel() is { } viewModel)
        {
            viewModel.ShowBestRoutePeek = true;
        }
    }

    private void BestRoutePeekReleased(object? sender, RoutedEventArgs e)
    {
        if (GetViewModel() is { } viewModel)
        {
            viewModel.ShowBestRoutePeek = false;
        }
    }

    private async void BrowseGraphClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (GetViewModel() is not { } viewModel || StorageProvider is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выбор STP-файла",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("STP Graph")
                {
                    Patterns = ["*.stp"]
                }
            ]
        });

        var file = files.FirstOrDefault();
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await viewModel.LoadGraphFromPathAsync(path);
        GetGraphCanvas()?.ResetView();
    }

    private void OpenAdvancedSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (GetViewModel() is not { } viewModel)
        {
            return;
        }

        var window = new AdvancedSettingsWindow
        {
            DataContext = viewModel
        };

        window.Show(this);
    }

    private void OpenHistoryClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (GetViewModel() is not { } viewModel || !viewModel.CanShowHistory)
        {
            return;
        }

        var window = new HistoryWindow
        {
            DataContext = viewModel
        };

        window.Show(this);
    }

    private async void ExportGraphImageClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (StorageProvider is null || GetGraphCanvas() is not { } graphCanvas)
        {
            return;
        }

        var path = await PickSavePathAsync("graph.png", [new FilePickerFileType("PNG") { Patterns = ["*.png"] }]);
        if (path is null)
        {
            return;
        }

        await graphCanvas.ExportPngAsync(path);
    }

    private async void ExportRouteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (StorageProvider is null || GetViewModel() is not { } viewModel)
        {
            return;
        }

        var path = await PickSavePathAsync("best-route.txt", [new FilePickerFileType("Text") { Patterns = ["*.txt"] }]);
        if (path is null)
        {
            return;
        }

        await viewModel.ExportRouteAsync(path);
    }

    private async Task<string?> PickSavePathAsync(string suggestedName, IReadOnlyList<FilePickerFileType> fileTypes)
    {
        if (StorageProvider is null)
        {
            return null;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранение файла",
            SuggestedFileName = suggestedName,
            FileTypeChoices = fileTypes
        });

        return file?.TryGetLocalPath();
    }
}
