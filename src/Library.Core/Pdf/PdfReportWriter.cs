using System.Globalization;
using System.Text;
using Library.Core.Reports;

namespace Library.Core.Pdf;

/// <summary>
/// A small, dependency-free PDF writer for report exports. It uses the built-in Helvetica fonts, so the file needs no
/// embedded fonts and opens in any PDF viewer. Layout: title block, summary box, then each table with a header row and
/// repeating headers across pages.
/// </summary>
public static class PdfReportWriter
{
    // Helvetica glyph widths (1000 units per em) for ASCII 32..126.
    private static readonly int[] W =
    {
        278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278, 556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,
        1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778, 667,778,722,667,611,722,667,944,667,667,611,278,278,278,469,556,
        333,556,556,500,556,556,278,556,556,222,222,500,222,833,556,556, 556,556,333,500,278,556,500,722,500,500,500,334,260,334,584
    };

    private static double Width(string s, double size, bool bold)
    {
        double w = 0;
        foreach (var ch in s) w += (ch >= 32 && ch <= 126 ? W[ch - 32] : 556) * (bold ? 1.06 : 1.0);
        return w * size / 1000.0;
    }

    private sealed class Page
    {
        public StringBuilder Content { get; } = new();
    }

    public static byte[] Write(ReportDocument doc)
    {
        // Landscape when a table is wide; otherwise portrait A4.
        var landscape = doc.Tables.Any(t => t.Columns.Count > 6);
        double pw = landscape ? 842 : 595, ph = landscape ? 595 : 842;
        const double margin = 40;
        var usable = pw - 2 * margin;
        var pages = new List<Page>();
        Page page = null!;
        double y = 0;

        void NewPage()
        {
            page = new Page();
            pages.Add(page);
            y = ph - margin;
        }

        void Text(double x, double yy, string text, double size, bool bold = false, double gray = 0)
        {
            page.Content.Append(CultureInfo.InvariantCulture, $"BT {gray:0.##} g /{(bold ? "F2" : "F1")} {size:0.##} Tf {x:0.##} {yy:0.##} Td ({Escape(text)}) Tj ET\n");
        }
        void Rect(double x, double yy, double w, double h, double gray)
        {
            page.Content.Append(CultureInfo.InvariantCulture, $"{gray:0.##} g {x:0.##} {yy:0.##} {w:0.##} {h:0.##} re f 0 g\n");
        }
        void Line(double x1, double y1, double x2, double y2, double gray = 0.75)
        {
            page.Content.Append(CultureInfo.InvariantCulture, $"{gray:0.##} G 0.5 w {x1:0.##} {y1:0.##} m {x2:0.##} {y2:0.##} l S 0 G\n");
        }
        string Fit(string s, double maxWidth, double size, bool bold)
        {
            s = Clean(s);
            if (Width(s, size, bold) <= maxWidth) return s;
            while (s.Length > 1 && Width(s + "...", size, bold) > maxWidth) s = s[..^1];
            return s + "...";
        }

        NewPage();
        Rect(margin, y - 30, usable, 30, 0.12);
        page.Content.Append("1 g\n");
        Text(margin + 10, y - 21, Clean(doc.Title), 16, true, 1);
        y -= 46;
        Text(margin, y, Fit(doc.Subtitle, usable, 10, false), 10, false, 0.25);
        y -= 14;
        Text(margin, y, "Generated " + doc.GeneratedAt.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture) + " - Library Platform", 8, false, 0.45);
        y -= 22;

        if (doc.Summary.Count > 0)
        {
            Text(margin, y, "Summary", 11, true);
            y -= 6;
            foreach (var s in doc.Summary)
            {
                if (y < margin + 30) { NewPage(); }
                y -= 15;
                Text(margin + 4, y, Fit(s.Key, usable * 0.65, 9.5, false), 9.5);
                var v = Fit(s.Value, usable * 0.3, 9.5, true);
                Text(margin + usable - 4 - Width(v, 9.5, true), y, v, 9.5, true);
                Line(margin, y - 4, margin + usable, y - 4, 0.88);
            }
            y -= 22;
        }

        foreach (var t in doc.Tables)
        {
            if (y < margin + 70) NewPage();
            Text(margin, y, Clean(t.Heading), 11, true);
            y -= 8;

            // Column widths from content, scaled to fit the page.
            var n = t.Columns.Count;
            var natural = new double[n];
            for (var c = 0; c < n; c++)
            {
                natural[c] = Width(t.Columns[c], 8.5, true) + 12;
                foreach (var r in t.Rows.Take(200)) natural[c] = Math.Max(natural[c], Width(Clean(r[c]), 8.5, false) + 12);
                natural[c] = Math.Min(natural[c], usable * 0.45);
            }
            var total = natural.Sum();
            var scale = total > usable ? usable / total : 1;
            var widths = natural.Select(w => w * scale).ToArray();
            if (total < usable) { var extra = (usable - total) / n; widths = natural.Select(w => w + extra).ToArray(); }
            var right = new HashSet<int>(t.RightAligned ?? Array.Empty<int>());

            void Header()
            {
                y -= 14;
                Rect(margin, y - 4, usable, 16, 0.9);
                var x = margin;
                for (var c = 0; c < n; c++)
                {
                    var label = Fit(t.Columns[c], widths[c] - 8, 8.5, true);
                    Text(right.Contains(c) ? x + widths[c] - 4 - Width(label, 8.5, true) : x + 4, y, label, 8.5, true);
                    x += widths[c];
                }
                y -= 6;
            }

            Header();
            if (t.Rows.Count == 0)
            {
                y -= 14;
                Text(margin + 4, y, "(nothing to show)", 8.5, false, 0.45);
            }
            var shade = false;
            foreach (var r in t.Rows)
            {
                if (y < margin + 24) { NewPage(); Header(); shade = false; }
                y -= 14;
                if (shade) { var save = page.Content.Length; Rect(margin, y - 4, usable, 14, 0.97); }
                shade = !shade;
                var x = margin;
                for (var c = 0; c < n; c++)
                {
                    var cell = Fit(r[c], widths[c] - 8, 8.5, false);
                    Text(right.Contains(c) ? x + widths[c] - 4 - Width(cell, 8.5, false) : x + 4, y, cell, 8.5);
                    x += widths[c];
                }
            }
            y -= 24;
        }

        // Footer on every page: "Page n of N".
        for (var i = 0; i < pages.Count; i++)
        {
            var label = $"Page {i + 1} of {pages.Count}";
            pages[i].Content.Append(CultureInfo.InvariantCulture, $"BT 0.45 g /F1 8 Tf {pw - margin - Width(label, 8, false):0.##} 24 Td ({label}) Tj ET\n");
            pages[i].Content.Append(CultureInfo.InvariantCulture, $"BT 0.45 g /F1 8 Tf {margin:0.##} 24 Td ({Escape(Clean(doc.Title))}) Tj ET\n");
        }

        return Assemble(pages, pw, ph);
    }

    private static byte[] Assemble(List<Page> pages, double pw, double ph)
    {
        var latin1 = Encoding.Latin1;
        var offsets = new List<long>();
        using var ms = new MemoryStream();

        void Raw(string s) { var b = latin1.GetBytes(s); ms.Write(b, 0, b.Length); }
        void BeginObj(int id) { offsets.Add(ms.Position); Raw($"{id} 0 obj\n"); }

        Raw("%PDF-1.4\n%âãÏÓ\n");
        // Object numbering: 1 catalog, 2 pages, 3 F1, 4 F2, then (page, content) pairs.
        var pageIds = Enumerable.Range(0, pages.Count).Select(i => 5 + i * 2).ToList();

        BeginObj(1); Raw("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        BeginObj(2); Raw($"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ", pageIds.Select(i => $"{i} 0 R"))}] >>\nendobj\n");
        BeginObj(3); Raw("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");
        BeginObj(4); Raw("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");
        for (var i = 0; i < pages.Count; i++)
        {
            var content = latin1.GetBytes(pages[i].Content.ToString());
            BeginObj(pageIds[i]);
            Raw(string.Create(CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pw:0.##} {ph:0.##}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {pageIds[i] + 1} 0 R >>\nendobj\n"));
            BeginObj(pageIds[i] + 1);
            Raw($"<< /Length {content.Length} >>\nstream\n");
            ms.Write(content, 0, content.Length);
            Raw("\nendstream\nendobj\n");
        }

        var xref = ms.Position;
        Raw($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) Raw($"{o:D10} 00000 n \n");
        Raw($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }

    /// <summary>Replace characters the built-in font can't show (outside Latin-1) so text never breaks the file.</summary>
    private static string Clean(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s ?? "")
            sb.Append(ch switch { '–' or '—' => '-', '‘' or '’' => '\'', '“' or '”' => '"', '…' => '.', < ' ' => ' ', > 'ÿ' => '?', _ => ch });
        return sb.ToString();
    }

    private static string Escape(string s) => Clean(s).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
