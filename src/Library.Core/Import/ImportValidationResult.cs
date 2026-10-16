using Library.Core.Contracts;

namespace Library.Core.Import;

public sealed record ImportLookups(
    IReadOnlyList<BranchSummary> Branches,
    IReadOnlyList<CategoryInfo> Categories,
    IReadOnlyCollection<string> ExistingCodes);

public sealed record ImportValidationResult(
    IReadOnlyList<ValidItemRow> ValidRows,
    IReadOnlyList<ImportRowResult> RejectedRows,
    IReadOnlyList<string> FileErrors)
{
    public bool CanImport => FileErrors.Count == 0 && ValidRows.Count > 0;
}
