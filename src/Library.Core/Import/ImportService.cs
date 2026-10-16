using System.Text.RegularExpressions;
using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Models;
using Library.Core.Services;
using Microsoft.Extensions.Options;

namespace Library.Core.Import;

/// <summary>
/// Data importer (feature 3.6): reads a CSV file or an external provider, checks every row, shows a preview, and imports the
/// valid rows into one branch. Every run is recorded as an <see cref="ImportBatch"/>.
/// </summary>
public sealed partial class ImportService(
    ILibraryStore store,
    IClock clock,
    IOptions<ImportOptions> options)
{
    public const string CsvTemplate =
        "type,code,name,description,branch,category,author,artist,year,age\r\n" +
        "book,BK-900,Example Book,A short description,CENTRAL,Science Fiction,Example Author,,,\r\n" +
        "music,MU-900,Example Album,,CENTRAL,Rock,,Example Artist,1999,\r\n" +
        "toy,TY-900,Example Toy,,CENTRAL,Puzzle,,,,6\r\n";

    private static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["type"] = "type", ["itemtype"] = "type",
        ["code"] = "code", ["librarycode"] = "code", ["id"] = "code",
        ["name"] = "name", ["title"] = "name",
        ["description"] = "description", ["desc"] = "description",
        ["branch"] = "branch", ["branchcode"] = "branch", ["location"] = "branch",
        ["category"] = "category", ["genre"] = "category", ["toytype"] = "category",
        ["author"] = "author", ["artist"] = "artist", ["year"] = "year",
        ["age"] = "age", ["recommendedage"] = "age", ["minage"] = "age",
    };

    // ---------------------------------------------------------------- reading

    public IReadOnlyList<ImportRecord> ReadCsv(string text, out string? problem)
    {
        problem = null;
        var records = new List<ImportRecord>();
        var rows = Csv.Parse(text);
        if (rows.Count == 0) { problem = "The file is empty."; return records; }

        var map = new Dictionary<string, int>();
        for (var i = 0; i < rows[0].Count; i++)
        {
            var key = NonLetters().Replace(rows[0][i], "");
            if (HeaderAliases.TryGetValue(key, out var field) && !map.ContainsKey(field)) map[field] = i;
        }
        var missing = new[] { "type", "code", "name" }.Where(f => !map.ContainsKey(f)).ToList();
        if (missing.Count > 0)
        {
            problem = $"The first line must be a header row. Missing column(s): {string.Join(", ", missing)}. Expected columns: type, code, name, description, branch, category, author, artist, year, age.";
            return records;
        }
        if (rows.Count - 1 > options.Value.MaxRows)
        {
            problem = $"The file has {rows.Count - 1} rows; the limit is {options.Value.MaxRows}. Split it into smaller files.";
            return records;
        }

        string Cell(List<string> r, string f) => map.TryGetValue(f, out var i) && i < r.Count ? r[i].Trim() : "";
        for (var n = 1; n < rows.Count; n++)
        {
            var r = rows[n];
            records.Add(new ImportRecord
            {
                RowNumber = n + 1, // the line number as seen in a spreadsheet (header is line 1)
                Type = Cell(r, "type"), Code = Cell(r, "code"), Name = Cell(r, "name"), Description = Cell(r, "description"),
                Branch = Cell(r, "branch"), Category = Cell(r, "category"), Author = Cell(r, "author"), Artist = Cell(r, "artist"),
                Year = Cell(r, "year"), Age = Cell(r, "age")
            });
        }
        return records;
    }

    // ---------------------------------------------------------------- checking

    public ImportPreview PreviewCsv(string text, string sourceName, int defaultBranchId)
    {
        var records = ReadCsv(text, out var problem);
        var preview = Check(records, sourceName, defaultBranchId);
        preview.FileProblem = problem;
        return preview;
    }

    public ImportPreview PreviewProvider(IExternalCatalogueProvider provider, int defaultBranchId) =>
        Check(provider.Fetch(), provider.Name, defaultBranchId);

    public ImportPreview Check(IEnumerable<ImportRecord> records, string source, int defaultBranchId)
    {
        var preview = new ImportPreview { Source = source, DefaultBranchId = defaultBranchId };
        var snapshot = store.Read(d => (
            Branches: d.Branches.Select(b => (b.Id, b.Code, b.IsActive)).ToList(),
            Categories: d.Categories.Select(c => (c.Id, c.Name, c.ItemType)).ToList(),
            Codes: new HashSet<string>(d.Items.Select(i => i.LibraryCode), StringComparer.OrdinalIgnoreCase)));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var thisYear = clock.Today.Year;

        foreach (var rec in records)
        {
            var row = new ImportRow { Record = rec };
            preview.Rows.Add(row);

            // Type
            ItemType? type = rec.Type.Trim().ToLowerInvariant() switch
            {
                "book" => ItemType.Book, "music" => ItemType.Music, "toy" => ItemType.Toy, _ => null
            };
            if (type is null) row.Errors.Add($"Type '{rec.Type}' isn't valid. Use book, music or toy.");

            // Code
            var code = rec.Code.Trim().ToUpperInvariant();
            if (code.Length == 0) row.Errors.Add("Library code is required.");
            else if (code.Length > 30 || !CodePattern().IsMatch(code)) row.Errors.Add($"Library code '{rec.Code}' may only contain letters, digits, '-', '_' and '.' (max 30 characters).");
            else if (snapshot.Codes.Contains(code)) row.Errors.Add($"Library code '{code}' already exists in the catalogue (duplicate).");
            else if (!seen.Add(code)) row.Errors.Add($"Library code '{code}' appears more than once in this file (duplicate).");

            // Name
            if (string.IsNullOrWhiteSpace(rec.Name)) row.Errors.Add("Name is required.");
            else if (rec.Name.Length > 200) row.Errors.Add("Name is longer than 200 characters.");

            // Branch
            var branchId = defaultBranchId;
            if (!string.IsNullOrWhiteSpace(rec.Branch))
            {
                var b = snapshot.Branches.FirstOrDefault(x => string.Equals(x.Code, rec.Branch.Trim(), StringComparison.OrdinalIgnoreCase));
                if (b.Id == 0) row.Errors.Add($"Branch '{rec.Branch}' doesn't exist. Known branches: {string.Join(", ", snapshot.Branches.Select(x => x.Code))}.");
                else if (!b.IsActive) row.Errors.Add($"Branch '{rec.Branch}' is closed down.");
                else branchId = b.Id;
            }

            // Category and type-specific fields
            int? categoryId = null;
            if (type is { } t)
            {
                var cats = snapshot.Categories.Where(c => c.ItemType == t).ToList();
                if (string.IsNullOrWhiteSpace(rec.Category)) row.Errors.Add("Category is required.");
                else
                {
                    var c = cats.FirstOrDefault(x => string.Equals(x.Name, rec.Category.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (c.Id == 0) row.Errors.Add($"Category '{rec.Category}' doesn't exist for {t.ToString().ToLowerInvariant()}s. Known: {string.Join(", ", cats.Select(x => x.Name))}.");
                    else categoryId = c.Id;
                }

                switch (t)
                {
                    case ItemType.Book:
                        if (string.IsNullOrWhiteSpace(rec.Author)) row.Errors.Add("Author is required for a book.");
                        break;
                    case ItemType.Music:
                        if (string.IsNullOrWhiteSpace(rec.Artist)) row.Errors.Add("Artist is required for music.");
                        if (!int.TryParse(rec.Year, out var y) || y < 1000 || y > thisYear + 1)
                            row.Errors.Add($"Year '{rec.Year}' isn't valid (1000 to {thisYear + 1}).");
                        break;
                    case ItemType.Toy:
                        if (!int.TryParse(rec.Age, out var a) || a < 0 || a > 120)
                            row.Errors.Add($"Age '{rec.Age}' isn't valid (0 to 120).");
                        break;
                }
            }

            if (row.Errors.Count == 0 && type is { } tt)
            {
                var category = snapshot.Categories.First(c => c.Id == categoryId);
                Item item = tt switch
                {
                    ItemType.Book => new Book { Author = rec.Author.Trim(), Genre = category.Name },
                    ItemType.Music => new Music { Artist = rec.Artist.Trim(), Year = int.Parse(rec.Year) },
                    _ => new Toy { ToyType = category.Name, RecommendedAge = int.Parse(rec.Age) }
                };
                item.LibraryCode = code;
                item.Name = rec.Name.Trim();
                item.Description = rec.Description.Trim();
                item.BranchId = branchId;
                item.CategoryId = categoryId;
                row.Item = item;
            }
        }
        return preview;
    }

    // --------------------------------------------------------------- importing

    /// <summary>Imports the valid rows of a preview (invalid rows are skipped and listed in the history). Returns the batch record.</summary>
    public ImportBatch Commit(ImportPreview preview, string importedBy)
    {
        var valid = preview.Rows.Where(r => r.IsValid).ToList();
        return store.Write(data =>
        {
            var imported = 0;
            var problems = preview.Rows.Where(r => !r.IsValid)
                .Select(r => $"Line {r.Record.RowNumber} ({r.Record.Code}): {string.Join(" ", r.Errors)}").ToList();

            foreach (var row in valid)
            {
                var item = row.Item!;
                // Re-check at the moment of writing: someone may have added the same code since the preview.
                if (data.FindItemByCode(item.LibraryCode) != null)
                {
                    problems.Add($"Line {row.Record.RowNumber} ({item.LibraryCode}): the code was added by someone else while you were reviewing.");
                    continue;
                }
                item.Id = data.TakeId();
                item.Status = ItemStatus.Available;
                data.Items.Add(item);
                imported++;
            }

            var batch = new ImportBatch
            {
                Id = data.TakeId(), Source = preview.Source, BranchId = preview.DefaultBranchId, ImportedBy = importedBy,
                ImportedAt = clock.Now, TotalRows = preview.Rows.Count, ImportedRows = imported,
                SkippedRows = preview.Rows.Count - imported, Problems = problems
            };
            data.ImportBatches.Add(batch);
            return batch;
        });
    }

    public IReadOnlyList<ImportBatch> History() =>
        store.Read(d => (IReadOnlyList<ImportBatch>)d.ImportBatches.OrderByDescending(b => b.ImportedAt).ThenByDescending(b => b.Id).ToList());

    /// <summary>The problems from a preview as a CSV the user can fix and upload again.</summary>
    public string ProblemsCsv(ImportPreview preview) =>
        Csv.Write(new[] { new[] { "line", "code", "name", "problems" } }.Concat(
            preview.Rows.Where(r => !r.IsValid).Select(r => new[] { r.Record.RowNumber.ToString(), r.Record.Code, r.Record.Name, string.Join(" ", r.Errors) })));

    [GeneratedRegex("[^A-Za-z]")] private static partial Regex NonLetters();
    [GeneratedRegex(@"^[A-Z0-9._-]+$")] private static partial Regex CodePattern();
}
