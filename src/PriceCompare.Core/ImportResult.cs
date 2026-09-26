namespace PriceCompare.Core;

public sealed record ImportResult(
    string FilePath,
    IReadOnlyList<PriceListRow> Rows,
    IReadOnlyList<ImportIssue> Issues,
    IReadOnlyList<DuplicateSku> Duplicates,
    int SourceRowCount)
{
    public int ErrorCount => Issues.Count;
}
