namespace PriceCompare.Core;

public sealed record PriceListRow(
    int RowNumber,
    string Sku,
    string? Name,
    decimal Price,
    decimal? Stock);
