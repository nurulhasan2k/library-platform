using Library.Core.Contracts;

namespace Library.Core.Services;

// TEMPORARY in-memory demo data (Person B).
// Person A's database is not merged yet, so Person B's services read items, patrons and loans from here.
// When A's DbContext lands, replace the calls to this class with database queries.

public sealed class DemoItem
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public int BranchId { get; init; } = 1;
    public ItemStatus Status { get; set; }
}

public sealed class DemoLoan
{
    public required int LoanId { get; init; }
    public required int ItemId { get; init; }
    public required int BorrowerId { get; init; }
    public required DateOnly BorrowedOn { get; init; }
    public required DateOnly DueOn { get; init; }
    public bool Returned { get; set; }
}

public sealed record DemoPatron(
    int Id,
    string CardNumber,
    string Pin,
    string Name,
    NotificationChannel Channel,
    string Email,
    string Phone);

public sealed class DemoLibraryStore
{
    private readonly object _sync = new();
    private readonly List<DemoItem> _items = new();
    private readonly List<DemoLoan> _loans = new();
    private readonly List<DemoPatron> _patrons = new();
    private int _nextLoanId = 1;

    public DemoLibraryStore(IClock clock)
    {
        var today = clock.Today;

        // Every patron has PIN 1234 (demo only).
        _patrons.Add(new DemoPatron(1, "1001", "1234", "Alice Smith", NotificationChannel.Email, "alice@example.test", "+61 491 570 001"));
        _patrons.Add(new DemoPatron(2, "1002", "1234", "Bob Jones", NotificationChannel.Sms, "bob@example.test", "+61 491 570 002"));
        _patrons.Add(new DemoPatron(3, "1003", "1234", "Carol Taylor", NotificationChannel.Both, "carol@example.test", "+61 491 570 003"));
        _patrons.Add(new DemoPatron(4, "1004", "1234", "Dan Brown", NotificationChannel.Email, "dan@example.test", "+61 491 570 004"));
        _patrons.Add(new DemoPatron(5, "1005", "1234", "Erin Wilson", NotificationChannel.Sms, "erin@example.test", "+61 491 570 005"));

        _items.Add(new DemoItem { Id = 101, Code = "LIB-0001", Name = "Dune", Status = ItemStatus.Borrowed });
        _items.Add(new DemoItem { Id = 102, Code = "LIB-0002", Name = "Clean Code", Status = ItemStatus.Borrowed });
        _items.Add(new DemoItem { Id = 103, Code = "LIB-0003", Name = "Abbey Road (CD)", Status = ItemStatus.Borrowed });
        _items.Add(new DemoItem { Id = 104, Code = "LIB-0004", Name = "Lego City Set", Status = ItemStatus.Available });
        _items.Add(new DemoItem { Id = 105, Code = "LIB-0005", Name = "The Hobbit", Status = ItemStatus.Available });
        _items.Add(new DemoItem { Id = 106, Code = "LIB-0006", Name = "Chess Set", Status = ItemStatus.Damaged });

        // Dan: Dune (due in 10 days) and Clean Code (due in 2 days). Erin: Abbey Road (5 days overdue).
        AddLoan(101, 4, today.AddDays(-4), today.AddDays(10));
        AddLoan(102, 4, today.AddDays(-12), today.AddDays(2));
        AddLoan(103, 5, today.AddDays(-19), today.AddDays(-5));
    }

    public IReadOnlyList<DemoItem> Items => _items;
    public IReadOnlyList<DemoPatron> Patrons => _patrons;

    public IReadOnlyList<DemoLoan> LoansSnapshot()
    {
        lock (_sync) return _loans.ToList();
    }

    public DemoItem? FindItem(int id) => _items.FirstOrDefault(i => i.Id == id);

    public DemoItem? FindItemByCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var c = code.Trim();
        return _items.FirstOrDefault(i => string.Equals(i.Code, c, StringComparison.OrdinalIgnoreCase));
    }

    public DemoPatron? FindPatron(int id) => _patrons.FirstOrDefault(p => p.Id == id);

    public DemoPatron? FindPatronByCard(string? card)
    {
        if (string.IsNullOrWhiteSpace(card)) return null;
        var c = card.Trim();
        return _patrons.FirstOrDefault(p => p.CardNumber == c);
    }

    public DemoLoan? FindLoan(int loanId)
    {
        lock (_sync) return _loans.FirstOrDefault(l => l.LoanId == loanId);
    }

    public DemoLoan? ActiveLoanForItem(int itemId)
    {
        lock (_sync) return _loans.FirstOrDefault(l => l.ItemId == itemId && !l.Returned);
    }

    public DemoLoan AddLoan(int itemId, int borrowerId, DateOnly borrowedOn, DateOnly dueOn)
    {
        lock (_sync)
        {
            var loan = new DemoLoan
            {
                LoanId = _nextLoanId++,
                ItemId = itemId,
                BorrowerId = borrowerId,
                BorrowedOn = borrowedOn,
                DueOn = dueOn
            };
            _loans.Add(loan);
            return loan;
        }
    }

    public void MarkReturned(DemoLoan loan)
    {
        lock (_sync) loan.Returned = true;
    }
}
