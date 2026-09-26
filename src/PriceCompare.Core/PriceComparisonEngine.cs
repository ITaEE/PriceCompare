namespace PriceCompare.Core;

public sealed class PriceComparisonEngine
{
    public ComparisonResult Compare(
        ImportResult oldImport,
        ImportResult newImport,
        bool compareStock = true)
    {
        ArgumentNullException.ThrowIfNull(oldImport);
        ArgumentNullException.ThrowIfNull(newImport);

        var ambiguousSkus = oldImport.Duplicates
            .Select(x => SkuNormalizer.Normalize(x.Sku))
            .Concat(newImport.Duplicates.Select(x => SkuNormalizer.Normalize(x.Sku)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var oldBySku = oldImport.Rows
            .Where(x => !ambiguousSkus.Contains(SkuNormalizer.Normalize(x.Sku)))
            .ToDictionary(x => SkuNormalizer.Normalize(x.Sku), StringComparer.OrdinalIgnoreCase);

        var newBySku = newImport.Rows
            .Where(x => !ambiguousSkus.Contains(SkuNormalizer.Normalize(x.Sku)))
            .ToDictionary(x => SkuNormalizer.Normalize(x.Sku), StringComparer.OrdinalIgnoreCase);

        var allSkus = oldBySku.Keys
            .Union(newBySku.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        var items = new List<ComparisonItem>();

        foreach (var normalizedSku in allSkus)
        {
            var hasOld = oldBySku.TryGetValue(normalizedSku, out var oldRow);
            var hasNew = newBySku.TryGetValue(normalizedSku, out var newRow);

            if (!hasOld && newRow is not null)
            {
                items.Add(new ComparisonItem(
                    ComparisonStatus.Added,
                    newRow.Sku,
                    newRow.Name,
                    null,
                    newRow.Price,
                    null,
                    null,
                    null,
                    newRow.Stock));
                continue;
            }

            if (!hasNew && oldRow is not null)
            {
                items.Add(new ComparisonItem(
                    ComparisonStatus.Removed,
                    oldRow.Sku,
                    oldRow.Name,
                    oldRow.Price,
                    null,
                    null,
                    null,
                    oldRow.Stock,
                    null));
                continue;
            }

            if (oldRow is null || newRow is null)
                continue;

            var priceChanged = oldRow.Price != newRow.Price;
            var stockChanged = compareStock && oldRow.Stock != newRow.Stock;
            var status = priceChanged || stockChanged
                ? ComparisonStatus.Changed
                : ComparisonStatus.Unchanged;

            var difference = newRow.Price - oldRow.Price;
            decimal? differencePercent = oldRow.Price == 0m
                ? null
                : difference / oldRow.Price;

            items.Add(new ComparisonItem(
                status,
                newRow.Sku,
                newRow.Name ?? oldRow.Name,
                oldRow.Price,
                newRow.Price,
                difference,
                differencePercent,
                oldRow.Stock,
                newRow.Stock));
        }

        return new ComparisonResult(items, oldImport, newImport);
    }
}
