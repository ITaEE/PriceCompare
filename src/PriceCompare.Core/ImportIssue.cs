namespace PriceCompare.Core;

public sealed record ImportIssue(
    int? RowNumber,
    string Code,
    string? Field,
    string Message);
