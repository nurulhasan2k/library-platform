namespace Library.Core.Reports;

public static class FineRevenueCalculator
{
    public static ReportResult<FineRevenueReport> Calculate(IEnumerable<LoanRow> loans, ReportFilter filter, DateOnly today, decimal finePerDay)
    {
        var errors = ReportValidation.Errors(filter);
        if (errors.Length > 0) return ReportResult<FineRevenueReport>.Invalid(errors);
        if (finePerDay < 0) return ReportResult<FineRevenueReport>.Invalid("The fine per day cannot be negative.");
        var source = loans.ToArray();
        var lines = source.Where(row => row.ReturnedOn >= filter.From && row.ReturnedOn <= filter.To && row.FineAmount > 0 &&
            (filter.BranchId is null || row.ReturnedAtBranch?.Id == filter.BranchId))
            .OrderByDescending(row => row.ReturnedOn).ThenBy(row => row.Id)
            .Select(row => new FineRevenueLine(row.Id, row.ItemCode, row.ItemName, row.BorrowerName, row.CardNumber,
                row.ReturnedAtBranch, row.DueOn, row.ReturnedOn!.Value,
                Math.Max(0, row.ReturnedOn.Value.DayNumber - row.DueOn.DayNumber), row.FineAmount, row.FinePaid, row.FinePaidOn)).ToArray();
        var charged = lines.Sum(line => line.Amount);
        var collected = lines.Where(line => line.Paid).Sum(line => line.Amount);
        // Accruing fines describe current overdue loans, independently of the charge-date range.
        var accruing = source.Where(row => row.ReturnedOn is null && row.DueOn < today &&
            (filter.BranchId is null || row.BorrowedAtBranch.Id == filter.BranchId))
            .Sum(row => (today.DayNumber - row.DueOn.DayNumber) * finePerDay);
        var months = lines.GroupBy(line => (line.ReturnedOn.Year, line.ReturnedOn.Month)).ToDictionary(group => group.Key,
            group => (Charged: group.Sum(line => line.Amount), Collected: group.Where(line => line.Paid).Sum(line => line.Amount)));
        return ReportResult<FineRevenueReport>.Valid(new(filter, today, charged, collected, charged - collected, accruing,
            ReportValidation.Months(filter).Select(month =>
            {
                var totals = months.GetValueOrDefault((month.Year, month.Month));
                return new FineMonth(month, totals.Charged, totals.Collected, totals.Charged - totals.Collected);
            }).ToArray(), lines));
    }
}
