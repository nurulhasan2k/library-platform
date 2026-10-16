namespace Library.Core.Reports;

public interface IReportDataSource
{
    // Include loans by either borrow or return date, plus active loans for current accrual.
    Task<ReportData> ReadAsync(ReportFilter filter, CancellationToken ct = default);
}

public sealed class InMemoryReportDataSource(ReportData data) : IReportDataSource
{
    public Task<ReportData> ReadAsync(ReportFilter filter, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var items = data.Items.Where(item => filter.BranchId is null || item.Branch.Id == filter.BranchId).ToArray();
        var codes = items.Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var loans = data.Loans.Where(loan =>
            (filter.BranchId is null || loan.BorrowedAtBranch.Id == filter.BranchId || loan.ReturnedAtBranch?.Id == filter.BranchId) &&
            (loan.ReturnedOn is null || (loan.BorrowedOn >= filter.From && loan.BorrowedOn <= filter.To) ||
                (loan.ReturnedOn >= filter.From && loan.ReturnedOn <= filter.To))).ToArray();
        return Task.FromResult(new ReportData(loans, items,
            data.QueueCounts.Where(queue => codes.Contains(queue.ItemCode)).ToArray(),
            data.ActiveTransfers.Where(transfer => codes.Contains(transfer.ItemCode)).ToArray()));
    }
}
