using Library.Core.Contracts;
using Library.Core.Models;
using Library.Core.Services;

namespace Library.Tests;

public static class SeedTests
{
    public static void Run()
    {
        T.Section("Demo data (docs/SEED-SCENARIOS.md)");
        using var p = new TestPlatform();

        T.Test("creates 3 branches, 4 staff, 9 categories, 45 items, 10 patrons", () =>
        {
            T.Equal(3, p.Store.Read(d => d.Branches.Count));
            T.Equal(4, p.Store.Read(d => d.Users.Count));
            T.Equal(9, p.Store.Read(d => d.Categories.Count));
            T.Equal(45, p.Store.Read(d => d.Items.Count));
            T.Equal(10, p.Store.Read(d => d.Borrowers.Count));
        });
        T.Test("items per branch are 18 / 14 / 13 (books/music/toys as planned)", () =>
        {
            var by = p.Store.Read(d => d.Items.GroupBy(i => i.BranchId).ToDictionary(g => g.Key, g => g.Count()));
            T.Equal(18, by[p.Branch("CENTRAL").Id]);
            T.Equal(14, by[p.Branch("NORTH").Id]);
            T.Equal(13, by[p.Branch("RIVER").Id]);
            T.Equal(24, p.Store.Read(d => d.Items.Count(i => i.Type == ItemType.Book)));
            T.Equal(12, p.Store.Read(d => d.Items.Count(i => i.Type == ItemType.Music)));
            T.Equal(9, p.Store.Read(d => d.Items.Count(i => i.Type == ItemType.Toy)));
        });
        T.Test("every branch has two desks and at least one closed day", () =>
        {
            foreach (var b in p.Store.Read(d => d.Branches.ToList()))
            {
                T.Equal(2, b.Desks.Count, b.Code + " desks");
                T.True(b.Hours.Any(h => h.IsClosed), b.Code + " has no closed day");
            }
        });
        T.Test("history has about 150 returned loans over 180 days", () =>
        {
            var returned = p.Store.Read(d => d.Loans.Count(l => !l.IsActive));
            T.True(returned is >= 145 and <= 155, "returned loans: " + returned);
            var oldest = p.Store.Read(d => d.Loans.Min(l => l.BorrowedOn));
            T.True(p.Clock.Today.DayNumber - oldest.DayNumber >= 150, "history should reach back ~180 days");
        });
        T.Test("named scenario items are in the documented state", () =>
        {
            T.Equal(ItemStatus.Available, p.Item("BK-001").Status);
            T.Equal(ItemStatus.Borrowed, p.Item("BK-010").Status);
            T.Equal(ItemStatus.Damaged, p.Item("MU-010").Status);
            T.Equal(ItemStatus.Destroy, p.Item("BK-040").Status);
            var hobbit = p.Store.Read(d => d.Loans.First(l => l.ItemId == p.Item("BK-020").Id && l.IsActive));
            T.Equal(p.Clock.Today.AddDays(-12), hobbit.DueOn);
            var puzzle = p.Store.Read(d => d.Loans.First(l => l.ItemId == p.Item("TY-003").Id && l.IsActive));
            T.Equal(p.Clock.Today.AddDays(2), puzzle.DueOn);
        });
        T.Test("transfers exist in all four states and MU-012 is in transit", () =>
        {
            var states = p.Store.Read(d => d.Transfers.Select(t => t.Status).Distinct().Count());
            T.Equal(4, states);
            T.True(p.Store.Read(d => d.IsInTransit(p.Item("MU-012").Id)));
            T.Equal(p.Branch("NORTH").Id, p.Item("TY-010").BranchId);
        });
        T.Test("BK-010 has a waitlist of three, BK-011 is held for Ben", () =>
        {
            var q = TestPlatform.Wait(p.Get<IReservationService>().GetQueueAsync(p.Item("BK-010").Id));
            T.Equal(3, q.Count);
            T.Equal("Grace Liu", q[0].BorrowerName);
            var q2 = TestPlatform.Wait(p.Get<IReservationService>().GetQueueAsync(p.Item("BK-011").Id));
            T.Equal(ReservationStatus.Ready, q2[0].Status);
        });
        T.Test("Emma has five active loans; Jack has none; Ava has no unpaid fines", () =>
        {
            var loans = p.Get<LoanQueryService>();
            T.Equal(5, loans.GetAccount(p.Patron("LIB-1005").Id)!.ActiveLoans.Count);
            T.Equal(0, loans.GetAccount(p.Patron("LIB-1010").Id)!.ActiveLoans.Count);
            T.Equal(0m, loans.GetAccount(p.Patron("LIB-1001").Id)!.UnpaidFines);
        });
        T.Test("Daniel has $6.00 + $2.50 accruing and an earlier unpaid $3.00 fine", () =>
        {
            var a = p.Get<LoanQueryService>().GetAccount(p.Patron("LIB-1004").Id)!;
            T.Equal(8.50m, a.AccruingFines);
            T.True(a.UnpaidFines >= 3.00m, "unpaid fines: " + a.UnpaidFines);
        });
        T.Test("about 20 notifications are seeded, including Chloe's two-record example", () =>
        {
            var n = p.Store.Read(d => d.Notifications.Count);
            T.True(n is >= 18 and <= 26, "notifications: " + n);
            T.True(p.Store.Read(d => d.Notifications.Count(x => x.BorrowerId == p.Patron("LIB-1003").Id && x.Channel == NotificationChannel.Sms)) > 0);
        });
        T.Test("seeding twice with the same clock gives the same loan history (fixed random seed)", () =>
        {
            using var q = new TestPlatform();
            string Sig(TestPlatform x) => string.Join("|", x.Store.Read(d => d.Loans.Select(l => $"{d.FindItem(l.ItemId)!.LibraryCode}:{l.BorrowedOn}:{l.ReturnedOn}:{l.FineAmount}").ToList()));
            T.Equal(Sig(p), Sig(q));
        });
        T.Test("reset deletes everything and seeds again", () =>
        {
            using var q = new TestPlatform();
            q.Inventory.AddCategory("Extra", ItemType.Book);
            q.Get<SeedRunner>().Reset();
            T.Equal(9, q.Store.Read(d => d.Categories.Count));
        });
        T.Test("SeedIfEmpty does nothing when data exists", () => T.False(p.Get<SeedRunner>().SeedIfEmpty()));
    }
}
