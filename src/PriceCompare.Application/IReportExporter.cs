using PriceCompare.Core;

namespace PriceCompare.Application;

public interface IReportExporter
{
    Task ExportAsync(
        string destinationPath,
        ComparisonResult result,
        CancellationToken cancellationToken = default);
}
