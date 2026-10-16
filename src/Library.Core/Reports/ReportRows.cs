using Library.Core.Contracts;

namespace Library.Core.Reports;

public sealed record ReportBranch(int Id, string Code, string Name);
public sealed record ReportFilter(DateOnly From, DateOnly To, int? BranchId = null);

public sealed record LoanRow(
    int Id, string ItemCode, string ItemName, ItemType ItemType, string Category,
    DateOnly BorrowedOn, DateOnly DueOn, DateOnly? ReturnedOn,
    ReportBranch BorrowedAtBranch, ReportBranch? ReturnedAtBranch,
    decimal FineAmount, bool FinePaid, DateOnly? FinePaidOn, string BorrowerName, string CardNumber);

public sealed record ItemRow(
    string Code, string Name, ItemType Type, string Category, ReportBranch Branch, ItemStatus Status,
    DateOnly? CreatedOn, DateOnly? LastBorrowedOn, bool InTransit, bool HasActiveLoan, DateOnly? ActiveLoanDueOn);

public sealed record ItemQueueCounts(string ItemCode, int Waiting, int Ready);
public sealed record ActiveTransferRow(string ItemCode, string ItemName, ReportBranch From, ReportBranch To, TransferStatus Status);
public sealed record ReportData(
    IReadOnlyList<LoanRow> Loans, IReadOnlyList<ItemRow> Items,
    IReadOnlyList<ItemQueueCounts> QueueCounts, IReadOnlyList<ActiveTransferRow> ActiveTransfers);

public sealed record ReportResult<T>(T? Report, IReadOnlyList<string> Errors) where T : class
{
    public bool IsValid => Errors.Count == 0;
    public static ReportResult<T> Invalid(params string[] errors) => new(null, errors);
    public static ReportResult<T> Valid(T report) => new(report, []);
}

internal static class ReportValidation
{
    public static string[] Errors(ReportFilter filter)
        => filter.From > filter.To ? ["The from date must be on or before the to date."] : [];

    public static IEnumerable<DateOnly> Months(ReportFilter filter)
    {
        var month = new DateOnly(filter.From.Year, filter.From.Month, 1);
        var end = new DateOnly(filter.To.Year, filter.To.Month, 1);
        while (true)
        {
            yield return month;
            if (month == end) yield break;
            month = month.AddMonths(1);
        }
    }
}
