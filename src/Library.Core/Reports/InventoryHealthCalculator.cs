using Library.Core.Contracts;

namespace Library.Core.Reports;

public static class InventoryHealthCalculator
{
    public static ReportResult<InventoryHealthReport> Calculate(IEnumerable<ItemRow> items, IEnumerable<ItemQueueCounts> queueCounts,
        IEnumerable<ActiveTransferRow> transfers, DateOnly today, int idleDays, int? branchId = null)
    {
        if (idleDays < 0) return ReportResult<InventoryHealthReport>.Invalid("Idle days cannot be negative.");
        var rows = items.Where(item => item.Status != ItemStatus.Destroy && (branchId is null || item.Branch.Id == branchId))
            .OrderBy(item => item.Code, StringComparer.Ordinal).ToArray();
        var codes = rows.Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var activeTransfers = transfers.Where(transfer => codes.Contains(transfer.ItemCode) &&
            transfer.Status is TransferStatus.Requested or TransferStatus.InTransit)
            .OrderBy(transfer => transfer.ItemCode, StringComparer.Ordinal).ToArray();
        var inTransit = activeTransfers.Select(transfer => transfer.ItemCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var queues = queueCounts.GroupBy(queue => queue.ItemCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (Waiting: group.Sum(queue => queue.Waiting), Ready: group.Sum(queue => queue.Ready)), StringComparer.OrdinalIgnoreCase);
        var lines = rows.Select(item =>
        {
            var transit = item.InTransit || inTransit.Contains(item.Code);
            DateOnly? idleSince = item.LastBorrowedOn;
            if (item.CreatedOn is { } created && (idleSince is null || created > idleSince)) idleSince = created;
            int? daysIdle = idleSince is { } since ? Math.Max(0, today.DayNumber - since.DayNumber) : null;
            var idle = item.Status == ItemStatus.Available && !transit && (daysIdle is null || daysIdle > idleDays);
            var overdue = item.HasActiveLoan && item.ActiveLoanDueOn < today;
            var queue = queues.GetValueOrDefault(item.Code);
            return new InventoryItemLine(item.Code, item.Name, item.Type, item.Category, item.Branch, item.Status,
                item.LastBorrowedOn, transit, overdue, overdue ? today.DayNumber - item.ActiveLoanDueOn!.Value.DayNumber : 0,
                idle, daysIdle, item.LastBorrowedOn is { } last ? today.DayNumber - last.DayNumber : null, queue.Waiting, queue.Ready);
        }).ToArray();
        return ReportResult<InventoryHealthReport>.Valid(new(today, branchId, idleDays,
            rows.GroupBy(item => (item.Branch.Id, item.Status)).OrderBy(group => group.First().Branch.Code, StringComparer.Ordinal).ThenBy(group => group.Key.Status)
                .Select(group => new InventoryStatusCount(group.First().Branch, group.Key.Status, group.Count())).ToArray(),
            rows.GroupBy(item => item.Category).OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => new CategoryCount(group.Key, group.Count())).ToArray(),
            lines, lines.Where(line => line.Status == ItemStatus.Damaged).ToArray(), lines.Where(line => line.Overdue).ToArray(),
            activeTransfers, lines.Where(line => line.Idle).ToArray(), lines.Where(line => line.Waiting > 0).ToArray()));
    }
}
