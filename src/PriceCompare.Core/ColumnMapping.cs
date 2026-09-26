namespace PriceCompare.Core;

public sealed record ColumnMapping(
    string SkuColumn,
    string? NameColumn,
    string PriceColumn,
    string? StockColumn);
