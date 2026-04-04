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

    public void ZoomInPlotClick(object? sender, RoutedEventArgs e)
        => GetGraphCanvas()?.ZoomIn();

    public void ZoomOutPlotClick(object? sender, RoutedEventArgs e)
        => GetGraphCanvas()?.ZoomOut();

    public void ResetPlotViewClick(object? sender, RoutedEventArgs e)
        => GetGraphCanvas()?.ResetView();

    public void BestRoutePeekPressed(object? sender, PointerPressedEventArgs e)
    {
        if (GetViewModel() is { } viewModel)
        {
            viewModel.ShowBestRoutePeek = true;
        }
    }

    public void BestRoutePeekReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (GetViewModel() is { } viewModel)
        {
            viewModel.ShowBestRoutePeek = false;
        }
    }


    public void BestRoutePeekCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (GetViewModel() is { } viewModel)
        {
            viewModel.ShowBestRoutePeek = false;
        }
    }

    public async void BrowseGraphClick(object? sender, RoutedEventArgs e)
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

    public void OpenAdvancedSettingsClick(object? sender, RoutedEventArgs e)
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

    public void OpenHistoryClick(object? sender, RoutedEventArgs e)
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

    public async void ExportGraphImageClick(object? sender, RoutedEventArgs e)
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

    public async void ExportRouteClick(object? sender, RoutedEventArgs e)
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
