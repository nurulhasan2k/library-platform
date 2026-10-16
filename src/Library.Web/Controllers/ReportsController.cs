using Library.Core.Pdf;
using Library.Core.Reports;
using Library.Core.Services;
using Library.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

public sealed class ReportPage
{
    public required ReportDocument Document { get; init; }
    public required string Route { get; init; }
    public required ReportFilter Filter { get; init; }
    public bool UsesDates { get; init; } = true;
}

/// <summary>Manager analytics (feature 3.7): a dashboard and three reports, each viewable on screen and exportable as CSV or PDF.</summary>
[Route("reports"), Authorize(Roles = Auth.AdminOrManager)]
public sealed class ReportsController(ReportService reports, InventoryService inventory) : AppController
{
    [HttpGet("")]
    public IActionResult Index(DateOnly? from, DateOnly? to, int? branch)
    {
        var f = Filter(from, to, branch);
        ViewBag.Filter = f;
        ViewBag.Branches = inventory.GetBranches();
        return View(reports.GetDashboard(f));
    }

    [HttpGet("borrowing")]
    public IActionResult Borrowing(DateOnly? from, DateOnly? to, int? branch, string? format) =>
        Render("borrowing", Filter(from, to, branch), f => reports.Borrowing(f), format, true);

    [HttpGet("fines")]
    public IActionResult Fines(DateOnly? from, DateOnly? to, int? branch, string? format) =>
        Render("fines", Filter(from, to, branch), f => reports.Fines(f), format, true);

    [HttpGet("inventory")]
    public IActionResult Inventory(int? branch, string? format) =>
        Render("inventory", Filter(null, null, branch), f => reports.Inventory(f.BranchId), format, false);

    private ReportFilter Filter(DateOnly? from, DateOnly? to, int? branch)
    {
        var d = reports.DefaultFilter();
        var f = new ReportFilter(from ?? d.From, to ?? d.To, branch);
        return f.From > f.To ? f with { From = f.To } : f;
    }

    private IActionResult Render(string route, ReportFilter f, Func<ReportFilter, ReportDocument> build, string? format, bool usesDates)
    {
        var doc = build(f);
        var stamp = DateTime.Now.ToString("yyyyMMdd");
        switch (format?.ToLowerInvariant())
        {
            case "csv": return File(CsvExporter.Export(doc), "text/csv", $"{doc.FileStem}-{stamp}.csv");
            case "pdf": return File(PdfReportWriter.Write(doc), "application/pdf", $"{doc.FileStem}-{stamp}.pdf");
        }
        ViewBag.Branches = inventory.GetBranches();
        return View("Report", new ReportPage { Document = doc, Route = route, Filter = f, UsesDates = usesDates });
    }
}
