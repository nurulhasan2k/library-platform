using Library.Core.Contracts;

namespace Library.Core.Import;

public sealed record ValidItemRow(
    int RowNumber, string LibraryCode, ItemType ItemType, string Name, string Description,
    int CategoryId, int BranchId, ItemStatus Status, string Author, string Genre,
    string Artist, int? Year, string ToyType, int? RecommendedAge);

public sealed record ImportRowResult(
    int RowNumber, IReadOnlyDictionary<string, string> Fields, IReadOnlyList<string> Errors)
{
    public string Reason => string.Join(" ", Errors);
}
