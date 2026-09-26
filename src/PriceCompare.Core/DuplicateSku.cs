namespace PriceCompare.Core;

public sealed record DuplicateSku(
    string Sku,
    IReadOnlyList<int> RowNumbers);
