using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Library.Web.Models.Reports;

public sealed class ReportFilterInput
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Choose a valid branch.")]
    public int? BranchId { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "Idle days cannot be negative.")]
    public int? IdleDays { get; set; }
}

public sealed record ReportTile(string Label, string Value);
public sealed record ReportTable(string Title, IReadOnlyList<string> Headers, IReadOnlyList<string[]> Rows, bool MonthlyChart = false);

public sealed class ReportPageViewModel
{
    public string Title { get; set; } = "Reports";
    public string Action { get; set; } = "Index";
    public ReportFilterInput Filter { get; set; } = new();
    public IReadOnlyList<SelectListItem> Branches { get; set; } = [];
    public bool Inventory { get; set; }
    public bool Dashboard { get; set; }
    public bool SampleData { get; set; }
    public bool Empty { get; set; } = true;
    public DateOnly Today { get; set; }
    public List<ReportTile> Tiles { get; } = [];
    public List<ReportTable> Tables { get; } = [];
    public string? Reconciliation { get; set; }
    public string? Accruing { get; set; }
}
