using System.Text;

namespace Library.Core.Import;

/// <summary>A small RFC 4180 CSV reader/writer (quoted fields, doubled quotes, commas and line breaks inside quotes).</summary>
public static class Csv
{
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrEmpty(text)) return rows;
        if (text[0] == '﻿') text = text[1..];

        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var fieldWasQuoted = false;

        void EndField() { row.Add(field.ToString()); field.Clear(); fieldWasQuoted = false; }
        void EndRow()
        {
            EndField();
            if (!(row.Count == 1 && row[0].Length == 0 && !fieldWasQuoted)) rows.Add(row);
            row = new List<string>();
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else if (c == '"') { inQuotes = true; fieldWasQuoted = true; }
            else if (c == ',') EndField();
            else if (c == '\r') { if (i + 1 < text.Length && text[i + 1] == '\n') i++; EndRow(); }
            else if (c == '\n') EndRow();
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) EndRow();
        return rows;
    }

    public static string Escape(string? value)
    {
        value ??= "";
        // Guard against spreadsheet formula injection when a value starts with = + - @ (keep plain negative numbers).
        if (value.Length > 0 && "=+@".Contains(value[0])) value = "'" + value;
        return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    public static string Write(IEnumerable<IEnumerable<string>> rows)
    {
        var sb = new StringBuilder();
        foreach (var r in rows) sb.Append(string.Join(',', r.Select(Escape))).Append("\r\n");
        return sb.ToString();
    }
}
