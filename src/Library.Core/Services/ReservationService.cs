using Library.Core.Configuration;
using Library.Core.Contracts;
using Microsoft.Extensions.Options;

namespace Library.Core.Services;

public sealed class ReservationRecord
{
    public required int Id { get; init; }
    public required int ItemId { get; init; }
    public required int BorrowerId { get; init; }
    public required ReservationSource PlacedVia { get; init; }
    public required DateTime CreatedAt { get; init; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Waiting;
    public DateTime? NotifiedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>What a patron sees under "My holds".</summary>
public sealed record HoldInfo(int ReservationId, int ItemId, string ItemName, ReservationStatus Status, int Position, DateTime? ExpiresAt);

/// <summary>
/// Real IReservationService (Person B). Holds are per item (decision 4).
/// Rules (brief 2.4): hold only if Borrowed or Damaged; one active hold per patron per item;
/// not on an item you have on loan; limit of active holds; queue ordered by CreatedAt then Id;
/// first Waiting patron becomes Ready when the item is available; Ready expires after ReadyHoldDays;
/// a Ready item can only be borrowed by the patron it is held for.
/// </summary>
public sealed class ReservationService(
    DemoLibraryStore store,
    IClock clock,
    INotificationService notifications,
    IOptions<ReservationOptions> options) : IReservationService
{
    private readonly object _sync = new();
    private readonly List<ReservationRecord> _all = new();
    private int _nextId = 1;

    private static bool IsActive(ReservationRecord r)
        => r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.Ready;

    public Task<ReservationResult> PlaceAsync(int itemId, int borrowerId, ReservationSource placedVia, CancellationToken ct = default)
    {
        var item = store.FindItem(itemId);
        if (item is null)
            return Task.FromResult(ReservationResult.Fail("That item could not be found. It may have been removed."));

        if (store.FindPatron(borrowerId) is null)
            return Task.FromResult(ReservationResult.Fail("We could not find your account."));

        if (item.Status == ItemStatus.Destroy)
            return Task.FromResult(ReservationResult.Fail("This item has been destroyed, so it cannot be held."));

        var loan = store.ActiveLoanForItem(itemId);
        if (loan is not null && loan.BorrowerId == borrowerId)
            return Task.FromResult(ReservationResult.Fail("You already have this item on loan."));

        lock (_sync)
        {
            if (item.Status == ItemStatus.Available)
            {
                var heldForOther = _all.Any(r => r.ItemId == itemId && r.Status == ReservationStatus.Ready && r.BorrowerId != borrowerId);
                return Task.FromResult(ReservationResult.Fail(heldForOther
                    ? "This item is being held for another patron."
                    : "This item is available now, so you can borrow it instead of placing a hold."));
            }

            if (_all.Any(r => IsActive(r) && r.ItemId == itemId && r.BorrowerId == borrowerId))
                return Task.FromResult(ReservationResult.Fail("You already have a hold on this item."));

            var max = options.Value.MaxActiveHoldsPerPatron;
            if (_all.Count(r => IsActive(r) && r.BorrowerId == borrowerId) >= max)
                return Task.FromResult(ReservationResult.Fail($"You have reached the limit of {max} active holds."));

            var record = new ReservationRecord
            {
                Id = _nextId++,
                ItemId = itemId,
                BorrowerId = borrowerId,
                PlacedVia = placedVia,
                CreatedAt = clock.Now
            };
            _all.Add(record);
            return Task.FromResult(ReservationResult.Ok(record.Id));
        }
    }

    public async Task<ReservationResult> CancelAsync(int reservationId, int requestedByBorrowerId, CancellationToken ct = default)
    {
        var pending = new List<NotificationRequest>();
        ReservationResult result;

        lock (_sync)
        {
            var r = _all.FirstOrDefault(x => x.Id == reservationId);
            if (r is null)
            {
                result = ReservationResult.Fail("That hold could not be found.");
            }
            else if (r.BorrowerId != requestedByBorrowerId)
            {
                result = ReservationResult.Fail("You can only cancel your own holds.");
            }
            else if (!IsActive(r))
            {
                result = ReservationResult.Fail("This hold has already finished.");
            }
            else
            {
                var wasReady = r.Status == ReservationStatus.Ready;
                r.Status = ReservationStatus.Cancelled;
                if (wasReady) PromoteNextLocked(r.ItemId, pending);
                result = ReservationResult.Ok(r.Id);
            }
        }

        await SendAsync(pending, ct);
        return result;
    }

    public Task<IReadOnlyList<QueueEntry>> GetQueueAsync(int itemId, CancellationToken ct = default)
        => Task.FromResult(QueueFor(itemId));

    public Task<int> GetQueueLengthAsync(int itemId, CancellationToken ct = default)
    {
        lock (_sync) return Task.FromResult(_all.Count(r => r.ItemId == itemId && IsActive(r)));
    }

    public Task<BorrowCheck> CheckBorrowAllowedAsync(int itemId, int borrowerId, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var ready = _all.FirstOrDefault(r => r.ItemId == itemId && r.Status == ReservationStatus.Ready);
            if (ready is null || ready.BorrowerId == borrowerId)
                return Task.FromResult(BorrowCheck.Yes());
            return Task.FromResult(BorrowCheck.No("This item is being held for another patron."));
        }
    }

    public Task OnBorrowedAsync(int itemId, int borrowerId, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var hold = _all.FirstOrDefault(r => r.ItemId == itemId && r.BorrowerId == borrowerId && IsActive(r));
            if (hold is not null) hold.Status = ReservationStatus.Fulfilled;
        }
        return Task.CompletedTask;
    }

    public async Task OnItemBecameAvailableAsync(int itemId, CancellationToken ct = default)
    {
        var pending = new List<NotificationRequest>();
        lock (_sync) PromoteNextLocked(itemId, pending);
        await SendAsync(pending, ct);
    }

    /// <summary>Expire Ready holds whose date has passed, notify the patron, and promote the next person. Returns how many expired.</summary>
    public async Task<int> ExpireOverdueHoldsAsync(DateOnly asOf, CancellationToken ct = default)
    {
        var pending = new List<NotificationRequest>();
        var count = 0;

        lock (_sync)
        {
            var due = _all
                .Where(r => r.Status == ReservationStatus.Ready && r.ExpiresAt is { } e && DateOnly.FromDateTime(e) <= asOf)
                .ToList();

            foreach (var r in due)
            {
                r.Status = ReservationStatus.Expired;
                count++;
                pending.Add(new NotificationRequest(NotificationType.ReservationExpired, r.BorrowerId, r.ItemId, null, r.Id));
                PromoteNextLocked(r.ItemId, pending);
            }
        }

        await SendAsync(pending, ct);
        return count;
    }

    /// <summary>The active holds of one patron with their queue positions (kiosk account summary, My holds).</summary>
    public IReadOnlyList<HoldInfo> GetHoldsForBorrower(int borrowerId)
    {
        List<ReservationRecord> mine;
        lock (_sync) mine = _all.Where(r => r.BorrowerId == borrowerId && IsActive(r)).ToList();

        return mine.Select(r =>
        {
            var queue = QueueFor(r.ItemId);
            var position = queue.FirstOrDefault(q => q.ReservationId == r.Id)?.Position ?? 0;
            return new HoldInfo(r.Id, r.ItemId, store.FindItem(r.ItemId)?.Name ?? "Unknown item", r.Status, position, r.ExpiresAt);
        }).ToList();
    }

    private IReadOnlyList<QueueEntry> QueueFor(int itemId)
    {
        lock (_sync)
        {
            return _all
                .Where(r => r.ItemId == itemId && IsActive(r))
                .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                .Select((r, index) => new QueueEntry(
                    r.Id, r.BorrowerId, store.FindPatron(r.BorrowerId)?.Name ?? "Unknown",
                    index + 1, r.Status, r.CreatedAt, r.ExpiresAt))
                .ToList();
        }
    }

    // Must be called inside lock (_sync).
    private void PromoteNextLocked(int itemId, List<NotificationRequest> pending)
    {
        if (_all.Any(r => r.ItemId == itemId && r.Status == ReservationStatus.Ready)) return;

        var next = _all
            .Where(r => r.ItemId == itemId && r.Status == ReservationStatus.Waiting)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .FirstOrDefault();
        if (next is null) return;

        var now = clock.Now;
        next.Status = ReservationStatus.Ready;
        next.NotifiedAt = now;
        next.ExpiresAt = now.AddDays(options.Value.ReadyHoldDays);
        pending.Add(new NotificationRequest(NotificationType.ReservationReady, next.BorrowerId, itemId, null, next.Id));
    }

    private async Task SendAsync(List<NotificationRequest> pending, CancellationToken ct)
    {
        foreach (var request in pending)
        {
            await notifications.RecordAsync(request, ct);
        }
    }
}
