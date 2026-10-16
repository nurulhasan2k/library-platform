using Library.Web.Models.Reports;
using Library.Web.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

[Authorize(Roles = "Manager,Admin")]
public sealed class ReportsController(ReportScreenService screens) : Controller
{
    [HttpGet]
    public Task<IActionResult> Index([FromQuery] ReportFilterInput filter, CancellationToken ct) => Show(ReportScreen.Dashboard, filter, null, ct);

    [HttpGet]
    public Task<IActionResult> Borrowing([FromQuery] ReportFilterInput filter, string? format, CancellationToken ct) => Show(ReportScreen.Borrowing, filter, format, ct);

    [HttpGet]
    public Task<IActionResult> FineRevenue([FromQuery] ReportFilterInput filter, string? format, CancellationToken ct) => Show(ReportScreen.FineRevenue, filter, format, ct);

    [HttpGet]
    public Task<IActionResult> Inventory([FromQuery] ReportFilterInput filter, string? format, CancellationToken ct) => Show(ReportScreen.Inventory, filter, format, ct);

    private async Task<IActionResult> Show(ReportScreen screen, ReportFilterInput filter, string? format, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await screens.BuildAsync(screen, filter, format, ModelState, ct);
        if (result.Export is { } export)
            return File(export.Content, format == "csv" ? "text/csv; charset=utf-8" : "application/pdf", export.FileName);
        return View("Report", result.Page);
    }
}
