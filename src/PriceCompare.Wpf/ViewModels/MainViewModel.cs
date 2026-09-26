using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using Microsoft.Extensions.Logging;
using PriceCompare.Application;
using PriceCompare.Core;
using PriceCompare.Wpf.Services;

namespace PriceCompare.Wpf.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IPriceListFileService _fileService;
    private readonly IComparisonWorkflow _workflow;
    private readonly IReportExporter _reportExporter;
    private readonly ColumnMappingSuggester _suggester;
    private readonly IFileDialogService _dialogs;
    private readonly ILogger<MainViewModel> _logger;

    private bool _isBusy;
    private string _statusText = "Select old and new price lists.";
    private string _selectedStatus = "All";
    private string _searchText = string.Empty;
    private ComparisonResult? _lastResult;

    public MainViewModel(
        IPriceListFileService fileService,
        IComparisonWorkflow workflow,
        IReportExporter reportExporter,
        ColumnMappingSuggester suggester,
        IFileDialogService dialogs,
        ILogger<MainViewModel> logger)
    {
        _fileService = fileService;
        _workflow = workflow;
        _reportExporter = reportExporter;
        _suggester = suggester;
        _dialogs = dialogs;
        _logger = logger;

        ResultsView = CollectionViewSource.GetDefaultView(Results);
        ResultsView.Filter = FilterResult;

        SelectOldFileCommand = new AsyncRelayCommand(
            ct => SelectFileAsync(OldFile, "old", ct),
            () => !IsBusy);

        SelectNewFileCommand = new AsyncRelayCommand(
            ct => SelectFileAsync(NewFile, "new", ct),
            () => !IsBusy);

        CompareCommand = new AsyncRelayCommand(
            CompareAsync,
            CanCompare);

        ExportReportCommand = new AsyncRelayCommand(
            ExportReportAsync,
            () => !IsBusy && _lastResult is not null);

        CancelCommand = new RelayCommand(Cancel, () => IsBusy);

        OldFile.PropertyChanged += SourceFilePropertyChanged;
        NewFile.PropertyChanged += SourceFilePropertyChanged;
    }

    public SourceFileViewModel OldFile { get; } = new();
    public SourceFileViewModel NewFile { get; } = new();

    public ObservableCollection<ComparisonItem> Results { get; } = [];
    public ICollectionView ResultsView { get; }
    public ObservableCollection<ValidationIssueDisplay> ValidationIssues { get; } = [];
    public ObservableCollection<DuplicateGroupDisplay> DuplicateGroups { get; } = [];

    public IReadOnlyList<string> StatusFilters { get; } =
        ["All", "Changed", "Added", "Removed", "Unchanged"];

    public AsyncRelayCommand SelectOldFileCommand { get; }
    public AsyncRelayCommand SelectNewFileCommand { get; }
    public AsyncRelayCommand CompareCommand { get; }
    public AsyncRelayCommand ExportReportCommand { get; }
    public RelayCommand CancelCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsInputEnabled));
                RaiseCommandStates();
            }
        }
    }

    public bool IsInputEnabled => !IsBusy;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value))
                ResultsView.Refresh();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ResultsView.Refresh();
        }
    }

    public int AddedCount => _lastResult?.Added ?? 0;
    public int RemovedCount => _lastResult?.Removed ?? 0;
    public int ChangedCount => _lastResult?.Changed ?? 0;
    public int UnchangedCount => _lastResult?.Unchanged ?? 0;
    public int ErrorCount => _lastResult?.Errors ?? 0;
    public int DuplicateCount => _lastResult?.Duplicates ?? 0;

    private async Task SelectFileAsync(SourceFileViewModel target, string sourceLabel, CancellationToken cancellationToken)
    {
        var path = _dialogs.SelectInputFile();
        if (string.IsNullOrWhiteSpace(path))
            return;

        await RunBusyAsync(
            $"Inspecting {sourceLabel} price list...",
            async () =>
            {
                var inspection = await _fileService.InspectAsync(path, cancellationToken);
                target.ApplyInspection(inspection, _suggester);
                StatusText = $"{sourceLabel[..1].ToUpperInvariant() + sourceLabel[1..]} file loaded: {inspection.DataRowCount:N0} rows.";
            });
    }

    private bool CanCompare()
    {
        if (IsBusy || !OldFile.HasFile || !NewFile.HasFile)
            return false;

        return ColumnMappingValidator.Validate(OldFile.CreateMapping(), OldFile.Headers).Count == 0
            && ColumnMappingValidator.Validate(NewFile.CreateMapping(), NewFile.Headers).Count == 0;
    }

    private async Task CompareAsync(CancellationToken cancellationToken)
    {
        await RunBusyAsync(
            "Comparing price lists...",
            async () =>
            {
                var result = await _workflow.CompareAsync(
                    OldFile.FilePath!,
                    OldFile.CreateMapping(),
                    NewFile.FilePath!,
                    NewFile.CreateMapping(),
                    cancellationToken);

                _lastResult = result;
                Results.Clear();
                ValidationIssues.Clear();
                DuplicateGroups.Clear();

                foreach (var item in result.Items)
                    Results.Add(item);

                AddValidationDetails("OLD", result.OldImport);
                AddValidationDetails("NEW", result.NewImport);

                ResultsView.Refresh();
                OnSummaryChanged();

                StatusText =
                    $"Comparison completed. Added: {result.Added:N0}, Removed: {result.Removed:N0}, Changed: {result.Changed:N0}, Unchanged: {result.Unchanged:N0}.";
            });
    }

    private async Task ExportReportAsync(CancellationToken cancellationToken)
    {
        if (_lastResult is null)
            return;

        var destination = _dialogs.SelectReportDestination();
        if (string.IsNullOrWhiteSpace(destination))
            return;

        await RunBusyAsync(
            "Exporting Excel report...",
            async () =>
            {
                await _reportExporter.ExportAsync(destination, _lastResult, cancellationToken);
                StatusText = "Report exported successfully.";
            });
    }

    private async Task RunBusyAsync(string status, Func<Task> operation)
    {
        IsBusy = true;
        StatusText = status;

        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Operation canceled.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning("UI operation failed. Exception type: {ExceptionType}.", ex.GetType().Name);
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private bool FilterResult(object item)
    {
        if (item is not ComparisonItem result)
            return false;

        if (!string.Equals(SelectedStatus, "All", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(result.Status.ToString(), SelectedStatus, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        var text = SearchText.Trim();

        return result.Sku.Contains(text, StringComparison.OrdinalIgnoreCase)
            || (result.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void Cancel()
    {
        SelectOldFileCommand.Cancel();
        SelectNewFileCommand.Cancel();
        CompareCommand.Cancel();
        ExportReportCommand.Cancel();
        StatusText = "Cancel requested.";
    }

    private void RaiseCommandStates()
    {
        SelectOldFileCommand.RaiseCanExecuteChanged();
        SelectNewFileCommand.RaiseCanExecuteChanged();
        CompareCommand.RaiseCanExecuteChanged();
        ExportReportCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }

    private void OnSummaryChanged()
    {
        OnPropertyChanged(nameof(AddedCount));
        OnPropertyChanged(nameof(RemovedCount));
        OnPropertyChanged(nameof(ChangedCount));
        OnPropertyChanged(nameof(UnchangedCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(DuplicateCount));
        ExportReportCommand.RaiseCanExecuteChanged();
    }

    private void SourceFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SourceFileViewModel.FilePath)
            or nameof(SourceFileViewModel.SkuColumn)
            or nameof(SourceFileViewModel.NameColumn)
            or nameof(SourceFileViewModel.PriceColumn)
            or nameof(SourceFileViewModel.StockColumn))
        {
            InvalidateComparisonResult();
        }

        RaiseCommandStates();
    }

    private void InvalidateComparisonResult()
    {
        if (_lastResult is null && Results.Count == 0 && ValidationIssues.Count == 0 && DuplicateGroups.Count == 0)
            return;

        _lastResult = null;
        Results.Clear();
        ValidationIssues.Clear();
        DuplicateGroups.Clear();
        ResultsView.Refresh();
        OnSummaryChanged();
        StatusText = "Inputs changed. Run comparison again.";
    }

    private void AddValidationDetails(string source, ImportResult import)
    {
        foreach (var issue in import.Issues.Where(x => x.Code != "DuplicateSku"))
        {
            ValidationIssues.Add(new ValidationIssueDisplay(
                source,
                issue.RowNumber,
                issue.Code,
                issue.Message));
        }

        foreach (var duplicate in import.Duplicates)
        {
            DuplicateGroups.Add(new DuplicateGroupDisplay(
                source,
                string.Join(", ", duplicate.RowNumbers),
                "Duplicate SKU",
                "Duplicate SKU rows were excluded from comparison."));
        }
    }
}
