using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TSP.Avalonia.Services;
using TSP.Avalonia.ViewModels;
using TSP.Avalonia.Views;

namespace TSP.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var parser = new StpGraphParser();
            var layout = new GraphLayoutService();
            var examples = new GraphExampleCatalog();
            var session = new TspExperimentSession();
            var exporter = new RouteExportService();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(session, parser, layout, examples, exporter)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
