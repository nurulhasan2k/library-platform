using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Models;
using Library.Core.Services;
using Library.Web.Infrastructure;
using Library.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

/// <summary>Item management across all branches (brief 1 and 2a, with the multi-branch inventory of feature 3.4).</summary>
[Route("admin/items"), Authorize(Roles = Auth.Admin)]
public sealed class ItemsController(InventoryService inventory, ILibraryStore store) : AppController
{
    private const int PageSize = 25;

    [HttpGet("")]
    public IActionResult Index(string? q, ItemType? type, ItemStatus? status, int? branch, int? category, int page = 1)
    {
        var all = inventory.SearchItems(new ItemSearch(q, type, status, branch, category));
        var model = new ItemsPage
        {
            Q = q, Type = type, Status = status, Branch = branch, Category = category, Page = Math.Max(1, page), PageSize = PageSize,
            Items = all.Skip((Math.Max(1, page) - 1) * PageSize).Take(PageSize).ToList(),
            Branches = inventory.GetBranches(), Categories = inventory.GetCategories(),
            InTransit = store.Read(d => d.Transfers.Where(t => t.IsActive).Select(t => t.ItemId).ToHashSet())
        };
        ViewBag.Total = all.Count;
        return View(model);
    }

    [HttpGet("new")]
    public IActionResult Create(ItemType type = ItemType.Book) =>
        View("Form", Fill(new ItemForm { Type = type, BranchId = Here.CurrentBranch?.Id ?? inventory.GetBranches(true).FirstOrDefault()?.Id ?? 0 }));

    [HttpPost("new")]
    public IActionResult Create(ItemForm form)
    {
        if (!Try(() => inventory.AddItem(form.ToItem()))) return View("Form", Fill(form));
        Flash($"Added \"{form.Name}\".");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/edit")]
    public IActionResult Edit(int id) => inventory.GetItem(id) is { } i ? View("Form", Fill(ItemForm.From(i))) : NotFound();

    [HttpPost("{id:int}/edit")]
    public IActionResult Edit(int id, ItemForm form)
    {
        form.Id = id;
        if (!Try(() => inventory.UpdateItem(form.ToItem()))) return View("Form", Fill(form));
        Flash("Saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/status")]
    public IActionResult Status(int id, ItemStatus status, string? returnUrl)
    {
        if (Try(() => inventory.ChangeItemStatus(id, status))) Flash($"Status set to {status.ToString().ToLowerInvariant()}.");
        return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/admin/items");
    }

    [HttpPost("{id:int}/delete")]
    public IActionResult Delete(int id)
    {
        if (Try(() => inventory.DeleteItem(id))) Flash("Item deleted.");
        return RedirectToAction(nameof(Index));
    }

    private ItemForm Fill(ItemForm f)
    {
        f.Branches = inventory.GetBranches().ToList();
        f.Categories = inventory.GetCategories().Where(c => c.ItemType == f.Type).ToList();
        return f;
    }
}

[Route("admin/branches"), Authorize(Roles = Auth.Admin)]
public sealed class BranchesController(InventoryService inventory, ILibraryStore store) : AppController
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewBag.Counts = store.Read(d => d.Branches.ToDictionary(b => b.Id, b => d.Items.Count(i => i.BranchId == b.Id && i.Status != ItemStatus.Destroy)));
        return View(inventory.GetBranches());
    }

    [HttpGet("new")]
    public IActionResult Create() => View("Form", new BranchForm());

    [HttpPost("new")]
    public IActionResult Create(BranchForm form)
    {
        if (!Try(() => inventory.AddBranch(ToBranch(form)))) return View("Form", form);
        Flash($"Added {form.Name}.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/edit")]
    public IActionResult Edit(int id)
    {
        var b = inventory.GetBranch(id);
        if (b is null) return NotFound();
        var days = DayHours.Defaults();
        foreach (var d in days)
        {
            var h = b.HoursFor(d.Day);
            d.Open = h is { IsClosed: false } ? h.Open!.Value.ToString("HH:mm") : null;
            d.Close = h is { IsClosed: false } ? h.Close!.Value.ToString("HH:mm") : null;
        }
        return View("Form", new BranchForm
        {
            Id = b.Id, Code = b.Code, Name = b.Name, Address = b.Address, Phone = b.Phone, IsActive = b.IsActive,
            Desks = string.Join("\n", b.Desks.Select(x => x.Name)), Days = days
        });
    }

    [HttpPost("{id:int}/edit")]
    public IActionResult Edit(int id, BranchForm form)
    {
        form.Id = id;
        if (!Try(() => inventory.UpdateBranch(ToBranch(form)))) return View("Form", form);
        Flash("Saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    public IActionResult Delete(int id)
    {
        if (Try(() => inventory.DeleteBranch(id))) Flash("Branch deleted.");
        return RedirectToAction(nameof(Index));
    }

    private static Branch ToBranch(BranchForm f)
    {
        var branch = new Branch { Id = f.Id, Code = f.Code, Name = f.Name, Address = f.Address, Phone = f.Phone, IsActive = f.IsActive };
        foreach (var d in f.Days)
        {
            var hasOpen = TimeOnly.TryParse(d.Open, out var open);
            var hasClose = TimeOnly.TryParse(d.Close, out var close);
            if (!string.IsNullOrWhiteSpace(d.Open) && !hasOpen) throw new LibraryException($"{d.Day}: '{d.Open}' isn't a time. Use 24-hour times like 09:00.");
            if (!string.IsNullOrWhiteSpace(d.Close) && !hasClose) throw new LibraryException($"{d.Day}: '{d.Close}' isn't a time. Use 24-hour times like 17:30.");
            if (hasOpen != hasClose) throw new LibraryException($"{d.Day}: give both an opening and a closing time, or leave both blank for closed.");
            branch.Hours.Add(new OpeningHours(d.Day, hasOpen ? open : null, hasClose ? close : null));
        }
        foreach (var line in (f.Desks ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            branch.Desks.Add(new Desk { Name = line });
        return branch;
    }
}

[Route("admin/categories"), Authorize(Roles = Auth.Admin)]
public sealed class CategoriesController(InventoryService inventory, ICatalogueQuery catalogue) : AppController
{
    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await catalogue.GetCategoriesAsync());

    [HttpPost("new")]
    public IActionResult Create(string name, ItemType type)
    {
        if (Try(() => inventory.AddCategory(name, type))) Flash($"Added category \"{name}\".");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/rename")]
    public IActionResult Rename(int id, string name)
    {
        if (Try(() => inventory.RenameCategory(id, name))) Flash("Renamed.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    public IActionResult Delete(int id)
    {
        if (Try(() => inventory.DeleteCategory(id))) Flash("Category deleted.");
        return RedirectToAction(nameof(Index));
    }
}

[Route("admin/users"), Authorize(Roles = Auth.Admin)]
public sealed class UsersController(StaffService staff, InventoryService inventory) : AppController
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewBag.Branches = inventory.GetBranches();
        return View(staff.ListUsers());
    }

    [HttpPost("new")]
    public IActionResult Create(string username, string password, UserRole role, int[]? branchIds)
    {
        if (Try(() => staff.CreateUser(username, password, role, branchIds))) Flash($"Added {username}.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/password")]
    public IActionResult Password(int id, string password)
    {
        if (Try(() => staff.ResetPassword(id, password))) Flash("Password changed.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/branches")]
    public IActionResult Branches(int id, int[]? branchIds)
    {
        if (Try(() => staff.SetBranches(id, branchIds ?? Array.Empty<int>()))) Flash("Branches updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    public IActionResult Delete(int id)
    {
        if (Try(() => staff.DeleteUser(id, UserId))) Flash("User deleted.");
        return RedirectToAction(nameof(Index));
    }
}
