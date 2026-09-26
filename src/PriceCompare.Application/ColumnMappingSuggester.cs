namespace PriceCompare.Application;

public sealed record MappingSuggestion(
    string? SkuColumn,
    string? NameColumn,
    string? PriceColumn,
    string? StockColumn);

public sealed class ColumnMappingSuggester
{
    private static readonly string[] SkuAliases =
    [
        "sku", "артикул", "код", "productcode", "product code", "itemcode", "item code"
    ];

    private static readonly string[] NameAliases =
    [
        "name", "productname", "product name", "наименование", "название", "товар"
    ];

    private static readonly string[] PriceAliases =
    [
        "price", "цена", "стоимость", "cost"
    ];

    private static readonly string[] StockAliases =
    [
        "stock", "остаток", "остатки", "quantity", "qty", "количество"
    ];

    public MappingSuggestion Suggest(IReadOnlyList<string> headers) =>
        new(
            Find(headers, SkuAliases),
            Find(headers, NameAliases),
            Find(headers, PriceAliases),
            Find(headers, StockAliases));

    private static string? Find(IReadOnlyList<string> headers, IReadOnlyList<string> aliases)
    {
        foreach (var header in headers)
        {
            var normalized = NormalizeHeader(header);
            if (aliases.Any(alias => NormalizeHeader(alias) == normalized))
                return header;
        }

        return null;
    }

    private static string NormalizeHeader(string value) =>
        new(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
}
