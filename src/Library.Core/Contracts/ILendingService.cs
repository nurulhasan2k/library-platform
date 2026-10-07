namespace Library.Core.Contracts;

public sealed record LoanReceipt(
    int LoanId,
    string LibraryCode,
    string ItemName,
    int BranchId,
    DateOnly BorrowedOn,
    DateOnly DueOn);

public sealed record BorrowResult(bool Success, LoanReceipt? Receipt, string? Error)
{
    public static BorrowResult Ok(LoanReceipt receipt) => new(true, receipt, null);
    public static BorrowResult Fail(string error) => new(false, null, error);
}

public sealed record ReturnResult(
    bool Success,
    string? Error,
    int DaysLate = 0,
    decimal FineAmount = 0m,
    bool ItemMovedBranch = false,
    string? MovedFromBranchName = null,
    string? MovedToBranchName = null)
{
    public static ReturnResult Fail(string error) => new(false, error);
}

/// <summary>
/// The ONLY place the lending rules live (14-day loan, $0.50 per full late day, item must be at this branch,
/// not in transit, not held for someone else). Desk screens and the kiosk both call this; nobody re-implements it.
/// Error strings are plain sentences meant to be shown to staff or patrons.
/// Built by Person A (refactored from the Assignment 2 LibraryService).
/// </summary>
public interface ILendingService
{
    Task<BorrowResult> BorrowAsync(string libraryCode, int borrowerId, int branchId, CancellationToken ct = default);

    Task<ReturnResult> ReturnAsync(string libraryCode, int branchId, bool damaged, CancellationToken ct = default);
}
