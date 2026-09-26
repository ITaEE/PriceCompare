using PriceCompare.Core;

namespace PriceCompare.Tests;

public sealed class PriceComparisonEngineTests
{
    private readonly PriceComparisonEngine _engine = new();

    [Fact]
    public void Identical_rows_are_unchanged()
    {
        var oldImport = TestData.Import(TestData.Row(2, "A-1", 100m, 5m));
        var newImport = TestData.Import(TestData.Row(2, "A-1", 100m, 5m));

        var result = _engine.Compare(oldImport, newImport);

        Assert.Equal(ComparisonStatus.Unchanged, Assert.Single(result.Items).Status);
    }

    [Fact]
    public void Price_change_is_changed()
    {
        var result = _engine.Compare(
            TestData.Import(TestData.Row(2, "A-1", 100m)),
            TestData.Import(TestData.Row(2, "A-1", 120m)));

        var item = Assert.Single(result.Items);
        Assert.Equal(ComparisonStatus.Changed, item.Status);
        Assert.Equal(20m, item.PriceDifference);
    }

    [Fact]
    public void Price_increase_percentage_is_calculated()
    {
        var item = Assert.Single(_engine.Compare(
            TestData.Import(TestData.Row(2, "A-1", 100m)),
            TestData.Import(TestData.Row(2, "A-1", 120m))).Items);

        Assert.Equal(0.20m, item.PriceDifferencePercent);
    }

    [Fact]
    public void Price_decrease_percentage_is_calculated()
    {
        var item = Assert.Single(_engine.Compare(
            TestData.Import(TestData.Row(2, "A-1", 200m)),
            TestData.Import(TestData.Row(2, "A-1", 150m))).Items);

        Assert.Equal(-0.25m, item.PriceDifferencePercent);
    }

    [Fact]
    public void Added_item_is_detected()
    {
        var result = _engine.Compare(
            TestData.Import(),
            TestData.Import(TestData.Row(2, "A-1", 100m)));

        Assert.Equal(ComparisonStatus.Added, Assert.Single(result.Items).Status);
    }

    [Fact]
    public void Removed_item_is_detected()
    {
        var result = _engine.Compare(
            TestData.Import(TestData.Row(2, "A-1", 100m)),
            TestData.Import());

        Assert.Equal(ComparisonStatus.Removed, Assert.Single(result.Items).Status);
    }

    [Fact]
    public void Stock_change_is_changed()
    {
        var result = _engine.Compare(
            TestData.Import(TestData.Row(2, "A-1", 100m, 5m)),
            TestData.Import(TestData.Row(2, "A-1", 100m, 6m)));

        Assert.Equal(ComparisonStatus.Changed, Assert.Single(result.Items).Status);
    }

    [Fact]
    public void Sku_comparison_is_case_insensitive()
    {
        var result = _engine.Compare(
            TestData.Import(TestData.Row(2, "abc", 100m)),
            TestData.Import(TestData.Row(2, "ABC", 100m)));

        Assert.Equal(ComparisonStatus.Unchanged, Assert.Single(result.Items).Status);
    }

    [Fact]
    public void Sku_is_trimmed_for_comparison()
    {
        var result = _engine.Compare(
            TestData.Import(TestData.Row(2, " A-1 ", 100m)),
            TestData.Import(TestData.Row(2, "A-1", 100m)));

        Assert.Equal(ComparisonStatus.Unchanged, Assert.Single(result.Items).Status);
    }

    [Fact]
    public void Zero_old_price_has_no_percentage()
    {
        var item = Assert.Single(_engine.Compare(
            TestData.Import(TestData.Row(2, "A-1", 0m)),
            TestData.Import(TestData.Row(2, "A-1", 10m))).Items);

        Assert.Null(item.PriceDifferencePercent);
        Assert.Equal(10m, item.PriceDifference);
    }

    [Fact]
    public void Empty_inputs_produce_empty_result()
    {
        var result = _engine.Compare(TestData.Import(), TestData.Import());

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
    }

    [Fact]
    public void Duplicate_only_in_old_excludes_matching_new_row_from_comparison()
    {
        var oldImport = new ImportResult(
            "old.csv",
            Array.Empty<PriceListRow>(),
            new[] { new ImportIssue(2, "DuplicateSku", "SKU", "Duplicate SKU detected.") },
            new[] { new DuplicateSku("A-1", new[] { 2, 3 }) },
            2);

        var result = _engine.Compare(oldImport, TestData.Import(TestData.Row(2, "A-1", 100m)));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(1, result.Duplicates);
    }

    [Fact]
    public void Duplicate_only_in_new_excludes_matching_old_row_from_comparison()
    {
        var newImport = new ImportResult(
            "new.csv",
            Array.Empty<PriceListRow>(),
            new[] { new ImportIssue(2, "DuplicateSku", "SKU", "Duplicate SKU detected.") },
            new[] { new DuplicateSku("A-1", new[] { 2, 3 }) },
            2);

        var result = _engine.Compare(TestData.Import(TestData.Row(2, "A-1", 100m)), newImport);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(1, result.Duplicates);
    }

    [Fact]
    public void Duplicate_in_both_files_is_excluded_once_from_each_import_summary()
    {
        var oldImport = new ImportResult(
            "old.csv",
            Array.Empty<PriceListRow>(),
            Array.Empty<ImportIssue>(),
            new[] { new DuplicateSku("A-1", new[] { 2, 3 }) },
            2);
        var newImport = new ImportResult(
            "new.csv",
            Array.Empty<PriceListRow>(),
            Array.Empty<ImportIssue>(),
            new[] { new DuplicateSku("A-1", new[] { 2, 3 }) },
            2);

        var result = _engine.Compare(oldImport, newImport);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(2, result.Duplicates);
    }

    [Fact]
    public void Stock_change_is_ignored_when_stock_comparison_is_disabled()
    {
        var result = _engine.Compare(
            TestData.Import(TestData.Row(2, "A-1", 100m, 5m)),
            TestData.Import(TestData.Row(2, "A-1", 100m, 6m)),
            compareStock: false);

        Assert.Equal(ComparisonStatus.Unchanged, Assert.Single(result.Items).Status);
    }

    [Fact]
    public void Stock_only_in_old_does_not_change_status_when_stock_comparison_is_disabled()
    {
        var result = _engine.Compare(
            TestData.Import(TestData.Row(2, "A-1", 100m, 5m)),
            TestData.Import(TestData.Row(2, "A-1", 100m)),
            compareStock: false);

        Assert.Equal(ComparisonStatus.Unchanged, Assert.Single(result.Items).Status);
    }

    [Fact]
    public void Stock_only_in_new_does_not_change_status_when_stock_comparison_is_disabled()
    {
        var result = _engine.Compare(
            TestData.Import(TestData.Row(2, "A-1", 100m)),
            TestData.Import(TestData.Row(2, "A-1", 100m, 5m)),
            compareStock: false);

        Assert.Equal(ComparisonStatus.Unchanged, Assert.Single(result.Items).Status);
    }

    [Fact]
    public void Unmapped_stock_keeps_price_only_comparison()
    {
        var result = _engine.Compare(
            TestData.Import(TestData.Row(2, "A-1", 100m, 5m)),
            TestData.Import(TestData.Row(2, "A-1", 120m)),
            compareStock: false);

        var item = Assert.Single(result.Items);
        Assert.Equal(ComparisonStatus.Changed, item.Status);
        Assert.Equal(20m, item.PriceDifference);
    }
}
