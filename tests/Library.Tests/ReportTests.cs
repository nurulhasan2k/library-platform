using System.Text;
using Library.Core.Contracts;
using Library.Core.Models;
using Library.Core.Pdf;
using Library.Core.Reports;
using Library.Core.Services;

namespace Library.Tests;

public static class ReportTests
{
    public static void Run()
    {
        T.Section("Manager analytics and exports");
        using var p = new TestPlatform();
        var reports = p.Get<ReportService>();
        var f = reports.DefaultFilter();
        string Summary(ReportDocument d, string key) => d.Summary.First(s => s.Key == key).Value;

        T.Test("borrowing report counts every loan in the period and breaks it down", () =>
        {
            var doc = reports.Borrowing(f);
            var total = p.Store.Read(d => d.Loans.Count(l => l.BorrowedOn >= f.From && l.BorrowedOn <= f.To));
            T.Equal(total.ToString(), Summary(doc, "Loans in period"));
            T.Equal(total, doc.Tables.First(t => t.Heading == "Loans by month").Rows.Sum(r => int.Parse(r[1])));
            T.Equal(total, doc.Tables.First(t => t.Heading == "Loans by item type").Rows.Sum(r => int.Parse(r[1])));
            T.Equal(10, doc.Tables.First(t => t.Heading.StartsWith("Ten most")).Rows.Count);
        });
        T.Test("books are the biggest share of loans, as designed", () =>
        {
            var rows = reports.Borrowing(f).Tables.First(t => t.Heading == "Loans by item type").Rows;
            T.Equal("Book", rows[0][0]);
        });
        T.Test("filtering to one branch only counts that branch's loans", () =>
        {
            var north = p.Branch("NORTH").Id;
            var doc = reports.Borrowing(f with { BranchId = north });
            var expected = p.Store.Read(d => d.Loans.Count(l => l.BorrowedOn >= f.From && l.BorrowedOn <= f.To && l.BranchId == north));
            T.Equal(expected.ToString(), Summary(doc, "Loans in period"));
        });
        T.Test("fine revenue: charged = collected + outstanding, and matches the loans", () =>
        {
            var doc = reports.Fines(f);
            decimal Parse(string s) => decimal.Parse(s.TrimStart('$'));
            var charged = Parse(Summary(doc, "Fines charged"));
            T.Equal(charged, Parse(Summary(doc, "Fines collected")) + Parse(Summary(doc, "Fines outstanding")));
            var expected = p.Store.Read(d => d.Loans.Where(l => !l.IsActive && l.ReturnedOn >= f.From && l.ReturnedOn <= f.To).Sum(l => l.FineAmount));
            T.Equal(expected, charged);
            T.True(charged > 0 && Parse(Summary(doc, "Fines outstanding")) > 0, "seed data should include paid and unpaid fines");
        });
        T.Test("marking a fine paid moves it from outstanding to collected", () =>
        {
            var before = reports.Fines(f);
            var loan = p.Get<LoanQueryService>().GetLoans(new LoanFilter(UnpaidFinesOnly: true)).First(l => l.Loan.ReturnedOn >= f.From);
            p.Get<LoanQueryService>().MarkFinePaid(loan.Loan.Id);
            var after = reports.Fines(f);
            decimal Parse(string s) => decimal.Parse(s.TrimStart('$'));
            T.Equal(Parse(Summary(before, "Fines collected")) + loan.Loan.FineAmount, Parse(Summary(after, "Fines collected")));
            T.Equal(Summary(before, "Fines charged"), Summary(after, "Fines charged"));
        });
        T.Test("inventory health lists idle, damaged and in-transit items", () =>
        {
            var doc = reports.Inventory(null);
            T.True(int.Parse(Summary(doc, "Damaged")) >= 3);
            var idle = doc.Tables.First(t => t.Heading.StartsWith("Idle"));
            T.True(idle.Rows.Any(r => r[0] == "BK-016" && r[4] == "never"), "a never-borrowed item should be idle");
            T.True(doc.Tables.First(t => t.Heading == "Transfers in progress").Rows.Count == 2);
            T.True(doc.Tables.First(t => t.Heading.StartsWith("Damaged")).Rows.Any(r => r[0] == "MU-010"));
        });
        T.Test("inventory health totals add up per branch", () =>
        {
            var doc = reports.Inventory(null);
            var rows = doc.Tables.First(t => t.Heading == "Stock by branch").Rows;
            T.Equal(3, rows.Count);
            T.Equal(43, rows.Sum(r => int.Parse(r[1])));
        });
        T.Test("the dashboard figures agree with the reports", () =>
        {
            var dash = reports.GetDashboard(f);
            T.Equal(p.Store.Read(d => d.Loans.Count(l => l.IsActive)), dash.ActiveLoans);
            T.Equal(2, dash.OverdueLoans);
            T.Equal(2, dash.ItemsInTransit);
        });

        T.Test("CSV export has a summary and every table, opens with a BOM, and escapes commas and formulas", () =>
        {
            var doc = new ReportDocument("Test, report", "sub", new DateTime(2026, 1, 1), new[] { new KeyValuePair<string, string>("Total", "3") },
                new[] { ReportTable.Of("T", new[] { "A", "B" }, new[] { new[] { "x,y", "=SUM(A1)" } }) });
            var bytes = CsvExporter.Export(doc);
            T.True(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            var text = Encoding.UTF8.GetString(bytes);
            T.True(text.Contains("\"Test, report\""));
            T.True(text.Contains("\"x,y\",'=SUM(A1)"));
        });
        T.Test("CSV export of a real report contains its headline figures", () =>
        {
            var text = Encoding.UTF8.GetString(CsvExporter.Export(reports.Fines(f)));
            T.True(text.Contains("Fines charged") && text.Contains("Patrons with unpaid fines"));
        });
        T.Test("PDF export is a well-formed PDF with a trailer and at least one page", () =>
        {
            var bytes = PdfReportWriter.Write(reports.Borrowing(f));
            var text = Encoding.Latin1.GetString(bytes);
            T.True(text.StartsWith("%PDF-1.4"));
            T.True(text.TrimEnd().EndsWith("%%EOF"));
            T.True(text.Contains("/Type /Page "));
            T.True(text.Contains("Borrowing statistics"));
            // The xref table's offsets must point at "N 0 obj".
            var xrefAt = int.Parse(text[(text.LastIndexOf("startxref", StringComparison.Ordinal) + 10)..].Split('\n')[0]);
            T.True(text[xrefAt..].StartsWith("xref"));
        });
        T.Test("a long report paginates and survives awkward characters", () =>
        {
            var rows = Enumerable.Range(1, 300).Select(i => new[] { $"R{i}", "Café (\"quoted\") \\ back’s 中文", i.ToString() }).ToList();
            var doc = new ReportDocument("Big", "s", DateTime.Now, Array.Empty<KeyValuePair<string, string>>(), new[] { ReportTable.Of("Rows", new[] { "A", "B", "C" }, rows, 2) });
            var text = Encoding.Latin1.GetString(PdfReportWriter.Write(doc));
            T.True(System.Text.RegularExpressions.Regex.Matches(text, "/Type /Page ").Count >= 6);
            T.True(text.Contains("Page 1 of "));
        });
    }
}
