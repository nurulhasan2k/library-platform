namespace Library.Core.Contracts;

public enum TransferStatus
{
    Requested,
    InTransit,
    Received,
    Cancelled
}

public sealed record TransferInfo(
    int Id,
    int ItemId,
    string LibraryCode,
    string ItemName,
    int FromBranchId,
    int ToBranchId,
    TransferStatus Status,
    DateTime RequestedAt,
    DateTime? DispatchedAt,
    DateTime? ReceivedAt,
    string? Note);

public sealed record TransferResult(bool Success, TransferInfo? Transfer, string? Error)
{
    public static TransferResult Ok(TransferInfo t) => new(true, t, null);
    public static TransferResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// Inter-branch transfers: Requested -> InTransit -> Received, or Cancelled before dispatch.
/// An item with an active transfer (Requested or InTransit) cannot be borrowed or changed.
/// Built by Person A. Person C's inventory-health report reads transfers through the list methods.
/// </summary>
public interface ITransferService
{
    Task<TransferResult> RequestAsync(int itemId, int toBranchId, int requestedByUserId, string? note, CancellationToken ct = default);
    Task<TransferResult> DispatchAsync(int transferId, int userId, CancellationToken ct = default);
    Task<TransferResult> ReceiveAsync(int transferId, int userId, CancellationToken ct = default);
    Task<TransferResult> CancelAsync(int transferId, int userId, CancellationToken ct = default);

    /// <summary>Active (Requested or InTransit) transfers coming to this branch.</summary>
    Task<IReadOnlyList<TransferInfo>> GetInboundAsync(int branchId, CancellationToken ct = default);

    /// <summary>Active (Requested or InTransit) transfers leaving this branch.</summary>
    Task<IReadOnlyList<TransferInfo>> GetOutboundAsync(int branchId, CancellationToken ct = default);

    /// <summary>True when the item has a Requested or InTransit transfer.</summary>
    Task<bool> IsInTransitAsync(int itemId, CancellationToken ct = default);
}
