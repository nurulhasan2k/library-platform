using System.Text;
using Library.Core.Import;

namespace Library.Core.Reports;

/// <summary>Turns a report into one CSV file: a summary block followed by each table, separated by a blank line.</summary>
public static class CsvExporter
{
    public static byte[] Export(ReportDocument doc)
    {
        var rows = new List<IEnumerable<string>>
        {
            new[] { doc.Title }, new[] { doc.Subtitle }, new[] { "Generated", doc.GeneratedAt.ToString("yyyy-MM-dd HH:mm") }, Array.Empty<string>()
        };
        if (doc.Summary.Count > 0)
        {
            rows.Add(new[] { "Summary" });
            rows.AddRange(doc.Summary.Select(s => new[] { s.Key, s.Value }));
            rows.Add(Array.Empty<string>());
        }
        foreach (var t in doc.Tables)
        {
            rows.Add(new[] { t.Heading });
            rows.Add(t.Columns);
            rows.AddRange(t.Rows);
            rows.Add(Array.Empty<string>());
        }
        // UTF-8 with a byte-order mark so Excel opens accented characters correctly.
        return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(Csv.Write(rows))).ToArray();
    }
}
