using Library.Core.Contracts;
using Library.Core.Services;
using Library.Web.Infrastructure;
using Library.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

/// <summary>The public site: search the catalogue, see an item's availability by branch, find branches and opening hours.</summary>
public sealed class HomeController(ICatalogueQuery catalogue, IReservationService reservations, ReservationService reservationQueries, IClock clock) : Controller
{
    private const int PageSize = 12;

    [HttpGet("/")]
    public async Task<IActionResult> Index(string? q, ItemType? type, int? category, int? branch, bool available = false, int page = 1)
    {
        var result = await catalogue.SearchAsync(new CatalogueFilter(q, type, category, branch, available, Math.Max(1, page), PageSize));
        return View(new SearchPage
        {
            Q = q, Type = type, Category = category, Branch = branch, Available = available, Result = result,
            Categories = await catalogue.GetCategoriesAsync(),
            Branches = (await catalogue.GetBranchesAsync()).Where(b => b.IsActive).ToList()
        });
    }

    [HttpGet("/items/{code}")]
    public async Task<IActionResult> Item(string code)
    {
        var item = await catalogue.FindByCodeAsync(code);
        if (item is null) return NotFound(View("NotFound"));

        var patron = await HttpContext.AuthenticateAsync(Auth.Patron);
        var queue = await reservations.GetQueueLengthAsync(item.Id);
        var hasHold = patron.Succeeded && reservationQueries.ListHolds(true, null, int.Parse(patron.Principal!.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value), item.Id).Any();
        var canHold = item.Status != ItemStatus.Available || item.InTransit || queue > 0;
        var branch = (await catalogue.GetBranchesAsync()).FirstOrDefault(b => b.Id == item.BranchId);
        return View(new ItemPage { Item = item, QueueLength = queue, PatronSignedIn = patron.Succeeded, PatronHasHold = hasHold, CanHold = canHold, Branch = branch });
    }

    [HttpGet("/branches")]
    public async Task<IActionResult> Branches() =>
        View(new BranchesPage { Branches = (await catalogue.GetBranchesAsync()).Where(b => b.IsActive).ToList(), Now = clock.Now });

    [HttpGet("/Home/Error")]
    public IActionResult Error() => View();
}
