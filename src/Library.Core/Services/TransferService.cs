using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Models;

namespace Library.Core.Services;

/// <summary>Inter-branch transfers: Requested -> InTransit -> Received, or Cancelled before dispatch (feature 3.4).</summary>
public sealed class TransferService(ILibraryStore store, IClock clock) : ITransferService
{
    public Task<TransferResult> RequestAsync(int itemId, int toBranchId, int requestedByUserId, string? note, CancellationToken ct = default) =>
        Run(data =>
        {
            var item = data.FindItem(itemId) ?? throw new LibraryException("No such item.");
            var to = data.FindBranch(toBranchId) ?? throw new LibraryException("Choose the branch the item should go to.");
            var from = data.FindBranch(item.BranchId);
            if (!to.IsActive) throw new LibraryException($"{to.Name} is closed down and can't receive items.");
            if (item.BranchId == toBranchId) throw new LibraryException($"'{item.Name}' is already at {to.Name}.");
            if (item.Status == ItemStatus.Destroy) throw new LibraryException($"'{item.Name}' has been destroyed and can't be moved.");
            if (item.Status == ItemStatus.Borrowed) throw new LibraryException($"'{item.Name}' is out on loan. It can be moved once it is back.");
            if (data.IsInTransit(itemId)) throw new LibraryException($"'{item.Name}' already has a transfer in progress.");
            if (data.Reservations.Any(r => r.ItemId == itemId && r.Status == ReservationStatus.Ready))
                throw new LibraryException($"'{item.Name}' is being held for a patron and can't be moved until the hold is collected or expires.");
            if (!data.Users.Any(u => u.Id == requestedByUserId)) throw new LibraryException("Unknown staff member.");

            var t = new Transfer
            {
                Id = data.TakeId(), ItemId = itemId, FromBranchId = item.BranchId, ToBranchId = toBranchId,
                Status = TransferStatus.Requested, RequestedByUserId = requestedByUserId,
                RequestedAt = clock.Now, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
            };
            data.Transfers.Add(t);
            return Info(data, t);
        });

    public Task<TransferResult> DispatchAsync(int transferId, int userId, CancellationToken ct = default) =>
        Run(data =>
        {
            var t = Find(data, transferId);
            if (t.Status != TransferStatus.Requested) throw new LibraryException($"This transfer is {t.Status}; only a requested transfer can be dispatched.");
            var item = data.FindItem(t.ItemId)!;
            if (item.Status == ItemStatus.Borrowed) throw new LibraryException($"'{item.Name}' has been borrowed since the request. Cancel the transfer.");
            t.Status = TransferStatus.InTransit;
            t.DispatchedAt = clock.Now;
            return Info(data, t);
        });

    public Task<TransferResult> ReceiveAsync(int transferId, int userId, CancellationToken ct = default) =>
        Run(data =>
        {
            var t = Find(data, transferId);
            if (t.Status != TransferStatus.InTransit) throw new LibraryException($"This transfer is {t.Status}; only an item in transit can be received.");
            t.Status = TransferStatus.Received;
            t.ReceivedAt = clock.Now;
            data.FindItem(t.ItemId)!.BranchId = t.ToBranchId;
            return Info(data, t);
        });

    public Task<TransferResult> CancelAsync(int transferId, int userId, CancellationToken ct = default) =>
        Run(data =>
        {
            var t = Find(data, transferId);
            if (t.Status != TransferStatus.Requested) throw new LibraryException($"This transfer is {t.Status}; only a requested transfer can be cancelled (before it is dispatched).");
            t.Status = TransferStatus.Cancelled;
            t.CancelledAt = clock.Now;
            return Info(data, t);
        });

    public Task<IReadOnlyList<TransferInfo>> GetInboundAsync(int branchId, CancellationToken ct = default) =>
        Task.FromResult(List(t => t.IsActive && t.ToBranchId == branchId));

    public Task<IReadOnlyList<TransferInfo>> GetOutboundAsync(int branchId, CancellationToken ct = default) =>
        Task.FromResult(List(t => t.IsActive && t.FromBranchId == branchId));

    public Task<bool> IsInTransitAsync(int itemId, CancellationToken ct = default) =>
        Task.FromResult(store.Read(d => d.IsInTransit(itemId)));

    /// <summary>Every transfer, newest first (for the staff list and the inventory-health report).</summary>
    public IReadOnlyList<TransferInfo> ListAll(int? branchId = null, bool activeOnly = false) =>
        List(t => (!activeOnly || t.IsActive) && (branchId is null || t.FromBranchId == branchId || t.ToBranchId == branchId));

    private IReadOnlyList<TransferInfo> List(Func<Transfer, bool> filter) =>
        store.Read(d => (IReadOnlyList<TransferInfo>)d.Transfers.Where(filter).OrderByDescending(t => t.RequestedAt).ThenByDescending(t => t.Id)
            .Select(t => Info(d, t)).ToList());

    private static Transfer Find(LibraryData data, int id) =>
        data.Transfers.FirstOrDefault(t => t.Id == id) ?? throw new LibraryException("No such transfer.");

    internal static TransferInfo Info(LibraryData d, Transfer t)
    {
        var item = d.FindItem(t.ItemId);
        return new TransferInfo(t.Id, t.ItemId, item?.LibraryCode ?? "?", item?.Name ?? "(deleted item)", t.FromBranchId, t.ToBranchId,
            t.Status, t.RequestedAt, t.DispatchedAt, t.ReceivedAt, t.Note);
    }

    private Task<TransferResult> Run(Func<LibraryData, TransferInfo> action)
    {
        try { return Task.FromResult(TransferResult.Ok(store.Write(action))); }
        catch (LibraryException ex) { return Task.FromResult(TransferResult.Fail(ex.Message)); }
    }
}
