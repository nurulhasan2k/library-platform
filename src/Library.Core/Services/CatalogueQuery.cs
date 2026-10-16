using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Models;

namespace Library.Core.Services;

/// <summary>
/// Read-only queries over items, categories and branches, shared by the public pages, the REST API and the importer's
/// duplicate check. Never returns patron, loan or fine data, and never returns destroyed items.
/// </summary>
public sealed class CatalogueQuery(ILibraryStore store) : ICatalogueQuery
{
    public Task<PagedResult<CatalogueItem>> SearchAsync(CatalogueFilter filter, CancellationToken ct = default)
    {
        var page = Math.Max(1, filter.Page);
        var size = Math.Clamp(filter.PageSize, 1, 500);
        var terms = (filter.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = store.Read(d =>
        {
            IEnumerable<Item> q = d.Items.Where(i => i.Status != ItemStatus.Destroy);
            if (filter.Type is { } t) q = q.Where(i => i.Type == t);
            if (filter.CategoryId is { } c) q = q.Where(i => i.CategoryId == c);
            if (filter.BranchId is { } b) q = q.Where(i => i.BranchId == b);
            if (filter.AvailableOnly) q = q.Where(i => i.Status == ItemStatus.Available && !d.IsInTransit(i.Id));
            foreach (var term in terms)
            {
                var x = term;
                q = q.Where(i => i.GetSearchText().Contains(x, StringComparison.OrdinalIgnoreCase));
            }

            var all = q.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.LibraryCode).ToList();
            var items = all.Skip((page - 1) * size).Take(size).Select(i => Map(d, i)).ToList();
            return new PagedResult<CatalogueItem>(items, all.Count, page, size);
        });
        return Task.FromResult(result);
    }

    public Task<CatalogueItem?> FindByCodeAsync(string libraryCode, CancellationToken ct = default) =>
        Task.FromResult(store.Read(d =>
        {
            var i = d.FindItemByCode(libraryCode);
            return i is null || i.Status == ItemStatus.Destroy ? null : Map(d, i);
        }));

    public Task<bool> CodeExistsAsync(string libraryCode, CancellationToken ct = default) =>
        Task.FromResult(store.Read(d => d.FindItemByCode(libraryCode) != null));

    public Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken ct = default) =>
        Task.FromResult(store.Read(d => (IReadOnlyList<CategoryInfo>)d.Categories.OrderBy(c => c.ItemType).ThenBy(c => c.Name)
            .Select(c => new CategoryInfo(c.Id, c.Name, c.ItemType, d.Items.Count(i => i.CategoryId == c.Id && i.Status != ItemStatus.Destroy))).ToList()));

    public Task<IReadOnlyList<BranchSummary>> GetBranchesAsync(CancellationToken ct = default) =>
        Task.FromResult(store.Read(d => (IReadOnlyList<BranchSummary>)d.Branches.OrderBy(b => b.Name)
            .Select(b => new BranchSummary(b.Id, b.Code, b.Name, b.Address, b.Phone, b.IsActive,
                b.Hours.OrderBy(h => ((int)h.Day + 6) % 7).ToList())).ToList()));

    private static CatalogueItem Map(LibraryData d, Item i)
    {
        var branch = d.FindBranch(i.BranchId);
        var cat = i.CategoryId is { } cid ? d.Categories.FirstOrDefault(c => c.Id == cid) : null;
        return new CatalogueItem(i.Id, i.LibraryCode, i.Name, i.Type, string.IsNullOrWhiteSpace(i.Description) ? null : i.Description,
            i.Status, cat?.Id, cat?.Name, i.BranchId, branch?.Code ?? "", branch?.Name ?? "", d.IsInTransit(i.Id), i.GetExtraDetails());
    }
}
