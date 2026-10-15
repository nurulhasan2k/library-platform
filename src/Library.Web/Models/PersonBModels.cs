using Library.Core.Contracts;
using Library.Core.Services;

namespace Library.Web.Models;

// View models for Person B's pages (kiosk, notification dashboard, holds).

public sealed record KioskCheckoutVm(List<LoanReceipt> Items, string? Error, string? LastOk);

public sealed record KioskReceiptVm(string PatronName, string BranchName, List<LoanReceipt> Items);

public sealed record AccountLoan(string ItemName, DateOnly DueOn, int DaysLeft, decimal Fine);

public sealed record KioskAccountVm(string PatronName, List<AccountLoan> Loans, IReadOnlyList<HoldInfo> Holds, decimal FinesOwed);

public sealed record NotificationsVm(
    List<NotificationRecord> Rows,
    Dictionary<NotificationType, int> Counts,
    string? Type,
    string? Patron,
    DateOnly Today,
    bool IsSimulated)
{
    public string TodayIso => Today.ToString("yyyy-MM-dd");
}

public sealed record HoldRow(DemoItem Item, string? LoanHolder, DateOnly? DueOn, IReadOnlyList<QueueEntry> Queue);

public sealed record HoldsVm(List<HoldRow> Rows, IReadOnlyList<DemoPatron> Patrons);

public sealed record MyHoldsVm(string? PatronName, IReadOnlyList<HoldInfo> Holds);
