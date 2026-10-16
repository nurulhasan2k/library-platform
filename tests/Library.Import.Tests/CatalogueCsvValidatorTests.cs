using System.Diagnostics;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Import;
using Xunit;
using Xunit.Abstractions;

namespace Library.Import.Tests;

public sealed class CatalogueCsvValidatorTests(ITestOutputHelper output)
{
    [Fact]
    public void ValidSampleContainsTwentyItemsForRiver()
    {
        var result = ImportTestData.Validate();
        Assert.True(result.CanImport);
        Assert.Empty(result.FileErrors);
        Assert.Empty(result.RejectedRows);
        Assert.Equal(20, result.ValidRows.Count);
        Assert.All(result.ValidRows, row => Assert.Equal(2, row.BranchId));
        Assert.Equal(new[] { ItemType.Book, ItemType.Music, ItemType.Toy }, result.ValidRows.Select(row => row.ItemType).Distinct());
        Assert.All(result.ValidRows, row => Assert.True(int.Parse(row.LibraryCode.Split('-')[1], CultureInfo.InvariantCulture) >= 500));
    }

    [Fact]
    public void NonCsvFileIsRefused()
    {
        var result = ImportTestData.ValidateBytes(ImportTestData.Sample("items-valid.csv"), "items.txt");
        Assert.Contains(".csv", Assert.Single(result.FileErrors));
        Assert.False(result.CanImport);
    }

    [Fact]
    public void FileSizeIsBoundedBeforeParsing()
    {
        var bytes = ImportTestData.Sample("items-valid.csv").Concat(new byte[1024 * 1024]).ToArray();
        var result = ImportTestData.ValidateBytes(bytes, options: new ImportOptions { MaxFileSizeMb = 1 });
        Assert.Contains("exceeds 1 MB", Assert.Single(result.FileErrors));
        Assert.Empty(result.ValidRows);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidUtf8AndUtf16AreRefused(bool utf16)
    {
        var sample = ImportTestData.Sample("items-valid.csv");
        var bytes = utf16 ? Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Encoding.UTF8.GetString(sample))).ToArray()
            : sample.Concat(new byte[] { 0xC3, 0x28 }).ToArray();
        var result = ImportTestData.ValidateBytes(bytes);
        Assert.Contains("UTF-8", Assert.Single(result.FileErrors));
        Assert.False(result.CanImport);
    }

    [Fact]
    public void RowLimitStopsConfirmationAndDoesNotCountBlankLines()
    {
        var result = ImportTestData.Validate(options: new ImportOptions { MaxRows = 19 });
        Assert.Contains("exceeds 19 data rows", Assert.Single(result.FileErrors));
        Assert.Empty(result.ValidRows);
        Assert.False(result.CanImport);
        var valid = Encoding.UTF8.GetString(ImportTestData.Sample("items-valid.csv"));
        var withBlanks = valid.Replace("\r\n", "\r\n\r\n");
        var accepted = ImportTestData.ValidateBytes(Encoding.UTF8.GetBytes(withBlanks), options: new ImportOptions { MaxRows = 20 });
        Assert.Empty(accepted.FileErrors);
        Assert.Equal(20, accepted.ValidRows.Count);
    }

    [Theory]
    [InlineData("LibraryCode")]
    [InlineData("ItemType")]
    [InlineData("Name")]
    [InlineData("Category")]
    public void MissingRequiredHeaderStopsBeforeRows(string column)
    {
        var sample = Encoding.UTF8.GetString(ImportTestData.Sample("items-valid.csv"));
        var headerEnd = sample.IndexOf('\n');
        var text = sample[..headerEnd].Replace(column, "OtherColumn", StringComparison.Ordinal) + sample[headerEnd..];
        var result = ImportTestData.ValidateBytes(Encoding.UTF8.GetBytes(text));
        Assert.Contains("Missing required columns: " + column, Assert.Single(result.FileErrors));
        Assert.Empty(result.ValidRows);
        Assert.Empty(result.RejectedRows);
    }

    [Fact]
    public void HeaderNamesAreTrimmedAndCaseInsensitive()
    {
        var text = Encoding.UTF8.GetString(ImportTestData.Sample("items-valid.csv"));
        var end = text.IndexOf('\n');
        var header = string.Join(',', text[..end].TrimEnd('\r').Split(',').Select(value => " " + value.ToLowerInvariant() + " "));
        var result = ImportTestData.ValidateBytes(Encoding.UTF8.GetBytes(header + text[end..]));
        Assert.Empty(result.FileErrors);
        Assert.Equal(20, result.ValidRows.Count);
    }

    [Theory]
    [InlineData("LibraryCode")]
    [InlineData("ItemType")]
    [InlineData("Name")]
    [InlineData("Category")]
    public void RequiredValuesAreCheckedOnEveryRow(string column)
        => Assert.Contains(ImportTestData.Validate("items-bad-rows.csv").RejectedRows,
            row => string.IsNullOrWhiteSpace(row.Fields[column]) && row.Errors.Contains(column + " is required."));

    [Fact]
    public void UnknownItemTypeIsRejected()
        => Assert.Contains(ImportTestData.Validate("items-bad-rows.csv").RejectedRows,
            row => row.Fields["ItemType"] == "cd" && row.Reason.Contains("ItemType must be book, music or toy."));

    [Theory]
    [InlineData("Borrowed")]
    [InlineData("Destroy")]
    [InlineData("0")]
    public void NewItemsCannotHaveOtherStatuses(string status)
        => Assert.Contains(ImportTestData.Validate("items-bad-rows.csv").RejectedRows,
            row => row.Fields["Status"] == status && row.Reason.Contains("Status must be Available or Damaged."));

    [Fact]
    public void BlankStatusDefaultsToAvailableAndDamagedIsAllowed()
    {
        var result = ImportTestData.Validate();
        Assert.Equal(ItemStatus.Available, result.ValidRows[0].Status);
        Assert.Contains(result.ValidRows, row => row.Status == ItemStatus.Damaged);
    }

    [Theory]
    [InlineData("Author")]
    [InlineData("Genre")]
    [InlineData("Artist")]
    [InlineData("Year")]
    [InlineData("ToyType")]
    [InlineData("RecommendedAge")]
    public void TypeSpecificFieldsAreRequired(string column)
        => Assert.Contains(ImportTestData.Validate("items-bad-rows.csv").RejectedRows,
            row => row.Fields[column] == "" && row.Errors.Contains(column + " is required for this ItemType."));

    [Theory]
    [InlineData("abc")]
    [InlineData("999")]
    [InlineData("9999")]
    public void MusicYearMustBeNumericAndWithinRange(string year)
        => Assert.Contains(ImportTestData.Validate("items-bad-rows.csv").RejectedRows,
            row => row.Fields["Year"] == year && row.Reason.Contains("Year must be an integer from 1000 to 2026."));

    [Fact]
    public void CurrentYearComesFromTheClockAndIncludesBothBounds()
    {
        var sample = Encoding.UTF8.GetString(ImportTestData.Sample("items-valid.csv"));
        foreach (var year in new[] { 1000, 2026 })
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sample.Replace(",2000,", $",{year},")));
            var result = ImportTestData.Validator().Validate(stream, "items.csv", ImportTestData.Lookups);
            Assert.Empty(result.RejectedRows);
            Assert.All(result.ValidRows.Where(row => row.ItemType == ItemType.Music), row => Assert.Equal(year, row.Year));
        }
        using var older = new MemoryStream(ImportTestData.Sample("items-valid.csv"));
        var oldResult = ImportTestData.Validator(year: 1999).Validate(older, "items.csv", ImportTestData.Lookups);
        Assert.Equal(6, oldResult.RejectedRows.Count);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("19")]
    public void ToyAgeMustBeNumericAndWithinRange(string age)
        => Assert.Contains(ImportTestData.Validate("items-bad-rows.csv").RejectedRows,
            row => row.Fields["RecommendedAge"] == age && row.Reason.Contains("RecommendedAge must be an integer from 0 to 18."));

    [Theory]
    [InlineData(0)]
    [InlineData(18)]
    public void ToyAgeIncludesBothBounds(int age)
    {
        var sample = Encoding.UTF8.GetString(ImportTestData.Sample("items-valid.csv"));
        var result = ImportTestData.ValidateBytes(Encoding.UTF8.GetBytes(sample.Replace(",Puzzle,6", $",Puzzle,{age}")));
        Assert.Empty(result.RejectedRows);
        Assert.All(result.ValidRows.Where(row => row.ItemType == ItemType.Toy), row => Assert.Equal(age, row.RecommendedAge));
    }

    [Fact]
    public void BranchMustExistEvenWhenATargetIsSupplied()
        => Assert.Contains(ImportTestData.Validate("items-bad-rows.csv", targetBranchId: 1).RejectedRows,
            row => row.Fields["Branch"] == "WEST" && row.Reason.Contains("existing branch"));

    [Fact]
    public void MissingBranchNeedsATargetAndPerRowBranchOverridesIt()
    {
        Assert.Contains(ImportTestData.Validate("items-bad-rows.csv").RejectedRows,
            row => row.Fields["Name"] == "Missing branch" && row.Reason.Contains("valid target branch"));
        var result = ImportTestData.Validate("items-bad-rows.csv", targetBranchId: 1);
        Assert.Contains(result.ValidRows, row => row.Name == "Missing branch" && row.BranchId == 1);
        Assert.All(ImportTestData.Validate(targetBranchId: 1).ValidRows, row => Assert.Equal(2, row.BranchId));
        Assert.Contains("target branch does not exist", Assert.Single(ImportTestData.Validate(targetBranchId: 999).FileErrors));
    }

    [Fact]
    public void CategoryMustExist()
        => Assert.Contains(ImportTestData.Validate("items-bad-rows.csv").RejectedRows,
            row => row.Fields["Category"] == "Cooking" && row.Reason.Contains("Category does not exist."));

    [Fact]
    public void CategoryMustBelongToTheItemType()
        => Assert.Contains(ImportTestData.Validate("items-bad-rows.csv").RejectedRows,
            row => row.Fields["ItemType"] == "book" && row.Fields["Category"] == "Rock" && row.Reason.Contains("does not belong"));

    [Fact]
    public void CodesAreTrimmedAndDuplicatesAreCaseInsensitive()
    {
        var result = ImportTestData.Validate("items-bad-rows.csv");
        Assert.Contains(result.ValidRows, row => row.LibraryCode == "BK-1000");
        Assert.Contains(result.RejectedRows, row => row.Fields["LibraryCode"] == " bk-1000 " && row.Reason.Contains("repeats inside"));
        var text = Encoding.UTF8.GetString(ImportTestData.Sample("items-valid.csv")).Replace("BK-500,", " bk-500 ,");
        var accepted = ImportTestData.ValidateBytes(Encoding.UTF8.GetBytes(text));
        Assert.Equal("bk-500", accepted.ValidRows[0].LibraryCode);
    }

    [Fact]
    public void ExistingCodesAreComparedCaseInsensitivelyAfterTrimming()
    {
        var result = ImportTestData.Validate("items-bad-rows.csv", ImportTestData.Lookups with { ExistingCodes = [" bk-1500 "] });
        Assert.Contains(result.RejectedRows, row => row.Fields["LibraryCode"] == "BK-1500" && row.Reason.Contains("already exists"));
    }

    [Theory]
    [InlineData("LibraryCode", 30)]
    [InlineData("ItemType", 20)]
    [InlineData("Name", 200)]
    [InlineData("Description", 4000)]
    [InlineData("Category", 100)]
    [InlineData("Branch", 30)]
    [InlineData("Status", 20)]
    [InlineData("Author", 200)]
    [InlineData("Genre", 100)]
    [InlineData("Artist", 200)]
    [InlineData("Year", 10)]
    [InlineData("ToyType", 100)]
    [InlineData("RecommendedAge", 10)]
    public void AbsurdFieldLengthsAreRejected(string column, int maximum)
        => Assert.Contains(ImportTestData.Validate("items-bad-rows.csv").RejectedRows,
            row => row.Errors.Contains($"{column} must be at most {maximum} characters."));

    [Fact]
    public void BomQuotesCommasAndMultilineFieldsAreReadWithoutLosingPhysicalRows()
    {
        var bytes = ImportTestData.Sample("items-bad-rows.csv");
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        var result = ImportTestData.Validate("items-bad-rows.csv");
        Assert.Empty(result.FileErrors);
        var comma = result.ValidRows.Single(row => row.LibraryCode == "BK-1000");
        Assert.Equal("Dune, Messiah", comma.Name);
        Assert.Equal(2, comma.RowNumber);
        var quote = result.ValidRows.Single(row => row.LibraryCode == "BK-1001");
        Assert.Equal("He said \"wait\" and left", quote.Description);
        Assert.Equal(4, quote.RowNumber);
        Assert.Equal("Line one\nline two", result.ValidRows.Single(row => row.LibraryCode == "MU-1002").Description);
        Assert.Equal(7, result.ValidRows.Single(row => row.LibraryCode == "TY-1003").RowNumber);
        Assert.Contains(result.ValidRows, row => row.Name == "Café puzzle");
    }

    [Theory]
    [InlineData("=")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("@")]
    [InlineData("\t =")]
    public void ErrorCsvNeutralisesFormulaCellsAndPreservesTheirText(string prefix)
    {
        var result = ImportTestData.Validate("items-bad-rows.csv");
        var original = result.RejectedRows.Single(row => row.Fields["Name"] == prefix + "SUM(1+1)");
        var bytes = ImportErrorCsvWriter.Write(result);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        var found = false;
        while (csv.Read())
            if (csv.GetField<int>("RowNumber") == original.RowNumber)
            {
                Assert.Equal("'" + original.Fields["Name"], csv.GetField("Name"));
                Assert.Equal(original.Reason, csv.GetField("Reason"));
                found = true;
            }
        Assert.True(found);
    }

    [Fact]
    public void MalformedQuotingIsReportedWithoutAnException()
    {
        var header = string.Join(',', CatalogueCsvValidator.Columns);
        var result = ImportTestData.ValidateBytes(Encoding.UTF8.GetBytes(header + "\n\"unfinished"));
        Assert.False(result.CanImport);
        Assert.True(result.FileErrors.Count > 0 || result.RejectedRows.Any(row => row.Reason.Contains("malformed")));
    }

    [Fact]
    public void LargeSampleParsesWellUnderOneSecond()
    {
        ImportTestData.Validate();
        var stopwatch = Stopwatch.StartNew();
        var result = ImportTestData.Validate("items-large.csv");
        stopwatch.Stop();
        output.WriteLine($"Parsed and validated 2000 rows in {stopwatch.Elapsed.TotalMilliseconds:F1} ms.");
        Assert.Empty(result.FileErrors);
        Assert.Empty(result.RejectedRows);
        Assert.Equal(2000, result.ValidRows.Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(750), $"Parsing took {stopwatch.Elapsed.TotalMilliseconds:F1} ms.");
    }
}
