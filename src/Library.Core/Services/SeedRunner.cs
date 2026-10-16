using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Models;
using Microsoft.Extensions.Logging;

namespace Library.Core.Services;

/// <summary>
/// Creates the repeatable demo data described in docs/SEED-SCENARIOS.md. Every date is an offset from "today" (D) so the
/// due-soon, overdue and fine scenarios work whenever the app is reset. A fixed random seed (2026) makes the filler loan
/// history identical every time.
/// </summary>
public sealed class SeedRunner(ILibraryStore store, IClock clock, NotificationService notifications, ILogger<SeedRunner> log)
{
    public const int RandomSeed = 2026;

    /// <summary>Seeds only when the data file is empty. Returns true if it seeded.</summary>
    public bool SeedIfEmpty()
    {
        if (!store.IsEmpty) return false;
        Seed();
        return true;
    }

    /// <summary>Deletes everything and seeds again (the --reset-db command).</summary>
    public void Reset()
    {
        store.Clear();
        Seed();
    }

    public void Seed()
    {
        store.Write(data => Build(data, clock.Today));
        log.LogInformation("Demo data created (dates relative to {Today}).", clock.Today);
    }

    // =====================================================================================================

    private void Build(LibraryData data, DateOnly today)
    {
        DateTime At(int offset, int hour = 10) => today.AddDays(offset).ToDateTime(new TimeOnly(hour, 0));
        DateOnly Day(int offset) => today.AddDays(offset);
        var rng = new Random(RandomSeed);

        // ---- 1. branches, hours, desks -------------------------------------------------------------
        OpeningHours H(DayOfWeek d, int? o, int? c) => new(d, o is null ? null : new TimeOnly(o.Value, 0), c is null ? null : new TimeOnly(c.Value, 0));
        Branch AddBranch(string code, string name, string addr, string phone, params OpeningHours[] hours)
        {
            var b = new Branch { Id = data.TakeId(), Code = code, Name = name, Address = addr, Phone = phone, Hours = hours.ToList() };
            b.Desks.Add(new Desk { Id = data.TakeId(), Name = "Desk 1" });
            b.Desks.Add(new Desk { Id = data.TakeId(), Name = "Desk 2" });
            data.Branches.Add(b);
            return b;
        }
        const DayOfWeek Mo = DayOfWeek.Monday, Tu = DayOfWeek.Tuesday, We = DayOfWeek.Wednesday, Th = DayOfWeek.Thursday,
            Fr = DayOfWeek.Friday, Sa = DayOfWeek.Saturday, Su = DayOfWeek.Sunday;
        var central = AddBranch("CENTRAL", "Central Library", "1 Library Square", "(08) 5550 0101",
            H(Mo, 9, 18), H(Tu, 9, 18), H(We, 9, 18), H(Th, 9, 18), H(Fr, 9, 18), H(Sa, 10, 16), H(Su, null, null));
        var north = AddBranch("NORTH", "Northside Library", "22 Northside Road", "(08) 5550 0102",
            H(Mo, null, null), H(Tu, 10, 17), H(We, 10, 17), H(Th, 10, 17), H(Fr, 10, 17), H(Sa, 10, 14), H(Su, null, null));
        var river = AddBranch("RIVER", "Riverside Library", "5 Riverbank Walk", "(08) 5550 0103",
            H(Mo, 9, 17), H(Tu, 9, 17), H(We, 9, 17), H(Th, 9, 17), H(Fr, 9, 17), H(Sa, null, null), H(Su, 12, 16));

        // ---- 2. staff -------------------------------------------------------------------------------
        User AddUser(string name, string pw, UserRole role, params int[] branches)
        {
            var u = new User { Id = data.TakeId(), Username = name, PasswordHash = PasswordHasher.Hash(pw), Role = role, BranchIds = branches.ToList() };
            data.Users.Add(u);
            return u;
        }
        var admin = AddUser("admin", "admin123", UserRole.Admin);
        var reception = AddUser("reception", "reception123", UserRole.Reception, central.Id, north.Id);
        AddUser("reception.river", "reception123", UserRole.Reception, river.Id);
        AddUser("manager", "manager123", UserRole.Manager);

        // ---- 3. categories --------------------------------------------------------------------------
        void AddCats(ItemType t, params string[] names)
        {
            foreach (var n in names) data.Categories.Add(new Category { Id = data.TakeId(), Name = n, ItemType = t });
        }
        AddCats(ItemType.Book, "Science Fiction", "Fantasy", "Programming", "History");
        AddCats(ItemType.Music, "Rock", "Jazz", "Classical");
        AddCats(ItemType.Toy, "Puzzle", "Building");
        Category Cat(ItemType t, string n) => data.Categories.First(c => c.ItemType == t && c.Name == n);

        // ---- 4. items (45) --------------------------------------------------------------------------
        var items = new Dictionary<string, Item>();
        void B(string code, string name, string author, string cat, Branch br, string desc = "")
        {
            var c = Cat(ItemType.Book, cat);
            Add(new Book { Author = author, Genre = c.Name }, code, name, desc, br, c);
        }
        void M(string code, string name, string artist, int year, string cat, Branch br, string desc = "")
        {
            Add(new Music { Artist = artist, Year = year }, code, name, desc, br, Cat(ItemType.Music, cat));
        }
        void T(string code, string name, string cat, int age, Branch br, string desc = "")
        {
            var c = Cat(ItemType.Toy, cat);
            Add(new Toy { ToyType = c.Name, RecommendedAge = age }, code, name, desc, br, c);
        }
        void Add(Item item, string code, string name, string desc, Branch br, Category c)
        {
            item.Id = data.TakeId(); item.LibraryCode = code; item.Name = name; item.Description = desc;
            item.BranchId = br.Id; item.CategoryId = c.Id; item.Status = ItemStatus.Available;
            data.Items.Add(item);
            items[code] = item;
        }

        // CENTRAL: 10 books, 5 music, 3 toys
        B("BK-001", "Dune", "Frank Herbert", "Science Fiction", central, "Epic science fiction set on the desert planet Arrakis.");
        B("BK-002", "Neuromancer", "William Gibson", "Science Fiction", central, "The cyberpunk classic.");
        B("BK-003", "The Name of the Wind", "Patrick Rothfuss", "Fantasy", central);
        B("BK-010", "The Pragmatic Programmer", "Andrew Hunt and David Thomas", "Programming", central, "Your journey to mastery.");
        B("BK-011", "Clean Code", "Robert C. Martin", "Programming", central, "A handbook of agile software craftsmanship.");
        B("BK-020", "The Hobbit", "J. R. R. Tolkien", "Fantasy", central);
        B("BK-021", "Foundation", "Isaac Asimov", "Science Fiction", central);
        B("BK-030", "Piranesi", "Susanna Clarke", "Fantasy", central);
        B("BK-031", "A Short History of Nearly Everything", "Bill Bryson", "History", central);
        B("BK-032", "Project Hail Mary", "Andy Weir", "Science Fiction", central);
        M("MU-001", "Rumours", "Fleetwood Mac", 1977, "Rock", central);
        M("MU-002", "A Love Supreme", "John Coltrane", 1965, "Jazz", central);
        M("MU-003", "The Dark Side of the Moon", "Pink Floyd", 1973, "Rock", central);
        M("MU-005", "Symphony No. 5", "Ludwig van Beethoven", 1808, "Classical", central);
        M("MU-010", "Kind of Blue", "Miles Davis", 1959, "Jazz", central);
        T("TY-001", "Speed cube", "Puzzle", 8, central);
        T("TY-002", "Classic brick box", "Building", 4, central);
        T("TY-003", "1000-piece map puzzle", "Puzzle", 8, central);
        // NORTH: 7 books, 4 music, 3 toys
        B("BK-033", "The Martian", "Andy Weir", "Science Fiction", north);
        B("BK-005", "Sapiens", "Yuval Noah Harari", "History", north);
        B("BK-006", "Clean Architecture", "Robert C. Martin", "Programming", north);
        B("BK-007", "The Fellowship of the Ring", "J. R. R. Tolkien", "Fantasy", north);
        B("BK-008", "Snow Crash", "Neal Stephenson", "Science Fiction", north);
        B("BK-009", "Guns, Germs, and Steel", "Jared Diamond", "History", north);
        B("BK-012", "Eloquent JavaScript", "Marijn Haverbeke", "Programming", north);
        M("MU-004", "The Four Seasons", "Antonio Vivaldi", 1725, "Classical", north);
        M("MU-006", "Bitches Brew", "Miles Davis", 1970, "Jazz", north);
        M("MU-007", "Led Zeppelin IV", "Led Zeppelin", 1971, "Rock", north);
        M("MU-012", "Abbey Road", "The Beatles", 1969, "Rock", north);
        T("TY-004", "Solar system jigsaw", "Puzzle", 5, north);
        T("TY-005", "Wooden building blocks", "Building", 3, north);
        T("TY-010", "Marble run set", "Building", 6, north);
        // RIVER: 7 books, 3 music, 3 toys
        B("BK-013", "The Left Hand of Darkness", "Ursula K. Le Guin", "Science Fiction", river);
        B("BK-014", "Mistborn: The Final Empire", "Brandon Sanderson", "Fantasy", river);
        B("BK-015", "Refactoring", "Martin Fowler", "Programming", river);
        B("BK-016", "The Silk Roads", "Peter Frankopan", "History", river);
        B("BK-017", "Hyperion", "Dan Simmons", "Science Fiction", river);
        B("BK-018", "Introduction to Algorithms", "Thomas H. Cormen", "Programming", river);
        B("BK-040", "An old atlas", "Various", "History", river, "Withdrawn from the collection.");
        M("MU-008", "The Planets", "Gustav Holst", 1918, "Classical", river);
        M("MU-009", "Nevermind", "Nirvana", 1991, "Rock", river);
        M("MU-011", "Time Out", "The Dave Brubeck Quartet", 1959, "Jazz", river);
        T("TY-006", "Tangram set", "Puzzle", 6, river);
        T("TY-007", "Magnetic construction set", "Building", 5, river);
        T("TY-008", "Wooden train set", "Building", 3, river);

        // Statuses that are not Borrowed (borrowed ones are set when the active loans are created below)
        items["MU-010"].Status = ItemStatus.Damaged;
        items["BK-009"].Status = ItemStatus.Damaged;
        items["TY-007"].Status = ItemStatus.Damaged;
        items["BK-040"].Status = ItemStatus.Destroy;
        items["TY-001"].Status = ItemStatus.Destroy;

        // ---- 5. patrons (10) ------------------------------------------------------------------------
        var patrons = new Dictionary<string, Borrower>();
        void P(string card, string name, string pin, NotificationChannel ch, string addr)
        {
            var slug = name.ToLowerInvariant().Replace(' ', '.');
            var n = int.Parse(card[4..]) - 1000;
            var b = new Borrower
            {
                Id = data.TakeId(), Name = name, CardNumber = card, PinHash = PasswordHasher.Hash(pin), PreferredChannel = ch,
                Email = $"{slug}@example.com", Phone = $"0400 000 {100 + n:D3}", Address = addr
            };
            data.Borrowers.Add(b);
            patrons[card] = b;
        }
        P("LIB-1001", "Ava Thompson", "1111", NotificationChannel.Email, "12 Wattle Street");
        P("LIB-1002", "Ben Carter", "2222", NotificationChannel.Sms, "7 Fern Court");
        P("LIB-1003", "Chloe Nguyen", "3333", NotificationChannel.Both, "88 Harbour Road");
        P("LIB-1004", "Daniel Okafor", "4444", NotificationChannel.Email, "3 Elm Lane");
        P("LIB-1005", "Emma Rossi", "5555", NotificationChannel.Email, "41 Park Avenue");
        P("LIB-1006", "Farid Hassan", "6666", NotificationChannel.Sms, "19 Cedar Close");
        P("LIB-1007", "Grace Liu", "7777", NotificationChannel.Email, "5 Maple Drive");
        P("LIB-1008", "Hugo Martin", "8888", NotificationChannel.Sms, "27 Birch Way");
        P("LIB-1009", "Isla Brown", "9999", NotificationChannel.Both, "60 Willow Street");
        P("LIB-1010", "Jack Wilson", "0000", NotificationChannel.Email, "2 Oak Place");

        // ---- loans ------------------------------------------------------------------------------------
        var busy = new Dictionary<int, List<(DateOnly S, DateOnly E)>>();
        bool Free(int itemId, DateOnly s, DateOnly e) => !busy.TryGetValue(itemId, out var l) || l.All(x => e < x.S || s > x.E);
        void Reserve(int itemId, DateOnly s, DateOnly e) { if (!busy.TryGetValue(itemId, out var l)) busy[itemId] = l = new(); l.Add((s, e)); }

        Loan MakeLoan(string code, string card, int borrowedOffset, int dueOffset, int? returnedOffset = null, int? returnBranch = null,
            Branch? at = null, LoanChannel channel = LoanChannel.Desk, bool damaged = false)
        {
            var item = items[code];
            var br = at ?? data.FindBranch(item.BranchId)!;
            var loan = new Loan
            {
                Id = data.TakeId(), ItemId = item.Id, BorrowerId = patrons[card].Id, BranchId = br.Id, Channel = channel,
                DeskId = channel == LoanChannel.Desk ? br.Desks[0].Id : null,
                BorrowedOn = Day(borrowedOffset), DueOn = Day(dueOffset)
            };
            if (returnedOffset is { } r)
            {
                loan.ReturnedOn = Day(r);
                loan.ReturnBranchId = returnBranch ?? br.Id;
                loan.ReturnedDamaged = damaged;
                loan.FineAmount = Math.Max(0, r - dueOffset) * 0.5m;
            }
            else item.Status = ItemStatus.Borrowed;
            data.Loans.Add(loan);
            Reserve(item.Id, loan.BorrowedOn, loan.ReturnedOn ?? today.AddYears(5));
            return loan;
        }

        // ---- 6. active loans at seed time ---------------------------------------------------------------
        var avaLoan = MakeLoan("BK-003", "LIB-1001", -5, 9);
        var danielHobbit = MakeLoan("BK-020", "LIB-1004", -26, -12);
        var danielFoundation = MakeLoan("BK-021", "LIB-1004", -19, -5);
        var emmaLoan = MakeLoan("BK-010", "LIB-1005", -8, 6);
        MakeLoan("BK-005", "LIB-1005", -8, 6);
        MakeLoan("BK-015", "LIB-1005", -8, 6);
        MakeLoan("MU-006", "LIB-1005", -8, 6);
        MakeLoan("TY-004", "LIB-1005", -8, 6);
        var faridPuzzle = MakeLoan("TY-003", "LIB-1006", -12, 2);
        var faridVivaldi = MakeLoan("MU-004", "LIB-1006", -9, 5);

        // Daniel's earlier unpaid fine of $3.00 (6 days late)
        var danielOld = MakeLoan("BK-008", "LIB-1004", -60, -46, returnedOffset: -40);

        // ---- 7. loan history (~150 returned loans over 180 days) ------------------------------------------
        var never = new HashSet<string> { "BK-016", "BK-018", "MU-009", "TY-006", "TY-008" };
        var scenarioHold = new HashSet<string> { "BK-011", "BK-030", "BK-031", "BK-032", "BK-033", "MU-012", "TY-010", "TY-005", "MU-003" };
        var pool = data.Items.Where(i => !never.Contains(i.LibraryCode) && i.Status != ItemStatus.Destroy).ToList();
        var patronList = patrons.Values.Where(p => p.CardNumber != "LIB-1010").ToList();
        var monthly = new[] { 20, 22, 25, 27, 28, 28 };
        var history = new List<Loan>();

        double Weight(Item i)
        {
            var cat = data.Categories.FirstOrDefault(c => c.Id == i.CategoryId)?.Name;
            return cat is "Science Fiction" or "Rock" ? 3 : 1;
        }
        Item PickItem(ItemType type)
        {
            var cand = pool.Where(i => i.Type == type).ToList();
            var total = cand.Sum(Weight);
            var x = rng.NextDouble() * total;
            foreach (var i in cand) { x -= Weight(i); if (x <= 0) return i; }
            return cand[^1];
        }

        for (var window = 0; window < monthly.Length; window++)
        {
            var made = 0;
            var attempts = 0;
            while (made < monthly[window] && attempts++ < 4000)
            {
                var roll = rng.NextDouble();
                var type = roll < 0.55 ? ItemType.Book : roll < 0.80 ? ItemType.Music : ItemType.Toy;
                var item = PickItem(type);
                var startOffset = -180 + window * 30 + rng.Next(0, 30);
                var late = rng.NextDouble() < 0.20;
                var length = late ? rng.Next(15, 29) : rng.Next(3, 15);
                var endOffset = startOffset + length;
                if (endOffset > -1) continue;
                var s = Day(startOffset); var e = Day(endOffset);
                if (!Free(item.Id, s, e)) continue;

                var br = data.FindBranch(item.BranchId)!;
                var patron = patronList[rng.Next(patronList.Count)];
                var kiosk = rng.NextDouble() < 0.20;
                var elsewhere = rng.NextDouble() < 0.15;
                var returnBranch = elsewhere ? data.Branches.Where(b => b.Id != br.Id).OrderBy(_ => rng.Next()).First() : br;
                var loan = new Loan
                {
                    Id = data.TakeId(), ItemId = item.Id, BorrowerId = patron.Id, BranchId = br.Id,
                    Channel = kiosk ? LoanChannel.Kiosk : LoanChannel.Desk,
                    DeskId = kiosk ? null : br.Desks[rng.Next(br.Desks.Count)].Id,
                    BorrowedOn = s, DueOn = s.AddDays(14), ReturnedOn = e, ReturnBranchId = returnBranch.Id
                };
                loan.FineAmount = Math.Max(0, e.DayNumber - loan.DueOn.DayNumber) * 0.5m;
                if (loan.FineAmount > 0)
                {
                    var ageDays = today.DayNumber - e.DayNumber;
                    var paid = patron.CardNumber == "LIB-1001" || rng.NextDouble() < (ageDays > 60 ? 0.85 : 0.40);
                    loan.FinePaid = paid;
                    if (paid) loan.FinePaidOn = e.AddDays(Math.Min(rng.Next(0, 6), Math.Max(0, today.DayNumber - e.DayNumber)));
                }
                data.Loans.Add(loan);
                Reserve(item.Id, s, e);
                history.Add(loan);
                made++;
            }
        }
        for (var i = 10; i < history.Count; i += 37) history[i].ReturnedDamaged = true; // 4 returned damaged (repaired since)
        _ = scenarioHold;

        // ---- 8. transfers ---------------------------------------------------------------------------------
        Transfer Tr(string code, Branch from, Branch to, TransferStatus status, int requested, int? dispatched, int? received, int? cancelled, User by, string? note = null)
        {
            var t = new Transfer
            {
                Id = data.TakeId(), ItemId = items[code].Id, FromBranchId = from.Id, ToBranchId = to.Id, Status = status,
                RequestedByUserId = by.Id, RequestedAt = At(requested), Note = note,
                DispatchedAt = dispatched is { } d ? At(d, 14) : null,
                ReceivedAt = received is { } r ? At(r, 11) : null,
                CancelledAt = cancelled is { } c ? At(c, 12) : null
            };
            data.Transfers.Add(t);
            return t;
        }
        Tr("BK-030", central, river, TransferStatus.Requested, -1, null, null, null, reception, "Requested by a Riverside patron.");
        Tr("MU-012", north, central, TransferStatus.InTransit, -3, -2, null, null, reception, "For the Beatles display.");
        Tr("TY-010", river, north, TransferStatus.Received, -22, -20, -18, null, admin);
        items["TY-010"].BranchId = north.Id;
        Tr("BK-031", central, north, TransferStatus.Cancelled, -10, null, null, -9, reception, "Cancelled: the patron found a copy elsewhere.");

        // ---- 9. reservations (holds) ----------------------------------------------------------------------
        Reservation Res(string code, string card, ReservationStatus st, int created, int? ready = null, int? expires = null, int? closed = null, ReservationSource src = ReservationSource.Web)
        {
            var r = new Reservation
            {
                Id = data.TakeId(), ItemId = items[code].Id, BorrowerId = patrons[card].Id, Status = st, Source = src, CreatedAt = At(created, 9),
                ReadyAt = ready is { } rd ? At(rd, 9) : null, ExpiresAt = expires is { } ex ? At(ex, 17) : null, ClosedAt = closed is { } cl ? At(cl, 12) : null
            };
            data.Reservations.Add(r);
            return r;
        }
        Res("BK-010", "LIB-1007", ReservationStatus.Waiting, -4, src: ReservationSource.Kiosk);
        Res("BK-010", "LIB-1008", ReservationStatus.Waiting, -3);
        Res("BK-010", "LIB-1009", ReservationStatus.Waiting, -1, src: ReservationSource.Desk);
        var benHold = Res("BK-011", "LIB-1002", ReservationStatus.Ready, -6, ready: -1, expires: 2);
        var chloeHold = Res("TY-005", "LIB-1003", ReservationStatus.Expired, -8, ready: -5, expires: -2, closed: -2);
        Res("MU-003", "LIB-1008", ReservationStatus.Cancelled, -10, closed: -7);
        Res("MU-010", "LIB-1007", ReservationStatus.Waiting, -2);

        // ---- 10. notifications (about 20 over the last 30 days) ------------------------------------------
        void N(NotificationType type, string card, string code, Loan? loan, Reservation? res, int offset, int hour = 10)
        {
            notifications.CreateInto(data, new NotificationRequest(type, patrons[card].Id, items[code].Id, loan?.Id, res?.Id), At(offset, hour));
        }
        N(NotificationType.Borrowed, "LIB-1001", "BK-003", avaLoan, null, -5);
        N(NotificationType.Borrowed, "LIB-1004", "BK-020", danielHobbit, null, -26);
        N(NotificationType.Borrowed, "LIB-1004", "BK-021", danielFoundation, null, -19);
        N(NotificationType.Borrowed, "LIB-1005", "BK-010", emmaLoan, null, -8);
        N(NotificationType.Borrowed, "LIB-1006", "TY-003", faridPuzzle, null, -12);
        N(NotificationType.Borrowed, "LIB-1006", "MU-004", faridVivaldi, null, -9);
        var chloeLoans = history.Where(l => l.BorrowerId == patrons["LIB-1003"].Id && l.BorrowedOn >= Day(-28)).Take(2).ToList();
        foreach (var l in chloeLoans) // Chloe prefers "Both": two records per event
            notifications.CreateInto(data, new NotificationRequest(NotificationType.Borrowed, l.BorrowerId, l.ItemId, l.Id), l.BorrowedOn.ToDateTime(new TimeOnly(11, 0)));
        N(NotificationType.DueSoon, "LIB-1004", "BK-020", danielHobbit, null, -14);
        N(NotificationType.DueSoon, "LIB-1004", "BK-021", danielFoundation, null, -7);
        N(NotificationType.FineAccruing, "LIB-1004", "BK-020", danielHobbit, null, -11);
        N(NotificationType.FineAccruing, "LIB-1004", "BK-020", danielHobbit, null, -1);
        N(NotificationType.FineAccruing, "LIB-1004", "BK-021", danielFoundation, null, -2);
        N(NotificationType.ReservationReady, "LIB-1003", "TY-005", null, chloeHold, -5);
        N(NotificationType.ReservationReady, "LIB-1002", "BK-011", null, benHold, -1);
        N(NotificationType.ReservationExpired, "LIB-1003", "TY-005", null, chloeHold, -2);

        _ = danielOld;
    }
}
