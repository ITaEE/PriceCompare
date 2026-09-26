using PriceCompare.Core;

namespace PriceCompare.Application;

public interface IPriceListFileService
{
    Task<FileInspection> InspectAsync(string filePath, CancellationToken cancellationToken = default);

    Task<ImportResult> ImportAsync(
        string filePath,
        ColumnMapping mapping,
        CancellationToken cancellationToken = default);
}
