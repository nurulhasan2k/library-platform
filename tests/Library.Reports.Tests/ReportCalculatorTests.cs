using Library.Core.Contracts;
using Library.Core.Reports;
using Xunit;

namespace Library.Reports.Tests;

public sealed class ReportCalculatorTests
{
    [Fact]
    public void BorrowingMatchesEveryFixedDatasetTotalAndBreakdown()
    {
        var report = BorrowingStatisticsCalculator.Calculate(FixedReportData.Loans, FixedReportData.BorrowingRange, FixedReportData.Today).Report!;
        Assert.False(report.IsEmpty);
        Assert.Equal((9, 2, 1, 7, 3, 4), (report.Loans, report.Active, report.Overdue, report.Returned, report.ReturnedLate, report.ReturnedOnTime));
        Assert.Equal(new[] { new MonthCount(new(2026, 8, 1), 3), new MonthCount(new(2026, 9, 1), 6) }, report.ByMonth);
        Assert.Equal(new[] { new BranchCount(FixedReportData.Central, 4), new BranchCount(FixedReportData.North, 5) }, report.ByBranch);
        Assert.Equal(new[] { new TypeCount(ItemType.Book, 5), new TypeCount(ItemType.Music, 2), new TypeCount(ItemType.Toy, 2) }, report.ByType);
        Assert.Equal(4, report.ByCategory.Count);
        Assert.Equal(3, report.ByCategory.Single(row => row.Category == "Science Fiction").Count);
        foreach (var category in new[] { "Fantasy", "Rock", "Puzzle" }) Assert.Equal(2, report.ByCategory.Single(row => row.Category == category).Count);
        Assert.Equal(new[] { ("BK-A", 3), ("BK-B", 2), ("MU-A", 2), ("TY-A", 2) }, report.TopItems.Select(row => (row.Code, row.Loans)));
        Assert.Equal(9, report.Lines.Count);
        Assert.Equal(BorrowingState.Overdue, report.Lines.Single(line => line.LoanId == 7).State);
        Assert.Equal(BorrowingState.Active, report.Lines.Single(line => line.LoanId == 8).State);
        Assert.Equal(3, report.Lines.Count(line => line.State == BorrowingState.ReturnedLate));
        Assert.Equal(4, report.Lines.Count(line => line.State == BorrowingState.Returned));
    }

    [Fact]
    public void NorthBorrowingUsesTheBorrowBranch()
    {
        var report = BorrowingStatisticsCalculator.Calculate(FixedReportData.Loans,
            FixedReportData.BorrowingRange with { BranchId = 2 }, FixedReportData.Today).Report!;
        Assert.Equal(5, report.Loans);
        Assert.Equal(new[] { new TypeCount(ItemType.Book, 1), new TypeCount(ItemType.Music, 2), new TypeCount(ItemType.Toy, 2) }, report.ByType);
        Assert.Equal(new[] { ("MU-A", 2), ("TY-A", 2), ("BK-B", 1) }, report.TopItems.Select(row => (row.Code, row.Loans)));
        Assert.All(report.Lines, line => Assert.Equal(2, line.BorrowedAtBranch.Id));
    }

    [Fact]
    public void FineRevenueMatchesTheFixedDatasetAndReconciles()
    {
        var report = FineRevenueCalculator.Calculate(FixedReportData.Loans, FixedReportData.FineRange, FixedReportData.Today, 0.5m).Report!;
        Assert.Equal((9.5m, 4.5m, 5m, 0.5m), (report.Charged, report.Collected, report.Outstanding, report.AccruingNotCharged));
        Assert.Equal(new[] { 5, 3, 2 }, report.Lines.Select(line => line.LoanId));
        Assert.Equal(new[] { 5, 10, 4 }, report.Lines.Select(line => line.DaysLate));
        Assert.Equal(new[] { new FineMonth(new(2026, 8, 1), 0m, 0m, 0m), new FineMonth(new(2026, 9, 1), 7m, 2m, 5m),
            new FineMonth(new(2026, 10, 1), 2.5m, 2.5m, 0m) }, report.ByMonth);
        Assert.True(report.Reconciles);
        Assert.Equal(report.Charged, report.Lines.Sum(line => line.Amount));
        Assert.Equal(report.Charged, report.Collected + report.Outstanding);
        Assert.Equal("Borrower 2", report.Lines.Single(line => line.LoanId == 2).BorrowerName);
        Assert.Equal("CARD-2", report.Lines.Single(line => line.LoanId == 2).CardNumber);
        Assert.Equal(new DateOnly(2026, 9, 10), report.Lines.Single(line => line.LoanId == 2).PaidOn);
    }

    [Theory]
    [InlineData(1, "2.00", "2.00", "0.00", "0.00")]
    [InlineData(2, "7.50", "2.50", "5.00", "0.50")]
    public void FineBranchesUseTheReturnBranchAndReconcile(int branch, string charged, string collected, string outstanding, string accruing)
    {
        decimal Number(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var report = FineRevenueCalculator.Calculate(FixedReportData.Loans, FixedReportData.FineRange with { BranchId = branch }, FixedReportData.Today, 0.5m).Report!;
        Assert.Equal(Number(charged), report.Charged);
        Assert.Equal(Number(collected), report.Collected);
        Assert.Equal(Number(outstanding), report.Outstanding);
        Assert.Equal(Number(accruing), report.AccruingNotCharged);
        Assert.Equal(branch == 1 ? new[] { 2 } : new[] { 5, 3 }, report.Lines.Select(line => line.LoanId));
        Assert.True(report.Reconciles);
        Assert.Equal(report.Charged, report.Lines.Sum(line => line.Amount));
        Assert.Equal(report.Charged, report.Collected + report.Outstanding);
    }

    [Fact]
    public void SeptemberFineRangeUsesChargeDates()
    {
        var report = FineRevenueCalculator.Calculate(FixedReportData.Loans, new(new(2026, 9, 1), new(2026, 9, 30)), FixedReportData.Today, 0.5m).Report!;
        Assert.Equal((7m, 2m, 5m), (report.Charged, report.Collected, report.Outstanding));
        Assert.Equal(new[] { 3, 2 }, report.Lines.Select(line => line.LoanId));
        Assert.True(report.Reconciles);
        var central = FineRevenueCalculator.Calculate(FixedReportData.Loans, FixedReportData.FineRange with { BranchId = 1 }, FixedReportData.Today, 0.5m).Report!;
        var north = FineRevenueCalculator.Calculate(FixedReportData.Loans, FixedReportData.FineRange with { BranchId = 2 }, FixedReportData.Today, 0.5m).Report!;
        Assert.Equal(9.5m, central.Charged + north.Charged);
    }

    [Fact]
    public void InventoryMatchesEveryFixedDatasetSection()
    {
        var report = Inventory(30);
        Assert.Equal(6, report.Items.Count);
        Assert.DoesNotContain(report.Items, item => item.Code == "BK-D");
        Assert.Equal(new[] { new InventoryStatusCount(FixedReportData.Central, ItemStatus.Available, 1),
            new InventoryStatusCount(FixedReportData.Central, ItemStatus.Damaged, 1),
            new InventoryStatusCount(FixedReportData.North, ItemStatus.Available, 2),
            new InventoryStatusCount(FixedReportData.North, ItemStatus.Borrowed, 2) }, report.ByStatusAndBranch);
        Assert.Equal("MU-B", Assert.Single(report.Damaged).Code);
        var overdue = Assert.Single(report.Overdue);
        Assert.Equal("BK-B", overdue.Code);
        Assert.Equal(1, overdue.DaysOverdue);
        var transfer = Assert.Single(report.InTransit);
        Assert.Equal(("BK-A", 1, 2, TransferStatus.Requested), (transfer.ItemCode, transfer.From.Id, transfer.To.Id, transfer.Status));
        var waiting = Assert.Single(report.Waitlists);
        Assert.Equal(("BK-B", 2, 0), (waiting.Code, waiting.Waiting, waiting.Ready));
        Assert.Equal(5, report.ByCategory.Count);
        Assert.Equal(2, report.ByCategory.Single(row => row.Category == "Science Fiction").Count);
        foreach (var category in new[] { "Fantasy", "Rock", "Puzzle", "Jazz" }) Assert.Equal(1, report.ByCategory.Single(row => row.Category == category).Count);
        Assert.Equal(6, report.ByCategory.Sum(row => row.Count));
        Assert.Equal("BK-C", Assert.Single(report.Idle).Code);
        Assert.Null(report.Idle[0].LastBorrowedOn);
        Assert.Equal(23, report.Items.Single(row => row.Code == "MU-A").DaysIdle);
    }

    [Fact]
    public void IdleTwentyIncludesMusicButNotTransfersOrBorrowedItems()
    {
        var report = Inventory(20);
        Assert.Equal(new[] { "BK-C", "MU-A" }, report.Idle.Select(row => row.Code));
        Assert.Equal(23, report.Idle.Single(row => row.Code == "MU-A").DaysIdle);
        Assert.DoesNotContain(report.Idle, row => row.Code is "BK-A" or "BK-B" or "TY-A");
    }

    [Theory]
    [InlineData(1, 2, 1)]
    [InlineData(2, 4, 0)]
    public void InventoryFiltersByCurrentBranchIncludingTransferSource(int branch, int items, int transfers)
    {
        var report = InventoryHealthCalculator.Calculate(FixedReportData.Items, FixedReportData.Queues, FixedReportData.Transfers, FixedReportData.Today, 30, branch).Report!;
        Assert.Equal(items, report.Items.Count);
        Assert.Equal(transfers, report.InTransit.Count);
        Assert.All(report.Items, row => Assert.Equal(branch, row.Branch.Id));
    }

    [Fact]
    public void EmptyPeriodsAndInventoryReturnSuccessfulEmptyReports()
    {
        var borrowing = BorrowingStatisticsCalculator.Calculate(FixedReportData.Loans, FixedReportData.EmptyRange, FixedReportData.Today);
        Assert.True(borrowing.IsValid);
        Assert.True(borrowing.Report!.IsEmpty);
        Assert.Equal((0, 0, 0, 0, 0, 0), (borrowing.Report.Loans, borrowing.Report.Active, borrowing.Report.Overdue,
            borrowing.Report.Returned, borrowing.Report.ReturnedLate, borrowing.Report.ReturnedOnTime));
        Assert.Empty(borrowing.Report.Lines);
        var fines = FineRevenueCalculator.Calculate(FixedReportData.Loans, FixedReportData.EmptyRange, FixedReportData.Today, 0.5m);
        Assert.True(fines.IsValid);
        Assert.True(fines.Report!.IsEmpty);
        Assert.Empty(fines.Report.Lines);
        Assert.Equal((0m, 0m, 0m), (fines.Report.Charged, fines.Report.Collected, fines.Report.Outstanding));
        Assert.True(fines.Report.Reconciles);
        var inventory = InventoryHealthCalculator.Calculate([], [], [], FixedReportData.Today, 30);
        Assert.True(inventory.IsValid);
        Assert.True(inventory.Report!.IsEmpty);
        Assert.Empty(inventory.Report.ByStatusAndBranch);
        Assert.Empty(inventory.Report.ByCategory);
        Assert.Empty(inventory.Report.Damaged);
        Assert.Empty(inventory.Report.Overdue);
        Assert.Empty(inventory.Report.InTransit);
        Assert.Empty(inventory.Report.Idle);
        Assert.Empty(inventory.Report.Waitlists);
    }

    [Fact]
    public void ReversedDatesReturnValidationResults()
    {
        var invalid = new ReportFilter(FixedReportData.Today, FixedReportData.Today.AddDays(-1));
        var borrowing = BorrowingStatisticsCalculator.Calculate(FixedReportData.Loans, invalid, FixedReportData.Today);
        var fines = FineRevenueCalculator.Calculate(FixedReportData.Loans, invalid, FixedReportData.Today, 0.5m);
        Assert.False(borrowing.IsValid);
        Assert.Null(borrowing.Report);
        Assert.Contains("from date", Assert.Single(borrowing.Errors));
        Assert.False(fines.IsValid);
        Assert.Null(fines.Report);
        Assert.Contains("from date", Assert.Single(fines.Errors));
    }

    private static InventoryHealthReport Inventory(int days)
        => InventoryHealthCalculator.Calculate(FixedReportData.Items, FixedReportData.Queues, FixedReportData.Transfers, FixedReportData.Today, days).Report!;
}
