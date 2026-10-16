using Library.Core.Configuration;
using Library.Core.Contracts;
using Microsoft.Extensions.Options;

namespace Library.Core.Reports;

public sealed class ReportService(IReportDataSource dataSource, IClock clock, IOptions<ReportOptions> options,
    IOptions<LendingOptions> lending) : IReportService
{
    public async Task<ReportResult<BorrowingStatisticsReport>> GetBorrowingStatisticsAsync(ReportFilter? filter = null, CancellationToken ct = default)
    {
        var today = clock.Today;
        if (filter is null && options.Value.DefaultRangeDays < 1)
            return ReportResult<BorrowingStatisticsReport>.Invalid("Default range days must be greater than zero.");
        filter ??= DefaultFilter(today);
        var errors = ReportValidation.Errors(filter);
        if (errors.Length > 0) return ReportResult<BorrowingStatisticsReport>.Invalid(errors);
        return BorrowingStatisticsCalculator.Calculate((await dataSource.ReadAsync(filter, ct)).Loans, filter, today);
    }

    public async Task<ReportResult<FineRevenueReport>> GetFineRevenueAsync(ReportFilter? filter = null, CancellationToken ct = default)
    {
        var today = clock.Today;
        if (filter is null && options.Value.DefaultRangeDays < 1)
            return ReportResult<FineRevenueReport>.Invalid("Default range days must be greater than zero.");
        filter ??= DefaultFilter(today);
        var errors = ReportValidation.Errors(filter);
        if (errors.Length > 0) return ReportResult<FineRevenueReport>.Invalid(errors);
        return FineRevenueCalculator.Calculate((await dataSource.ReadAsync(filter, ct)).Loans, filter, today, lending.Value.FinePerDay);
    }

    public async Task<ReportResult<InventoryHealthReport>> GetInventoryHealthAsync(int? branchId = null, int? idleDays = null, CancellationToken ct = default)
    {
        var today = clock.Today;
        var days = idleDays ?? options.Value.IdleItemDays;
        if (days < 0) return ReportResult<InventoryHealthReport>.Invalid("Idle days cannot be negative.");
        var data = await dataSource.ReadAsync(new ReportFilter(today, today, branchId), ct);
        return InventoryHealthCalculator.Calculate(data.Items, data.QueueCounts, data.ActiveTransfers, today, days, branchId);
    }

    private ReportFilter DefaultFilter(DateOnly today)
        => new(DateOnly.FromDayNumber(Math.Max(0, today.DayNumber - options.Value.DefaultRangeDays + 1)), today);
}
