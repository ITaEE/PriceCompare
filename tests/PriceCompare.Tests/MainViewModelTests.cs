using Microsoft.Extensions.Logging.Abstractions;
using PriceCompare.Application;
using PriceCompare.Core;
using PriceCompare.Wpf.Services;
using PriceCompare.Wpf.ViewModels;

namespace PriceCompare.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void Changing_mapping_invalidates_previous_result_and_disables_export()
    {
        var result = new PriceComparisonEngine().Compare(
            TestData.Import(TestData.Row(2, "A-1", 100m)),
            TestData.Import(TestData.Row(2, "A-1", 120m)));
        var viewModel = new MainViewModel(
            new NoopFileService(),
            new FixedWorkflow(result),
            new NoopReportExporter(),
            new ColumnMappingSuggester(),
            new NoopFileDialogService(),
            NullLogger<MainViewModel>.Instance);

        Configure(viewModel.OldFile, "old.csv");
        Configure(viewModel.NewFile, "new.csv");

        viewModel.CompareCommand.Execute(null);

        Assert.True(viewModel.ExportReportCommand.CanExecute(null));
        Assert.Single(viewModel.Results);

        viewModel.NewFile.PriceColumn = "ReplacementPrice";

        Assert.False(viewModel.ExportReportCommand.CanExecute(null));
        Assert.Empty(viewModel.Results);
        Assert.Equal(0, viewModel.ChangedCount);
        Assert.Equal("Inputs changed. Run comparison again.", viewModel.StatusText);
    }

    private static void Configure(SourceFileViewModel source, string path)
    {
        source.FilePath = path;
        source.Headers.Add("SKU");
        source.Headers.Add("Price");
        source.SkuColumn = "SKU";
        source.PriceColumn = "Price";
    }

    private sealed class NoopFileService : IPriceListFileService
    {
        public Task<FileInspection> InspectAsync(string filePath, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ImportResult> ImportAsync(
            string filePath,
            ColumnMapping mapping,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedWorkflow(ComparisonResult result) : IComparisonWorkflow
    {
        public Task<ComparisonResult> CompareAsync(
            string oldFilePath,
            ColumnMapping oldMapping,
            string newFilePath,
            ColumnMapping newMapping,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class NoopReportExporter : IReportExporter
    {
        public Task ExportAsync(
            string destinationPath,
            ComparisonResult result,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoopFileDialogService : IFileDialogService
    {
        public string? SelectInputFile() => null;
        public string? SelectReportDestination() => null;
    }
}
