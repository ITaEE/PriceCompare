using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using PriceCompare.Application;
using PriceCompare.Core;

namespace PriceCompare.Infrastructure;

public sealed class ExcelReportExporter(ILogger<ExcelReportExporter> logger) : IReportExporter
{
    public Task ExportAsync(
        string destinationPath,
        ComparisonResult result,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Export(destinationPath, result, cancellationToken), cancellationToken);

    private void Export(string destinationPath, ComparisonResult result, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path is required.", nameof(destinationPath));

        ArgumentNullException.ThrowIfNull(result);

        logger.LogInformation("Report export started. Stage: Export.");

        using var workbook = new XLWorkbook();

        AddSummary(workbook, result);
        AddItemsSheet(workbook, "Changes", result.Items.Where(x => x.Status == ComparisonStatus.Changed), cancellationToken);
        AddItemsSheet(workbook, "Added", result.Items.Where(x => x.Status == ComparisonStatus.Added), cancellationToken);
        AddItemsSheet(workbook, "Removed", result.Items.Where(x => x.Status == ComparisonStatus.Removed), cancellationToken);
        AddErrors(workbook, result, cancellationToken);

        workbook.SaveAs(destinationPath);

        logger.LogInformation("Report export completed. Stage: Export.");
    }

    private static void AddSummary(XLWorkbook workbook, ComparisonResult result)
    {
        var ws = workbook.Worksheets.Add("Summary");

        var rows = new (string Label, string Value)[]
        {
            ("Comparison completed", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
            ("Old rows", result.OldRows.ToString()),
            ("New rows", result.NewRows.ToString()),
            ("Added", result.Added.ToString()),
            ("Removed", result.Removed.ToString()),
            ("Changed", result.Changed.ToString()),
            ("Unchanged", result.Unchanged.ToString()),
            ("Errors", result.Errors.ToString()),
            ("Duplicate groups", result.Duplicates.ToString())
        };

        ws.Cell(1, 1).Value = "Metric";
        ws.Cell(1, 2).Value = "Value";

        for (var i = 0; i < rows.Length; i++)
        {
            ws.Cell(i + 2, 1).Value = rows[i].Label;
            ws.Cell(i + 2, 2).Value = rows[i].Value;
        }

        FormatTableLikeRange(ws, 1, 2, rows.Length + 1);
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void AddItemsSheet(
        XLWorkbook workbook,
        string name,
        IEnumerable<ComparisonItem> items,
        CancellationToken cancellationToken)
    {
        var ws = workbook.Worksheets.Add(name);
        var headers = new[]
        {
            "SKU",
            "Name",
            "OldPrice",
            "NewPrice",
            "PriceDifference",
            "PriceDifferencePercent",
            "OldStock",
            "NewStock"
        };

        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var row = 2;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ws.Cell(row, 1).Value = item.Sku;
            ws.Cell(row, 2).Value = item.Name ?? string.Empty;
            SetNullableNumber(ws.Cell(row, 3), item.OldPrice);
            SetNullableNumber(ws.Cell(row, 4), item.NewPrice);
            SetNullableNumber(ws.Cell(row, 5), item.PriceDifference);
            SetNullableNumber(ws.Cell(row, 6), item.PriceDifferencePercent);
            SetNullableNumber(ws.Cell(row, 7), item.OldStock);
            SetNullableNumber(ws.Cell(row, 8), item.NewStock);
            row++;
        }

        FormatTableLikeRange(ws, 1, headers.Length, Math.Max(1, row - 1));
        ws.SheetView.FreezeRows(1);

        ws.Column(3).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(4).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(5).Style.NumberFormat.Format = "+#,##0.00;-#,##0.00;0.00";
        ws.Column(6).Style.NumberFormat.Format = "+0.00%;-0.00%;0.00%";
        ws.Column(7).Style.NumberFormat.Format = "#,##0.##";
        ws.Column(8).Style.NumberFormat.Format = "#,##0.##";

        ws.Columns().AdjustToContents();
        CapWidths(ws);
    }

    private static void AddErrors(
        XLWorkbook workbook,
        ComparisonResult result,
        CancellationToken cancellationToken)
    {
        var ws = workbook.Worksheets.Add("Errors");
        var headers = new[] { "Source", "Row", "Code", "Field", "Message" };

        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        var row = 2;
        foreach (var entry in EnumerateIssues(result))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ws.Cell(row, 1).Value = entry.Source;
            if (entry.Issue.RowNumber.HasValue)
                ws.Cell(row, 2).Value = entry.Issue.RowNumber.Value;
            ws.Cell(row, 3).Value = entry.Issue.Code;
            ws.Cell(row, 4).Value = entry.Issue.Field ?? string.Empty;
            ws.Cell(row, 5).Value = entry.Issue.Message;
            row++;
        }

        FormatTableLikeRange(ws, 1, headers.Length, Math.Max(1, row - 1));
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        CapWidths(ws);
    }

    private static IEnumerable<(string Source, ImportIssue Issue)> EnumerateIssues(ComparisonResult result)
    {
        foreach (var issue in result.OldImport.Issues)
            yield return ("Old", issue);

        foreach (var issue in result.NewImport.Issues)
            yield return ("New", issue);
    }

    private static void SetNullableNumber(IXLCell cell, decimal? value)
    {
        if (value.HasValue)
            cell.Value = (double)value.Value;
    }

    private static void FormatTableLikeRange(IXLWorksheet ws, int headerRow, int lastColumn, int lastRow)
    {
        var range = ws.Range(headerRow, 1, lastRow, lastColumn);
        range.SetAutoFilter();
        ws.Range(headerRow, 1, headerRow, lastColumn).Style.Font.Bold = true;
    }

    private static void CapWidths(IXLWorksheet ws)
    {
        foreach (var column in ws.ColumnsUsed())
        {
            if (column.Width > 45)
                column.Width = 45;
        }
    }
}
