using Library.Core.Contracts;
using Library.Core.Models;
using Library.Core.Services;

namespace Library.Tests;

public static class InventoryTests
{
    public static void Run()
    {
        T.Section("Items, categories and branches (admin)");
        using var p = new TestPlatform();
        var inv = p.Inventory;
        int Central() => p.Branch("CENTRAL").Id;
        int Cat(string name, ItemType t) => p.Store.Read(d => d.Categories.First(c => c.Name == name && c.ItemType == t).Id);

        T.Test("adds a book with a category; genre follows the category", () =>
        {
            var b = (Book)inv.AddItem(new Book { Name = "New Book", LibraryCode = "bk-900", Author = "A. Writer", BranchId = Central(), CategoryId = Cat("Fantasy", ItemType.Book) });
            T.Equal("BK-900", b.LibraryCode);
            T.Equal("Fantasy", b.Genre);
            T.Equal(ItemStatus.Available, b.Status);
        });
        T.Test("rejects a duplicate library code, ignoring case", () =>
            T.Rejects("already used", () => inv.AddItem(new Book { Name = "X", LibraryCode = "bk-001", Author = "A", BranchId = Central(), CategoryId = Cat("Fantasy", ItemType.Book) })));
        T.Test("rejects a category of the wrong type", () =>
            T.Rejects("category", () => inv.AddItem(new Music { Name = "X", LibraryCode = "MU-900", Artist = "A", Year = 2000, BranchId = Central(), CategoryId = Cat("Fantasy", ItemType.Book) })));
        T.Test("rejects missing required fields", () =>
        {
            T.Rejects("Author", () => inv.AddItem(new Book { Name = "X", LibraryCode = "BK-901", BranchId = Central(), CategoryId = Cat("Fantasy", ItemType.Book) }));
            T.Rejects("Year", () => inv.AddItem(new Music { Name = "X", LibraryCode = "MU-901", Artist = "A", Year = 5, BranchId = Central(), CategoryId = Cat("Rock", ItemType.Music) }));
            T.Rejects("Age", () => inv.AddItem(new Toy { Name = "X", LibraryCode = "TY-901", RecommendedAge = 500, BranchId = Central(), CategoryId = Cat("Puzzle", ItemType.Toy) }));
        });
        T.Test("updating keeps status and branch, and can't change the type", () =>
        {
            var existing = (Book)p.Item("BK-001");
            var edit = new Book { Id = existing.Id, Name = "Dune (Special)", LibraryCode = existing.LibraryCode, Author = existing.Author, BranchId = p.Branch("RIVER").Id, CategoryId = existing.CategoryId };
            var saved = inv.UpdateItem(edit);
            T.Equal("Dune (Special)", saved.Name);
            T.Equal(Central(), saved.BranchId, "branch must not change by edit");
            T.Rejects("type", () => inv.UpdateItem(new Toy { Id = existing.Id, Name = "x", LibraryCode = "BK-001", ToyType = "Puzzle", RecommendedAge = 3, BranchId = Central() }));
        });
        T.Test("changing status works, but not to borrowed and not while on loan", () =>
        {
            T.Equal(ItemStatus.Damaged, inv.ChangeItemStatus(p.Item("BK-002").Id, ItemStatus.Damaged).Status);
            T.Rejects("only when", () => inv.ChangeItemStatus(p.Item("BK-002").Id, ItemStatus.Borrowed));
            T.Rejects("out on loan", () => inv.ChangeItemStatus(p.Item("BK-020").Id, ItemStatus.Damaged));
        });
        T.Test("an item in transit can't change status", () =>
            T.Rejects("transfer", () => inv.ChangeItemStatus(p.Item("MU-012").Id, ItemStatus.Damaged)));
        T.Test("deleting is refused for borrowed items and items with history; allowed for a fresh item", () =>
        {
            T.Rejects("on loan", () => inv.DeleteItem(p.Item("BK-020").Id));
            var withHistory = p.Store.Read(d => d.Loans.First(l => !l.IsActive).ItemId);
            T.Rejects("history", () => inv.DeleteItem(withHistory));
            var fresh = inv.AddItem(new Toy { Name = "Temp", LibraryCode = "TY-950", ToyType = "Puzzle", RecommendedAge = 3, BranchId = Central(), CategoryId = Cat("Puzzle", ItemType.Toy) });
            inv.DeleteItem(fresh.Id);
            T.True(inv.GetItem(fresh.Id) is null);
        });
        T.Test("repairing a damaged item promotes the first waiting hold (hook)", () =>
        {
            var grace = p.Patron("LIB-1007");
            var notesBefore = p.Store.Read(d => d.Notifications.Count(n => n.BorrowerId == grace.Id));
            inv.ChangeItemStatus(p.Item("MU-010").Id, ItemStatus.Available);
            var hold = p.Store.Read(d => d.Reservations.First(r => r.ItemId == p.Item("MU-010").Id));
            T.Equal(ReservationStatus.Ready, hold.Status);
            T.True(p.Store.Read(d => d.Notifications.Count(n => n.BorrowerId == grace.Id)) > notesBefore, "Grace should be notified");
        });
        T.Test("categories: add, rename (updates items), refuse delete while used", () =>
        {
            var c = inv.AddCategory("Cooking", ItemType.Book);
            T.Rejects("already", () => inv.AddCategory("cooking", ItemType.Book));
            inv.AddItem(new Book { Name = "Pasta", LibraryCode = "BK-960", Author = "Chef", BranchId = Central(), CategoryId = c.Id });
            inv.RenameCategory(c.Id, "Food");
            T.Equal("Food", ((Book)p.Item("BK-960")).Genre);
            T.Rejects("still has items", () => inv.DeleteCategory(c.Id));
        });
        T.Test("branches: add with hours and desks; code must be unique; closing needs no items on loan", () =>
        {
            var b = inv.AddBranch(new Branch
            {
                Code = "east", Name = "East Library",
                Hours = Enum.GetValues<DayOfWeek>().Select(d => new OpeningHours(d, new TimeOnly(9, 0), new TimeOnly(17, 0))).ToList(),
                Desks = { new Desk { Name = "Desk A" } }
            });
            T.Equal("EAST", b.Code);
            T.True(b.Desks[0].Id > 0);
            T.Rejects("already used", () => inv.AddBranch(new Branch { Code = "EAST", Name = "Again", Desks = { new Desk { Name = "D" } } }));
            T.Rejects("desk", () => inv.AddBranch(new Branch { Code = "WEST", Name = "West" }));
            T.Rejects("after opening", () => inv.AddBranch(new Branch
            {
                Code = "BAD", Name = "Bad", Desks = { new Desk { Name = "D" } },
                Hours = { new OpeningHours(DayOfWeek.Monday, new TimeOnly(17, 0), new TimeOnly(9, 0)) }
            }));
        });
        T.Test("a branch with items can't be deleted", () => T.Rejects("items or history", () => inv.DeleteBranch(p.Branch("NORTH").Id)));
        T.Test("branch opening hours: open on a weekday, closed on its closed day", () =>
        {
            var north = p.Branch("NORTH");
            T.True(north.IsOpenAt(new DateTime(2026, 10, 14, 11, 0, 0)), "Wed 11:00 should be open");
            T.False(north.IsOpenAt(new DateTime(2026, 10, 12, 11, 0, 0)), "Monday is closed at North");
            T.False(north.IsOpenAt(new DateTime(2026, 10, 14, 18, 0, 0)), "after closing");
        });

        T.Section("Borrowers and staff");
        T.Test("a new borrower gets a card number and a PIN that works", () =>
        {
            var created = p.Borrowers.AddBorrower(new Borrower { Name = "Kim Lee", Email = "kim@example.com" });
            T.Equal("LIB-1011", created.Borrower.CardNumber);
            T.Equal(4, created.Pin.Length);
            var r = TestPlatform.Wait(p.Get<IPatronAuthService>().SignInAsync("LIB-1011", created.Pin));
            T.True(r.Success, r.Error ?? "");
        });
        T.Test("borrower validation: contact details and channel needs", () =>
        {
            T.Rejects("phone number or an email", () => p.Borrowers.AddBorrower(new Borrower { Name = "No Contact" }));
            T.Rejects("phone number is needed", () => p.Borrowers.AddBorrower(new Borrower { Name = "Sms", Email = "a@b.c", PreferredChannel = NotificationChannel.Sms }));
        });
        T.Test("a borrower with loan history can't be deleted; a new one can", () =>
        {
            T.Rejects("can't be deleted", () => p.Borrowers.DeleteBorrower(p.Patron("LIB-1001").Id));
            var b = p.Borrowers.AddBorrower(new Borrower { Name = "Temp", Email = "t@e.com" }).Borrower;
            p.Borrowers.DeleteBorrower(b.Id);
            T.True(p.Borrowers.GetBorrower(b.Id) is null);
        });
        T.Test("resetting a PIN clears a lockout", () =>
        {
            var jack = p.Patron("LIB-1010");
            var auth = p.Get<IPatronAuthService>();
            for (var i = 0; i < 3; i++) TestPlatform.Wait(auth.SignInAsync("LIB-1010", "9"));
            T.True(TestPlatform.Wait(auth.SignInAsync("LIB-1010", "0000")).LockedOut);
            var pin = p.Borrowers.ResetPin(jack.Id);
            T.True(TestPlatform.Wait(auth.SignInAsync("LIB-1010", pin)).Success);
        });
        T.Test("staff: login works, wrong password fails, hashes aren't plain text", () =>
        {
            var staff = p.Get<StaffService>();
            T.Equal(UserRole.Admin, staff.Authenticate("admin", "admin123")!.Role);
            T.True(staff.Authenticate("admin", "nope") is null);
            T.True(staff.Authenticate("ghost", "x") is null);
            T.False(p.User("admin").PasswordHash.Contains("admin123"));
        });
        T.Test("staff: reception.river is limited to one branch; the last admin can't be deleted", () =>
        {
            var u = p.User("reception.river");
            T.True(u.MayUseBranch(p.Branch("RIVER").Id));
            T.False(u.MayUseBranch(p.Branch("CENTRAL").Id));
            T.Rejects("last administrator", () => p.Get<StaffService>().DeleteUser(p.User("admin").Id, p.User("manager").Id));
        });
    }
}
