using Library.Core.Contracts;

namespace Library.Core.Reports;

internal static class SampleReportData
{
    public static readonly DateOnly Today = new(2026, 10, 5);
    public static readonly ReportBranch Central = new(1, "CENTRAL", "Central");
    public static readonly ReportBranch North = new(2, "NORTH", "North");
    public static readonly ReportFilter BorrowingRange = new(new(2026, 8, 1), new(2026, 9, 30));
    public static readonly ReportFilter FineRange = new(new(2026, 8, 1), Today);
    public static readonly ReportFilter EmptyRange = new(new(2025, 1, 1), new(2025, 1, 31));

    public static readonly ItemRow[] Items = [
        new("BK-A", "Book A", ItemType.Book, "Science Fiction", Central, ItemStatus.Available, null, new(2026, 9, 22), true, false, null),
        new("BK-B", "Book B", ItemType.Book, "Fantasy", North, ItemStatus.Borrowed, null, new(2026, 9, 20), false, true, new(2026, 10, 4)),
        new("MU-A", "Music A", ItemType.Music, "Rock", North, ItemStatus.Available, null, new(2026, 9, 12), false, false, null),
        new("TY-A", "Toy A", ItemType.Toy, "Puzzle", North, ItemStatus.Borrowed, null, new(2026, 9, 25), false, true, new(2026, 10, 9)),
        new("BK-C", "Book C", ItemType.Book, "Science Fiction", North, ItemStatus.Available, null, null, false, false, null),
        new("MU-B", "Music B", ItemType.Music, "Jazz", Central, ItemStatus.Damaged, null, null, false, false, null),
        new("BK-D", "Book D", ItemType.Book, "History", Central, ItemStatus.Destroy, null, null, false, false, null)];

    public static readonly LoanRow[] Loans = [
        Loan(1, "BK-A", "2026-08-01", "2026-08-15", "2026-08-15", Central, Central),
        Loan(2, "BK-A", "2026-08-20", "2026-09-03", "2026-09-07", Central, Central, 2m, "2026-09-10"),
        Loan(3, "BK-B", "2026-08-25", "2026-09-08", "2026-09-18", Central, North, 5m),
        Loan(4, "MU-A", "2026-09-01", "2026-09-15", "2026-09-10", North, North),
        Loan(5, "MU-A", "2026-09-12", "2026-09-26", "2026-10-01", North, North, 2.5m, "2026-10-02"),
        Loan(6, "TY-A", "2026-09-05", "2026-09-19", "2026-09-19", North, North),
        Loan(7, "BK-B", "2026-09-20", "2026-10-04", null, North, null),
        Loan(8, "TY-A", "2026-09-25", "2026-10-09", null, North, null),
        Loan(9, "BK-A", "2026-09-22", "2026-10-06", "2026-09-30", Central, Central)];

    public static readonly ItemQueueCounts[] Queues = [new("BK-B", 2, 0)];
    public static readonly ActiveTransferRow[] Transfers = [new("BK-A", "Book A", Central, North, TransferStatus.Requested)];
    public static ReportData Data => new(Loans, Items, Queues, Transfers);

    private static LoanRow Loan(int id, string code, string borrowed, string due, string? returned,
        ReportBranch from, ReportBranch? to, decimal fine = 0m, string? paidOn = null)
    {
        var item = Items.Single(item => item.Code == code);
        return new(id, code, item.Name, item.Type, item.Category, DateOnly.ParseExact(borrowed, "yyyy-MM-dd"),
            DateOnly.ParseExact(due, "yyyy-MM-dd"), returned is null ? null : DateOnly.ParseExact(returned, "yyyy-MM-dd"),
            from, to, fine, paidOn is not null, paidOn is null ? null : DateOnly.ParseExact(paidOn, "yyyy-MM-dd"),
            "Borrower " + id, "CARD-" + id);
    }
}

