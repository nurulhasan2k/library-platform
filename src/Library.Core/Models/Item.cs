using System.Globalization;
using System.Text.Json.Serialization;
using Library.Core.Contracts;

namespace Library.Core.Models;

/// <summary>
/// Base class for everything the library lends out (brief 1a-1d). Book, Music and Toy inherit from it.
/// <see cref="BranchId"/> is where the copy physically is (decision 5).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "itemType")]
[JsonDerivedType(typeof(Book), "book")]
[JsonDerivedType(typeof(Music), "music")]
[JsonDerivedType(typeof(Toy), "toy")]
public abstract class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string LibraryCode { get; set; } = "";
    public ItemStatus Status { get; set; } = ItemStatus.Available;
    public int BranchId { get; set; }
    public int? CategoryId { get; set; }

    [JsonIgnore] public abstract ItemType Type { get; }
    [JsonIgnore] public string TypeName => Type.ToString().ToLowerInvariant();

    public virtual IReadOnlyDictionary<string, string> GetExtraDetails() => new Dictionary<string, string>();

    public virtual IEnumerable<string> GetValidationErrors()
    {
        if (string.IsNullOrWhiteSpace(Name)) yield return "Name is required.";
        if (string.IsNullOrWhiteSpace(LibraryCode)) yield return "Library code is required.";
        if (BranchId <= 0) yield return "Choose the branch the item is at.";
    }

    public string GetSearchText() =>
        string.Join(' ', new[] { Name, Description, LibraryCode, TypeName }.Concat(GetExtraDetails().Values));
}

public sealed class Book : Item
{
    public string Author { get; set; } = "";
    public string Genre { get; set; } = "";
    [JsonIgnore] public override ItemType Type => ItemType.Book;

    public override IReadOnlyDictionary<string, string> GetExtraDetails() =>
        new Dictionary<string, string> { ["Author"] = Author, ["Genre"] = Genre };

    public override IEnumerable<string> GetValidationErrors()
    {
        foreach (var e in base.GetValidationErrors()) yield return e;
        if (string.IsNullOrWhiteSpace(Author)) yield return "Author is required.";
        if (string.IsNullOrWhiteSpace(Genre)) yield return "Genre is required (choose a category).";
    }
}

public sealed class Music : Item
{
    public string Artist { get; set; } = "";
    public int Year { get; set; }
    [JsonIgnore] public override ItemType Type => ItemType.Music;

    public override IReadOnlyDictionary<string, string> GetExtraDetails() =>
        new Dictionary<string, string> { ["Artist"] = Artist, ["Year"] = Year.ToString(CultureInfo.InvariantCulture) };

    public override IEnumerable<string> GetValidationErrors()
    {
        foreach (var e in base.GetValidationErrors()) yield return e;
        if (string.IsNullOrWhiteSpace(Artist)) yield return "Artist is required.";
        if (Year < 1000 || Year > DateTime.Now.Year + 1) yield return $"Year must be between 1000 and {DateTime.Now.Year + 1}.";
    }
}

public sealed class Toy : Item
{
    public string ToyType { get; set; } = "";
    public int RecommendedAge { get; set; }
    [JsonIgnore] public override ItemType Type => ItemType.Toy;

    public override IReadOnlyDictionary<string, string> GetExtraDetails() =>
        new Dictionary<string, string>
        {
            ["Toy type"] = ToyType,
            ["Age"] = RecommendedAge.ToString(CultureInfo.InvariantCulture) + "+"
        };

    public override IEnumerable<string> GetValidationErrors()
    {
        foreach (var e in base.GetValidationErrors()) yield return e;
        if (string.IsNullOrWhiteSpace(ToyType)) yield return "Toy type is required (choose a category).";
        if (RecommendedAge < 0 || RecommendedAge > 120) yield return "Age must be between 0 and 120.";
    }
}

public static class ItemStatusExtensions
{
    public static string ToLabel(this ItemStatus status) => status.ToString().ToLowerInvariant();
}
