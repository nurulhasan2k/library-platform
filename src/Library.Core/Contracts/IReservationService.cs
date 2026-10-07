namespace Library.Core.Contracts;

public enum ReservationStatus
{
    Waiting,
    Ready,
    Fulfilled,
    Cancelled,
    Expired
}

public enum ReservationSource
{
    Kiosk,
    Web,
    Desk
}

public sealed record ReservationResult(bool Success, int? ReservationId, string? Error)
{
    public static ReservationResult Ok(int id) => new(true, id, null);
    public static ReservationResult Fail(string error) => new(false, null, error);
}

/// <summary>Answer to "may this patron borrow this item right now?" Used by lending before every borrow.</summary>
public sealed record BorrowCheck(bool Allowed, string? Reason)
{
    public static BorrowCheck Yes() => new(true, null);
    public static BorrowCheck No(string reason) => new(false, reason);
}

public sealed record QueueEntry(
    int ReservationId,
    int BorrowerId,
    string BorrowerName,
    int Position,
    ReservationStatus Status,
    DateTime CreatedAt,
    DateTime? ExpiresAt);

/// <summary>
/// Holds and waitlists, per item (decision 4). Built by Person B. The stub allows everything.
/// The hook rule: lending calls CheckBorrowAllowed before lending, OnBorrowed after a borrow,
/// and OnItemBecameAvailable after a return or after an admin changes a damaged item back to available.
/// Nobody else edits a queue directly.
/// </summary>
public interface IReservationService
{
    Task<ReservationResult> PlaceAsync(int itemId, int borrowerId, ReservationSource placedVia, CancellationToken ct = default);
    Task<ReservationResult> CancelAsync(int reservationId, int requestedByBorrowerId, CancellationToken ct = default);
    Task<IReadOnlyList<QueueEntry>> GetQueueAsync(int itemId, CancellationToken ct = default);
    Task<int> GetQueueLengthAsync(int itemId, CancellationToken ct = default);

    /// <summary>An item held (Ready) for one patron cannot be borrowed by another.</summary>
    Task<BorrowCheck> CheckBorrowAllowedAsync(int itemId, int borrowerId, CancellationToken ct = default);

    /// <summary>The patron borrowed the item: fulfil their hold if they had one.</summary>
    Task OnBorrowedAsync(int itemId, int borrowerId, CancellationToken ct = default);

    /// <summary>The item is available again: promote the first Waiting patron to Ready and notify them.</summary>
    Task OnItemBecameAvailableAsync(int itemId, CancellationToken ct = default);
}
