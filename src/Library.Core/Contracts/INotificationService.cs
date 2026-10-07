namespace Library.Core.Contracts;

public enum NotificationType
{
    Borrowed,
    DueSoon,
    FineAccruing,
    ReservationReady,
    ReservationExpired
}

/// <summary>Everything needed to create a notification. The service builds the subject and body from a template.</summary>
public sealed record NotificationRequest(
    NotificationType Type,
    int BorrowerId,
    int? ItemId = null,
    int? LoanId = null,
    int? ReservationId = null);

/// <summary>
/// Simulated email / SMS. Messages are stored in the Notification table (dashboard) and also written to the console log.
/// Nothing is ever really sent. A patron whose preference is "Both" gets two records.
/// Built by Person B. Until then the stub only logs. Person A's lending code calls Record after a borrow.
/// </summary>
public interface INotificationService
{
    /// <summary>Create the notification(s) for this event. Returns the ids created (empty if de-duplicated).</summary>
    Task<IReadOnlyList<int>> RecordAsync(NotificationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Run the date-based checks (due soon, fine accruing, expired holds) as of the given date.
    /// The daily background job and the admin "Run checks now" button both call this. Returns how many notifications were created.
    /// </summary>
    Task<int> RunScheduledChecksAsync(DateOnly asOf, CancellationToken ct = default);
}
