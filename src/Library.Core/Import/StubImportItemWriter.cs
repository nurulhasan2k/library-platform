using Library.Core.Contracts;

namespace Library.Core.Import;

public sealed class StubImportItemWriter : IImportItemWriter
{
    public Task<ImportWriteResult> CreateManyAsync(IReadOnlyList<ValidItemRow> rows, int targetBranchId, CancellationToken ct = default)
        => Task.FromResult(new ImportWriteResult(false, 0, "Item creation is not available yet."));
}
