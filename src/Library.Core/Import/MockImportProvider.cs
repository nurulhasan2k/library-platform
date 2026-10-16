using System.Globalization;
using System.Text;
using CsvHelper;

namespace Library.Core.Import;

public static class MockImportProvider
{
    public static byte[] GetCsv()
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using (var csv = new CsvWriter(text, CultureInfo.InvariantCulture, leaveOpen: true))
        {
            foreach (var column in CatalogueCsvValidator.Columns) csv.WriteField(column);
            csv.NextRecord();
            string[][] rows = [
                ["BK-900", "book", "Mock book", "Provider sample", "Science Fiction", "", "Available", "Sample Writer", "Science Fiction", "", "", "", ""],
                ["MU-900", "music", "Mock album", "Provider sample", "Rock", "", "Available", "", "", "Sample Artist", "2000", "", ""],
                ["TY-900", "toy", "Mock puzzle", "Provider sample", "Puzzle", "", "Available", "", "", "", "", "Puzzle", "6"]];
            foreach (var row in rows)
            {
                foreach (var value in row) csv.WriteField(value);
                csv.NextRecord();
            }
        }
        return Encoding.UTF8.GetBytes(text.ToString());
    }
}
