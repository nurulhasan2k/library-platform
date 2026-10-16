namespace Library.Core.Reports;

public interface IReportService
{
    Task<ReportResult<BorrowingStatisticsReport>> GetBorrowingStatisticsAsync(ReportFilter? filter = null, CancellationToken ct = default);
    Task<ReportResult<FineRevenueReport>> GetFineRevenueAsync(ReportFilter? filter = null, CancellationToken ct = default);
    Task<ReportResult<InventoryHealthReport>> GetInventoryHealthAsync(int? branchId = null, int? idleDays = null, CancellationToken ct = default);
}
