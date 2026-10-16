using Library.Core.Contracts;

namespace Library.Core.Import;

public sealed class CatalogueCodeLookup(ICatalogueQuery catalogue) : ICatalogueCodeLookup
{
    public async Task<IReadOnlyCollection<string>> GetExistingCodesAsync(IEnumerable<string> codes, CancellationToken ct = default)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in codes.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            if (await catalogue.CodeExistsAsync(code, ct))
                existing.Add(code);
        }
        return existing;
    }
}
