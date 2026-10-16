using Library.Core.Models;

namespace Library.Core.Data;

/// <summary>Everything the platform stores. This is the shape of the JSON data file.</summary>
public sealed class LibraryData
{
    public int NextId { get; set; } = 1;

    public Settings Settings { get; set; } = new();
    public List<Branch> Branches { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<Item> Items { get; set; } = new();
    public List<Borrower> Borrowers { get; set; } = new();
    public List<Loan> Loans { get; set; } = new();
    public List<User> Users { get; set; } = new();
    public List<Transfer> Transfers { get; set; } = new();
    public List<Reservation> Reservations { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
    public List<ImportBatch> ImportBatches { get; set; } = new();

    /// <summary>One counter for every table keeps ids unique across the file.</summary>
    public int TakeId() => NextId++;

    public Item? FindItem(int id) => Items.FirstOrDefault(i => i.Id == id);

    public Item? FindItemByCode(string? code)
    {
        var c = (code ?? "").Trim();
        return Items.FirstOrDefault(i => string.Equals(i.LibraryCode, c, StringComparison.OrdinalIgnoreCase));
    }

    public Branch? FindBranch(int id) => Branches.FirstOrDefault(b => b.Id == id);
    public Borrower? FindBorrower(int id) => Borrowers.FirstOrDefault(b => b.Id == id);
    public bool IsInTransit(int itemId) => Transfers.Any(t => t.ItemId == itemId && t.IsActive);
}
