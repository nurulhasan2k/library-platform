using Library.Core.Contracts;
using Library.Core.Services;
using Library.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

/// <summary>Holds and waiting lists (feature 3.5), as staff see them. Patrons place their own holds on the web and at the kiosk.</summary>
[Route("holds"), Authorize(Roles = Auth.AdminOrReception)]
public sealed class HoldsController(ReservationService holds, IReservationService reservations, ICatalogueQuery catalogue, BorrowerService borrowers) : AppController
{
    [HttpGet("")]
    public IActionResult Index(bool allBranches = false)
    {
        int? branch = allBranches ? null : Here.CurrentBranch?.Id;
        ViewBag.All = allBranches;
        ViewBag.HasBranch = Here.CurrentBranch is not null;
        ViewBag.Borrowers = borrowers.ListBorrowers();
        return View(holds.ListHolds(openOnly: true, branchId: branch));
    }

    [HttpPost("place")]
    public async Task<IActionResult> Place(string code, string borrower)
    {
        var item = await catalogue.FindByCodeAsync(code);
        var b = borrowers.FindByCard((borrower ?? "").Split('(').Last().TrimEnd(')')) ?? borrowers.FindByCard(borrower ?? "");
        if (item is null) Fail($"No item has the library code '{code}'.");
        else if (b is null) Fail("Choose a borrower from the list (or type their card number).");
        else
        {
            var r = await reservations.PlaceAsync(item.Id, b.Id, ReservationSource.Desk);
            if (r.Success) Flash($"Hold placed on \"{item.Name}\" for {b.Name}."); else Fail(r.Error ?? "Could not place the hold.");
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var r = await reservations.CancelAsync(id, 0); // 0 = staff on behalf of the patron
        if (r.Success) Flash("Hold cancelled."); else Fail(r.Error ?? "Could not cancel the hold.");
        return RedirectToAction(nameof(Index));
    }
}
