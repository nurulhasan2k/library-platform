namespace Library.Core.Import;

public sealed record ExternalCatalogueResult(IReadOnlyList<IReadOnlyDictionary<string, string>> Rows, string? Error);

public interface IExternalCatalogueProvider
{
    Task<ExternalCatalogueResult> ReadAsync(CancellationToken ct = default);
}
