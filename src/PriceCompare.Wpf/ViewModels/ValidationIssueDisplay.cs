namespace PriceCompare.Wpf.ViewModels;

public sealed record ValidationIssueDisplay(
    string Source,
    int? Row,
    string Code,
    string Message);

public sealed record DuplicateGroupDisplay(
    string Source,
    string Rows,
    string Code,
    string Message);
