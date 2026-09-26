using PriceCompare.Application;

namespace PriceCompare.Tests;

public sealed class ColumnMappingSuggesterTests
{
    [Fact]
    public void Common_aliases_are_suggested()
    {
        var headers = new[] { "ProductCode", "ProductName", "Price", "Quantity" };

        var suggestion = new ColumnMappingSuggester().Suggest(headers);

        Assert.Equal("ProductCode", suggestion.SkuColumn);
        Assert.Equal("ProductName", suggestion.NameColumn);
        Assert.Equal("Price", suggestion.PriceColumn);
        Assert.Equal("Quantity", suggestion.StockColumn);
    }

    [Fact]
    public void Unknown_headers_are_left_unmapped()
    {
        var suggestion = new ColumnMappingSuggester().Suggest(new[] { "X", "Y", "Z" });

        Assert.Null(suggestion.SkuColumn);
        Assert.Null(suggestion.PriceColumn);
    }
}
