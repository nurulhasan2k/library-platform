using Library.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace Library.Core.Stubs;

// Safe placeholder implementations so the whole solution compiles and runs from day one.
// Each stub is replaced by its real service (see docs/ARCHITECTURE.md, "Replacing a stub").
// Stubs never invent data: they log, return empty results or refuse with a clear message.

/// <summary>Real clock. Person B replaces it with the simulated-date clock (decision 10).</summary>
public sealed class SystemClock : IClock
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
    public DateTime Now => DateTime.Now;
}

/// <summary>Always "Central". Person A replaces it with the session/kiosk-based branch context.</summary>
public sealed class StubBranchContext : IBranchContext
{
    public BranchInfo? CurrentBranch { get; } = new(1, "CENTRAL", "Central Library");
    public int? CurrentDeskId => null;
    public bool IsKiosk => false;
}

/// <summary>Only logs. Person B replaces it with the real notification service.</summary>
public sealed class StubNotificationService(ILogger<StubNotificationService> log) : INotificationService
{
    public Task<IReadOnlyList<int>> RecordAsync(NotificationRequest request, CancellationToken ct = default)
    {
        log.LogInformation("[stub] Notification {Type} for borrower {BorrowerId} (item {ItemId}, loan {LoanId}) not stored",
            request.Type, request.BorrowerId, request.ItemId, request.LoanId);
        return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
    }

    public Task<int> RunScheduledChecksAsync(DateOnly asOf, CancellationToken ct = default)
    {
        log.LogInformation("[stub] Scheduled notification checks for {AsOf} skipped", asOf);
        return Task.FromResult(0);
    }
}

/// <summary>Allows every borrow, has no queues. Person B replaces it with the real reservation service.</summary>
public sealed class StubReservationService : IReservationService
{
    public Task<ReservationResult> PlaceAsync(int itemId, int borrowerId, ReservationSource placedVia, CancellationToken ct = default)
        => Task.FromResult(ReservationResult.Fail("Holds are not available yet."));

    public Task<ReservationResult> CancelAsync(int reservationId, int requestedByBorrowerId, CancellationToken ct = default)
        => Task.FromResult(ReservationResult.Fail("Holds are not available yet."));

    public Task<IReadOnlyList<QueueEntry>> GetQueueAsync(int itemId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<QueueEntry>>(Array.Empty<QueueEntry>());

    public Task<int> GetQueueLengthAsync(int itemId, CancellationToken ct = default) => Task.FromResult(0);

    public Task<BorrowCheck> CheckBorrowAllowedAsync(int itemId, int borrowerId, CancellationToken ct = default)
        => Task.FromResult(BorrowCheck.Yes());

    public Task OnBorrowedAsync(int itemId, int borrowerId, CancellationToken ct = default) => Task.CompletedTask;

    public Task OnItemBecameAvailableAsync(int itemId, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>Returns nothing. Person A replaces it with the EF Core query; Person C extends it.</summary>
public sealed class StubCatalogueQuery : ICatalogueQuery
{
    public Task<PagedResult<CatalogueItem>> SearchAsync(CatalogueFilter filter, CancellationToken ct = default)
        => Task.FromResult(new PagedResult<CatalogueItem>(Array.Empty<CatalogueItem>(), 0, filter.Page, filter.PageSize));

    public Task<CatalogueItem?> FindByCodeAsync(string libraryCode, CancellationToken ct = default)
        => Task.FromResult<CatalogueItem?>(null);

    public Task<bool> CodeExistsAsync(string libraryCode, CancellationToken ct = default) => Task.FromResult(false);

    public Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CategoryInfo>>(Array.Empty<CategoryInfo>());

    public Task<IReadOnlyList<BranchSummary>> GetBranchesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BranchSummary>>(Array.Empty<BranchSummary>());
}

/// <summary>Refuses every sign-in. Person A replaces it with the card + PIN service.</summary>
public sealed class StubPatronAuthService : IPatronAuthService
{
    public Task<PatronAuthResult> SignInAsync(string cardNumber, string pin, CancellationToken ct = default)
        => Task.FromResult(PatronAuthResult.Fail("Patron sign-in is not available yet."));
}

/// <summary>Refuses every action. Person A replaces it with the real lending rules.</summary>
public sealed class StubLendingService : ILendingService
{
    public Task<BorrowResult> BorrowAsync(string libraryCode, int borrowerId, int branchId, CancellationToken ct = default)
        => Task.FromResult(BorrowResult.Fail("Lending is not available yet."));

    public Task<ReturnResult> ReturnAsync(string libraryCode, int branchId, bool damaged, CancellationToken ct = default)
        => Task.FromResult(ReturnResult.Fail("Lending is not available yet."));
}

/// <summary>No transfers. Person A replaces it with the real transfer workflow.</summary>
public sealed class StubTransferService : ITransferService
{
    private static Task<TransferResult> NotYet() => Task.FromResult(TransferResult.Fail("Transfers are not available yet."));

    public Task<TransferResult> RequestAsync(int itemId, int toBranchId, int requestedByUserId, string? note, CancellationToken ct = default) => NotYet();
    public Task<TransferResult> DispatchAsync(int transferId, int userId, CancellationToken ct = default) => NotYet();
    public Task<TransferResult> ReceiveAsync(int transferId, int userId, CancellationToken ct = default) => NotYet();
    public Task<TransferResult> CancelAsync(int transferId, int userId, CancellationToken ct = default) => NotYet();

    public Task<IReadOnlyList<TransferInfo>> GetInboundAsync(int branchId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TransferInfo>>(Array.Empty<TransferInfo>());

    public Task<IReadOnlyList<TransferInfo>> GetOutboundAsync(int branchId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TransferInfo>>(Array.Empty<TransferInfo>());

    public Task<bool> IsInTransitAsync(int itemId, CancellationToken ct = default) => Task.FromResult(false);
}
