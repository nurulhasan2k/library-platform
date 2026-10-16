using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Reports;
using Microsoft.Extensions.Options;
using Xunit;

namespace Library.Reports.Tests;

public sealed class ReportBoundaryTests
{
    [Theory]
    [InlineData(null, null, true, null)]
    [InlineData(-10, null, false, 10)]
    [InlineData(-30, null, false, 30)]
    [InlineData(-31, null, true, 31)]
    [InlineData(-10, -60, false, 10)]
    [InlineData(-60, -10, false, 10)]
    [InlineData(-60, -31, true, 31)]
    public void IdleUsesLaterCreationOrBorrowDateWithStrictThreshold(int? createdOffset, int? borrowedOffset, bool idle, int? days)
    {
        var row = FixedReportData.Items.Single(row => row.Code == "BK-C") with
        {
            CreatedOn = createdOffset is { } c ? FixedReportData.Today.AddDays(c) : null,
            LastBorrowedOn = borrowedOffset is { } b ? FixedReportData.Today.AddDays(b) : null
        };
        var report = InventoryHealthCalculator.Calculate([row], [], [], FixedReportData.Today, 30).Report!;
        Assert.Equal(idle, Assert.Single(report.Items).Idle);
        Assert.Equal(days, report.Items[0].DaysIdle);
    }

    [Fact]
    public void ReadyCountsAreSeparateAndTransfersOverlapStoredStatuses()
    {
        var report = InventoryHealthCalculator.Calculate(FixedReportData.Items,
            [new("BK-B", 2, 1), new("MU-A", 0, 3)], FixedReportData.Transfers, FixedReportData.Today, 30).Report!;
        Assert.Equal(("BK-B", 2, 1), (Assert.Single(report.Waitlists).Code, report.Waitlists[0].Waiting, report.Waitlists[0].Ready));
        Assert.Equal(3, report.Items.Single(row => row.Code == "MU-A").Ready);
        Assert.Equal(ItemStatus.Available, report.Items.Single(row => row.Code == "BK-A").Status);
        Assert.True(report.Items.Single(row => row.Code == "BK-A").InTransit);
    }

    [Fact]
    public void DestroyedItemsKeepHistoricalLoansAndFines()
    {
        var historical = FixedReportData.Loans[1] with { ItemCode = "BK-D", ItemName = "Destroyed item" };
        Assert.Equal(1, BorrowingStatisticsCalculator.Calculate([historical], FixedReportData.BorrowingRange, FixedReportData.Today).Report!.Loans);
        Assert.Equal(2m, FineRevenueCalculator.Calculate([historical], FixedReportData.FineRange, FixedReportData.Today, 0.5m).Report!.Charged);
    }

    [Fact]
    public void SameDayBoundsAreInclusiveAndPaymentDateDoesNotChangeCohort()
    {
        var fine = FixedReportData.Loans[1] with { FinePaidOn = new DateOnly(2027, 1, 1) };
        var date = fine.ReturnedOn!.Value;
        var report = FineRevenueCalculator.Calculate([fine], new(date, date), FixedReportData.Today, 0.5m).Report!;
        Assert.Equal((2m, 2m, 0m), (report.Charged, report.Collected, report.Outstanding));
        Assert.Equal(1, BorrowingStatisticsCalculator.Calculate([fine], new(fine.BorrowedOn, fine.BorrowedOn), FixedReportData.Today).Report!.Loans);
    }

    [Fact]
    public void TopItemsAreLimitedToTenAndTiesUseCode()
    {
        var loans = Enumerable.Range(1, 12).Select(id => FixedReportData.Loans[0] with { Id = id, ItemCode = $"BK-{id:00}" }).Reverse().ToArray();
        var report = BorrowingStatisticsCalculator.Calculate(loans, FixedReportData.BorrowingRange, FixedReportData.Today).Report!;
        Assert.Equal(Enumerable.Range(1, 10).Select(id => $"BK-{id:00}"), report.TopItems.Select(item => item.Code));
    }

    [Fact]
    public async Task ServiceUsesClockDefaultRangeFineSettingAndInventoryDefault()
    {
        var clock = new FixedClock();
        IReportService service = new ReportService(new InMemoryReportDataSource(FixedReportData.Data), clock,
            Options.Create(new ReportOptions()), Options.Create(new LendingOptions { FinePerDay = 1m }));
        var borrowing = (await service.GetBorrowingStatisticsAsync()).Report!;
        Assert.Equal(180, borrowing.Filter.To.DayNumber - borrowing.Filter.From.DayNumber + 1);
        Assert.Equal(FixedReportData.Today, borrowing.Today);
        Assert.Equal(9, borrowing.Loans);
        Assert.Equal(1m, (await service.GetFineRevenueAsync(FixedReportData.FineRange)).Report!.AccruingNotCharged);
        Assert.Equal(90, (await service.GetInventoryHealthAsync()).Report!.IdleDays);
        Assert.Equal(2, (await service.GetInventoryHealthAsync(idleDays: 20)).Report!.Idle.Count);
        clock.Today = FixedReportData.Today.AddDays(1);
        Assert.Equal(2m, (await service.GetFineRevenueAsync()).Report!.AccruingNotCharged);
        Assert.Equal(clock.Today, (await service.GetInventoryHealthAsync()).Report!.Today);
    }

    [Fact]
    public async Task ServiceValidatesDatesBeforeReadingData()
    {
        var source = new UnavailableDataSource();
        var service = new ReportService(source, new FixedClock(), Options.Create(new ReportOptions()), Options.Create(new LendingOptions()));
        var invalid = new ReportFilter(FixedReportData.Today, FixedReportData.Today.AddDays(-1));
        Assert.False((await service.GetBorrowingStatisticsAsync(invalid)).IsValid);
        Assert.False((await service.GetFineRevenueAsync(invalid)).IsValid);
    }

    [Fact]
    public async Task DataSourcePreservesReturnCohortsAndCurrentStateOutsideTheDateRange()
    {
        var source = new InMemoryReportDataSource(FixedReportData.Data);
        var data = await source.ReadAsync(new(new(2026, 10, 1), new(2026, 10, 5), 2));
        Assert.Equal(new[] { 5, 7, 8 }, data.Loans.Select(loan => loan.Id));
        Assert.Equal(4, data.Items.Count);
        Assert.All(data.Items, item => Assert.Equal(2, item.Branch.Id));
        Assert.Empty(data.ActiveTransfers);
        Assert.Equal("BK-B", Assert.Single(data.QueueCounts).ItemCode);
        var fines = FineRevenueCalculator.Calculate(data.Loans, new(new(2026, 10, 1), new(2026, 10, 5), 2), FixedReportData.Today, 0.5m).Report!;
        Assert.Equal(2.5m, fines.Charged);
        Assert.Equal(0.5m, fines.AccruingNotCharged);
    }

    [Fact]
    public async Task ServiceMatchesTheBranchVariantsUsingTheInMemoryDataSource()
    {
        IReportService service = new ReportService(new InMemoryReportDataSource(FixedReportData.Data), new FixedClock(),
            Options.Create(new ReportOptions()), Options.Create(new LendingOptions()));
        var borrowing = (await service.GetBorrowingStatisticsAsync(FixedReportData.BorrowingRange with { BranchId = 2 })).Report!;
        Assert.Equal(5, borrowing.Loans);
        var fines = (await service.GetFineRevenueAsync(FixedReportData.FineRange with { BranchId = 2 })).Report!;
        Assert.Equal((7.5m, 2.5m, 5m), (fines.Charged, fines.Collected, fines.Outstanding));
        var inventory = (await service.GetInventoryHealthAsync(1, 30)).Report!;
        Assert.Equal(2, inventory.Items.Count);
        Assert.Equal("BK-A", Assert.Single(inventory.InTransit).ItemCode);
    }

    [Fact]
    public void DueTodayIsNotOverdueAndCompletedTransfersDoNotCount()
    {
        var loan = FixedReportData.Loans[6] with { DueOn = FixedReportData.Today };
        Assert.Equal(0, BorrowingStatisticsCalculator.Calculate([loan], FixedReportData.BorrowingRange, FixedReportData.Today).Report!.Overdue);
        Assert.Equal(0m, FineRevenueCalculator.Calculate([loan], FixedReportData.FineRange, FixedReportData.Today, 0.5m).Report!.AccruingNotCharged);
        var item = FixedReportData.Items[0] with { InTransit = false, HasActiveLoan = true, ActiveLoanDueOn = FixedReportData.Today };
        var completed = FixedReportData.Transfers[0] with { Status = TransferStatus.Received };
        var report = InventoryHealthCalculator.Calculate([item], [], [completed], FixedReportData.Today, 30).Report!;
        Assert.Empty(report.Overdue);
        Assert.Empty(report.InTransit);
    }

    private sealed class FixedClock : IClock
    {
        public DateOnly Today { get; set; } = FixedReportData.Today;
        public DateTime Now => Today.ToDateTime(TimeOnly.MinValue);
    }
    private sealed class UnavailableDataSource : IReportDataSource
    {
        public Task<ReportData> ReadAsync(ReportFilter filter, CancellationToken ct = default)
            => throw new InvalidOperationException("Invalid filters must not reach the data source.");
    }
}
