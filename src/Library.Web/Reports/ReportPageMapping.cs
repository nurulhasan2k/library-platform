using System.Globalization;
using Library.Core.Reports;
using Library.Web.Models.Reports;
using static Library.Web.Reports.ReportScreenService;

namespace Library.Web.Reports;

internal static class ReportPageMapping
{
    public static void Borrowing(ReportPageViewModel page, BorrowingStatisticsReport report)
    {
        page.Tiles.AddRange([new("Loans", Number(report.Loans)), new("Active", Number(report.Active)), new("Overdue", Number(report.Overdue)),
            new("Returned", Number(report.Returned)), new("Returned late", Number(report.ReturnedLate)), new("Returned on time", Number(report.ReturnedOnTime))]);
        page.Tables.Add(new("By month", ["Month", "Loans"], report.ByMonth.Select(m => Row(Date(m.Month), m.Count)).ToArray(), true));
        Add(page, "By branch", ["Branch", "Loans"], report.ByBranch.Select(b => Row(Branch(b.Branch), b.Count)));
        Add(page, "By type", ["Type", "Loans"], report.ByType.Select(t => Row(t.Type, t.Count)));
        Add(page, "By category", ["Category", "Loans"], report.ByCategory.Select(c => Row(c.Category, c.Count)));
        Add(page, "Top borrowed items", ["Code", "Name", "Borrowed at branches", "Loans"], report.TopItems.Select(i => Row(i.Code, i.Name, string.Join(", ", i.Branches.Select(Branch)), i.Loans)));
    }

    public static void Fines(ReportPageViewModel page, FineRevenueReport report)
    {
        page.Tiles.AddRange([new("Charged", Money(report.Charged)), new("Collected", Money(report.Collected)), new("Outstanding", Money(report.Outstanding))]);
        page.Accruing = Money(report.AccruingNotCharged);
        page.Reconciliation = $"Charged {Money(report.Charged)} = sum of lines {Money(report.Lines.Sum(l => l.Amount))}; Collected {Money(report.Collected)} + Outstanding {Money(report.Outstanding)} = Charged {Money(report.Charged)}. " + (report.Reconciles ? "Reconciliation passed." : "Reconciliation failed.");
        Add(page, "By month", ["Month", "Charged", "Collected", "Outstanding"], report.ByMonth.Select(m => Row(Date(m.Month), Money(m.Charged), Money(m.Collected), Money(m.Outstanding))));
        Add(page, "Fine lines", ["Loan", "Code", "Item", "Patron", "Card number", "Returned at", "Due on", "Returned on", "Days late", "Amount", "Paid", "Paid on"],
            report.Lines.Select(l => Row(l.LoanId, l.ItemCode, l.ItemName, l.BorrowerName, l.CardNumber, Branch(l.ReturnedAtBranch), Date(l.DueOn), Date(l.ReturnedOn), l.DaysLate, Money(l.Amount), l.Paid ? "Yes" : "No", Date(l.PaidOn))));
    }

    public static void Inventory(ReportPageViewModel page, InventoryHealthReport report)
    {
        page.Tiles.AddRange([new("Items", Number(report.Items.Count)), new("Damaged", Number(report.Damaged.Count)), new("Overdue", Number(report.Overdue.Count)), new("Idle", Number(report.Idle.Count))]);
        Add(page, "Items by status and branch", ["Branch", "Status", "Count"], report.ByStatusAndBranch.Select(s => Row(Branch(s.Branch), s.Status, s.Count)));
        Add(page, "Items per category", ["Category", "Count"], report.ByCategory.Select(c => Row(c.Category, c.Count)));
        Add(page, "Damaged", ["Code", "Name", "Branch"], report.Damaged.Select(i => Row(i.Code, i.Name, Branch(i.Branch))));
        Add(page, "Overdue", ["Code", "Name", "Branch", "Days overdue"], report.Overdue.Select(i => Row(i.Code, i.Name, Branch(i.Branch), i.DaysOverdue)));
        Add(page, "In transit (overlaps with the table above)", ["Code", "Name", "From", "To", "Status"], report.InTransit.Select(t => Row(t.ItemCode, t.ItemName, Branch(t.From), Branch(t.To), t.Status)));
        Add(page, "Idle items", ["Code", "Name", "Branch", "Last borrowed", "Days idle"], report.Idle.Select(i => Row(i.Code, i.Name, Branch(i.Branch), i.LastBorrowedOn is null ? "never" : Date(i.LastBorrowedOn), i.DaysIdle?.ToString(CultureInfo.InvariantCulture) ?? "unknown")));
        Add(page, "Waitlist lengths", ["Code", "Name", "Branch", "Waiting", "Ready"], report.Waitlists.Select(i => Row(i.Code, i.Name, Branch(i.Branch), i.Waiting, i.Ready)));
    }

    private static string Branch(ReportBranch? branch) => branch is null ? "" : $"{branch.Code} ({branch.Name})";
    private static string[] Row(params object[] values) => values.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "").ToArray();
    private static void Add(ReportPageViewModel page, string title, string[] headers, IEnumerable<string[]> rows) => page.Tables.Add(new(title, headers, rows.ToArray()));
}
