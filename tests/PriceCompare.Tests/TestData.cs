using PriceCompare.Core;

namespace PriceCompare.Tests;

internal static class TestData
{
    public static ImportResult Import(params PriceListRow[] rows) =>
        new("test.csv", rows, Array.Empty<ImportIssue>(), Array.Empty<DuplicateSku>(), rows.Length);

    public static PriceListRow Row(
        int rowNumber,
        string sku,
        decimal price,
        decimal? stock = null,
        string? name = null) =>
        new(rowNumber, sku, name, price, stock);
}
