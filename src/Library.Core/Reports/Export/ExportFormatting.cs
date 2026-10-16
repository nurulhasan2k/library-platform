using System.Globalization;

namespace Library.Core.Reports.Export;

internal static class ExportFormatting
{
    public const string NoData = "No data for this period";
    public static string Date(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
    public static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    public static string YesNo(bool value) => value ? "Yes" : "No";
    public static string Branch(ReportBranch? branch) => branch is null ? "" : $"{branch.Code} ({branch.Name})";
    public static string FileName(string report, DateOnly from, DateOnly to, int? branch, string extension) =>
        $"{report}-{Date(from)}_{Date(to)}{(branch is null ? "" : "-" + branch.Value.ToString(CultureInfo.InvariantCulture))}.{extension}";
    public static string Range(ReportFilter filter) => $"Range: {Date(filter.From)} to {Date(filter.To)}; Branch: {filter.BranchId?.ToString(CultureInfo.InvariantCulture) ?? "All"}";
}
