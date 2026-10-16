using System.Globalization;
using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Reports;
using Library.Core.Reports.Export;
using Library.Web.Models.Reports;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace Library.Web.Reports;

public enum ReportScreen { Dashboard, Borrowing, FineRevenue, Inventory }
public sealed record ReportScreenResult(ReportPageViewModel Page, ReportExport? Export = null);

public sealed class ReportScreenService(IReportService reports, ICatalogueQuery catalogue, IClock clock,
    IOptions<ReportOptions> options, IReportDataSource source, IReportCsvExporter csv, IReportPdfExporter pdf)
{
    public async Task<ReportScreenResult> BuildAsync(ReportScreen screen, ReportFilterInput filter, string? format, ModelStateDictionary errors, CancellationToken ct)
    {
        var today = clock.Today;
        var branches = await catalogue.GetBranchesAsync(ct);
        var page = new ReportPageViewModel
        {
            Title = screen switch { ReportScreen.Dashboard => "Manager dashboard", ReportScreen.Borrowing => "Borrowing statistics", ReportScreen.FineRevenue => "Fine revenue audit", _ => "Inventory health" },
            Action = screen == ReportScreen.Dashboard ? "Index" : screen.ToString(),
            Filter = filter, Today = today, Inventory = screen == ReportScreen.Inventory,
            Dashboard = screen == ReportScreen.Dashboard, SampleData = source is SampleReportDataSource,
            Branches = branches.OrderBy(b => b.Name).Select(b => new SelectListItem($"{b.Code} — {b.Name}", b.Id.ToString(CultureInfo.InvariantCulture), b.Id == filter.BranchId)).ToArray()
        };
        filter.To ??= today;
        if (options.Value.DefaultRangeDays < 1) errors.AddModelError("", "Default range days must be greater than zero.");
        filter.From ??= DateOnly.FromDayNumber((int)Math.Max(0L, (long)today.DayNumber - Math.Max(1, options.Value.DefaultRangeDays) + 1));
        filter.IdleDays ??= options.Value.IdleItemDays;
        if (filter.BranchId is not null && !branches.Any(b => b.Id == filter.BranchId)) errors.AddModelError("BranchId", "Choose a known branch.");
        if (format is not null && format is not ("csv" or "pdf")) errors.AddModelError("", "Choose CSV or PDF for the export.");
        if (!errors.IsValid) return new(page);
        var range = new ReportFilter(filter.From.Value, filter.To.Value, filter.BranchId);
        ReportExport? export = null;
        if (screen is ReportScreen.Dashboard or ReportScreen.Borrowing)
        {
            var result = await reports.GetBorrowingStatisticsAsync(range, ct);
            AddErrors(result.Errors);
            if (result.Report is { } report)
            {
                if (page.Dashboard) page.Tiles.Add(new("Loans in period", Number(report.Loans)));
                else
                {
                    ReportPageMapping.Borrowing(page, report);
                    if (!report.IsEmpty) export = format switch { "csv" => csv.ExportBorrowingStatistics(report), "pdf" => pdf.ExportBorrowingStatistics(report), _ => null };
                }
                page.Empty = report.IsEmpty;
            }
        }
        if (screen is ReportScreen.Dashboard or ReportScreen.FineRevenue)
        {
            var result = await reports.GetFineRevenueAsync(range, ct);
            AddErrors(result.Errors);
            if (result.Report is { } report)
            {
                if (page.Dashboard) page.Tiles.Add(new("Fines charged", Money(report.Charged)));
                else
                {
                    ReportPageMapping.Fines(page, report);
                    if (!report.IsEmpty) export = format switch { "csv" => csv.ExportFineRevenue(report), "pdf" => pdf.ExportFineRevenue(report), _ => null };
                }
                page.Empty = page.Dashboard ? page.Empty && report.IsEmpty : report.IsEmpty;
            }
        }
        if (screen is ReportScreen.Dashboard or ReportScreen.Inventory)
        {
            var result = await reports.GetInventoryHealthAsync(filter.BranchId, filter.IdleDays, ct);
            AddErrors(result.Errors);
            if (result.Report is { } report)
            {
                if (page.Dashboard)
                {
                    page.Tiles.Add(new("Items as at today", Number(report.Items.Count)));
                    page.Tiles.Add(new("Overdue items", Number(report.Overdue.Count)));
                }
                else
                {
                    ReportPageMapping.Inventory(page, report);
                    if (!report.IsEmpty) export = format switch { "csv" => csv.ExportInventoryHealth(report), "pdf" => pdf.ExportInventoryHealth(report), _ => null };
                }
                page.Empty = page.Dashboard ? page.Empty && report.IsEmpty : report.IsEmpty;
            }
        }
        if (format is not null && page.Empty && errors.IsValid) errors.AddModelError("", "No data for this period. Choose different filters before exporting.");
        return new(page, errors.IsValid ? export : null);

        void AddErrors(IEnumerable<string> messages) { foreach (var message in messages) errors.AddModelError("", message); }
    }

    internal static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    internal static string Money(decimal value) => "$" + value.ToString("0.00", CultureInfo.InvariantCulture);
    internal static string Date(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
}
