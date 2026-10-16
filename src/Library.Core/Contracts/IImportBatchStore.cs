using Library.Core.Import;

namespace Library.Core.Contracts;

public interface IImportBatchStore
{
    Task<ImportBatch> AddAsync(ImportBatch batch, CancellationToken ct = default);
    Task<ImportBatch?> FindAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ImportBatch>> ListAsync(CancellationToken ct = default);
}
