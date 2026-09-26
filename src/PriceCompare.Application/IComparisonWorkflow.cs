using PriceCompare.Core;

namespace PriceCompare.Application;

public interface IComparisonWorkflow
{
    Task<ComparisonResult> CompareAsync(
        string oldFilePath,
        ColumnMapping oldMapping,
        string newFilePath,
        ColumnMapping newMapping,
        CancellationToken cancellationToken = default);
}
