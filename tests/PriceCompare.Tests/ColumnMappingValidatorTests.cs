using PriceCompare.Core;

namespace PriceCompare.Tests;

public sealed class ColumnMappingValidatorTests
{
    [Fact]
    public void Valid_mapping_passes()
    {
        var headers = new[] { "SKU", "Name", "Price", "Stock" };
        var mapping = new ColumnMapping("SKU", "Name", "Price", "Stock");

        var errors = ColumnMappingValidator.Validate(mapping, headers);

        Assert.Empty(errors);
    }

    [Fact]
    public void Missing_required_mapping_fails()
    {
        var headers = new[] { "SKU", "Price" };
        var mapping = new ColumnMapping("", null, "Price", null);

        var errors = ColumnMappingValidator.Validate(mapping, headers);

        Assert.Contains(errors, x => x.Contains("SKU column is required", StringComparison.OrdinalIgnoreCase));
        Assert.Single(errors);
    }
}
