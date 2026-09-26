namespace PriceCompare.Core;

public sealed record FileInspection(
    string FilePath,
    IReadOnlyList<string> Headers,
    int DataRowCount);
