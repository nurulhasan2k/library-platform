using System.Globalization;
using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Library.Core.Services;

/// <summary>
/// Simulated email / SMS (feature 3.3). Messages are stored in the notification table (the staff dashboard) and written
/// to the console log. Nothing is ever really sent. A patron who prefers "Both" gets two records.
/// </summary>
public sealed class NotificationService(
    ILibraryStore store,
    IClock clock,
    IOptions<NotificationOptions> options,
    IOptions<LendingOptions> lending,
    IServiceProvider services,
    ILogger<NotificationService> log) : INotificationService
{
    public Task<IReadOnlyList<int>> RecordAsync(NotificationRequest request, CancellationToken ct = default)
    {
        var ids = store.Write(data => Create(data, request, clock.Now));
        return Task.FromResult<IReadOnlyList<int>>(ids);
    }

    public async Task<int> RunScheduledChecksAsync(DateOnly asOf, CancellationToken ct = default)
    {
        var at = asOf.ToDateTime(TimeOnly.FromDateTime(clock.Now));
        var created = 0;

        // 1. Holds that were not collected in time expire; the next person in the queue is then promoted.
        var expired = store.Write(data =>
        {
            var list = new List<(int ItemId, int ReservationId, int BorrowerId)>();
            foreach (var r in data.Reservations.Where(r => r.Status == ReservationStatus.Ready && r.ExpiresAt is { } e && DateOnly.FromDateTime(e) < asOf))
            {
                r.Status = ReservationStatus.Expired;
                r.ClosedAt = at;
                list.Add((r.ItemId, r.Id, r.BorrowerId));
            }
            foreach (var (itemId, resId, borrowerId) in list)
                created += Create(data, new NotificationRequest(NotificationType.ReservationExpired, borrowerId, itemId, null, resId), at).Count;
            return list;
        });
        foreach (var e in expired)
        {
            try { await services.GetRequiredService<IReservationService>().OnItemBecameAvailableAsync(e.ItemId, ct); }
            catch (Exception ex) { log.LogWarning(ex, "Could not promote the next hold for item {ItemId}", e.ItemId); }
        }

        // 2. Loans that are due soon or overdue.
        created += store.Write(data =>
        {
            var n = 0;
            foreach (var loan in data.Loans.Where(l => l.IsActive).ToList())
            {
                var daysToDue = loan.DueOn.DayNumber - asOf.DayNumber;
                if (daysToDue is >= 0 && daysToDue <= options.Value.DueSoonDays)
                    n += Create(data, new NotificationRequest(NotificationType.DueSoon, loan.BorrowerId, loan.ItemId, loan.Id), at).Count;
                else if (daysToDue < 0)
                    n += Create(data, new NotificationRequest(NotificationType.FineAccruing, loan.BorrowerId, loan.ItemId, loan.Id), at).Count;
            }
            return n;
        });

        log.LogInformation("Scheduled notification checks for {AsOf}: {Count} new notification(s)", asOf, created);
        return created;
    }

    /// <summary>Notifications for the dashboard, newest first.</summary>
    public IReadOnlyList<Notification> List(int? branchId = null, NotificationType? type = null, NotificationChannel? channel = null, int? borrowerId = null) =>
        store.Read(d => (IReadOnlyList<Notification>)d.Notifications
            .Where(n => (branchId is null || n.BranchId == branchId) && (type is null || n.Type == type) &&
                        (channel is null || n.Channel == channel) && (borrowerId is null || n.BorrowerId == borrowerId))
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).ToList());

    /// <summary>Creates notifications inside an open unit of work at a chosen time. Used by the demo-data seeder.</summary>
    public List<int> CreateInto(LibraryData data, NotificationRequest request, DateTime at) => Create(data, request, at);

    // ------------------------------------------------------------------ build

    private List<int> Create(LibraryData data, NotificationRequest req, DateTime at)
    {
        var ids = new List<int>();
        var borrower = data.FindBorrower(req.BorrowerId);
        if (borrower is null) return ids;

        var item = req.ItemId is { } iid ? data.FindItem(iid) : null;
        var loan = req.LoanId is { } lid ? data.Loans.FirstOrDefault(l => l.Id == lid) : null;
        var res = req.ReservationId is { } rid ? data.Reservations.FirstOrDefault(r => r.Id == rid) : null;
        var branch = item is null ? null : data.FindBranch(item.BranchId);
        var asOf = DateOnly.FromDateTime(at);

        var (subject, body) = Compose(req.Type, borrower, item, loan, res, branch, asOf);

        var channels = borrower.PreferredChannel switch
        {
            NotificationChannel.Both => new[] { NotificationChannel.Email, NotificationChannel.Sms },
            var c => new[] { c }
        };

        foreach (var channel in channels)
        {
            var recipient = channel == NotificationChannel.Email ? borrower.Email : borrower.Phone;
            if (string.IsNullOrWhiteSpace(recipient)) continue;

            var day = req.Type == NotificationType.FineAccruing ? asOf.ToString("yyyy-MM-dd") : "";
            var key = $"{req.Type}|{req.LoanId}|{req.ReservationId}|{(loan is null && res is null ? req.ItemId : null)}|{channel}|{day}";
            if (req.Type != NotificationType.Borrowed && data.Notifications.Any(n => n.DedupKey == key)) continue;

            var n = new Notification
            {
                Id = data.TakeId(), Type = req.Type, Channel = channel, BorrowerId = borrower.Id, BorrowerName = borrower.Name,
                Recipient = recipient, Subject = subject, Body = channel == NotificationChannel.Sms ? Sms(body) : body,
                CreatedAt = at, BranchId = item?.BranchId, ItemId = item?.Id, LoanId = loan?.Id, ReservationId = res?.Id, DedupKey = key
            };
            data.Notifications.Add(n);
            ids.Add(n.Id);
            log.LogInformation("[SIMULATED {Channel}] to {Recipient} | {Subject}", channel.ToString().ToUpperInvariant(), recipient, subject);
        }
        return ids;
    }

    private (string Subject, string Body) Compose(NotificationType type, Borrower b, Item? item, Loan? loan, Reservation? res, Branch? branch, DateOnly asOf)
    {
        var name = b.Name.Split(' ')[0];
        var title = item?.Name ?? "your item";
        var where = branch?.Name ?? "the library";
        var perDay = lending.Value.FinePerDay.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
        string D(DateOnly d) => d.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);

        switch (type)
        {
            case NotificationType.Borrowed:
                return ($"You borrowed \"{title}\"",
                    $"Hi {name}, you borrowed \"{title}\" ({item?.LibraryCode}) from {where}. It is due back on {(loan is null ? "the due date" : D(loan.DueOn))}.");
            case NotificationType.DueSoon:
            {
                var days = loan is null ? 0 : loan.DueOn.DayNumber - asOf.DayNumber;
                var when = days <= 0 ? "today" : days == 1 ? "tomorrow" : $"in {days} days";
                return ($"\"{title}\" is due {when}",
                    $"Hi {name}, \"{title}\" is due back {when} ({(loan is null ? "" : D(loan.DueOn))}) at {where}. Late items are fined {perDay} per day.");
            }
            case NotificationType.FineAccruing:
            {
                var late = loan?.DaysLate(asOf) ?? 0;
                var fine = late * lending.Value.FinePerDay;
                return ($"\"{title}\" is {late} day{(late == 1 ? "" : "s")} overdue",
                    $"Hi {name}, \"{title}\" was due on {(loan is null ? "" : D(loan.DueOn))} and is now {late} day{(late == 1 ? "" : "s")} overdue. " +
                    $"The fine so far is {fine.ToString("C2", CultureInfo.GetCultureInfo("en-US"))} ({perDay} per day). Please return it to any branch.");
            }
            case NotificationType.ReservationReady:
                return ($"\"{title}\" is ready for you",
                    $"Hi {name}, your hold on \"{title}\" is ready to collect at {where}. We will keep it for you until {(res?.ExpiresAt is { } e ? D(DateOnly.FromDateTime(e)) : "the hold expires")}.");
            case NotificationType.ReservationExpired:
                return ($"Your hold on \"{title}\" has expired",
                    $"Hi {name}, your hold on \"{title}\" was not collected in time and has expired. You can place a new hold at any time.");
            default:
                return ("Library notice", "");
        }
    }

    private static string Sms(string body) => body.Length <= 160 ? body : body[..157] + "...";
}
