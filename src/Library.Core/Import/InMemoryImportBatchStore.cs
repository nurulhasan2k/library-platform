using System.Collections.Concurrent;
using Library.Core.Contracts;

namespace Library.Core.Import;

public sealed class InMemoryImportBatchStore : IImportBatchStore
{
    private readonly ConcurrentDictionary<int, ImportBatch> batches = new();
    private int nextId;

    public Task<ImportBatch> AddAsync(ImportBatch batch, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var saved = batch with { Id = Interlocked.Increment(ref nextId) };
        batches[saved.Id] = saved;
        return Task.FromResult(saved);
    }

    public Task<ImportBatch?> FindAsync(int id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(batches.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<ImportBatch>> ListAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ImportBatch>>(batches.Values.OrderByDescending(batch => batch.At)
            .ThenByDescending(batch => batch.Id).ToArray());
    }
}
