namespace PriceCompare.Core;

public static class ColumnMappingValidator
{
    public static IReadOnlyList<string> Validate(ColumnMapping mapping, IReadOnlyCollection<string> headers)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(mapping.SkuColumn))
            errors.Add("SKU column is required.");

        if (string.IsNullOrWhiteSpace(mapping.PriceColumn))
            errors.Add("Price column is required.");

        if (!string.IsNullOrWhiteSpace(mapping.SkuColumn))
            ValidateHeader(mapping.SkuColumn, "SKU", headers, errors);

        if (!string.IsNullOrWhiteSpace(mapping.PriceColumn))
            ValidateHeader(mapping.PriceColumn, "Price", headers, errors);

        if (!string.IsNullOrWhiteSpace(mapping.NameColumn))
            ValidateHeader(mapping.NameColumn!, "Name", headers, errors);

        if (!string.IsNullOrWhiteSpace(mapping.StockColumn))
            ValidateHeader(mapping.StockColumn!, "Stock", headers, errors);

        var selected = new[] { mapping.SkuColumn, mapping.NameColumn, mapping.PriceColumn, mapping.StockColumn }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToArray();

        if (selected.Distinct(StringComparer.OrdinalIgnoreCase).Count() != selected.Length)
            errors.Add("The same source column cannot be mapped to multiple fields.");

        return errors;
    }

    private static void ValidateHeader(
        string value,
        string logicalName,
        IReadOnlyCollection<string> headers,
        ICollection<string> errors)
    {
        if (!headers.Contains(value, StringComparer.OrdinalIgnoreCase))
            errors.Add($"{logicalName} column '{value}' does not exist in the source file.");
    }
}
