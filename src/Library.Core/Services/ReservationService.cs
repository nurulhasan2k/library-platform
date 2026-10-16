using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Library.Core.Services;

/// <summary>A hold shown on the staff screens and the patron's account.</summary>
public sealed record HoldDetails(
    int ReservationId, int ItemId, string LibraryCode, string ItemName, int BranchId, string BranchName,
    int BorrowerId, string BorrowerName, ReservationStatus Status, ReservationSource Source, int Position, int QueueLength,
    DateTime CreatedAt, DateTime? ExpiresAt);

/// <summary>Holds and waitlists, per item (feature 3.5, decision 4).</summary>
public sealed class ReservationService(
    ILibraryStore store,
    IClock clock,
    IOptions<ReservationOptions> options,
    INotificationService notifications,
    ILogger<ReservationService> log) : IReservationService
{
    public async Task<ReservationResult> PlaceAsync(int itemId, int borrowerId, ReservationSource placedVia, CancellationToken ct = default)
    {
        try
        {
            var id = store.Write(data =>
            {
                var item = data.FindItem(itemId);
                if (item is null || item.Status == ItemStatus.Destroy) throw new LibraryException("That item isn't available for holds.");
                var borrower = data.FindBorrower(borrowerId) ?? throw new LibraryException("Unknown patron.");

                if (data.Reservations.Any(r => r.ItemId == itemId && r.BorrowerId == borrowerId && r.IsOpen))
                    throw new LibraryException("You already have a hold on this item.");
                if (data.Loans.Any(l => l.ItemId == itemId && l.BorrowerId == borrowerId && l.IsActive))
                    throw new LibraryException("You already have this item on loan.");
                if (data.Reservations.Count(r => r.BorrowerId == borrowerId && r.IsOpen) >= options.Value.MaxActiveHoldsPerPatron)
                    throw new LibraryException($"You can have at most {options.Value.MaxActiveHoldsPerPatron} holds at a time.");

                var queueExists = data.Reservations.Any(r => r.ItemId == itemId && r.IsOpen);
                if (item.Status == ItemStatus.Available && !data.IsInTransit(itemId) && !queueExists)
                {
                    var where = data.FindBranch(item.BranchId)?.Name ?? "its branch";
                    throw new LibraryException($"'{item.Name}' is available now at {where}. You can borrow it instead of placing a hold.");
                }

                var r = new Reservation
                {
                    Id = data.TakeId(), ItemId = itemId, BorrowerId = borrowerId, Status = ReservationStatus.Waiting,
                    Source = placedVia, CreatedAt = clock.Now
                };
                data.Reservations.Add(r);
                return r.Id;
            });

            // If the item is sitting available (for example in transit, now arrived) make sure the queue moves.
            await OnItemBecameAvailableAsync(itemId, ct);
            return ReservationResult.Ok(id);
        }
        catch (LibraryException ex)
        {
            return ReservationResult.Fail(ex.Message);
        }
    }

    public async Task<ReservationResult> CancelAsync(int reservationId, int requestedByBorrowerId, CancellationToken ct = default)
    {
        try
        {
            var (itemId, wasReady) = store.Write(data =>
            {
                var r = data.Reservations.FirstOrDefault(x => x.Id == reservationId) ?? throw new LibraryException("No such hold.");
                if (requestedByBorrowerId > 0 && r.BorrowerId != requestedByBorrowerId)
                    throw new LibraryException("That hold belongs to someone else.");
                if (!r.IsOpen) throw new LibraryException("That hold has already ended.");
                var ready = r.Status == ReservationStatus.Ready;
                r.Status = ReservationStatus.Cancelled;
                r.ClosedAt = clock.Now;
                return (r.ItemId, ready);
            });
            if (wasReady) await OnItemBecameAvailableAsync(itemId, ct);
            return ReservationResult.Ok(reservationId);
        }
        catch (LibraryException ex)
        {
            return ReservationResult.Fail(ex.Message);
        }
    }

    public Task<IReadOnlyList<QueueEntry>> GetQueueAsync(int itemId, CancellationToken ct = default) =>
        Task.FromResult(store.Read(d =>
        {
            var open = OrderedQueue(d, itemId);
            return (IReadOnlyList<QueueEntry>)open.Select((r, i) =>
                new QueueEntry(r.Id, r.BorrowerId, d.FindBorrower(r.BorrowerId)?.Name ?? "?", i + 1, r.Status, r.CreatedAt, r.ExpiresAt)).ToList();
        }));

    public Task<int> GetQueueLengthAsync(int itemId, CancellationToken ct = default) =>
        Task.FromResult(store.Read(d => OrderedQueue(d, itemId).Count));

    public Task<BorrowCheck> CheckBorrowAllowedAsync(int itemId, int borrowerId, CancellationToken ct = default)
    {
        var today = clock.Today;
        var check = store.Read(d =>
        {
            var held = d.Reservations.FirstOrDefault(r => r.ItemId == itemId && r.Status == ReservationStatus.Ready &&
                                                          r.ExpiresAt is { } e && DateOnly.FromDateTime(e) >= today);
            if (held is null || held.BorrowerId == borrowerId) return BorrowCheck.Yes();
            var until = DateOnly.FromDateTime(held.ExpiresAt!.Value).ToString("d MMM yyyy");
            return BorrowCheck.No($"'{d.FindItem(itemId)?.Name}' is being held for another patron until {until}.");
        });
        return Task.FromResult(check);
    }

    public Task OnBorrowedAsync(int itemId, int borrowerId, CancellationToken ct = default)
    {
        store.Write(data =>
        {
            foreach (var r in data.Reservations.Where(r => r.ItemId == itemId && r.BorrowerId == borrowerId && r.IsOpen))
            {
                r.Status = ReservationStatus.Fulfilled;
                r.ClosedAt = clock.Now;
            }
        });
        return Task.CompletedTask;
    }

    public async Task OnItemBecameAvailableAsync(int itemId, CancellationToken ct = default)
    {
        var promoted = store.Write(data =>
        {
            var item = data.FindItem(itemId);
            if (item is null || item.Status != ItemStatus.Available || data.IsInTransit(itemId)) return (int?)null;
            if (data.Reservations.Any(r => r.ItemId == itemId && r.Status == ReservationStatus.Ready)) return null;
            var next = data.Reservations.Where(r => r.ItemId == itemId && r.Status == ReservationStatus.Waiting)
                .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).FirstOrDefault();
            if (next is null) return null;
            next.Status = ReservationStatus.Ready;
            next.ReadyAt = clock.Now;
            next.ExpiresAt = clock.Now.AddDays(options.Value.ReadyHoldDays);
            return next.Id;
        });

        if (promoted is { } id)
        {
            var (borrowerId, item) = store.Read(d =>
            {
                var r = d.Reservations.First(x => x.Id == id);
                return (r.BorrowerId, r.ItemId);
            });
            try { await notifications.RecordAsync(new NotificationRequest(NotificationType.ReservationReady, borrowerId, item, null, id), ct); }
            catch (Exception ex) { log.LogWarning(ex, "Could not record the hold-ready notification"); }
        }
    }

    // ------------------------------------------------------- staff / account views

    public IReadOnlyList<HoldDetails> ListHolds(bool openOnly = true, int? branchId = null, int? borrowerId = null, int? itemId = null) =>
        store.Read(d =>
        {
            var q = d.Reservations.AsEnumerable();
            if (openOnly) q = q.Where(r => r.IsOpen);
            if (borrowerId is { } b) q = q.Where(r => r.BorrowerId == b);
            if (itemId is { } i) q = q.Where(r => r.ItemId == i);
            var rows = q.Select(r => Describe(d, r)).Where(h => branchId is null || h.BranchId == branchId);
            return (IReadOnlyList<HoldDetails>)rows.OrderBy(h => h.ItemName).ThenBy(h => h.Position).ThenByDescending(h => h.CreatedAt).ToList();
        });

    private static HoldDetails Describe(LibraryData d, Reservation r)
    {
        var item = d.FindItem(r.ItemId);
        var queue = OrderedQueue(d, r.ItemId);
        var pos = r.IsOpen ? queue.FindIndex(x => x.Id == r.Id) + 1 : 0;
        var branch = item is null ? null : d.FindBranch(item.BranchId);
        return new HoldDetails(r.Id, r.ItemId, item?.LibraryCode ?? "?", item?.Name ?? "(deleted)", item?.BranchId ?? 0, branch?.Name ?? "",
            r.BorrowerId, d.FindBorrower(r.BorrowerId)?.Name ?? "?", r.Status, r.Source, pos, queue.Count, r.CreatedAt, r.ExpiresAt);
    }

    private static List<Reservation> OrderedQueue(LibraryData d, int itemId) =>
        d.Reservations.Where(r => r.ItemId == itemId && r.IsOpen)
            .OrderBy(r => r.Status == ReservationStatus.Ready ? 0 : 1).ThenBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList();
}
