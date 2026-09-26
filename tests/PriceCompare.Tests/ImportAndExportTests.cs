using System.Globalization;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using PriceCompare.Core;
using PriceCompare.Infrastructure;

namespace PriceCompare.Tests;

public sealed class ImportAndExportTests
{
    private readonly PriceListFileService _service = new(NullLogger<PriceListFileService>.Instance);

    [Fact]
    public async Task Csv_import_reads_rows_and_decimal_separators()
    {
        var path = TempPath(".csv");
        await File.WriteAllTextAsync(
            path,
            "SKU,Name,Price,Stock\nA-1,Alpha,\"10,50\",5\nA-2,Beta,20.25,7\n");

        try
        {
            var result = await _service.ImportAsync(
                path,
                new ColumnMapping("SKU", "Name", "Price", "Stock"));

            Assert.Equal(2, result.Rows.Count);
            Assert.Equal(10.50m, result.Rows[0].Price);
            Assert.Equal(20.25m, result.Rows[1].Price);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Sample_files_produce_the_expected_comparison()
    {
        var projectRoot = FindProjectRoot();
        var oldImport = await _service.ImportAsync(
            Path.Combine(projectRoot, "samples", "price-old.csv"),
            new ColumnMapping("SKU", "Name", "Price", "Stock"));
        var newImport = await _service.ImportAsync(
            Path.Combine(projectRoot, "samples", "price-new.csv"),
            new ColumnMapping("ProductCode", "ProductName", "Price", "Quantity"));

        var result = new PriceComparisonEngine().Compare(oldImport, newImport);

        Assert.Equal(8, result.OldRows);
        Assert.Equal(8, result.NewRows);
        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Removed);
        Assert.Equal(3, result.Changed);
        Assert.Equal(4, result.Unchanged);
        Assert.Equal(0, result.Errors);
        Assert.Equal(0, result.Duplicates);
        Assert.Equal(new[] { "A-1001", "A-1003", "A-1005" }, result.Items
            .Where(x => x.Status == ComparisonStatus.Changed)
            .Select(x => x.Sku)
            .OrderBy(x => x));
        Assert.Equal("A-1009", Assert.Single(result.Items, x => x.Status == ComparisonStatus.Added).Sku);
        Assert.Equal("A-1006", Assert.Single(result.Items, x => x.Status == ComparisonStatus.Removed).Sku);
    }

    [Fact]
    public async Task Xlsx_import_reads_rows()
    {
        var path = TempPath(".xlsx");

        try
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Prices");
                ws.Cell(1, 1).Value = "SKU";
                ws.Cell(1, 2).Value = "Name";
                ws.Cell(1, 3).Value = "Price";
                ws.Cell(1, 4).Value = "Stock";
                ws.Cell(2, 1).Value = "A-1";
                ws.Cell(2, 2).Value = "Alpha";
                ws.Cell(2, 3).Value = 100d;
                ws.Cell(2, 4).Value = 5d;
                workbook.SaveAs(path);
            }

            var result = await _service.ImportAsync(
                path,
                new ColumnMapping("SKU", "Name", "Price", "Stock"));

            var row = Assert.Single(result.Rows);
            Assert.Equal("A-1", row.Sku);
            Assert.Equal(100m, row.Price);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Duplicate_sku_is_detected_and_excluded()
    {
        var path = TempPath(".csv");
        await File.WriteAllTextAsync(
            path,
            "SKU,Price\nA-1,10\n a-1 ,20\nB-1,30\n");

        try
        {
            var result = await _service.ImportAsync(
                path,
                new ColumnMapping("SKU", null, "Price", null));

            Assert.Single(result.Duplicates);
            Assert.Single(result.Rows);
            Assert.Equal("B-1", result.Rows[0].Sku);
            Assert.Equal(2, result.Issues.Count(x => x.Code == "DuplicateSku"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Empty_sku_becomes_issue()
    {
        var path = TempPath(".csv");
        await File.WriteAllTextAsync(path, "SKU,Price\n,10\n");

        try
        {
            var result = await _service.ImportAsync(path, new ColumnMapping("SKU", null, "Price", null));

            Assert.Empty(result.Rows);
            Assert.Contains(result.Issues, x => x.Code == "EmptySku");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Malformed_price_becomes_issue()
    {
        var path = TempPath(".csv");
        await File.WriteAllTextAsync(path, "SKU,Price\nA-1,not-a-number\n");

        try
        {
            var result = await _service.ImportAsync(path, new ColumnMapping("SKU", null, "Price", null));

            Assert.Empty(result.Rows);
            Assert.Contains(result.Issues, x => x.Code == "MalformedPrice");
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Fact]
    public async Task Malformed_stock_becomes_issue_but_row_is_kept()
    {
        var path = TempPath(".csv");
        await File.WriteAllTextAsync(path, "SKU,Price,Stock\nA-1,10,invalid\n");

        try
        {
            var result = await _service.ImportAsync(path, new ColumnMapping("SKU", null, "Price", "Stock"));

            var row = Assert.Single(result.Rows);
            Assert.Null(row.Stock);
            Assert.Contains(result.Issues, x => x.Code == "MalformedStock");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Unsupported_extension_is_rejected()
    {
        var path = TempPath(".txt");
        await File.WriteAllTextAsync(path, "SKU,Price\nA-1,10\n");

        try
        {
            await Assert.ThrowsAsync<PriceListImportException>(
                () => _service.InspectAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Missing_csv_fields_become_issue_and_later_valid_rows_are_imported()
    {
        var path = TempPath(".csv");
        await File.WriteAllTextAsync(
            path,
            "SKU,Price,Stock\nA-1,10,1\nA-2,20\nA-3,30,3\n");

        try
        {
            var result = await _service.ImportAsync(path, new ColumnMapping("SKU", null, "Price", "Stock"));

            Assert.Equal(new[] { "A-1", "A-3" }, result.Rows.Select(x => x.Sku));
            Assert.Contains(result.Issues, x => x.Code == "MissingFields" && x.RowNumber == 3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Extra_csv_fields_become_issue_and_later_valid_rows_are_imported()
    {
        var path = TempPath(".csv");
        await File.WriteAllTextAsync(
            path,
            "SKU,Price,Stock\nA-1,10,1\nA-2,20,2,unexpected\nA-3,30,3\n");

        try
        {
            var result = await _service.ImportAsync(path, new ColumnMapping("SKU", null, "Price", "Stock"));

            Assert.Equal(new[] { "A-1", "A-3" }, result.Rows.Select(x => x.Sku));
            Assert.Contains(result.Issues, x => x.Code == "ExtraFields" && x.RowNumber == 3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Malformed_csv_quoting_becomes_issue_and_later_valid_rows_are_imported()
    {
        var path = TempPath(".csv");
        await File.WriteAllTextAsync(
            path,
            "SKU,Price\nA-1,10\nA-2,\"broken\"quote\nA-3,30\n");

        try
        {
            var result = await _service.ImportAsync(path, new ColumnMapping("SKU", null, "Price", null));

            Assert.Equal(new[] { "A-1", "A-3" }, result.Rows.Select(x => x.Sku));
            Assert.Contains(result.Issues, x => x.Code == "MalformedCsv" && x.RowNumber == 3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Pre_cancelled_import_propagates_operation_canceled_exception()
    {
        var path = TempPath(".csv");
        await File.WriteAllTextAsync(path, "SKU,Price\nA-1,10\n");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _service.ImportAsync(path, new ColumnMapping("SKU", null, "Price", null), cancellation.Token));
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Fact]
    public async Task Sample_scenario_produces_expected_summary()
    {
        var oldPath = TempPath(".csv");
        var newPath = TempPath(".csv");

        await File.WriteAllTextAsync(
            oldPath,
            "SKU,Name,Price,Stock\n" +
            "A-1001,Wireless Mouse,24.90,18\n" +
            "A-1002,Mechanical Keyboard,79.00,12\n" +
            "A-1003,USB-C Hub,39.50,9\n" +
            "A-1004,Desk Lamp,29.90,15\n" +
            "A-1005,Laptop Stand,45.00,7\n" +
            "A-1006,Webcam Cover,4.50,120\n" +
            "A-1007,HDMI Cable,11.90,32\n" +
            "A-1008,Portable SSD Case,16.00,11\n");

        await File.WriteAllTextAsync(
            newPath,
            "ProductCode,ProductName,Price,Quantity\n" +
            "A-1001,Wireless Mouse,26.90,20\n" +
            "A-1002,Mechanical Keyboard,79.00,12\n" +
            "A-1003,USB-C Hub,35.00,6\n" +
            "A-1004,Desk Lamp,29.90,15\n" +
            "A-1005,Laptop Stand,45.00,10\n" +
            "A-1007,HDMI Cable,11.90,32\n" +
            "A-1008,Portable SSD Case,16.00,11\n" +
            "A-1009,USB Microphone,64.90,8\n");

        try
        {
            var oldImport = await _service.ImportAsync(
                oldPath,
                new ColumnMapping("SKU", "Name", "Price", "Stock"));

            var newImport = await _service.ImportAsync(
                newPath,
                new ColumnMapping("ProductCode", "ProductName", "Price", "Quantity"));

            var result = new PriceComparisonEngine().Compare(oldImport, newImport);

            Assert.Equal(1, result.Added);
            Assert.Equal(1, result.Removed);
            Assert.Equal(3, result.Changed);
            Assert.Equal(4, result.Unchanged);
            Assert.Equal(0, result.Errors);
            Assert.Equal(0, result.Duplicates);
        }
        finally
        {
            File.Delete(oldPath);
            File.Delete(newPath);
        }
    }

    [Fact]
    public async Task Report_generation_creates_expected_sheets_and_can_be_reopened()
    {
        var oldImport = TestData.Import(
            TestData.Row(2, "A-1", 100m, 5m),
            TestData.Row(3, "B-1", 50m, 2m),
            TestData.Row(4, "C-1", 0m, 1m));
        var newImport = TestData.Import(
            TestData.Row(2, "A-1", 120m, 6m),
            TestData.Row(3, "C-1", 10m, 1m),
            TestData.Row(4, "D-1", 50m, 2m));

        var result = new PriceComparisonEngine().Compare(oldImport, newImport);
        var path = TempPath(".xlsx");

        try
        {
            var exporter = new ExcelReportExporter(NullLogger<ExcelReportExporter>.Instance);
            await exporter.ExportAsync(path, result);

            using var workbook = new XLWorkbook(path);

            var summary = workbook.Worksheet("Summary");
            var changes = workbook.Worksheet("Changes");
            var added = workbook.Worksheet("Added");
            var removed = workbook.Worksheet("Removed");
            var errors = workbook.Worksheet("Errors");

            Assert.Equal("Metric", summary.Cell(1, 1).GetString());
            Assert.Equal("Value", summary.Cell(1, 2).GetString());
            Assert.Equal("Changed", summary.Cell(7, 1).GetString());
            Assert.Equal("2", summary.Cell(7, 2).GetString());

            Assert.Equal(
                new[] { "SKU", "Name", "OldPrice", "NewPrice", "PriceDifference", "PriceDifferencePercent", "OldStock", "NewStock" },
                changes.Row(1).Cells(1, 8).Select(x => x.GetString()));
            Assert.Equal("A-1", changes.Cell(2, 1).GetString());
            Assert.Equal(0.20d, changes.Cell(2, 6).GetDouble());
            Assert.Equal("+0.00%;-0.00%;0.00%", changes.Column(6).Style.NumberFormat.Format);
            Assert.True(changes.Cell(3, 6).IsEmpty());
            Assert.True(changes.AutoFilter.IsEnabled);
            Assert.Equal(1, changes.SheetView.SplitRow);

            Assert.Equal("D-1", added.Cell(2, 1).GetString());
            Assert.Equal("B-1", removed.Cell(2, 1).GetString());
            Assert.Equal(new[] { "Source", "Row", "Code", "Field", "Message" }, errors.Row(1).Cells(1, 5).Select(x => x.GetString()));
            Assert.True(errors.AutoFilter.IsEnabled);
            Assert.Equal(1, errors.SheetView.SplitRow);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"PriceCompare-{Guid.NewGuid():N}{extension}");

    private static string FindProjectRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PriceCompare.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("PriceCompare solution root was not found.");
    }
}
