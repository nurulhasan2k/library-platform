using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Library.Core.Services;

/// <summary>
/// The ONLY place the lending rules live (14-day loan, $0.50 per full late day, item must be at this branch, not in
/// transit, not held for someone else). Desk screens and the kiosk both call it. Refactored from Assignment 2's LibraryService.
/// </summary>
public sealed class LendingService(
    ILibraryStore store,
    IClock clock,
    IOptions<LendingOptions> options,
    IBranchContext branchContext,
    IReservationService reservations,
    INotificationService notifications,
    ILogger<LendingService> log) : ILendingService
{
    private LendingOptions Rules => options.Value;

    public decimal CalculateFine(DateOnly dueOn, DateOnly returnedOn) =>
        Math.Max(0, returnedOn.DayNumber - dueOn.DayNumber) * Rules.FinePerDay;

    public async Task<BorrowResult> BorrowAsync(string libraryCode, int borrowerId, int branchId, CancellationToken ct = default)
    {
        var today = clock.Today;

        // Ask the reservation service first (hook rule): may this patron borrow this item right now?
        var peek = store.Read(d => d.FindItemByCode(libraryCode));
        if (peek is not null)
        {
            var check = await reservations.CheckBorrowAllowedAsync(peek.Id, borrowerId, ct);
            if (!check.Allowed) return BorrowResult.Fail(check.Reason ?? "This item is on hold for someone else.");
        }

        BorrowResult result;
        try
        {
            result = store.Write(data =>
            {
                var item = data.FindItemByCode(libraryCode)
                           ?? throw new LibraryException($"No item has the library code '{(libraryCode ?? "").Trim()}'.");
                var borrower = data.FindBorrower(borrowerId) ?? throw new LibraryException($"No borrower has the id {borrowerId}.");
                var branch = data.FindBranch(branchId) ?? throw new LibraryException("Choose a branch before lending.");
                var itemBranch = data.FindBranch(item.BranchId);

                if (item.Status == ItemStatus.Destroy) throw new LibraryException($"'{item.Name}' has been destroyed and can't be borrowed.");
                if (data.IsInTransit(item.Id)) throw new LibraryException($"'{item.Name}' is in transit between branches and can't be borrowed yet.");
                if (item.Status != ItemStatus.Available)
                    throw new LibraryException($"'{item.Name}' can't be borrowed right now - it is {item.Status.ToLabel()}.");
                if (item.BranchId != branchId)
                    throw new LibraryException($"'{item.Name}' is at {itemBranch?.Name ?? "another branch"}, not at {branch.Name}. Request a transfer or borrow it there.");

                var loan = new Loan
                {
                    Id = data.TakeId(),
                    ItemId = item.Id,
                    BorrowerId = borrower.Id,
                    BranchId = branchId,
                    DeskId = branchContext.IsKiosk ? null : branchContext.CurrentDeskId,
                    Channel = branchContext.IsKiosk ? LoanChannel.Kiosk : LoanChannel.Desk,
                    BorrowedOn = today,
                    DueOn = today.AddDays(Rules.LoanDays)
                };
                data.Loans.Add(loan);
                item.Status = ItemStatus.Borrowed;
                return BorrowResult.Ok(new LoanReceipt(loan.Id, item.LibraryCode, item.Name, branchId, loan.BorrowedOn, loan.DueOn));
            });
        }
        catch (LibraryException ex)
        {
            return BorrowResult.Fail(ex.Message);
        }

        // Hooks: a failure here must never undo or fail the borrow.
        var receipt = result.Receipt!;
        var itemId = store.Read(d => d.FindItemByCode(libraryCode)!.Id);
        await Safely(() => reservations.OnBorrowedAsync(itemId, borrowerId, ct), "reservation hook (borrowed)");
        await Safely(() => notifications.RecordAsync(new NotificationRequest(NotificationType.Borrowed, borrowerId, itemId, receipt.LoanId), ct), "borrowed notification");
        return result;
    }

    public async Task<ReturnResult> ReturnAsync(string libraryCode, int branchId, bool damaged, CancellationToken ct = default)
    {
        var today = clock.Today;
        ReturnResult result;
        int itemId = 0;
        try
        {
            result = store.Write(data =>
            {
                var item = data.FindItemByCode(libraryCode)
                           ?? throw new LibraryException($"No item has the library code '{(libraryCode ?? "").Trim()}'.");
                var branch = data.FindBranch(branchId) ?? throw new LibraryException("Choose a branch before taking items back.");
                if (!branch.IsActive) throw new LibraryException($"{branch.Name} is closed down and can't take returns.");
                var loan = data.Loans.FirstOrDefault(l => l.ItemId == item.Id && l.IsActive)
                           ?? throw new LibraryException($"'{item.Name}' isn't out on loan, so it can't be returned.");
                if (today < loan.BorrowedOn)
                    throw new LibraryException($"The return date ({today:dd MMM yyyy}) is before the borrow date ({loan.BorrowedOn:dd MMM yyyy}).");

                var daysLate = loan.DaysLate(today);
                loan.ReturnedOn = today;
                loan.ReturnBranchId = branchId;
                loan.ReturnedDamaged = damaged;
                loan.FineAmount = CalculateFine(loan.DueOn, today);
                loan.FinePaid = false;
                loan.FinePaidOn = null;
                item.Status = damaged ? ItemStatus.Damaged : ItemStatus.Available;

                var moved = item.BranchId != branchId;
                var fromName = data.FindBranch(item.BranchId)?.Name;
                item.BranchId = branchId;
                itemId = item.Id;
                return new ReturnResult(true, null, daysLate, loan.FineAmount, moved, moved ? fromName : null, moved ? branch.Name : null);
            });
        }
        catch (LibraryException ex)
        {
            return ReturnResult.Fail(ex.Message);
        }

        if (!damaged) await Safely(() => reservations.OnItemBecameAvailableAsync(itemId, ct), "reservation hook (returned)");
        return result;
    }

    private async Task Safely(Func<Task> hook, string name)
    {
        try { await hook(); }
        catch (Exception ex) { log.LogWarning(ex, "The {Hook} failed; the lending action itself succeeded", name); }
    }
}
