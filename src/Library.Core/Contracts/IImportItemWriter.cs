using Library.Core.Import;

namespace Library.Core.Contracts;

public sealed record ImportWriteResult(bool Success, int CreatedCount, string? Error);

public interface IImportItemWriter
{
    // Revalidate against current data and create all rows in one transaction, or create none.
    Task<ImportWriteResult> CreateManyAsync(IReadOnlyList<ValidItemRow> rows, int targetBranchId, CancellationToken ct = default);
}
