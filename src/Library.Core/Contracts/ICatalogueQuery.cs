namespace Library.Core.Contracts;

public sealed record CategoryInfo(int Id, string Name, ItemType ItemType, int ItemCount);

public sealed record BranchSummary(
    int Id,
    string Code,
    string Name,
    string Address,
    string Phone,
    bool IsActive,
    IReadOnlyList<OpeningHours> Hours);

/// <summary>
/// A read-only view of an item that is safe to show publicly and to return from the API.
/// It never contains borrower, loan or fine information.
/// </summary>
public sealed record CatalogueItem(
    int Id,
    string LibraryCode,
    string Name,
    ItemType Type,
    string? Description,
    ItemStatus Status,
    int? CategoryId,
    string? CategoryName,
    int BranchId,
    string BranchCode,
    string BranchName,
    bool InTransit,
    IReadOnlyDictionary<string, string> Details);

/// <summary>Search filters. Null means "no filter". Destroyed items are never returned by this query.</summary>
public sealed record CatalogueFilter(
    string? Text = null,
    ItemType? Type = null,
    int? CategoryId = null,
    int? BranchId = null,
    bool AvailableOnly = false,
    int Page = 1,
    int PageSize = 20);

/// <summary>
/// Read-only queries over items, categories, branches and availability, shared by the public pages,
/// the REST API and the importer's duplicate check. Built by Person A, extended by Person C.
/// </summary>
public interface ICatalogueQuery
{
    Task<PagedResult<CatalogueItem>> SearchAsync(CatalogueFilter filter, CancellationToken ct = default);
    Task<CatalogueItem?> FindByCodeAsync(string libraryCode, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string libraryCode, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BranchSummary>> GetBranchesAsync(CancellationToken ct = default);
}
