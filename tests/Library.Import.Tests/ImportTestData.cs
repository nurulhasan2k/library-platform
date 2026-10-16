using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Import;
using Microsoft.Extensions.Options;

namespace Library.Import.Tests;

internal static class ImportTestData
{
    public static readonly ImportLookups Lookups = new(
        [new(1, "CENTRAL", "Central", "", "", true, []), new(2, "RIVER", "River", "", "", true, [])],
        [new(1, "Science Fiction", ItemType.Book, 0), new(2, "Fantasy", ItemType.Book, 0),
         new(3, "Programming", ItemType.Book, 0), new(4, "History", ItemType.Book, 0),
         new(5, "Rock", ItemType.Music, 0), new(6, "Jazz", ItemType.Music, 0), new(7, "Classical", ItemType.Music, 0),
         new(8, "Puzzle", ItemType.Toy, 0), new(9, "Building", ItemType.Toy, 0)], []);

    public static byte[] Sample(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "import", name));

    public static CatalogueCsvValidator Validator(ImportOptions? options = null, int year = 2026)
        => new(new FixedClock(year), Options.Create(options ?? new ImportOptions()));

    public static ImportValidationResult Validate(string name = "items-valid.csv", ImportLookups? lookups = null,
        ImportOptions? options = null, int? targetBranchId = null)
    {
        using var stream = new MemoryStream(Sample(name));
        return Validator(options).Validate(stream, name, lookups ?? Lookups, targetBranchId);
    }

    public static ImportValidationResult ValidateBytes(byte[] bytes, string fileName = "items.csv", ImportOptions? options = null)
    {
        using var stream = new MemoryStream(bytes);
        return Validator(options).Validate(stream, fileName, Lookups);
    }

    private sealed class FixedClock(int year) : IClock
    {
        public DateOnly Today => new(year, 10, 15);
        public DateTime Now => new(year, 10, 15, 12, 0, 0);
    }
}
