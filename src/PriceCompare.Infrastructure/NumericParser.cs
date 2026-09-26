using System.Globalization;

namespace PriceCompare.Infrastructure;

internal static class NumericParser
{
    public static bool TryParseDecimal(string? value, out decimal result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var text = value.Trim()
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        var lastComma = text.LastIndexOf(',');
        var lastDot = text.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            // When both separators are present, treat the last one as the decimal separator
            // and the other one as a grouping separator.
            var decimalSeparator = lastComma > lastDot ? ',' : '.';
            var groupingSeparator = decimalSeparator == ',' ? '.' : ',';

            text = text
                .Replace(groupingSeparator.ToString(), string.Empty, StringComparison.Ordinal)
                .Replace(decimalSeparator, '.');
        }
        else if (lastComma >= 0)
        {
            // A lone comma is treated as the decimal separator.
            text = text.Replace(',', '.');
        }
        else if (lastDot >= 0)
        {
            // A lone dot is already compatible with invariant decimal parsing.
        }

        return decimal.TryParse(
            text,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out result);
    }
}
