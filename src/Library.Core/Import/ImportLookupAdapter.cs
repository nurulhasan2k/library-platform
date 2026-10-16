using Library.Core.Contracts;

namespace Library.Core.Import;

public sealed class ImportLookupAdapter(ICatalogueQuery catalogue, ICatalogueCodeLookup codeLookup)
{
    public async Task<ImportLookups> LoadAsync(IEnumerable<string> codes, CancellationToken ct = default)
    {
        var categories = await catalogue.GetCategoriesAsync(ct);
        var branches = await catalogue.GetBranchesAsync(ct);
        var existing = await codeLookup.GetExistingCodesAsync(codes, ct);
        return new ImportLookups(branches, categories, existing);
    }
}
