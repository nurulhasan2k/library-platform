using System.Globalization;
using Library.Core.Configuration;
using Library.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Library.Core.Services;

/// <summary>One simulated email or SMS. Stored in memory for the dashboard and also written to the console log.</summary>
public sealed class NotificationRecord
{
    public required int Id { get; init; }
    public required NotificationType Type { get; init; }
    public required NotificationChannel Channel { get; init; } // Email or Sms only
    public required int BorrowerId { get; init; }
    public required string BorrowerName { get; init; }
    public int? ItemId { get; init; }
    public int? LoanId { get; init; }
    public required string Recipient { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public required DateTime CreatedAt { get; init; }
    public string Status { get; init; } = "Simulated sent";
    public required string DedupeKey { get; init; }
}

/// <summary>
/// Real INotificationService (Person B). Nothing is sent for real.
/// Rules: Both = two records; de-duplication by key (one DueSoon per loan, one fine per loan per day,
/// one ReservationReady per hold); bodies never contain a PIN or card number.
/// </summary>
public sealed class NotificationService(
    DemoLibraryStore store,
    IClock clock,
    IServiceProvider services,
    IOptions<NotificationOptions> notificationOptions,
    IOptions<LendingOptions> lendingOptions,
    IOptions<ReservationOptions> reservationOptions,
    ILogger<NotificationService> log) : INotificationService
{
    private readonly object _sync = new();
    private readonly List<NotificationRecord> _records = new();
    private int _nextId = 1;

    /// <summary>All notifications, newest first.</summary>
    public IReadOnlyList<NotificationRecord> GetAll()
    {
        lock (_sync) return _records.OrderByDescending(r => r.Id).ToList();
    }

    /// <summary>Count per notification type (used later by C's inventory-health report).</summary>
    public int Count()
    {
        lock (_sync) return _records.Count;
    }

    /// <summary>Demo helper: empty the dashboard.</summary>
    public void Clear()
    {
        lock (_sync) _records.Clear();
    }

    public Task<IReadOnlyList<int>> RecordAsync(NotificationRequest request, CancellationToken ct = default)
        => Task.FromResult(RecordCore(request, clock.Today));

    public async Task<int> RunScheduledChecksAsync(DateOnly asOf, CancellationToken ct = default)
    {
        var before = Count();
        var dueSoonDays = notificationOptions.Value.DueSoonDays;

        foreach (var loan in store.LoansSnapshot().Where(l => !l.Returned))
        {
            var daysToDue = loan.DueOn.DayNumber - asOf.DayNumber;
            if (daysToDue >= 0 && daysToDue <= dueSoonDays)
            {
                RecordCore(new NotificationRequest(NotificationType.DueSoon, loan.BorrowerId, loan.ItemId, loan.LoanId), asOf);
            }
            else if (daysToDue < 0)
            {
                RecordCore(new NotificationRequest(NotificationType.FineAccruing, loan.BorrowerId, loan.ItemId, loan.LoanId), asOf);
            }
        }

        // Expired Ready holds are handled by the reservation service (it also notifies and promotes the next patron).
        await services.GetRequiredService<ReservationService>().ExpireOverdueHoldsAsync(asOf, ct);

        return Count() - before;
    }

    private IReadOnlyList<int> RecordCore(NotificationRequest request, DateOnly today)
    {
        var patron = store.FindPatron(request.BorrowerId);
        if (patron is null) return Array.Empty<int>();

        var item = request.ItemId is { } itemId ? store.FindItem(itemId) : null;
        var loan = request.LoanId is { } loanId ? store.FindLoan(loanId) : null;
        var (subject, body, key) = Build(request, patron, item?.Name ?? "your item", loan, today);

        var channels = patron.Channel switch
        {
            NotificationChannel.Email => new[] { NotificationChannel.Email },
            NotificationChannel.Sms => new[] { NotificationChannel.Sms },
            _ => new[] { NotificationChannel.Email, NotificationChannel.Sms }
        };

        var created = new List<NotificationRecord>();
        lock (_sync)
        {
            if (_records.Any(r => r.DedupeKey == key && r.BorrowerId == patron.Id))
                return Array.Empty<int>();

            foreach (var channel in channels)
            {
                var record = new NotificationRecord
                {
                    Id = _nextId++,
                    Type = request.Type,
                    Channel = channel,
                    BorrowerId = patron.Id,
                    BorrowerName = patron.Name,
                    ItemId = request.ItemId,
                    LoanId = request.LoanId,
                    Recipient = channel == NotificationChannel.Email ? patron.Email : patron.Phone,
                    Subject = subject,
                    Body = body,
                    CreatedAt = clock.Now,
                    DedupeKey = key
                };
                _records.Add(record);
                created.Add(record);
            }
        }

        foreach (var r in created)
        {
            log.LogInformation("[SIMULATED {Channel}] to {Recipient}: {Subject} | {Body}", r.Channel, r.Recipient, r.Subject, r.Body);
        }

        return created.Select(r => r.Id).ToList();
    }

    private (string Subject, string Body, string Key) Build(
        NotificationRequest r, DemoPatron patron, string itemName, DemoLoan? loan, DateOnly today)
    {
        var due = loan is null
            ? "the due date"
            : loan.DueOn.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

        switch (r.Type)
        {
            case NotificationType.Borrowed:
                return ("Item borrowed",
                    $"Hello {patron.Name}, you borrowed \"{itemName}\". It is due on {due}.",
                    $"Borrowed:{r.LoanId}");

            case NotificationType.DueSoon:
                return ("Item due soon",
                    $"Hello {patron.Name}, \"{itemName}\" is due on {due}. Please return it on time.",
                    $"DueSoon:{r.LoanId}");

            case NotificationType.FineAccruing:
            {
                var daysLate = loan is null ? 0 : Math.Max(0, today.DayNumber - loan.DueOn.DayNumber);
                var fine = daysLate * lendingOptions.Value.FinePerDay;
                return ("Fine accruing",
                    $"Hello {patron.Name}, \"{itemName}\" was due on {due} and is {daysLate} day(s) late. The fine is now ${fine:0.00}.",
                    $"Fine:{r.LoanId}:{today:yyyyMMdd}");
            }

            case NotificationType.ReservationReady:
                return ("Your hold is ready",
                    $"Hello {patron.Name}, \"{itemName}\" is ready for you. Please borrow it within {reservationOptions.Value.ReadyHoldDays} days.",
                    $"Ready:{r.ReservationId}");

            case NotificationType.ReservationExpired:
                return ("Your hold has expired",
                    $"Hello {patron.Name}, your hold on \"{itemName}\" has expired because it was not borrowed in time.",
                    $"Expired:{r.ReservationId}");

            default:
                throw new ArgumentOutOfRangeException(nameof(r), "Unknown notification type.");
        }
    }
}
