namespace PriceCompare.Core;

public sealed record ComparisonItem(
    ComparisonStatus Status,
    string Sku,
    string? Name,
    decimal? OldPrice,
    decimal? NewPrice,
    decimal? PriceDifference,
    decimal? PriceDifferencePercent,
    decimal? OldStock,
    decimal? NewStock);
