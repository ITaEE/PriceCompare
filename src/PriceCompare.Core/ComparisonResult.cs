namespace PriceCompare.Core;

public sealed record ComparisonResult(
    IReadOnlyList<ComparisonItem> Items,
    ImportResult OldImport,
    ImportResult NewImport)
{
    public int OldRows => OldImport.SourceRowCount;
    public int NewRows => NewImport.SourceRowCount;
    public int Added => Items.Count(x => x.Status == ComparisonStatus.Added);
    public int Removed => Items.Count(x => x.Status == ComparisonStatus.Removed);
    public int Changed => Items.Count(x => x.Status == ComparisonStatus.Changed);
    public int Unchanged => Items.Count(x => x.Status == ComparisonStatus.Unchanged);
    public int Errors => OldImport.Issues.Count + NewImport.Issues.Count;
    public int Duplicates => OldImport.Duplicates.Count + NewImport.Duplicates.Count;
}
