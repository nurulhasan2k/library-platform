using System.ComponentModel.DataAnnotations;
using Library.Core.Contracts;
using Library.Core.Models;
using Library.Core.Services;

namespace Library.Web.Models;

public sealed class LoginForm
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}

public sealed class PatronLoginForm
{
    public string CardNumber { get; set; } = "";
    public string Pin { get; set; } = "";
    public string? ReturnUrl { get; set; }
}

public sealed class BranchChoice
{
    public int BranchId { get; set; }
    public int DeskId { get; set; }
    public string? ReturnUrl { get; set; }
    public List<Branch> Branches { get; set; } = new();
}

public sealed class StaffDashboard
{
    public Branch? Branch { get; set; }
    public IReadOnlyList<LoanDetails> Overdue { get; set; } = Array.Empty<LoanDetails>();
    public IReadOnlyList<TransferInfo> Inbound { get; set; } = Array.Empty<TransferInfo>();
    public IReadOnlyList<TransferInfo> Outbound { get; set; } = Array.Empty<TransferInfo>();
    public IReadOnlyList<HoldDetails> ReadyHolds { get; set; } = Array.Empty<HoldDetails>();
    public int ActiveLoans { get; set; }
    public int NotificationsToday { get; set; }
    public DateOnly Today { get; set; }
    public bool Simulated { get; set; }
    public IReadOnlyDictionary<int, Branch> Branches { get; set; } = new Dictionary<int, Branch>();
}

public sealed class SearchPage
{
    public string? Q { get; set; }
    public ItemType? Type { get; set; }
    public int? Category { get; set; }
    public int? Branch { get; set; }
    public bool Available { get; set; }
    public PagedResult<CatalogueItem> Result { get; set; } = new(Array.Empty<CatalogueItem>(), 0, 1, 12);
    public IReadOnlyList<CategoryInfo> Categories { get; set; } = Array.Empty<CategoryInfo>();
    public IReadOnlyList<BranchSummary> Branches { get; set; } = Array.Empty<BranchSummary>();
}

public sealed class ItemPage
{
    public required CatalogueItem Item { get; init; }
    public int QueueLength { get; init; }
    public bool PatronSignedIn { get; init; }
    public bool PatronHasHold { get; init; }
    public bool CanHold { get; init; }
    public BranchSummary? Branch { get; init; }
}

public sealed class BranchesPage
{
    public IReadOnlyList<BranchSummary> Branches { get; set; } = Array.Empty<BranchSummary>();
    public DateTime Now { get; set; }
}

public sealed class KioskReceiptLine
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public DateOnly DueOn { get; set; }
}

public sealed class DeskPage
{
    public Branch Branch { get; set; } = new();
    public string? DeskName { get; set; }
    public IReadOnlyList<Borrower> Borrowers { get; set; } = Array.Empty<Borrower>();
    public IReadOnlyList<LoanDetails> RecentLoans { get; set; } = Array.Empty<LoanDetails>();
    public IReadOnlyList<LoanDetails> Overdue { get; set; } = Array.Empty<LoanDetails>();
    public string? LastBorrowCode { get; set; }
    public int? LastBorrowerId { get; set; }
}

public sealed class ItemForm
{
    public int Id { get; set; }
    public ItemType Type { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string LibraryCode { get; set; } = "";
    public int BranchId { get; set; }
    public int? CategoryId { get; set; }
    public string Author { get; set; } = "";
    public string Artist { get; set; } = "";
    public int Year { get; set; } = DateTime.Now.Year;
    public int RecommendedAge { get; set; } = 3;
    public List<Branch> Branches { get; set; } = new();
    public List<Category> Categories { get; set; } = new();

    public static ItemForm From(Item i) => new()
    {
        Id = i.Id, Type = i.Type, Name = i.Name, Description = i.Description, LibraryCode = i.LibraryCode, BranchId = i.BranchId, CategoryId = i.CategoryId,
        Author = (i as Book)?.Author ?? "", Artist = (i as Music)?.Artist ?? "", Year = (i as Music)?.Year ?? DateTime.Now.Year,
        RecommendedAge = (i as Toy)?.RecommendedAge ?? 3
    };

    public Item ToItem()
    {
        Item item = Type switch
        {
            ItemType.Book => new Book { Author = Author },
            ItemType.Music => new Music { Artist = Artist, Year = Year },
            _ => new Toy { RecommendedAge = RecommendedAge }
        };
        item.Id = Id; item.Name = Name; item.Description = Description; item.LibraryCode = LibraryCode;
        item.BranchId = BranchId; item.CategoryId = CategoryId;
        return item;
    }
}

public sealed class ItemsPage
{
    public IReadOnlyList<Item> Items { get; set; } = Array.Empty<Item>();
    public IReadOnlyList<Branch> Branches { get; set; } = Array.Empty<Branch>();
    public IReadOnlyList<Category> Categories { get; set; } = Array.Empty<Category>();
    public HashSet<int> InTransit { get; set; } = new();
    public string? Q { get; set; }
    public ItemType? Type { get; set; }
    public ItemStatus? Status { get; set; }
    public int? Branch { get; set; }
    public int? Category { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class BranchForm
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string Desks { get; set; } = "Desk 1\nDesk 2";
    /// <summary>Seven rows Monday..Sunday. Blank open time means closed.</summary>
    public List<DayHours> Days { get; set; } = DayHours.Defaults();
}

public sealed class DayHours
{
    public DayOfWeek Day { get; set; }
    public string? Open { get; set; }
    public string? Close { get; set; }

    public static List<DayHours> Defaults() => new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
        .Select(d => new DayHours { Day = d, Open = d == DayOfWeek.Sunday ? null : "09:00", Close = d == DayOfWeek.Sunday ? null : "17:00" }).ToList();
}
