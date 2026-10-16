using System.Globalization;
using System.Text;
using CsvHelper;
using Library.Core.Import;
using Xunit;

namespace Library.Import.Tests;

public sealed class CsvSafetyTests
{
    [Fact]
    public void ErrorExportRoundTripsCommasQuotesAndEmbeddedNewlines()
    {
        var result = ImportTestData.Validate("items-bad-rows.csv",
            ImportTestData.Lookups with { ExistingCodes = ["BK-1000", "BK-1001", "MU-1002"] });
        using var stream = new MemoryStream(ImportErrorCsvWriter.Write(result));
        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        var found = new Dictionary<string, (string? Name, string? Description)>();
        while (csv.Read())
            found[csv.GetField("LibraryCode")!] = (csv.GetField("Name"), csv.GetField("Description"));
        Assert.Equal("Dune, Messiah", found["BK-1000"].Name);
        Assert.Equal("He said \"wait\" and left", found["BK-1001"].Description);
        Assert.Equal("Line one\nline two", found["MU-1002"].Description);
    }

    [Fact]
    public void MissingOptionalColumnsCanUseTheTargetBranch()
    {
        var sample = "LibraryCode,ItemType,Name,Category,Author,Genre\nBK-800,book,Short template,Science Fiction,Writer,Science Fiction\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sample));
        var result = ImportTestData.Validator().Validate(stream, "items.CSV", ImportTestData.Lookups, 2);
        Assert.Empty(result.FileErrors);
        Assert.Empty(result.RejectedRows);
        var row = Assert.Single(result.ValidRows);
        Assert.Equal(2, row.BranchId);
        Assert.Equal(Library.Core.Contracts.ItemStatus.Available, row.Status);
    }

    [Fact]
    public void TypeStatusBranchAndCategoryValuesAreCaseInsensitive()
    {
        var sample = Encoding.UTF8.GetString(ImportTestData.Sample("items-valid.csv"))
            .Replace("book", "BoOk").Replace("music", "MUSIC").Replace("toy", "TOY")
            .Replace("RIVER", "river").Replace("Science Fiction", "science fiction")
            .Replace("Rock", "ROCK").Replace("Puzzle", "puzzle").Replace("Damaged", "dAmAgEd");
        var result = ImportTestData.ValidateBytes(Encoding.UTF8.GetBytes(sample));
        Assert.Empty(result.FileErrors);
        Assert.Empty(result.RejectedRows);
        Assert.Equal(20, result.ValidRows.Count);
    }

    [Fact]
    public void DuplicateHeaderNamesAreRefused()
    {
        var sample = Encoding.UTF8.GetString(ImportTestData.Sample("items-valid.csv")).Replace("Description", " NAME ");
        var result = ImportTestData.ValidateBytes(Encoding.UTF8.GetBytes(sample));
        Assert.Contains("duplicate columns", Assert.Single(result.FileErrors));
        Assert.False(result.CanImport);
    }

    [Fact]
    public void TooManyOrTooFewFieldsAreRowErrors()
    {
        var header = string.Join(',', CatalogueCsvValidator.Columns);
        foreach (var fields in new[] { "BK-800,book,Missing fields", "BK-800,book,Too many,,,,,,,,,,,extra" })
        {
            var result = ImportTestData.ValidateBytes(Encoding.UTF8.GetBytes(header + "\n" + fields));
            Assert.Contains("different number of fields", Assert.Single(result.RejectedRows).Reason);
            Assert.False(result.CanImport);
        }
    }

    [Fact]
    public void OversizedUnknownFieldsCannotBypassLengthChecks()
    {
        var sample = "LibraryCode,ItemType,Name,Category,Branch,Author,Genre,Extra\nBK-800,book,Title,Science Fiction,RIVER,Writer,Science Fiction," + new string('x', 4001);
        var result = ImportTestData.ValidateBytes(Encoding.UTF8.GetBytes(sample));
        Assert.Contains("unrecognised columns", Assert.Single(result.RejectedRows).Reason);
    }

    [Fact]
    public void ControlCharactersInValuesAreRejected()
    {
        var sample = Encoding.UTF8.GetString(ImportTestData.Sample("items-valid.csv")).Replace("Sample catalogue item", "Bad\0text");
        var result = ImportTestData.ValidateBytes(Encoding.UTF8.GetBytes(sample));
        Assert.Equal(20, result.RejectedRows.Count);
        Assert.All(result.RejectedRows, row => Assert.Contains("invalid control character", row.Reason));
    }
}
