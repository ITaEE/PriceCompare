using Microsoft.Extensions.Logging;
using PriceCompare.Core;

namespace PriceCompare.Application;

public sealed class ComparisonWorkflow(
    IPriceListFileService fileService,
    PriceComparisonEngine comparisonEngine,
    ILogger<ComparisonWorkflow> logger) : IComparisonWorkflow
{
    public async Task<ComparisonResult> CompareAsync(
        string oldFilePath,
        ColumnMapping oldMapping,
        string newFilePath,
        ColumnMapping newMapping,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Comparison started.");

        var oldTask = fileService.ImportAsync(oldFilePath, oldMapping, cancellationToken);
        var newTask = fileService.ImportAsync(newFilePath, newMapping, cancellationToken);

        await Task.WhenAll(oldTask, newTask).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var compareStock = !string.IsNullOrWhiteSpace(oldMapping.StockColumn)
            && !string.IsNullOrWhiteSpace(newMapping.StockColumn);

        var result = comparisonEngine.Compare(await oldTask, await newTask, compareStock);

        logger.LogInformation(
            "Comparison completed. Old rows: {OldRows}; New rows: {NewRows}; Issues: {Issues}; Duplicate groups: {Duplicates}.",
            result.OldRows,
            result.NewRows,
            result.Errors,
            result.Duplicates);

        return result;
    }
}
