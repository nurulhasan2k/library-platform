using Library.Core.Configuration;
using Library.Core.Contracts;
using Microsoft.Extensions.Options;

namespace Library.Core.Services;

// TEMPORARY demo versions of Person A's lending and sign-in services (Person B).
// They exist only so the kiosk and the hooks can be demonstrated before A's real services merge.
// They call the real hooks (CheckBorrowAllowed, OnBorrowed, OnItemBecameAvailable, Record).
// DELETE these stand-ins and the AddLibraryInMemoryLendingStubs() call in Program.cs when A's ILendingService and IPatronAuthService merge.

public sealed class StubInMemoryLendingService(
    DemoLibraryStore store,
    IClock clock,
    IReservationService reservations,
    INotificationService notifications,
    IOptions<LendingOptions> options) : ILendingService
{
    public async Task<BorrowResult> BorrowAsync(string libraryCode, int borrowerId, int branchId, CancellationToken ct = default)
    {
        var item = store.FindItemByCode(libraryCode);
        if (item is null)
            return BorrowResult.Fail("We could not find an item with that code. Please check the code and try again.");
        if (item.BranchId != branchId)
            return BorrowResult.Fail("This item belongs to a different branch.");
        if (item.Status == ItemStatus.Borrowed)
            return BorrowResult.Fail("This item is already borrowed.");
        if (item.Status == ItemStatus.Damaged)
            return BorrowResult.Fail("This item is damaged and cannot be borrowed.");
        if (item.Status == ItemStatus.Destroy)
            return BorrowResult.Fail("This item has been destroyed and cannot be borrowed.");

        var check = await reservations.CheckBorrowAllowedAsync(item.Id, borrowerId, ct);
        if (!check.Allowed)
            return BorrowResult.Fail(check.Reason ?? "This item cannot be borrowed right now.");

        var today = clock.Today;
        var loan = store.AddLoan(item.Id, borrowerId, today, today.AddDays(options.Value.LoanDays));
        item.Status = ItemStatus.Borrowed;

        await reservations.OnBorrowedAsync(item.Id, borrowerId, ct);
        await notifications.RecordAsync(new NotificationRequest(NotificationType.Borrowed, borrowerId, item.Id, loan.LoanId), ct);

        return BorrowResult.Ok(new LoanReceipt(loan.LoanId, item.Code, item.Name, item.BranchId, loan.BorrowedOn, loan.DueOn));
    }

    public async Task<ReturnResult> ReturnAsync(string libraryCode, int branchId, bool damaged, CancellationToken ct = default)
    {
        var item = store.FindItemByCode(libraryCode);
        if (item is null)
            return ReturnResult.Fail("We could not find an item with that code.");

        var loan = store.ActiveLoanForItem(item.Id);
        if (loan is null)
            return ReturnResult.Fail("This item is not on loan.");

        store.MarkReturned(loan);
        var daysLate = Math.Max(0, clock.Today.DayNumber - loan.DueOn.DayNumber);
        var fine = daysLate * options.Value.FinePerDay;
        item.Status = damaged ? ItemStatus.Damaged : ItemStatus.Available;

        if (!damaged)
            await reservations.OnItemBecameAvailableAsync(item.Id, ct);

        return new ReturnResult(true, null, daysLate, fine);
    }

    /// <summary>Admin action: a damaged item is repaired and becomes available again (hook point for holds).</summary>
    public async Task<string?> MarkAvailableAsync(int itemId, CancellationToken ct = default)
    {
        var item = store.FindItem(itemId);
        if (item is null) return "That item could not be found.";
        if (item.Status != ItemStatus.Damaged) return "Only a damaged item can be changed to available.";

        item.Status = ItemStatus.Available;
        await reservations.OnItemBecameAvailableAsync(item.Id, ct);
        return null;
    }
}

public sealed class StubInMemoryPatronAuthService(DemoLibraryStore store) : IPatronAuthService
{
    public Task<PatronAuthResult> SignInAsync(string cardNumber, string pin, CancellationToken ct = default)
    {
        var patron = store.FindPatronByCard(cardNumber);
        if (patron is null || patron.Pin != (pin ?? "").Trim())
            return Task.FromResult(PatronAuthResult.Fail("The card number or PIN is not correct. Please try again."));

        return Task.FromResult(PatronAuthResult.Ok(
            new PatronIdentity(patron.Id, patron.CardNumber, patron.Name, patron.Channel)));
    }
}
