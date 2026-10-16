using Library.Core.Contracts;
using Library.Core.Models;
using Library.Core.Services;

namespace Library.Tests;

public static class ReservationAndNotificationTests
{
    public static void Run()
    {
        T.Section("Reservations and waitlists");
        using var p = new TestPlatform();
        var res = p.Get<IReservationService>();
        var central = p.Branch("CENTRAL").Id;
        int Id(string card) => p.Patron(card).Id;

        T.Test("a hold can be placed on a borrowed item and joins the end of the queue", () =>
        {
            var r = TestPlatform.Wait(res.PlaceAsync(p.Item("BK-020").Id, Id("LIB-1001"), ReservationSource.Kiosk));
            T.True(r.Success, r.Error ?? "");
            var q = TestPlatform.Wait(res.GetQueueAsync(p.Item("BK-020").Id));
            T.Equal(1, q.Count);
            T.Equal(1, q[0].Position);
        });
        T.Test("a patron can't hold the same item twice", () =>
            T.True(TestPlatform.Wait(res.PlaceAsync(p.Item("BK-020").Id, Id("LIB-1001"), ReservationSource.Web)).Error!.Contains("already have a hold")));
        T.Test("a patron can't hold an item they already have on loan", () =>
            T.True(TestPlatform.Wait(res.PlaceAsync(p.Item("BK-010").Id, Id("LIB-1005"), ReservationSource.Web)).Error!.Contains("on loan")));
        T.Test("a hold on an available item with no queue is refused with a helpful message", () =>
            T.True(TestPlatform.Wait(res.PlaceAsync(p.Item("BK-001").Id, Id("LIB-1001"), ReservationSource.Web)).Error!.Contains("available now")));
        T.Test("holds on destroyed items are refused", () =>
            T.False(TestPlatform.Wait(res.PlaceAsync(p.Item("BK-040").Id, Id("LIB-1001"), ReservationSource.Web)).Success));
        T.Test("the number of open holds per patron is limited", () =>
        {
            using var q = new TestPlatform();
            var r = q.Get<IReservationService>();
            var grace = q.Patron("LIB-1007").Id;
            // Grace has 2 holds already (BK-010, MU-010); the limit is 5.
            foreach (var code in new[] { "BK-020", "BK-021", "TY-003" }) T.True(TestPlatform.Wait(r.PlaceAsync(q.Item(code).Id, grace, ReservationSource.Web)).Success, code);
            T.True(TestPlatform.Wait(r.PlaceAsync(q.Item("MU-004").Id, grace, ReservationSource.Web)).Error!.Contains("at most 5"));
        });
        T.Test("queue positions follow the order holds were placed", () =>
        {
            var q = TestPlatform.Wait(res.GetQueueAsync(p.Item("BK-010").Id));
            T.Equal("Grace Liu", q[0].BorrowerName);
            T.Equal("Hugo Martin", q[1].BorrowerName);
            T.Equal("Isla Brown", q[2].BorrowerName);
        });
        T.Test("cancelling your own hold works; someone else's hold can't be cancelled", () =>
        {
            var hugoHold = p.Store.Read(d => d.Reservations.First(r => r.ItemId == p.Item("BK-010").Id && r.BorrowerId == Id("LIB-1008")));
            T.False(TestPlatform.Wait(res.CancelAsync(hugoHold.Id, Id("LIB-1009"))).Success);
            T.True(TestPlatform.Wait(res.CancelAsync(hugoHold.Id, Id("LIB-1008"))).Success);
            var q = TestPlatform.Wait(res.GetQueueAsync(p.Item("BK-010").Id));
            T.Equal(2, q.Count);
            T.Equal(2, q[1].Position);
        });
        T.Test("cancelling a Ready hold passes it to the next patron", () =>
        {
            var graceHold = p.Store.Read(d => d.Reservations.First(r => r.ItemId == p.Item("MU-010").Id));
            p.Inventory.ChangeItemStatus(p.Item("MU-010").Id, ItemStatus.Available);
            T.Equal(ReservationStatus.Ready, p.Store.Read(d => d.Reservations.First(r => r.Id == graceHold.Id).Status));
            TestPlatform.Wait(res.PlaceAsync(p.Item("MU-010").Id, Id("LIB-1009"), ReservationSource.Desk));
            TestPlatform.Wait(res.CancelAsync(graceHold.Id, Id("LIB-1007")));
            var isla = p.Store.Read(d => d.Reservations.First(r => r.ItemId == p.Item("MU-010").Id && r.BorrowerId == Id("LIB-1009")));
            T.Equal(ReservationStatus.Ready, isla.Status);
        });
        T.Test("staff can cancel on behalf of a patron (borrower id 0)", () =>
        {
            var h = TestPlatform.Wait(res.PlaceAsync(p.Item("TY-003").Id, Id("LIB-1002"), ReservationSource.Desk));
            T.True(TestPlatform.Wait(res.CancelAsync(h.ReservationId!.Value, 0)).Success);
        });

        T.Section("Notifications (simulated email / SMS)");
        var notes = p.Get<NotificationService>();
        var inote = (INotificationService)notes;

        T.Test("the daily check creates a due-soon notice for Farid (due in 2 days) on his SMS channel", () =>
        {
            var created = TestPlatform.Wait(inote.RunScheduledChecksAsync(p.Clock.Today));
            T.True(created > 0);
            var farid = p.Patron("LIB-1006").Id;
            var n = p.Store.Read(d => d.Notifications.Where(x => x.BorrowerId == farid && x.Type == NotificationType.DueSoon).ToList());
            T.Equal(1, n.Count);
            T.Equal(NotificationChannel.Sms, n[0].Channel);
            T.True(n[0].Subject.Contains("1000-piece") || n[0].Subject.Contains("map puzzle"), n[0].Subject);
            T.True(n[0].Body.Length <= 160, "SMS should fit in 160 characters");
        });
        T.Test("running the checks twice on the same day doesn't duplicate notices", () =>
            T.Equal(0, TestPlatform.Wait(inote.RunScheduledChecksAsync(p.Clock.Today))));
        T.Test("overdue loans get a fine-accruing notice with the amount so far", () =>
        {
            var daniel = p.Patron("LIB-1004").Id;
            var n = p.Store.Read(d => d.Notifications.Where(x => x.BorrowerId == daniel && x.Type == NotificationType.FineAccruing && x.CreatedAt.Date == p.Clock.Today.ToDateTime(TimeOnly.MinValue)).ToList());
            T.True(n.Count >= 2, "one per overdue loan");
            T.True(n.Any(x => x.Body.Contains("$6.00")), "the Hobbit is 12 days late = $6.00");
        });
        T.Test("moving the simulated date forward produces new fine notices the next day", () =>
        {
            p.Clock.Today = p.Clock.Today.AddDays(1);
            T.True(TestPlatform.Wait(inote.RunScheduledChecksAsync(p.Clock.Today)) >= 2);
        });
        T.Test("a patron who prefers Both gets two records for one event", () =>
        {
            var chloe = p.Patron("LIB-1003").Id;
            var ids = TestPlatform.Wait(inote.RecordAsync(new NotificationRequest(NotificationType.Borrowed, chloe, p.Item("BK-002").Id)));
            T.Equal(2, ids.Count);
            var channels = p.Store.Read(d => d.Notifications.Where(x => ids.Contains(x.Id)).Select(x => x.Channel).OrderBy(c => c).ToList());
            T.Equal(NotificationChannel.Email, channels[0]);
            T.Equal(NotificationChannel.Sms, channels[1]);
        });
        T.Test("an uncollected hold expires, the patron is told, and the next person is promoted", () =>
        {
            using var q = new TestPlatform();
            // Ben's hold on BK-011 expires D+2. Put Hugo in the queue behind him, then jump past the expiry.
            TestPlatform.Wait(q.Get<IReservationService>().PlaceAsync(q.Item("BK-011").Id, q.Patron("LIB-1008").Id, ReservationSource.Web));
            q.Clock.Today = q.Clock.Today.AddDays(3);
            TestPlatform.Wait(((INotificationService)q.Get<NotificationService>()).RunScheduledChecksAsync(q.Clock.Today));
            var ben = q.Store.Read(d => d.Reservations.First(r => r.ItemId == q.Item("BK-011").Id && r.BorrowerId == q.Patron("LIB-1002").Id));
            T.Equal(ReservationStatus.Expired, ben.Status);
            T.True(q.Store.Read(d => d.Notifications.Any(n => n.Type == NotificationType.ReservationExpired && n.BorrowerId == ben.BorrowerId && n.CreatedAt.Date == q.Clock.Today.ToDateTime(TimeOnly.MinValue))));
            var hugo = q.Store.Read(d => d.Reservations.First(r => r.ItemId == q.Item("BK-011").Id && r.BorrowerId == q.Patron("LIB-1008").Id));
            T.Equal(ReservationStatus.Ready, hugo.Status);
        });
        T.Test("a failing notification never stops a borrow", () =>
        {
            using var q = new TestPlatform();
            q.Store.Write(d => d.Borrowers.First(b => b.CardNumber == "LIB-1001").Email = "");
            q.Store.Write(d => d.Borrowers.First(b => b.CardNumber == "LIB-1001").PreferredChannel = NotificationChannel.Email);
            var r = TestPlatform.Wait(((ILendingService)q.Lending).BorrowAsync("BK-001", q.Patron("LIB-1001").Id, q.Branch("CENTRAL").Id));
            T.True(r.Success, r.Error ?? "");
        });
    }
}
