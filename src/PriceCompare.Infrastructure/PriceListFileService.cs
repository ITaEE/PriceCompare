using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using PriceCompare.Application;
using PriceCompare.Core;

namespace PriceCompare.Infrastructure;

public sealed class PriceListFileService(ILogger<PriceListFileService> logger) : IPriceListFileService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".xlsx", ".csv" };

    public Task<FileInspection> InspectAsync(string filePath, CancellationToken cancellationToken = default) =>
        Task.Run(() => Inspect(filePath, cancellationToken), cancellationToken);

    public Task<ImportResult> ImportAsync(
        string filePath,
        ColumnMapping mapping,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Import(filePath, mapping, cancellationToken), cancellationToken);

    private FileInspection Inspect(string filePath, CancellationToken cancellationToken)
    {
        ValidateFilePath(filePath);

        logger.LogInformation("Inspecting price list. Stage: Inspect; Extension: {Extension}.", Path.GetExtension(filePath));

        try
        {
            return Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".csv" => InspectCsv(filePath, cancellationToken),
                ".xlsx" => InspectXlsx(filePath, cancellationToken),
                _ => throw new PriceListImportException("Unsupported file extension.")
            };
        }
        catch (PriceListImportException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            logger.LogWarning("Price list inspection failed. Exception type: {ExceptionType}.", ex.GetType().Name);
            throw new PriceListImportException("The file could not be read. It may be locked, unreadable, or damaged.", ex);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Price list inspection failed. Exception type: {ExceptionType}.", ex.GetType().Name);
            throw new PriceListImportException("The file could not be inspected.", ex);
        }
    }

    private ImportResult Import(string filePath, ColumnMapping mapping, CancellationToken cancellationToken)
    {
        ValidateFilePath(filePath);

        logger.LogInformation("Importing price list. Stage: Import; Extension: {Extension}.", Path.GetExtension(filePath));

        try
        {
            var result = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".csv" => ImportCsv(filePath, mapping, cancellationToken),
                ".xlsx" => ImportXlsx(filePath, mapping, cancellationToken),
                _ => throw new PriceListImportException("Unsupported file extension.")
            };

            logger.LogInformation(
                "Price list import completed. Rows: {Rows}; Issues: {Issues}; Duplicate groups: {Duplicates}.",
                result.SourceRowCount,
                result.Issues.Count,
                result.Duplicates.Count);

            return result;
        }
        catch (PriceListImportException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            logger.LogWarning("Price list import failed. Exception type: {ExceptionType}.", ex.GetType().Name);
            throw new PriceListImportException("The file could not be read. It may be locked, unreadable, or damaged.", ex);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Price list import failed. Exception type: {ExceptionType}.", ex.GetType().Name);
            throw new PriceListImportException("The file could not be imported.", ex);
        }
    }

    private static FileInspection InspectCsv(string filePath, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, CreateCsvConfiguration());

        if (!csv.Read() || !csv.ReadHeader())
            throw new PriceListImportException("The CSV file is empty or has no header row.");

        var headers = (csv.HeaderRecord ?? Array.Empty<string>())
            .Select(x => x?.Trim() ?? string.Empty)
            .ToArray();

        if (headers.Length == 0 || headers.All(string.IsNullOrWhiteSpace))
            throw new PriceListImportException("The CSV file has no usable columns.");

        var count = 0;
        while (csv.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCsvRecordEmpty(csv, headers.Length))
                count++;
        }

        return new FileInspection(filePath, headers, count);
    }

    private static ImportResult ImportCsv(
        string filePath,
        ColumnMapping mapping,
        CancellationToken cancellationToken)
    {
        var malformedRows = new HashSet<int>();

        using var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(
            reader,
            CreateCsvConfiguration(args =>
            {
                if (args.Context.Parser is { } parser)
                    malformedRows.Add(parser.Row);
            }));

        if (!csv.Read() || !csv.ReadHeader())
            throw new PriceListImportException("The CSV file is empty or has no header row.");

        var headers = (csv.HeaderRecord ?? Array.Empty<string>())
            .Select(x => x?.Trim() ?? string.Empty)
            .ToArray();

        ValidateMappingOrThrow(mapping, headers);

        var rows = new List<PriceListRow>();
        var issues = new List<ImportIssue>();
        var sourceRowCount = 0;

        while (csv.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(csv.Parser.RawRecord))
                continue;

            sourceRowCount++;
            var rowNumber = csv.Parser.Row;

            if (malformedRows.Contains(rowNumber) || HasMalformedCsvQuoting(csv.Parser.RawRecord, csv.Parser.Delimiter))
            {
                issues.Add(new ImportIssue(
                    rowNumber,
                    "MalformedCsv",
                    null,
                    "CSV record contains malformed quoting or invalid field data; the row was ignored."));
                continue;
            }

            if (csv.Parser.Count < headers.Length)
            {
                issues.Add(new ImportIssue(
                    rowNumber,
                    "MissingFields",
                    null,
                    "CSV record has fewer fields than the header row; the row was ignored."));
                continue;
            }

            if (csv.Parser.Count > headers.Length)
            {
                issues.Add(new ImportIssue(
                    rowNumber,
                    "ExtraFields",
                    null,
                    "CSV record has more fields than the header row; the row was ignored."));
                continue;
            }

            if (IsCsvRecordEmpty(csv, headers.Length))
            {
                sourceRowCount--;
                continue;
            }

            string? Read(string header)
            {
                var index = Array.FindIndex(headers, x => string.Equals(x, header, StringComparison.OrdinalIgnoreCase));
                return index >= 0 ? csv.GetField(index) : null;
            }

            TryCreateRow(
                rowNumber,
                Read(mapping.SkuColumn),
                string.IsNullOrWhiteSpace(mapping.NameColumn) ? null : Read(mapping.NameColumn!),
                Read(mapping.PriceColumn),
                string.IsNullOrWhiteSpace(mapping.StockColumn) ? null : Read(mapping.StockColumn!),
                rows,
                issues);
        }

        return FinalizeImport(filePath, rows, issues, sourceRowCount);
    }

    private static FileInspection InspectXlsx(string filePath, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new PriceListImportException("The workbook does not contain a worksheet.");

        var usedRange = worksheet.RangeUsed();
        if (usedRange is null)
            throw new PriceListImportException("The workbook is empty.");

        var firstRow = usedRange.FirstRow();
        var headers = firstRow.Cells()
            .Select(x => x.GetString().Trim())
            .ToArray();

        if (headers.Length == 0 || headers.All(string.IsNullOrWhiteSpace))
            throw new PriceListImportException("The workbook has no usable header row.");

        var dataRows = usedRange.RowsUsed().Skip(1);
        var count = 0;

        foreach (var row in dataRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!row.Cells(1, headers.Length).All(x => x.IsEmpty()))
                count++;
        }

        return new FileInspection(filePath, headers, count);
    }

    private static ImportResult ImportXlsx(
        string filePath,
        ColumnMapping mapping,
        CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new PriceListImportException("The workbook does not contain a worksheet.");

        var usedRange = worksheet.RangeUsed();
        if (usedRange is null)
            throw new PriceListImportException("The workbook is empty.");

        var firstRow = usedRange.FirstRow();
        var headerCells = firstRow.Cells().ToArray();
        var headers = headerCells.Select(x => x.GetString().Trim()).ToArray();

        ValidateMappingOrThrow(mapping, headers);

        var headerIndexes = headers
            .Select((name, index) => (name, index: index + 1))
            .Where(x => !string.IsNullOrWhiteSpace(x.name))
            .GroupBy(x => x.name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().index, StringComparer.OrdinalIgnoreCase);

        var rows = new List<PriceListRow>();
        var issues = new List<ImportIssue>();
        var sourceRowCount = 0;

        foreach (var row in usedRange.RowsUsed().Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (row.Cells(1, headers.Length).All(x => x.IsEmpty()))
                continue;

            sourceRowCount++;

            string? Read(string header)
            {
                var column = headerIndexes[header];
                return row.Cell(column).GetFormattedString();
            }

            TryCreateRow(
                row.RowNumber(),
                Read(mapping.SkuColumn),
                string.IsNullOrWhiteSpace(mapping.NameColumn) ? null : Read(mapping.NameColumn!),
                Read(mapping.PriceColumn),
                string.IsNullOrWhiteSpace(mapping.StockColumn) ? null : Read(mapping.StockColumn!),
                rows,
                issues);
        }

        return FinalizeImport(filePath, rows, issues, sourceRowCount);
    }

    private static void TryCreateRow(
        int rowNumber,
        string? skuRaw,
        string? nameRaw,
        string? priceRaw,
        string? stockRaw,
        ICollection<PriceListRow> rows,
        ICollection<ImportIssue> issues)
    {
        var sku = (skuRaw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sku))
        {
            issues.Add(new ImportIssue(rowNumber, "EmptySku", "SKU", "SKU is empty."));
            return;
        }

        if (!NumericParser.TryParseDecimal(priceRaw, out var price))
        {
            issues.Add(new ImportIssue(rowNumber, "MalformedPrice", "Price", "Price is empty or has an invalid numeric format."));
            return;
        }

        decimal? stock = null;
        if (!string.IsNullOrWhiteSpace(stockRaw))
        {
            if (NumericParser.TryParseDecimal(stockRaw, out var parsedStock))
            {
                stock = parsedStock;
            }
            else
            {
                issues.Add(new ImportIssue(rowNumber, "MalformedStock", "Stock", "Stock has an invalid numeric format; the value was ignored."));
            }
        }

        rows.Add(new PriceListRow(
            rowNumber,
            sku,
            string.IsNullOrWhiteSpace(nameRaw) ? null : nameRaw.Trim(),
            price,
            stock));
    }

    private static ImportResult FinalizeImport(
        string filePath,
        IReadOnlyCollection<PriceListRow> rows,
        List<ImportIssue> issues,
        int sourceRowCount)
    {
        var duplicateGroups = rows
            .GroupBy(x => SkuNormalizer.Normalize(x.Sku), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .ToArray();

        var duplicates = duplicateGroups
            .Select(g => new DuplicateSku(
                g.First().Sku,
                g.Select(x => x.RowNumber).OrderBy(x => x).ToArray()))
            .ToArray();

        var duplicateSkuSet = duplicateGroups
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var duplicate in duplicates)
        {
            foreach (var rowNumber in duplicate.RowNumbers)
            {
                issues.Add(new ImportIssue(
                    rowNumber,
                    "DuplicateSku",
                    "SKU",
                    "Duplicate SKU detected; all rows for this SKU were excluded from comparison."));
            }
        }

        var validRows = rows
            .Where(x => !duplicateSkuSet.Contains(SkuNormalizer.Normalize(x.Sku)))
            .ToArray();

        return new ImportResult(filePath, validRows, issues, duplicates, sourceRowCount);
    }

    private static void ValidateFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new PriceListImportException("File path is required.");

        var extension = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(extension))
            throw new PriceListImportException("Unsupported file extension. Use .xlsx or .csv.");

        if (!File.Exists(filePath))
            throw new PriceListImportException("The selected file does not exist.");
    }

    private static void ValidateMappingOrThrow(ColumnMapping mapping, IReadOnlyCollection<string> headers)
    {
        var errors = ColumnMappingValidator.Validate(mapping, headers);
        if (errors.Count > 0)
            throw new PriceListImportException(string.Join(Environment.NewLine, errors));
    }

    private static CsvConfiguration CreateCsvConfiguration(BadDataFound? badDataFound = null) =>
        new(CultureInfo.InvariantCulture)
        {
            BadDataFound = badDataFound,
            MissingFieldFound = null,
            HeaderValidated = null,
            DetectDelimiter = true,
            TrimOptions = TrimOptions.Trim
        };

    private static bool IsCsvRecordEmpty(CsvReader csv, int fieldCount)
    {
        for (var i = 0; i < fieldCount; i++)
        {
            if (!string.IsNullOrWhiteSpace(csv.GetField(i)))
                return false;
        }

        return true;
    }

    private static bool HasMalformedCsvQuoting(string rawRecord, string delimiter)
    {
        var inQuotedField = false;
        var atFieldStart = true;
        var quoteJustClosed = false;

        for (var index = 0; index < rawRecord.Length; index++)
        {
            if (!inQuotedField && rawRecord.AsSpan(index).StartsWith(delimiter, StringComparison.Ordinal))
            {
                atFieldStart = true;
                quoteJustClosed = false;
                index += delimiter.Length - 1;
                continue;
            }

            var character = rawRecord[index];
            if (character is '\r' or '\n')
                break;

            if (character == '"')
            {
                if (inQuotedField)
                {
                    if (index + 1 < rawRecord.Length && rawRecord[index + 1] == '"')
                    {
                        index++;
                        continue;
                    }

                    inQuotedField = false;
                    quoteJustClosed = true;
                    continue;
                }

                if (!atFieldStart)
                    return true;

                inQuotedField = true;
                atFieldStart = false;
                continue;
            }

            if (quoteJustClosed && !char.IsWhiteSpace(character))
                return true;

            if (!char.IsWhiteSpace(character) || !atFieldStart)
                atFieldStart = false;
        }

        return inQuotedField;
    }
}
