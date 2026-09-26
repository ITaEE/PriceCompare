using Microsoft.Extensions.Logging.Abstractions;
using PriceCompare.Application;
using PriceCompare.Core;

namespace PriceCompare.Tests;

public sealed class ComparisonWorkflowTests
{
    [Fact]
    public async Task Stock_is_not_compared_when_mapping_is_missing_from_one_file()
    {
        var oldImport = TestData.Import(TestData.Row(2, "A-1", 100m, 5m));
        var newImport = TestData.Import(TestData.Row(2, "A-1", 100m, 6m));
        var workflow = new ComparisonWorkflow(
            new TestFileService(oldImport, newImport),
            new PriceComparisonEngine(),
            NullLogger<ComparisonWorkflow>.Instance);

        var result = await workflow.CompareAsync(
            "old.csv",
            new ColumnMapping("SKU", null, "Price", "Stock"),
            "new.csv",
            new ColumnMapping("SKU", null, "Price", null));

        Assert.Equal(ComparisonStatus.Unchanged, Assert.Single(result.Items).Status);
    }

    private sealed class TestFileService(ImportResult oldImport, ImportResult newImport) : IPriceListFileService
    {
        public Task<FileInspection> InspectAsync(string filePath, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ImportResult> ImportAsync(
            string filePath,
            ColumnMapping mapping,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Equals(filePath, "old.csv", StringComparison.OrdinalIgnoreCase) ? oldImport : newImport);
    }
}
