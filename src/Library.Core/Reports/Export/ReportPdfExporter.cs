using System.Globalization;
using Library.Core.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static Library.Core.Reports.Export.ExportFormatting;

namespace Library.Core.Reports.Export;

public sealed class ReportPdfExporter(IClock clock) : IReportPdfExporter
{
    public ReportExport ExportBorrowingStatistics(BorrowingStatisticsReport report) => Create(
        "Borrowing statistics", Range(report.Filter),
        FileName("borrowing-statistics", report.Filter.From, report.Filter.To, report.Filter.BranchId, "pdf"), column =>
        {
            Table(column, "Summary", ["Loans", "Active", "Overdue", "Returned", "Returned late", "Returned on time"],
                [Cells(report.Loans, report.Active, report.Overdue, report.Returned, report.ReturnedLate, report.ReturnedOnTime)]);
            if (report.IsEmpty) column.Item().Text(NoData);
            Table(column, "By month", ["Month", "Loans"], report.ByMonth.Select(m => Cells(Date(m.Month), m.Count)));
            Table(column, "By branch", ["Branch", "Loans"], report.ByBranch.Select(b => Cells(Branch(b.Branch), b.Count)));
            Table(column, "By type", ["Type", "Loans"], report.ByType.Select(t => Cells(t.Type, t.Count)));
            Table(column, "By category", ["Category", "Loans"], report.ByCategory.Select(c => Cells(c.Category, c.Count)));
            Table(column, "Top borrowed items", ["Code", "Name", "Borrowed at branches", "Loans"],
                report.TopItems.Select(i => Cells(i.Code, i.Name, string.Join(", ", i.Branches.Select(Branch)), i.Loans)));
        });

    public ReportExport ExportFineRevenue(FineRevenueReport report) => Create(
        "Fine revenue", Range(report.Filter),
        FileName("fine-revenue", report.Filter.From, report.Filter.To, report.Filter.BranchId, "pdf"), column =>
        {
            Table(column, "Summary", ["Charged", "Collected", "Outstanding"], [Cells(Money(report.Charged), Money(report.Collected), Money(report.Outstanding))]);
            column.Item().Text($"Accruing on overdue loans (not yet charged): {Money(report.AccruingNotCharged)}");
            Table(column, "By month", ["Month", "Charged", "Collected", "Outstanding"],
                report.ByMonth.Select(m => Cells(Date(m.Month), Money(m.Charged), Money(m.Collected), Money(m.Outstanding))));
            Table(column, "Fine lines", ["Loan / item", "Patron / card", "Returned at", "Due / returned", "Days late / amount", "Paid / paid on"],
                report.Lines.Select(l => Cells($"{l.LoanId}\n{l.ItemCode}\n{l.ItemName}", $"{l.BorrowerName}\n{l.CardNumber}", Branch(l.ReturnedAtBranch),
                    $"{Date(l.DueOn)}\n{Date(l.ReturnedOn)}", $"{l.DaysLate}\n{Money(l.Amount)}", $"{YesNo(l.Paid)}\n{Date(l.PaidOn)}")));
        });

    public ReportExport ExportInventoryHealth(InventoryHealthReport report) => Create(
        "Inventory health", $"As at: {Date(report.Today)}; Branch: {report.BranchId?.ToString(CultureInfo.InvariantCulture) ?? "All"}; Idle days: {report.IdleDays}",
        FileName("inventory-health", report.Today, report.Today, report.BranchId, "pdf"), column =>
        {
            if (report.IsEmpty) column.Item().Text(NoData);
            Table(column, "Items by status and branch", ["Branch", "Status", "Items"], report.ByStatusAndBranch.Select(s => Cells(Branch(s.Branch), s.Status, s.Count)));
            Table(column, "Items per category", ["Category", "Items"], report.ByCategory.Select(c => Cells(c.Category, c.Count)));
            Table(column, "Damaged", ["Code", "Name", "Branch"], report.Damaged.Select(i => Cells(i.Code, i.Name, Branch(i.Branch))));
            Table(column, "Overdue", ["Code", "Name", "Branch", "Days overdue"], report.Overdue.Select(i => Cells(i.Code, i.Name, Branch(i.Branch), i.DaysOverdue)));
            Table(column, "In transit (overlaps with the table above)", ["Code", "Name", "From", "To", "Transfer status"],
                report.InTransit.Select(t => Cells(t.ItemCode, t.ItemName, Branch(t.From), Branch(t.To), t.Status)));
            Table(column, "Idle items", ["Code", "Name", "Branch", "Last borrowed", "Days idle"],
                report.Idle.Select(i => Cells(i.Code, i.Name, Branch(i.Branch), i.LastBorrowedOn is null ? "never" : Date(i.LastBorrowedOn), i.DaysIdle?.ToString(CultureInfo.InvariantCulture) ?? "unknown")));
            Table(column, "Waitlist lengths", ["Code", "Name", "Branch", "Waiting", "Ready"],
                report.Waitlists.Select(i => Cells(i.Code, i.Name, Branch(i.Branch), i.Waiting, i.Ready)));
        });

    private ReportExport Create(string title, string filters, string name, Action<ColumnDescriptor> content)
    {
        var generated = clock.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var bytes = Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(style => style.FontSize(9));
            page.Header().PaddingBottom(12).Column(column =>
            {
                column.Item().Text(title).FontSize(18).Bold();
                column.Item().Text($"Generated: {generated}");
                column.Item().Text(filters);
            });
            page.Content().Column(column => { column.Spacing(8); content(column); });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page "); text.CurrentPageNumber(); text.Span(" of "); text.TotalPages();
            });
        })).GeneratePdf();
        return new(bytes, name);
    }

    private static string[] Cells(params object[] values) => values.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "").ToArray();

    private static void Table(ColumnDescriptor column, string title, string[] headers, IEnumerable<string[]> rows)
    {
        column.Item().Text(title).FontSize(11).Bold();
        var data = rows.ToArray();
        if (data.Length == 0) { column.Item().Text(NoData); return; }
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns => { foreach (var _ in headers) columns.RelativeColumn(); });
            table.Header(header =>
            {
                foreach (var label in headers) header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(label).Bold();
            });
            foreach (var row in data)
                foreach (var value in row) table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(value);
        });
    }
}
