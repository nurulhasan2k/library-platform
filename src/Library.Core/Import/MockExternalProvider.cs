namespace Library.Core.Import;

/// <summary>
/// A pretend external system ("Partner Library Network") so the importer can be demonstrated without a real service.
/// It returns a fixed catalogue, including a few records that break the rules, so validation is visible.
/// </summary>
public sealed class MockExternalProvider : IExternalCatalogueProvider
{
    public string Name => "Partner Library Network (mock)";
    public string Description => "A built-in pretend external catalogue with 12 records (2 contain deliberate mistakes).";

    public IReadOnlyList<ImportRecord> Fetch()
    {
        ImportRecord R(int n, string type, string code, string name, string cat, string a = "", string artist = "", string year = "", string age = "", string desc = "", string branch = "") =>
            new() { RowNumber = n, Type = type, Code = code, Name = name, Category = cat, Author = a, Artist = artist, Year = year, Age = age, Description = desc, Branch = branch };

        return new List<ImportRecord>
        {
            R(1, "book", "EXT-BK-001", "The Left Hand of Darkness", "Science Fiction", a: "Ursula K. Le Guin", desc: "Partner network loan copy."),
            R(2, "book", "EXT-BK-002", "Mistborn: The Final Empire", "Fantasy", a: "Brandon Sanderson"),
            R(3, "book", "EXT-BK-003", "Refactoring", "Programming", a: "Martin Fowler"),
            R(4, "book", "EXT-BK-004", "Sapiens", "History", a: "Yuval Noah Harari"),
            R(5, "music", "EXT-MU-001", "Back in Black", "Rock", artist: "AC/DC", year: "1980"),
            R(6, "music", "EXT-MU-002", "Time Out", "Jazz", artist: "The Dave Brubeck Quartet", year: "1959"),
            R(7, "music", "EXT-MU-003", "Symphony No. 9", "Classical", artist: "Beethoven", year: "1824"),
            R(8, "toy", "EXT-TY-001", "Pentomino puzzle", "Puzzle", age: "7"),
            R(9, "toy", "EXT-TY-002", "Magnetic tiles", "Building", age: "3"),
            R(10, "toy", "EXT-TY-003", "Castle bricks set", "Building", age: "6"),
            // Deliberate mistakes: an unknown category and a missing author.
            R(11, "book", "EXT-BK-005", "Mystery Cookbook", "Cooking", a: "A. Chef"),
            R(12, "book", "EXT-BK-006", "Anonymous Pamphlet", "History"),
        };
    }
}
