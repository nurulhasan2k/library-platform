namespace Library.Core.Reports;

public static class BorrowingStatisticsCalculator
{
    public static ReportResult<BorrowingStatisticsReport> Calculate(IEnumerable<LoanRow> loans, ReportFilter filter, DateOnly today)
    {
        var errors = ReportValidation.Errors(filter);
        if (errors.Length > 0) return ReportResult<BorrowingStatisticsReport>.Invalid(errors);
        var rows = loans.Where(row => row.BorrowedOn >= filter.From && row.BorrowedOn <= filter.To &&
            (filter.BranchId is null || row.BorrowedAtBranch.Id == filter.BranchId)).ToArray();
        var active = rows.Count(row => row.ReturnedOn is null);
        var overdue = rows.Count(row => row.ReturnedOn is null && row.DueOn < today);
        var late = rows.Count(row => row.ReturnedOn > row.DueOn);
        var byMonth = rows.GroupBy(row => (row.BorrowedOn.Year, row.BorrowedOn.Month))
            .ToDictionary(group => group.Key, group => group.Count());
        return ReportResult<BorrowingStatisticsReport>.Valid(new(filter, today, rows.Length, active, overdue,
            rows.Length - active, late, rows.Length - active - late,
            ReportValidation.Months(filter).Select(month => new MonthCount(month, byMonth.GetValueOrDefault((month.Year, month.Month)))).ToArray(),
            rows.GroupBy(row => row.BorrowedAtBranch.Id).OrderBy(group => group.First().BorrowedAtBranch.Code, StringComparer.Ordinal)
                .Select(group => new BranchCount(group.First().BorrowedAtBranch, group.Count())).ToArray(),
            rows.GroupBy(row => row.ItemType).OrderBy(group => group.Key).Select(group => new TypeCount(group.Key, group.Count())).ToArray(),
            rows.GroupBy(row => row.Category).OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new CategoryCount(group.Key, group.Count())).ToArray(),
            rows.GroupBy(row => row.ItemCode).OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal).Take(10)
                .Select(group => new TopBorrowedItem(group.Key, group.First().ItemName,
                    group.Select(row => row.BorrowedAtBranch).DistinctBy(branch => branch.Id).OrderBy(branch => branch.Code, StringComparer.Ordinal).ToArray(), group.Count())).ToArray(),
            rows.OrderBy(row => row.BorrowedOn).ThenBy(row => row.Id).Select(row => new BorrowingLoanLine(
                row.Id, row.ItemCode, row.ItemName, row.ItemType, row.Category, row.BorrowedAtBranch,
                row.BorrowedOn, row.DueOn, row.ReturnedOn, row.ReturnedOn is { } returned
                    ? returned > row.DueOn ? BorrowingState.ReturnedLate : BorrowingState.Returned
                    : row.DueOn < today ? BorrowingState.Overdue : BorrowingState.Active)).ToArray()));
    }
}
