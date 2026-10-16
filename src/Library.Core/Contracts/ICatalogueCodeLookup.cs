namespace Library.Core.Contracts;

public interface ICatalogueCodeLookup
{
    Task<IReadOnlyCollection<string>> GetExistingCodesAsync(IEnumerable<string> codes, CancellationToken ct = default);
}
