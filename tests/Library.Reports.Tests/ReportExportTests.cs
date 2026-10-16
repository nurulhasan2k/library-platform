using System.Globalization;
using System.Text;
using CsvHelper;
using Library.Core;
using Library.Core.Contracts;
using Library.Core.Reports;
using Library.Core.Reports.Export;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig;
using Xunit;

namespace Library.Reports.Tests;

public sealed class ReportExportTests
{
    private readonly IReportCsvExporter csv;
    private readonly IReportPdfExporter pdf;

    public ReportExportTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(new FixedClock());
        services.AddLibraryReportExports();
        var provider = services.BuildServiceProvider();
        csv = provider.GetRequiredService<IReportCsvExporter>();
        pdf = provider.GetRequiredService<IReportPdfExporter>();
    }

    private static BorrowingStatisticsReport Borrowing => BorrowingStatisticsCalculator.Calculate(FixedReportData.Loans, FixedReportData.BorrowingRange, FixedReportData.Today).Report!;
    private static FineRevenueReport Fines => FineRevenueCalculator.Calculate(FixedReportData.Loans, FixedReportData.FineRange, FixedReportData.Today, 0.5m).Report!;
    private static InventoryHealthReport Inventory => InventoryHealthCalculator.Calculate(FixedReportData.Items, FixedReportData.Queues, FixedReportData.Transfers, FixedReportData.Today, 30).Report!;

    [Fact]
    public void Csv_headers_bom_and_detail_row_counts_match_each_report()
    {
        Check(csv.ExportBorrowingStatistics(Borrowing), "loanId,libraryCode,itemName,type,category,borrowedAtBranch,borrowedOn,dueOn,returnedOn,state", Borrowing.Lines.Count);
        Check(csv.ExportFineRevenue(Fines), "loanId,libraryCode,itemName,patronName,cardNumber,returnedAtBranch,dueOn,returnedOn,daysLate,amount,paid,paidOn", Fines.Lines.Count);
        Check(csv.ExportInventoryHealth(Inventory), "code,name,type,category,branch,status,inTransit,overdue,idle,daysSinceLastBorrowed,waitingCount", Inventory.Items.Count);
        static void Check(ReportExport result, string header, int count)
        {
            Assert.Equal(new byte[] { 239, 187, 191 }, result.Content.Take(3));
            Assert.StartsWith(header + "\r\n", Encoding.UTF8.GetString(result.Content[3..]));
            Assert.Equal(count + 1, Read(result).Count);
        }
    }

    [Fact]
    public void Csv_round_trips_commas_quotes_and_embedded_newlines_in_all_reports()
    {
        const string value = "A, \"quoted\" title\r\nwith another line";
        Assert.Equal(value, Read(csv.ExportBorrowingStatistics(Borrowing with { Lines = [Borrowing.Lines[0] with { ItemName = value }] }))[1][2]);
        Assert.Equal(value, Read(csv.ExportFineRevenue(Fines with { Lines = [Fines.Lines[0] with { ItemName = value }] }))[1][2]);
        Assert.Equal(value, Read(csv.ExportInventoryHealth(Inventory with { Items = [Inventory.Items[0] with { Name = value }] }))[1][1]);
    }

    [Theory]
    [InlineData("=SUM(1,2)")]
    [InlineData("+12")]
    [InlineData("-12")]
    [InlineData("@formula")]
    [InlineData(" \t=1")]
    public void Csv_neutralises_formulas_in_every_text_column(string value)
    {
        var branch = new ReportBranch(1, value, value);
        var loan = Borrowing.Lines[0] with { ItemCode = value, ItemName = value, Category = value, BorrowedAtBranch = branch };
        var fine = Fines.Lines[0] with { ItemCode = value, ItemName = value, BorrowerName = value, CardNumber = value, ReturnedAtBranch = branch };
        var item = Inventory.Items[0] with { Code = value, Name = value, Category = value, Branch = branch };
        var borrowing = Read(csv.ExportBorrowingStatistics(Borrowing with { Lines = [loan] }))[1];
        foreach (var index in new[] { 1, 2, 4, 5 }) Assert.StartsWith("'", borrowing[index]);
        var fines = Read(csv.ExportFineRevenue(Fines with { Lines = [fine] }))[1];
        foreach (var index in new[] { 1, 2, 3, 4, 5 }) Assert.StartsWith("'", fines[index]);
        var inventory = Read(csv.ExportInventoryHealth(Inventory with { Items = [item] }))[1];
        foreach (var index in new[] { 0, 1, 3, 4 }) Assert.StartsWith("'", inventory[index]);
    }

    [Fact]
    public void Csv_dates_money_and_file_names_are_invariant()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var report = Fines with { Filter = Fines.Filter with { BranchId = 2 } };
            var result = csv.ExportFineRevenue(report);
            var row = Read(result)[1];
            Assert.Equal("2.50", row[9]);
            Assert.Equal("2026-09-26", row[6]);
            Assert.Equal("2026-10-01", row[7]);
            Assert.Equal("Yes", row[10]);
            Assert.Equal("2026-10-02", row[11]);
            Assert.Equal("fine-revenue-2026-08-01_2026-10-05-2.csv", result.FileName);
            Assert.Equal("borrowing-statistics-2026-08-01_2026-09-30.csv", csv.ExportBorrowingStatistics(Borrowing).FileName);
            Assert.Equal("inventory-health-2026-10-05_2026-10-05.csv", csv.ExportInventoryHealth(Inventory).FileName);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void All_pdfs_have_content_clock_time_filters_and_page_numbers()
    {
        foreach (var result in new[] { pdf.ExportBorrowingStatistics(Borrowing), pdf.ExportFineRevenue(Fines), pdf.ExportInventoryHealth(Inventory) })
        {
            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(result.Content));
            Assert.True(result.Content.Length > 1000);
            using var document = PdfDocument.Open(result.Content);
            var text = Text(document);
            Assert.Contains("Generated:2026-10-0514:15:16", text);
            Assert.Contains("Branch:All", text);
            Assert.Contains($"Page1of{document.NumberOfPages}", text);
            Assert.EndsWith(".pdf", result.FileName);
            Assert.InRange((double)document.GetPage(1).Width, 594, 596);
            Assert.InRange((double)document.GetPage(1).Height, 841, 843);
        }
        using var inventory = PdfDocument.Open(pdf.ExportInventoryHealth(Inventory).Content);
        Assert.Contains("Idledays:30", Text(inventory));
        Assert.Contains("Ready", Text(inventory));
    }

    [Fact]
    public void Fine_table_continues_on_multiple_pages_with_repeated_headers()
    {
        var lines = Enumerable.Range(1, 150).Select(id => Fines.Lines[0] with { LoanId = id, ItemCode = $"CODE-{id}" }).ToArray();
        using var document = PdfDocument.Open(pdf.ExportFineRevenue(Fines with { Lines = lines }).Content);
        Assert.True(document.NumberOfPages > 2);
        foreach (var page in document.GetPages().Skip(1))
        {
            var text = Compact(page.Text);
            Assert.Contains("Patron/card", text);
            Assert.Contains($"Page{page.Number}of{document.NumberOfPages}", text);
        }
        Assert.Contains("CODE-150", Text(document));
    }

    [Fact]
    public void Empty_reports_export_headers_and_no_data_messages_in_both_formats()
    {
        var borrowing = BorrowingStatisticsCalculator.Calculate([], FixedReportData.EmptyRange, FixedReportData.Today).Report!;
        var fines = FineRevenueCalculator.Calculate([], FixedReportData.EmptyRange, FixedReportData.Today, 0.5m).Report!;
        var inventory = InventoryHealthCalculator.Calculate([], [], [], FixedReportData.Today, 30).Report!;
        foreach (var result in new[] { csv.ExportBorrowingStatistics(borrowing), csv.ExportFineRevenue(fines), csv.ExportInventoryHealth(inventory) })
        {
            Assert.Equal(2, Read(result).Count);
            Assert.Contains("No data for this period", Read(result)[1]);
        }
        foreach (var result in new[] { pdf.ExportBorrowingStatistics(borrowing), pdf.ExportFineRevenue(fines), pdf.ExportInventoryHealth(inventory) })
        {
            using var document = PdfDocument.Open(result.Content);
            Assert.Contains("Nodataforthisperiod", Text(document));
        }
    }

    private static List<string[]> Read(ReportExport result)
    {
        using var stream = new MemoryStream(result.Content);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var rows = new List<string[]>();
        while (csv.Read()) rows.Add(csv.Parser.Record!.ToArray());
        return rows;
    }

    private static string Compact(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
    private static string Text(PdfDocument document) => Compact(string.Concat(document.GetPages().Select(p => p.Text)));
    private sealed class FixedClock : IClock
    {
        public DateOnly Today => FixedReportData.Today;
        public DateTime Now => new(2026, 10, 5, 14, 15, 16);
    }
}
