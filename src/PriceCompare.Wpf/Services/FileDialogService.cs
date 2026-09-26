using Microsoft.Win32;

namespace PriceCompare.Wpf.Services;

public sealed class FileDialogService : IFileDialogService
{
    public string? SelectInputFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select price list",
            Filter = "Price lists (*.xlsx;*.csv)|*.xlsx;*.csv|Excel Workbook (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SelectReportDestination()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export comparison report",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            FileName = $"PriceCompare-Report-{DateTime.Now:yyyyMMdd-HHmm}.xlsx"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
