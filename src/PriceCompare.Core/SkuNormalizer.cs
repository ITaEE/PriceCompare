namespace PriceCompare.Core;

public static class SkuNormalizer
{
    public static string Normalize(string? sku) =>
        (sku ?? string.Empty).Trim().ToUpperInvariant();
}
