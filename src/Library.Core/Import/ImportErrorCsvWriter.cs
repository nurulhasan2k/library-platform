using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace Library.Core.Import;

public static class ImportErrorCsvWriter
{
    public static byte[] Write(ImportValidationResult result)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true))
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
        {
            foreach (var column in new[] { "RowNumber", "Reason" }.Concat(CatalogueCsvValidator.Columns))
                csv.WriteField(column);
            csv.NextRecord();
            foreach (var row in result.RejectedRows)
            {
                csv.WriteField(row.RowNumber.ToString(CultureInfo.InvariantCulture));
                csv.WriteField(SafeCell(row.Reason));
                foreach (var column in CatalogueCsvValidator.Columns)
                    csv.WriteField(SafeCell(row.Fields.GetValueOrDefault(column, "")));
                csv.NextRecord();
            }
        }
        return stream.ToArray();
    }

    private static string SafeCell(string value)
    {
        var start = value.AsSpan().TrimStart();
        return start.Length > 0 && start[0] is '=' or '+' or '-' or '@' ? "'" + value : value;
    }
}
