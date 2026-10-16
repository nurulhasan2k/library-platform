namespace Library.Core.Reports;

/// <summary>A table inside a report. Shared by the web dashboard, the CSV export and the PDF export.</summary>
public sealed record ReportTable(string Heading, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<int>? RightAligned = null)
{
    public static ReportTable Of(string heading, string[] columns, IEnumerable<string[]> rows, params int[] rightAligned) =>
        new(heading, columns, rows.Select(r => (IReadOnlyList<string>)r).ToList(), rightAligned);
}

/// <summary>A finished report: a title, headline figures and one or more tables.</summary>
public sealed record ReportDocument(
    string Title,
    string Subtitle,
    DateTime GeneratedAt,
    IReadOnlyList<KeyValuePair<string, string>> Summary,
    IReadOnlyList<ReportTable> Tables)
{
    public string FileStem => Title.ToLowerInvariant().Replace(' ', '-');
}

public sealed record ReportFilter(DateOnly From, DateOnly To, int? BranchId);

/// <summary>A labelled number for the simple bar charts on the dashboard.</summary>
public sealed record Bar(string Label, decimal Value, string Display);
