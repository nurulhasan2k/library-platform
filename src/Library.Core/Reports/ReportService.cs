using System.Globalization;
using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Models;
using Library.Core.Services;
using Microsoft.Extensions.Options;

namespace Library.Core.Reports;

/// <summary>
/// Manager analytics (feature 3.7): borrowing statistics, fine revenue and inventory health. Every report is built as a
/// <see cref="ReportDocument"/> so the dashboard, the CSV export and the PDF export always show the same numbers.
/// </summary>
public sealed class ReportService(ILibraryStore store, IClock clock, IOptions<ReportOptions> reportOptions, IOptions<LendingOptions> lending)
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static string Money(decimal v) => "$" + v.ToString("0.00", Inv);
    private static string Pct(int part, int whole) => whole == 0 ? "-" : (100.0 * part / whole).ToString("0", Inv) + "%";

    public ReportFilter DefaultFilter() => new(clock.Today.AddDays(-179), clock.Today, null);

    // ------------------------------------------------------------ borrowing

    public ReportDocument Borrowing(ReportFilter f) => store.Read(d =>
    {
        var loans = d.Loans.Where(l => l.BorrowedOn >= f.From && l.BorrowedOn <= f.To && (f.BranchId is null || l.BranchId == f.BranchId)).ToList();
        var returned = loans.Where(l => !l.IsActive).ToList();
        var onTime = returned.Count(l => l.ReturnedOn <= l.DueOn);
        var avgDays = returned.Count == 0 ? 0 : returned.Average(l => l.ReturnedOn!.Value.DayNumber - l.BorrowedOn.DayNumber);
        var overdueNow = d.Loans.Count(l => l.IsActive && l.DaysLate(clock.Today) > 0 && (f.BranchId is null || l.BranchId == f.BranchId));
        var crossBranch = returned.Count(l => l.ReturnBranchId is { } rb && rb != l.BranchId);
        var kiosk = loans.Count(l => l.Channel == LoanChannel.Kiosk);

        var summary = new List<KeyValuePair<string, string>>
        {
            new("Loans in period", loans.Count.ToString()),
            new("Returned", returned.Count.ToString()),
            new("Still out", (loans.Count - returned.Count).ToString()),
            new("Returned on time", Pct(onTime, returned.Count)),
            new("Average loan length (days)", avgDays.ToString("0.0", Inv)),
            new("Overdue right now", overdueNow.ToString()),
            new("Lent at a kiosk", Pct(kiosk, loans.Count)),
            new("Returned at a different branch", Pct(crossBranch, returned.Count)),
        };

        string TypeName(Loan l) => d.FindItem(l.ItemId)?.Type.ToString() ?? "?";
        var byType = loans.GroupBy(TypeName).OrderByDescending(g => g.Count())
            .Select(g => new[] { g.Key, g.Count().ToString(), Pct(g.Count(), loans.Count) });
        var byCategory = loans.GroupBy(l => d.Categories.FirstOrDefault(c => c.Id == d.FindItem(l.ItemId)?.CategoryId)?.Name ?? "(none)")
            .OrderByDescending(g => g.Count()).Select(g => new[] { g.Key, g.Count().ToString(), Pct(g.Count(), loans.Count) });
        var byBranch = loans.GroupBy(l => d.FindBranch(l.BranchId)?.Name ?? "?").OrderByDescending(g => g.Count())
            .Select(g => new[] { g.Key, g.Count().ToString(), Pct(g.Count(), loans.Count) });
        var byMonth = loans.GroupBy(l => l.BorrowedOn.ToString("yyyy-MM")).OrderBy(g => g.Key)
            .Select(g => new[] { g.Key, g.Count().ToString() });
        var top = loans.GroupBy(l => l.ItemId).OrderByDescending(g => g.Count()).ThenBy(g => d.FindItem(g.Key)?.Name).Take(10)
            .Select(g => { var i = d.FindItem(g.Key); return new[] { i?.LibraryCode ?? "?", i?.Name ?? "?", i?.Type.ToString() ?? "?", g.Count().ToString() }; });

        return new ReportDocument("Borrowing statistics", Subtitle(d, f), clock.Now, summary, new[]
        {
            ReportTable.Of("Loans by month", new[] { "Month", "Loans" }, byMonth, 1),
            ReportTable.Of("Loans by item type", new[] { "Type", "Loans", "Share" }, byType, 1, 2),
            ReportTable.Of("Loans by category", new[] { "Category", "Loans", "Share" }, byCategory, 1, 2),
            ReportTable.Of("Loans by branch", new[] { "Branch", "Loans", "Share" }, byBranch, 1, 2),
            ReportTable.Of("Ten most borrowed items", new[] { "Code", "Item", "Type", "Loans" }, top, 3),
        });
    });

    // ---------------------------------------------------------------- fines

    public ReportDocument Fines(ReportFilter f) => store.Read(d =>
    {
        var fined = d.Loans.Where(l => !l.IsActive && l.FineAmount > 0 && l.ReturnedOn >= f.From && l.ReturnedOn <= f.To &&
                                       (f.BranchId is null || (l.ReturnBranchId ?? l.BranchId) == f.BranchId)).ToList();
        var charged = fined.Sum(l => l.FineAmount);
        var collected = fined.Where(l => l.FinePaid).Sum(l => l.FineAmount);
        var outstanding = charged - collected;
        var accruing = d.Loans.Where(l => l.IsActive && l.DaysLate(clock.Today) > 0 && (f.BranchId is null || l.BranchId == f.BranchId))
            .Sum(l => l.DaysLate(clock.Today) * lending.Value.FinePerDay);

        var summary = new List<KeyValuePair<string, string>>
        {
            new("Fines charged", Money(charged)),
            new("Fines collected", Money(collected)),
            new("Fines outstanding", Money(outstanding)),
            new("Collection rate", charged == 0 ? "-" : (100 * collected / charged).ToString("0", Inv) + "%"),
            new("Loans with a fine", fined.Count.ToString()),
            new("Accruing on overdue loans (not yet charged)", Money(accruing)),
        };

        var byMonth = fined.GroupBy(l => l.ReturnedOn!.Value.ToString("yyyy-MM")).OrderBy(g => g.Key)
            .Select(g => new[] { g.Key, g.Count().ToString(), Money(g.Sum(l => l.FineAmount)), Money(g.Where(l => l.FinePaid).Sum(l => l.FineAmount)),
                Money(g.Where(l => !l.FinePaid).Sum(l => l.FineAmount)) });
        var byBranch = fined.GroupBy(l => d.FindBranch(l.ReturnBranchId ?? l.BranchId)?.Name ?? "?").OrderByDescending(g => g.Sum(l => l.FineAmount))
            .Select(g => new[] { g.Key, g.Count().ToString(), Money(g.Sum(l => l.FineAmount)), Money(g.Where(l => l.FinePaid).Sum(l => l.FineAmount)),
                Money(g.Where(l => !l.FinePaid).Sum(l => l.FineAmount)) });
        var owing = fined.Where(l => !l.FinePaid).GroupBy(l => l.BorrowerId).OrderByDescending(g => g.Sum(l => l.FineAmount))
            .Select(g => { var b = d.FindBorrower(g.Key); return new[] { b?.CardNumber ?? "?", b?.Name ?? "?", g.Count().ToString(), Money(g.Sum(l => l.FineAmount)) }; });

        return new ReportDocument("Fine revenue", Subtitle(d, f), clock.Now, summary, new[]
        {
            ReportTable.Of("Fines by month (by return date)", new[] { "Month", "Loans fined", "Charged", "Collected", "Outstanding" }, byMonth, 1, 2, 3, 4),
            ReportTable.Of("Fines by branch (where returned)", new[] { "Branch", "Loans fined", "Charged", "Collected", "Outstanding" }, byBranch, 1, 2, 3, 4),
            ReportTable.Of("Patrons with unpaid fines", new[] { "Card", "Patron", "Loans", "Owing" }, owing, 2, 3),
        });
    });

    // ------------------------------------------------------------ inventory

    public ReportDocument Inventory(int? branchId) => store.Read(d =>
    {
        var today = clock.Today;
        var idleDays = reportOptions.Value.IdleItemDays;
        var items = d.Items.Where(i => branchId is null || i.BranchId == branchId).ToList();
        var live = items.Where(i => i.Status != ItemStatus.Destroy).ToList();
        var inTransit = live.Count(i => d.IsInTransit(i.Id));
        var borrowed = live.Count(i => i.Status == ItemStatus.Borrowed);

        DateOnly? LastBorrowed(int id) => d.Loans.Where(l => l.ItemId == id).Select(l => (DateOnly?)l.BorrowedOn).DefaultIfEmpty(null).Max();
        var idle = live.Where(i => i.Status != ItemStatus.Borrowed)
            .Select(i => (Item: i, Last: LastBorrowed(i.Id)))
            .Where(x => x.Last is null || today.DayNumber - x.Last.Value.DayNumber > idleDays)
            .OrderBy(x => x.Last ?? DateOnly.MinValue).ToList();

        var summary = new List<KeyValuePair<string, string>>
        {
            new("Items in catalogue (not destroyed)", live.Count.ToString()),
            new("Available now", live.Count(i => i.Status == ItemStatus.Available && !d.IsInTransit(i.Id)).ToString()),
            new("On loan", borrowed.ToString()),
            new("Damaged", live.Count(i => i.Status == ItemStatus.Damaged).ToString()),
            new("In transit / transfer pending", inTransit.ToString()),
            new("Destroyed (not shown publicly)", items.Count(i => i.Status == ItemStatus.Destroy).ToString()),
            new("Utilisation (on loan / catalogue)", Pct(borrowed, live.Count)),
            new($"Idle items (not borrowed for {idleDays}+ days)", idle.Count.ToString()),
        };

        var matrix = d.Branches.Where(b => branchId is null || b.Id == branchId).Select(b =>
        {
            var bi = d.Items.Where(i => i.BranchId == b.Id).ToList();
            return new[] { b.Name, bi.Count(i => i.Status != ItemStatus.Destroy).ToString(),
                bi.Count(i => i.Status == ItemStatus.Available && !d.IsInTransit(i.Id)).ToString(), bi.Count(i => i.Status == ItemStatus.Borrowed).ToString(),
                bi.Count(i => i.Status == ItemStatus.Damaged).ToString(), bi.Count(i => i.Status == ItemStatus.Destroy).ToString(),
                bi.Count(i => d.IsInTransit(i.Id)).ToString() };
        });
        var byType = live.GroupBy(i => i.Type).OrderBy(g => g.Key)
            .Select(g => new[] { g.Key.ToString(), g.Count().ToString(), g.Count(i => i.Status == ItemStatus.Borrowed).ToString(), Pct(g.Count(i => i.Status == ItemStatus.Borrowed), g.Count()) });
        var idleRows = idle.Take(100).Select(x => new[] { x.Item.LibraryCode, x.Item.Name, x.Item.Type.ToString(), d.FindBranch(x.Item.BranchId)?.Name ?? "?",
            x.Last is null ? "never" : x.Last.Value.ToString("yyyy-MM-dd"), x.Last is null ? "-" : (today.DayNumber - x.Last.Value.DayNumber).ToString() });
        var damaged = live.Where(i => i.Status == ItemStatus.Damaged).Select(i => new[] { i.LibraryCode, i.Name, i.Type.ToString(), d.FindBranch(i.BranchId)?.Name ?? "?" });
        var transfers = d.Transfers.Where(t => t.IsActive && (branchId is null || t.FromBranchId == branchId || t.ToBranchId == branchId))
            .Select(t => { var i = d.FindItem(t.ItemId); return new[] { i?.LibraryCode ?? "?", i?.Name ?? "?", d.FindBranch(t.FromBranchId)?.Name ?? "?", d.FindBranch(t.ToBranchId)?.Name ?? "?", t.Status.ToString(), t.RequestedAt.ToString("yyyy-MM-dd") }; });
        var demand = d.Reservations.Where(r => r.IsOpen).GroupBy(r => r.ItemId)
            .Where(g => branchId is null || d.FindItem(g.Key)?.BranchId == branchId)
            .OrderByDescending(g => g.Count())
            .Select(g => { var i = d.FindItem(g.Key); return new[] { i?.LibraryCode ?? "?", i?.Name ?? "?", d.FindBranch(i?.BranchId ?? 0)?.Name ?? "?", g.Count().ToString() }; });

        return new ReportDocument("Inventory health", branchId is { } bid ? $"Branch: {d.FindBranch(bid)?.Name} - as at {today:d MMM yyyy}" : $"All branches - as at {today:d MMM yyyy}",
            clock.Now, summary, new[]
        {
            ReportTable.Of("Stock by branch", new[] { "Branch", "In catalogue", "Available", "On loan", "Damaged", "Destroyed", "In transit" }, matrix, 1, 2, 3, 4, 5, 6),
            ReportTable.Of("Utilisation by item type", new[] { "Type", "Items", "On loan", "Utilisation" }, byType, 1, 2, 3),
            ReportTable.Of($"Idle items (not borrowed for {idleDays}+ days)", new[] { "Code", "Item", "Type", "Branch", "Last borrowed", "Days idle" }, idleRows, 5),
            ReportTable.Of("Damaged items awaiting a decision", new[] { "Code", "Item", "Type", "Branch" }, damaged),
            ReportTable.Of("Transfers in progress", new[] { "Code", "Item", "From", "To", "Status", "Requested" }, transfers),
            ReportTable.Of("Items with waiting holds", new[] { "Code", "Item", "Branch", "Open holds" }, demand, 3),
        });
    });

    // ------------------------------------------------------------ dashboard

    public sealed record Dashboard(
        int LoansLast30, int ActiveLoans, int OverdueLoans, decimal OutstandingFines, int OpenHolds, int ItemsInTransit,
        IReadOnlyList<Bar> LoansByMonth, IReadOnlyList<Bar> LoansByType, IReadOnlyList<Bar> LoansByBranch);

    public Dashboard GetDashboard(ReportFilter f) => store.Read(d =>
    {
        var today = clock.Today;
        var inRange = d.Loans.Where(l => l.BorrowedOn >= f.From && l.BorrowedOn <= f.To && (f.BranchId is null || l.BranchId == f.BranchId)).ToList();
        IReadOnlyList<Bar> Bars(IEnumerable<IGrouping<string, Loan>> g) =>
            g.Select(x => new Bar(x.Key, x.Count(), x.Count().ToString())).ToList();
        return new Dashboard(
            d.Loans.Count(l => l.BorrowedOn >= today.AddDays(-29) && (f.BranchId is null || l.BranchId == f.BranchId)),
            d.Loans.Count(l => l.IsActive && (f.BranchId is null || l.BranchId == f.BranchId)),
            d.Loans.Count(l => l.IsActive && l.DaysLate(today) > 0 && (f.BranchId is null || l.BranchId == f.BranchId)),
            d.Loans.Where(l => l.HasOutstandingFine).Sum(l => l.FineAmount),
            d.Reservations.Count(r => r.IsOpen),
            d.Transfers.Count(t => t.IsActive),
            Bars(inRange.GroupBy(l => l.BorrowedOn.ToString("yyyy-MM")).OrderBy(g => g.Key)),
            Bars(inRange.GroupBy(l => d.FindItem(l.ItemId)?.Type.ToString() ?? "?").OrderByDescending(g => g.Count())),
            Bars(inRange.GroupBy(l => d.FindBranch(l.BranchId)?.Name ?? "?").OrderByDescending(g => g.Count())));
    });

    private string Subtitle(LibraryData d, ReportFilter f) =>
        $"{f.From:d MMM yyyy} to {f.To:d MMM yyyy} - " + (f.BranchId is { } b ? d.FindBranch(b)?.Name : "all branches");
}
