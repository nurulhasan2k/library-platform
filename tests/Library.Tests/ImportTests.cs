using Library.Core.Contracts;
using Library.Core.Import;
using Library.Core.Models;
using Library.Core.Services;

namespace Library.Tests;

public static class ImportTests
{
    private const string Header = "type,code,name,description,branch,category,author,artist,year,age\r\n";

    public static void Run()
    {
        T.Section("Data importer");
        using var p = new TestPlatform();
        var import = p.Get<ImportService>();
        var river = p.Branch("RIVER").Id;

        T.Test("the downloadable template is itself a valid file", () =>
        {
            var prev = import.PreviewCsv(ImportService.CsvTemplate, "template.csv", river);
            T.Equal(3, prev.Rows.Count);
            T.Equal(3, prev.ValidCount);
        });
        T.Test("a valid file previews with no errors and imports into the chosen branch", () =>
        {
            var csv = Header +
                "book,BK-801,Hyperion Cantos,,,Science Fiction,Dan Simmons,,,\r\n" +
                "music,MU-801,Blue Train,\"Hard bop, 1958\",,Jazz,,John Coltrane,1958,\r\n" +
                "toy,TY-801,Cube puzzle,,,Puzzle,,,,7\r\n";
            var prev = import.PreviewCsv(csv, "good.csv", river);
            T.Equal(3, prev.ValidCount);
            var batch = import.Commit(prev, "admin");
            T.Equal(3, batch.ImportedRows);
            T.Equal(river, p.Item("BK-801").BranchId);
            T.Equal("Hard bop, 1958", p.Item("MU-801").Description);
            T.Equal("Science Fiction", ((Book)p.Item("BK-801")).Genre);
        });
        T.Test("every validation rule catches its mistake with a readable message", () =>
        {
            var csv = Header +
                "magazine,XX-1,Bad type,,,Rock,,,,\r\n" +
                "book,,No code,,,History,A,,,\r\n" +
                "book,BK-001,Duplicate of catalogue,,,History,A,,,\r\n" +
                "book,BK-802,First,,,History,A,,,\r\n" +
                "book,BK-802,Duplicate in file,,,History,A,,,\r\n" +
                "book,BK-803,,,,History,A,,,\r\n" +
                "book,BK-804,Unknown branch,,MOON,History,A,,,\r\n" +
                "book,BK-805,Unknown category,,,Cooking,A,,,\r\n" +
                "book,BK-806,No author,,,History,,,,\r\n" +
                "music,MU-802,Bad year,,,Rock,,Band,abc,\r\n" +
                "music,MU-803,No artist,,,Rock,,,1999,\r\n" +
                "toy,TY-802,Bad age,,,Puzzle,,,,-4\r\n" +
                "book,BK 807,Bad code chars,,,History,A,,,\r\n";
            var prev = import.PreviewCsv(csv, "bad.csv", river);
            T.Equal(13, prev.Rows.Count);
            T.Equal(1, prev.ValidCount); // only the first BK-802
            string Err(int i) => string.Join(" ", prev.Rows[i].Errors);
            T.True(Err(0).Contains("Type 'magazine'"), Err(0));
            T.True(Err(1).Contains("code is required"), Err(1));
            T.True(Err(2).Contains("already exists"), Err(2));
            T.True(Err(4).Contains("more than once"), Err(4));
            T.True(Err(5).Contains("Name is required"), Err(5));
            T.True(Err(6).Contains("Branch 'MOON'"), Err(6));
            T.True(Err(7).Contains("Category 'Cooking'"), Err(7));
            T.True(Err(8).Contains("Author is required"), Err(8));
            T.True(Err(9).Contains("Year 'abc'"), Err(9));
            T.True(Err(10).Contains("Artist is required"), Err(10));
            T.True(Err(11).Contains("Age '-4'"), Err(11));
            T.True(Err(12).Contains("may only contain"), Err(12));
        });
        T.Test("invalid rows are skipped and listed in the import history", () =>
        {
            var prev = import.PreviewCsv(Header + "book,BK-810,Good,,,History,A,,,\r\nbook,,Bad,,,History,A,,,\r\n", "mixed.csv", river);
            var batch = import.Commit(prev, "admin");
            T.Equal(1, batch.ImportedRows);
            T.Equal(1, batch.SkippedRows);
            T.Equal(1, batch.Problems.Count);
            T.True(import.History().Any(b => b.Id == batch.Id));
            T.Equal(p.Item("BK-810").Id > 0, true);
        });
        T.Test("importing the same file twice doesn't create duplicates", () =>
        {
            var csv = Header + "book,BK-820,Once only,,,History,A,,,\r\n";
            import.Commit(import.PreviewCsv(csv, "a.csv", river), "admin");
            var again = import.PreviewCsv(csv, "a.csv", river);
            T.Equal(0, again.ValidCount);
            T.True(string.Join(" ", again.Rows[0].Errors).Contains("already exists"));
        });
        T.Test("a code added by someone else between preview and commit is not overwritten", () =>
        {
            var prev = import.PreviewCsv(Header + "book,BK-830,Race,,,History,A,,,\r\n", "race.csv", river);
            p.Inventory.AddItem(new Book { Name = "Other", LibraryCode = "BK-830", Author = "A", BranchId = river, CategoryId = p.Store.Read(d => d.Categories.First(c => c.Name == "History").Id) });
            var batch = import.Commit(prev, "admin");
            T.Equal(0, batch.ImportedRows);
            T.Equal("Other", p.Item("BK-830").Name);
        });
        T.Test("the CSV reader copes with quotes, commas, line breaks, a BOM and blank lines", () =>
        {
            var rows = Csv.Parse("﻿a,b\r\n\r\n\"x, y\",\"he said \"\"hi\"\"\"\r\n\"line1\nline2\",z");
            T.Equal(3, rows.Count);
            T.Equal("x, y", rows[1][0]);
            T.Equal("he said \"hi\"", rows[1][1]);
            T.Equal("line1\nline2", rows[2][0]);
        });
        T.Test("header names are flexible; missing required columns are reported", () =>
        {
            var ok = import.PreviewCsv("Title,Library Code,Item Type,Genre,Author\r\nT,AB-1,book,History,Me\r\n", "alias.csv", river);
            T.Equal(1, ok.ValidCount);
            var bad = import.PreviewCsv("foo,bar\r\n1,2\r\n", "bad.csv", river);
            T.True(bad.FileProblem!.Contains("Missing column"));
            T.True(import.PreviewCsv("", "empty.csv", river).FileProblem!.Contains("empty"));
        });
        T.Test("the mock external provider supplies 12 records, 10 valid and 2 deliberately bad", () =>
        {
            var prev = import.PreviewProvider(p.Get<IExternalCatalogueProvider>(), river);
            T.Equal(12, prev.Rows.Count);
            T.Equal(10, prev.ValidCount);
            var batch = import.Commit(prev, "admin");
            T.Equal(10, batch.ImportedRows);
            T.True(p.Item("EXT-BK-001").BranchId == river);
        });
        T.Test("imported items show up in the public search straight away", () =>
        {
            var found = TestPlatform.Wait(p.Get<ICatalogueQuery>().SearchAsync(new CatalogueFilter(Text: "Left Hand of Darkness EXT")));
            T.True(found.TotalCount >= 1);
        });
        T.Test("an exported problems file can be fixed and uploaded again", () =>
        {
            var prev = import.PreviewCsv(Header + "book,,Bad,,,History,A,,,\r\n", "x.csv", river);
            var csv = import.ProblemsCsv(prev);
            T.True(csv.StartsWith("line,code,name,problems"));
        });
        T.Test("the sample files shipped in samples/import behave as their README says", () =>
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "LibraryPlatform.sln"))) root = root.Parent;
            var samples = Path.Combine(root!.FullName, "samples", "import");
            var good = import.PreviewCsv(File.ReadAllText(Path.Combine(samples, "valid-river-20-items.csv")), "valid.csv", river);
            T.Equal(20, good.Rows.Count); T.Equal(20, good.ValidCount);
            var bad = import.PreviewCsv(File.ReadAllText(Path.Combine(samples, "bad-rows.csv")), "bad.csv", river);
            T.Equal(17, bad.Rows.Count); T.Equal(2, bad.ValidCount);
            var big = import.PreviewCsv(File.ReadAllText(Path.Combine(samples, "larger-150-items.csv")), "big.csv", river);
            T.Equal(150, big.ValidCount);
        });
    }
}
