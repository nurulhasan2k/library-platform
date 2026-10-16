using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Models;

namespace Library.Core.Services;

/// <summary>Admin management of items, categories and branches (brief 1, 2a and the multi-branch inventory of 3.4).</summary>
public sealed class InventoryService(ILibraryStore store, IReservationService reservations)
{
    // ------------------------------------------------------------------ items

    public Item AddItem(Item item)
    {
        Normalise(item);
        return store.Write(data =>
        {
            ApplyCategory(data, item);
            EnsureValid(item.GetValidationErrors());
            var branch = data.FindBranch(item.BranchId) ?? throw new LibraryException("That branch doesn't exist.");
            if (!branch.IsActive) throw new LibraryException($"{branch.Name} is closed down and can't receive new items.");
            if (data.FindItemByCode(item.LibraryCode) != null)
                throw new LibraryException($"Library code '{item.LibraryCode}' is already used by another item.");

            item.Id = data.TakeId();
            item.Status = ItemStatus.Available;
            data.Items.Add(item);
            return item;
        });
    }

    /// <summary>Saves an item's details. Type, status and branch are not changed here (use status change / transfers).</summary>
    public Item UpdateItem(Item updated)
    {
        Normalise(updated);
        return store.Write(data =>
        {
            var index = data.Items.FindIndex(i => i.Id == updated.Id);
            if (index < 0) throw new LibraryException($"No item with id {updated.Id}.");
            var existing = data.Items[index];
            if (existing.GetType() != updated.GetType())
                throw new LibraryException("An item's type can't be changed. Add a new item instead.");

            updated.Status = existing.Status;
            updated.BranchId = existing.BranchId;
            ApplyCategory(data, updated);
            EnsureValid(updated.GetValidationErrors());

            var owner = data.FindItemByCode(updated.LibraryCode);
            if (owner != null && owner.Id != updated.Id)
                throw new LibraryException($"Library code '{updated.LibraryCode}' is already used by another item.");

            data.Items[index] = updated;
            return updated;
        });
    }

    /// <summary>Sets an item to available, damaged or destroy (admin). Making a damaged item available again starts the hold queue.</summary>
    public Item ChangeItemStatus(int itemId, ItemStatus status)
    {
        var (item, becameAvailable) = store.Write(data =>
        {
            var item = data.FindItem(itemId) ?? throw new LibraryException($"No item with id {itemId}.");
            if (status == ItemStatus.Borrowed)
                throw new LibraryException("Items become 'borrowed' only when reception or the kiosk lends them out.");
            if (item.Status == ItemStatus.Borrowed)
                throw new LibraryException($"'{item.Name}' is out on loan. Return it at a desk before changing its status.");
            if (data.IsInTransit(itemId))
                throw new LibraryException($"'{item.Name}' has a transfer in progress. Receive or cancel the transfer first.");

            var wasUnavailable = item.Status != ItemStatus.Available;
            item.Status = status;
            return (item, wasUnavailable && status == ItemStatus.Available);
        });

        if (becameAvailable) HookSafely(() => reservations.OnItemBecameAvailableAsync(item.Id).GetAwaiter().GetResult());
        return item;
    }

    public void DeleteItem(int itemId)
    {
        store.Write(data =>
        {
            var item = data.FindItem(itemId) ?? throw new LibraryException($"No item with id {itemId}.");
            if (item.Status == ItemStatus.Borrowed)
                throw new LibraryException($"'{item.Name}' is currently on loan and can't be deleted.");
            if (data.Loans.Any(l => l.ItemId == itemId) || data.Transfers.Any(t => t.ItemId == itemId) ||
                data.Reservations.Any(r => r.ItemId == itemId))
                throw new LibraryException(
                    $"'{item.Name}' has loan, hold or transfer history that reports rely on, so it can't be deleted. Set its status to 'destroy' instead.");
            data.Items.Remove(item);
        });
    }

    public Item? GetItem(int itemId) => store.Read(d => d.FindItem(itemId));
    public Item? FindItemByCode(string code) => store.Read(d => d.FindItemByCode(code));

    public IReadOnlyList<Item> SearchItems(ItemSearch? search = null)
    {
        search ??= new ItemSearch();
        var terms = (search.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return store.Read(data =>
        {
            IEnumerable<Item> q = data.Items;
            if (!search.IncludeDestroyed) q = q.Where(i => i.Status != ItemStatus.Destroy);
            if (search.Type is { } t) q = q.Where(i => i.Type == t);
            if (search.Status is { } s) q = q.Where(i => i.Status == s);
            if (search.BranchId is { } b) q = q.Where(i => i.BranchId == b);
            if (search.CategoryId is { } c) q = q.Where(i => i.CategoryId == c);
            foreach (var term in terms)
            {
                var x = term;
                q = q.Where(i => i.GetSearchText().Contains(x, StringComparison.OrdinalIgnoreCase));
            }
            return (IReadOnlyList<Item>)q.OrderBy(i => i.Type).ThenBy(i => i.Name).ToList();
        });
    }

    // ------------------------------------------------------------- categories

    public IReadOnlyList<Category> GetCategories() =>
        store.Read(d => (IReadOnlyList<Category>)d.Categories.OrderBy(c => c.ItemType).ThenBy(c => c.Name).ToList());

    public Category AddCategory(string name, ItemType type)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) throw new LibraryException("Category name is required.");
        return store.Write(data =>
        {
            if (data.Categories.Any(c => c.ItemType == type && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new LibraryException($"There is already a {type.ToString().ToLowerInvariant()} category called '{name}'.");
            var c = new Category { Id = data.TakeId(), Name = name, ItemType = type };
            data.Categories.Add(c);
            return c;
        });
    }

    public Category RenameCategory(int id, string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) throw new LibraryException("Category name is required.");
        return store.Write(data =>
        {
            var c = data.Categories.FirstOrDefault(x => x.Id == id) ?? throw new LibraryException("No such category.");
            if (data.Categories.Any(x => x.Id != id && x.ItemType == c.ItemType && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new LibraryException($"There is already a category called '{name}'.");
            c.Name = name;
            foreach (var item in data.Items.Where(i => i.CategoryId == id)) ApplyCategory(data, item);
            return c;
        });
    }

    public void DeleteCategory(int id)
    {
        store.Write(data =>
        {
            var c = data.Categories.FirstOrDefault(x => x.Id == id) ?? throw new LibraryException("No such category.");
            if (data.Items.Any(i => i.CategoryId == id))
                throw new LibraryException($"'{c.Name}' still has items in it, so it can't be deleted.");
            data.Categories.Remove(c);
        });
    }

    // --------------------------------------------------------------- branches

    public IReadOnlyList<Branch> GetBranches(bool activeOnly = false) =>
        store.Read(d => (IReadOnlyList<Branch>)d.Branches.Where(b => !activeOnly || b.IsActive).OrderBy(b => b.Name).ToList());

    public Branch? GetBranch(int id) => store.Read(d => d.FindBranch(id));

    public Branch AddBranch(Branch branch)
    {
        Normalise(branch);
        return store.Write(data =>
        {
            ValidateBranch(data, branch);
            branch.Id = data.TakeId();
            foreach (var desk in branch.Desks) desk.Id = data.TakeId();
            data.Branches.Add(branch);
            return branch;
        });
    }

    public Branch UpdateBranch(Branch updated)
    {
        Normalise(updated);
        return store.Write(data =>
        {
            var existing = data.FindBranch(updated.Id) ?? throw new LibraryException("No such branch.");
            ValidateBranch(data, updated);
            if (!updated.IsActive && existing.IsActive && data.Items.Any(i => i.BranchId == existing.Id && i.Status == ItemStatus.Borrowed))
                throw new LibraryException("This branch still has items out on loan, so it can't be closed down yet.");

            existing.Code = updated.Code;
            existing.Name = updated.Name;
            existing.Address = updated.Address;
            existing.Phone = updated.Phone;
            existing.IsActive = updated.IsActive;
            existing.Hours = updated.Hours;

            // Keep the ids of desks that kept their name; give new desks new ids.
            var desks = new List<Desk>();
            foreach (var wanted in updated.Desks)
            {
                var keep = existing.Desks.FirstOrDefault(d => string.Equals(d.Name, wanted.Name, StringComparison.OrdinalIgnoreCase));
                desks.Add(keep ?? new Desk { Id = data.TakeId(), Name = wanted.Name });
            }
            existing.Desks = desks;
            return existing;
        });
    }

    public void DeleteBranch(int id)
    {
        store.Write(data =>
        {
            var b = data.FindBranch(id) ?? throw new LibraryException("No such branch.");
            if (data.Items.Any(i => i.BranchId == id) || data.Loans.Any(l => l.BranchId == id || l.ReturnBranchId == id) ||
                data.Transfers.Any(t => t.FromBranchId == id || t.ToBranchId == id))
                throw new LibraryException($"{b.Name} has items or history, so it can't be deleted. Close it down instead.");
            data.Branches.Remove(b);
            foreach (var u in data.Users) u.BranchIds.Remove(id);
        });
    }

    // ---------------------------------------------------------------- helpers

    private static void ValidateBranch(LibraryData data, Branch b)
    {
        if (string.IsNullOrWhiteSpace(b.Code)) throw new LibraryException("Branch code is required.");
        if (string.IsNullOrWhiteSpace(b.Name)) throw new LibraryException("Branch name is required.");
        if (data.Branches.Any(x => x.Id != b.Id && string.Equals(x.Code, b.Code, StringComparison.OrdinalIgnoreCase)))
            throw new LibraryException($"The branch code '{b.Code}' is already used.");
        foreach (var h in b.Hours)
            if (!h.IsClosed && h.Close <= h.Open)
                throw new LibraryException($"{h.Day}: closing time must be after opening time.");
        if (b.Desks.Count == 0) throw new LibraryException("A branch needs at least one reception desk.");
    }

    private static void ApplyCategory(LibraryData data, Item item)
    {
        if (item.CategoryId is not { } cid) return;
        var cat = data.Categories.FirstOrDefault(c => c.Id == cid) ?? throw new LibraryException("That category doesn't exist.");
        if (cat.ItemType != item.Type)
            throw new LibraryException($"'{cat.Name}' is a {cat.ItemType.ToString().ToLowerInvariant()} category and can't be used for this item.");
        switch (item)
        {
            case Book b: b.Genre = cat.Name; break;
            case Toy t: t.ToyType = cat.Name; break;
        }
    }

    internal static void EnsureValid(IEnumerable<string> errors)
    {
        var list = errors.ToList();
        if (list.Count > 0) throw new LibraryException(string.Join(" ", list));
    }

    private static void Normalise(Item item)
    {
        item.Name = item.Name?.Trim() ?? "";
        item.Description = item.Description?.Trim() ?? "";
        item.LibraryCode = item.LibraryCode?.Trim().ToUpperInvariant() ?? "";
        switch (item)
        {
            case Book b: b.Author = b.Author?.Trim() ?? ""; b.Genre = b.Genre?.Trim() ?? ""; break;
            case Music m: m.Artist = m.Artist?.Trim() ?? ""; break;
            case Toy t: t.ToyType = t.ToyType?.Trim() ?? ""; break;
        }
    }

    private static void Normalise(Branch b)
    {
        b.Code = b.Code?.Trim().ToUpperInvariant() ?? "";
        b.Name = b.Name?.Trim() ?? "";
        b.Address = b.Address?.Trim() ?? "";
        b.Phone = b.Phone?.Trim() ?? "";
        foreach (var d in b.Desks) d.Name = d.Name?.Trim() ?? "";
        b.Desks = b.Desks.Where(d => d.Name.Length > 0).ToList();
    }

    /// <summary>Hooks must never make the main action fail (hook rule in docs/ARCHITECTURE.md).</summary>
    internal static void HookSafely(Action hook)
    {
        try { hook(); }
        catch (Exception) { /* logged by the service that failed; the main action has already succeeded */ }
    }
}

/// <summary>What to look for when searching items in the staff screens. All parts are optional and combine with AND.</summary>
public sealed record ItemSearch(
    string? Text = null,
    ItemType? Type = null,
    ItemStatus? Status = null,
    int? BranchId = null,
    int? CategoryId = null,
    bool IncludeDestroyed = true);
