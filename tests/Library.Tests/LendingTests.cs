using Library.Core.Contracts;
using Library.Core.Models;
using Library.Core.Services;

namespace Library.Tests;

public static class LendingTests
{
    public static void Run()
    {
        T.Section("Lending and returning (the Assignment 2 rules, now per branch)");
        using var p = new TestPlatform();
        var central = p.Branch("CENTRAL").Id;
        var north = p.Branch("NORTH").Id;
        var ava = p.Patron("LIB-1001").Id;
        var jack = p.Patron("LIB-1010").Id;
        var lending = p.Lending;
        var ilending = (ILendingService)lending;
        p.Context.CurrentDeskId = p.Branch("CENTRAL").Desks[0].Id;

        T.Test("borrowing makes the item borrowed with a due date 14 days away", () =>
        {
            var r = TestPlatform.Wait(ilending.BorrowAsync("BK-001", ava, central));
            T.True(r.Success, r.Error ?? "");
            T.Equal(p.Clock.Today.AddDays(14), r.Receipt!.DueOn);
            T.Equal(ItemStatus.Borrowed, p.Item("BK-001").Status);
        });
        T.Test("a borrowed item can't be borrowed again", () =>
        {
            var r = TestPlatform.Wait(ilending.BorrowAsync("BK-001", jack, central));
            T.False(r.Success);
            T.True(r.Error!.Contains("borrowed"), r.Error);
        });
        T.Test("library codes are found ignoring case and spaces", () =>
            T.True(TestPlatform.Wait(ilending.BorrowAsync("  mu-001 ", ava, central)).Success));
        T.Test("an unknown code or borrower gives a clear message", () =>
        {
            T.True(TestPlatform.Wait(ilending.BorrowAsync("ZZ-999", ava, central)).Error!.Contains("No item has the library code"));
            T.True(TestPlatform.Wait(ilending.BorrowAsync("BK-002", 99999, central)).Error!.Contains("No borrower"));
        });
        T.Test("damaged and destroyed items can't be borrowed", () =>
        {
            T.True(TestPlatform.Wait(ilending.BorrowAsync("MU-010", ava, central)).Error!.Contains("damaged"));
            T.True(TestPlatform.Wait(ilending.BorrowAsync("TY-001", ava, central)).Error!.Contains("destroyed"));
        });
        T.Test("an item can only be lent at the branch where it is", () =>
        {
            var r = TestPlatform.Wait(ilending.BorrowAsync("BK-002", ava, north));
            T.False(r.Success);
            T.True(r.Error!.Contains("Central Library"), r.Error);
        });
        T.Test("an item in transit can't be borrowed", () =>
        {
            var r = TestPlatform.Wait(ilending.BorrowAsync("MU-012", ava, north));
            T.False(r.Success);
            T.True(r.Error!.Contains("in transit"), r.Error);
        });
        T.Test("an item held for another patron can't be borrowed; the holder can borrow it", () =>
        {
            var hugo = p.Patron("LIB-1008").Id;
            var blocked = TestPlatform.Wait(ilending.BorrowAsync("BK-011", hugo, central));
            T.False(blocked.Success);
            T.True(blocked.Error!.Contains("held for another patron"), blocked.Error);
            var ben = TestPlatform.Wait(ilending.BorrowAsync("BK-011", p.Patron("LIB-1002").Id, central));
            T.True(ben.Success, ben.Error ?? "");
            T.Equal(ReservationStatus.Fulfilled, p.Store.Read(d => d.Reservations.First(r => r.ItemId == p.Item("BK-011").Id).Status));
        });
        T.Test("borrowing at a desk records the desk; at a kiosk records the kiosk channel", () =>
        {
            var desk = p.Store.Read(d => d.Loans.First(l => l.ItemId == p.Item("MU-001").Id && l.IsActive));
            T.Equal(LoanChannel.Desk, desk.Channel);
            T.True(desk.DeskId is not null);
            p.Context.IsKiosk = true;
            TestPlatform.Wait(ilending.BorrowAsync("BK-002", ava, central));
            p.Context.IsKiosk = false;
            var kiosk = p.Store.Read(d => d.Loans.First(l => l.ItemId == p.Item("BK-002").Id && l.IsActive));
            T.Equal(LoanChannel.Kiosk, kiosk.Channel);
            T.True(kiosk.DeskId is null);
        });
        T.Test("borrowing records a (simulated) borrowed notification for the patron", () =>
            T.True(p.Store.Read(d => d.Notifications.Any(n => n.Type == NotificationType.Borrowed && n.BorrowerId == ava && n.ItemId == p.Item("MU-001").Id))));

        T.Test("returning on time costs nothing and makes the item available", () =>
        {
            var r = TestPlatform.Wait(ilending.ReturnAsync("MU-001", central, false));
            T.True(r.Success, r.Error ?? "");
            T.Equal(0m, r.FineAmount);
            T.Equal(ItemStatus.Available, p.Item("MU-001").Status);
        });
        T.Test("a fine is 50 cents per full day late", () =>
        {
            var r = TestPlatform.Wait(ilending.ReturnAsync("BK-020", central, false));
            T.Equal(12, r.DaysLate);
            T.Equal(6.00m, r.FineAmount);
            var loan = p.Store.Read(d => d.Loans.First(l => l.ItemId == p.Item("BK-020").Id && l.ReturnedOn != null && l.BorrowedOn == p.Clock.Today.AddDays(-26)));
            T.Equal(6.00m, loan.FineAmount);
            T.False(loan.FinePaid);
        });
        T.Test("the fine calculation boundaries", () =>
        {
            var due = new DateOnly(2026, 3, 15);
            T.Equal(0m, lending.CalculateFine(due, due));
            T.Equal(0m, lending.CalculateFine(due, due.AddDays(-3)));
            T.Equal(0.50m, lending.CalculateFine(due, due.AddDays(1)));
            T.Equal(7.00m, lending.CalculateFine(due, due.AddDays(14)));
        });
        T.Test("returning damaged marks the item damaged and does not start a hold queue", () =>
        {
            var r = TestPlatform.Wait(ilending.ReturnAsync("BK-021", central, true));
            T.True(r.Success);
            T.Equal(ItemStatus.Damaged, p.Item("BK-021").Status);
        });
        T.Test("returning an item that isn't out gives a clear message", () =>
            T.True(TestPlatform.Wait(ilending.ReturnAsync("BK-032", central, false)).Error!.Contains("isn't out on loan")));
        T.Test("returning at a different branch moves the item there", () =>
        {
            var r = TestPlatform.Wait(ilending.ReturnAsync("TY-003", north, false));
            T.True(r.Success, r.Error ?? "");
            T.True(r.ItemMovedBranch);
            T.Equal("Central Library", r.MovedFromBranchName);
            T.Equal("Northside Library", r.MovedToBranchName);
            T.Equal(north, p.Item("TY-003").BranchId);
            T.Equal(north, p.Store.Read(d => d.Loans.First(l => l.ItemId == p.Item("TY-003").Id && l.ReturnedOn == p.Clock.Today).ReturnBranchId!.Value));
        });
        T.Test("returning BK-010 makes Grace's hold Ready and notifies her; Hugo can't borrow it", () =>
        {
            var r = TestPlatform.Wait(ilending.ReturnAsync("BK-010", central, false));
            T.True(r.Success);
            var grace = p.Patron("LIB-1007");
            var hold = p.Store.Read(d => d.Reservations.First(x => x.ItemId == p.Item("BK-010").Id && x.BorrowerId == grace.Id));
            T.Equal(ReservationStatus.Ready, hold.Status);
            T.True(p.Store.Read(d => d.Notifications.Any(n => n.Type == NotificationType.ReservationReady && n.BorrowerId == grace.Id && n.ItemId == hold.ItemId)));
            var hugo = TestPlatform.Wait(ilending.BorrowAsync("BK-010", p.Patron("LIB-1008").Id, central));
            T.False(hugo.Success);
            T.True(TestPlatform.Wait(ilending.BorrowAsync("BK-010", grace.Id, central)).Success);
        });
        T.Test("the loan period and fine rate come from settings, not code", () =>
        {
            var opts = Microsoft.Extensions.Options.Options.Create(new Library.Core.Configuration.LendingOptions { LoanDays = 7, FinePerDay = 1.00m });
            var custom = new LendingService(p.Store, p.Clock, opts, p.Context, p.Get<IReservationService>(), p.Get<INotificationService>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<LendingService>.Instance);
            T.Equal(3.00m, custom.CalculateFine(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 4)));
            var receipt = TestPlatform.Wait(((ILendingService)custom).BorrowAsync("BK-032", ava, central)).Receipt!;
            T.Equal(p.Clock.Today.AddDays(7), receipt.DueOn);
        });
        T.Test("marking a fine paid works once; items with no fine can't be marked", () =>
        {
            var q = p.Get<LoanQueryService>();
            var unpaid = q.GetLoans(new LoanFilter(UnpaidFinesOnly: true)).First();
            q.MarkFinePaid(unpaid.Loan.Id);
            T.True(p.Store.Read(d => d.Loans.First(l => l.Id == unpaid.Loan.Id).FinePaid));
            T.Rejects("already", () => q.MarkFinePaid(unpaid.Loan.Id));
            var clean = p.Store.Read(d => d.Loans.First(l => !l.IsActive && l.FineAmount == 0)).Id;
            T.Rejects("no fine", () => q.MarkFinePaid(clean));
        });
        T.Test("the simulated clock moves 'today' for fines", () =>
        {
            using var q = new TestPlatform();
            q.Clock.Today = q.Clock.Today.AddDays(10);
            var late = TestPlatform.Wait(((ILendingService)q.Lending).ReturnAsync("BK-021", q.Branch("CENTRAL").Id, false));
            T.Equal(15, late.DaysLate);
            T.Equal(7.50m, late.FineAmount);
        });
    }
}
