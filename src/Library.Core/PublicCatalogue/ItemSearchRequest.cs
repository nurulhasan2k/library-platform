using Library.Core.Contracts;

namespace Library.Core.PublicCatalogue;

public sealed record ItemSearchRequest(
    string? Text, string? Type, string? Category, string? Branch,
    string? AvailableOnly, string? Page, string? PageSize);

public sealed record ItemSearchResult(
    PagedResult<CatalogueItem>? Page, IReadOnlyDictionary<string, string[]> Errors);

public sealed record ItemLookupResult(CatalogueItem? Item, string? Error);
