using Library.Core.Contracts;

namespace Library.Core.Reports;

public sealed record MonthCount(DateOnly Month, int Count);
public sealed record BranchCount(ReportBranch Branch, int Count);
public sealed record TypeCount(ItemType Type, int Count);
public sealed record CategoryCount(string Category, int Count);
public sealed record TopBorrowedItem(string Code, string Name, IReadOnlyList<ReportBranch> Branches, int Loans);
public enum BorrowingState { Active, Overdue, Returned, ReturnedLate }
public sealed record BorrowingLoanLine(
    int LoanId, string ItemCode, string ItemName, ItemType ItemType, string Category, ReportBranch BorrowedAtBranch,
    DateOnly BorrowedOn, DateOnly DueOn, DateOnly? ReturnedOn, BorrowingState State);

public sealed record BorrowingStatisticsReport(
    ReportFilter Filter, DateOnly Today, int Loans, int Active, int Overdue, int Returned, int ReturnedLate, int ReturnedOnTime,
    IReadOnlyList<MonthCount> ByMonth, IReadOnlyList<BranchCount> ByBranch, IReadOnlyList<TypeCount> ByType,
    IReadOnlyList<CategoryCount> ByCategory, IReadOnlyList<TopBorrowedItem> TopItems, IReadOnlyList<BorrowingLoanLine> Lines)
{
    public bool IsEmpty => Loans == 0;
}

public sealed record FineRevenueLine(
    int LoanId, string ItemCode, string ItemName, string BorrowerName, string CardNumber,
    ReportBranch? ReturnedAtBranch, DateOnly DueOn, DateOnly ReturnedOn, int DaysLate,
    decimal Amount, bool Paid, DateOnly? PaidOn);
public sealed record FineMonth(DateOnly Month, decimal Charged, decimal Collected, decimal Outstanding);
public sealed record FineRevenueReport(
    ReportFilter Filter, DateOnly Today, decimal Charged, decimal Collected, decimal Outstanding,
    decimal AccruingNotCharged, IReadOnlyList<FineMonth> ByMonth, IReadOnlyList<FineRevenueLine> Lines)
{
    public bool IsEmpty => Lines.Count == 0;
    public bool Reconciles => Charged == Lines.Sum(line => line.Amount) && Collected + Outstanding == Charged;
}

public sealed record InventoryStatusCount(ReportBranch Branch, ItemStatus Status, int Count);
public sealed record InventoryItemLine(
    string Code, string Name, ItemType Type, string Category, ReportBranch Branch, ItemStatus Status,
    DateOnly? LastBorrowedOn, bool InTransit, bool Overdue, int DaysOverdue,
    bool Idle, int? DaysIdle, int? DaysSinceLastBorrowed, int Waiting, int Ready);
public sealed record InventoryHealthReport(
    DateOnly Today, int? BranchId, int IdleDays, IReadOnlyList<InventoryStatusCount> ByStatusAndBranch,
    IReadOnlyList<CategoryCount> ByCategory, IReadOnlyList<InventoryItemLine> Items,
    IReadOnlyList<InventoryItemLine> Damaged, IReadOnlyList<InventoryItemLine> Overdue,
    IReadOnlyList<ActiveTransferRow> InTransit, IReadOnlyList<InventoryItemLine> Idle, IReadOnlyList<InventoryItemLine> Waitlists)
{
    public bool IsEmpty => Items.Count == 0;
}
