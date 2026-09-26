using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PriceCompare.Application;
using PriceCompare.Core;
using PriceCompare.Infrastructure;
using PriceCompare.Wpf.Services;
using PriceCompare.Wpf.ViewModels;

namespace PriceCompare.Wpf;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddDebug();
        });

        services.AddSingleton<PriceComparisonEngine>();
        services.AddSingleton<ColumnMappingSuggester>();
        services.AddSingleton<IPriceListFileService, PriceListFileService>();
        services.AddSingleton<IComparisonWorkflow, ComparisonWorkflow>();
        services.AddSingleton<IReportExporter, ExcelReportExporter>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var window = _serviceProvider.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
