using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using static Library.Core.Reports.Export.ExportFormatting;

namespace Library.Core.Reports.Export;

public sealed class ReportCsvExporter : IReportCsvExporter
{
    public ReportExport ExportBorrowingStatistics(BorrowingStatisticsReport report) => Write(
        FileName("borrowing-statistics", report.Filter.From, report.Filter.To, report.Filter.BranchId, "csv"),
        ["loanId", "libraryCode", "itemName", "type", "category", "borrowedAtBranch", "borrowedOn", "dueOn", "returnedOn", "state"],
        report.Lines.Select(l => new string[] { l.LoanId.ToString(CultureInfo.InvariantCulture), l.ItemCode, l.ItemName,
            l.ItemType.ToString().ToLowerInvariant(), l.Category, Branch(l.BorrowedAtBranch), Date(l.BorrowedOn), Date(l.DueOn), Date(l.ReturnedOn), l.State.ToString() }));

    public ReportExport ExportFineRevenue(FineRevenueReport report) => Write(
        FileName("fine-revenue", report.Filter.From, report.Filter.To, report.Filter.BranchId, "csv"),
        ["loanId", "libraryCode", "itemName", "patronName", "cardNumber", "returnedAtBranch", "dueOn", "returnedOn", "daysLate", "amount", "paid", "paidOn"],
        report.Lines.Select(l => new string[] { l.LoanId.ToString(CultureInfo.InvariantCulture), l.ItemCode, l.ItemName, l.BorrowerName,
            l.CardNumber, Branch(l.ReturnedAtBranch), Date(l.DueOn), Date(l.ReturnedOn), l.DaysLate.ToString(CultureInfo.InvariantCulture), Money(l.Amount), YesNo(l.Paid), Date(l.PaidOn) }));

    public ReportExport ExportInventoryHealth(InventoryHealthReport report) => Write(
        FileName("inventory-health", report.Today, report.Today, report.BranchId, "csv"),
        ["code", "name", "type", "category", "branch", "status", "inTransit", "overdue", "idle", "daysSinceLastBorrowed", "waitingCount"],
        report.Items.Select(i => new string[] { i.Code, i.Name, i.Type.ToString().ToLowerInvariant(), i.Category, Branch(i.Branch), i.Status.ToString(),
            YesNo(i.InTransit), YesNo(i.Overdue), YesNo(i.Idle), i.DaysSinceLastBorrowed?.ToString(CultureInfo.InvariantCulture) ?? "", i.Waiting.ToString(CultureInfo.InvariantCulture) }));

    private static ReportExport Write(string name, string[] headers, IEnumerable<string[]> rows)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true))
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ",", NewLine = "\r\n" }))
        {
            WriteRow(headers);
            var any = false;
            foreach (var row in rows) { WriteRow(row); any = true; }
            if (!any)
            {
                var empty = Enumerable.Repeat("", headers.Length).ToArray();
                empty[2] = NoData;
                WriteRow(empty);
            }
            void WriteRow(IEnumerable<string> values)
            {
                foreach (var value in values)
                {
                    var trimmed = value.TrimStart();
                    csv.WriteField(trimmed.Length > 0 && "=+-@".Contains(trimmed[0]) ? "'" + value : value);
                }
                csv.NextRecord();
            }
        }
        return new(stream.ToArray(), name);
    }
}
