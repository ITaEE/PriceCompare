using System.Collections.ObjectModel;
using PriceCompare.Application;
using PriceCompare.Core;

namespace PriceCompare.Wpf.ViewModels;

public sealed class SourceFileViewModel : ObservableObject
{
    private string? _filePath;
    private int _rowCount;
    private string? _skuColumn;
    private string? _nameColumn;
    private string? _priceColumn;
    private string? _stockColumn;

    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(HasFile));
            }
        }
    }

    public string FileName => string.IsNullOrWhiteSpace(FilePath)
        ? "No file selected"
        : System.IO.Path.GetFileName(FilePath);

    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    public int RowCount
    {
        get => _rowCount;
        set => SetProperty(ref _rowCount, value);
    }

    public ObservableCollection<string> Headers { get; } = [];

    public ObservableCollection<string?> OptionalHeaders { get; } = [null];

    public string? SkuColumn
    {
        get => _skuColumn;
        set => SetProperty(ref _skuColumn, value);
    }

    public string? NameColumn
    {
        get => _nameColumn;
        set => SetProperty(ref _nameColumn, value);
    }

    public string? PriceColumn
    {
        get => _priceColumn;
        set => SetProperty(ref _priceColumn, value);
    }

    public string? StockColumn
    {
        get => _stockColumn;
        set => SetProperty(ref _stockColumn, value);
    }

    public void ApplyInspection(FileInspection inspection, ColumnMappingSuggester suggester)
    {
        FilePath = inspection.FilePath;
        RowCount = inspection.DataRowCount;

        Headers.Clear();
        OptionalHeaders.Clear();
        OptionalHeaders.Add(null);

        foreach (var header in inspection.Headers)
        {
            Headers.Add(header);
            OptionalHeaders.Add(header);
        }

        var suggestion = suggester.Suggest(inspection.Headers);
        SkuColumn = suggestion.SkuColumn;
        NameColumn = suggestion.NameColumn;
        PriceColumn = suggestion.PriceColumn;
        StockColumn = suggestion.StockColumn;
    }

    public ColumnMapping CreateMapping() =>
        new(
            SkuColumn ?? string.Empty,
            NameColumn,
            PriceColumn ?? string.Empty,
            StockColumn);
}
