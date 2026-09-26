namespace PriceCompare.Wpf.Services;

public interface IFileDialogService
{
    string? SelectInputFile();
    string? SelectReportDestination();
}
