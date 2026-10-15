using Library.Core.Contracts;
using Library.Core.Services;
using Library.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

/// <summary>
/// Staff view of items, queues and holds (client 3.5), plus desk borrow/return buttons so the whole
/// chain (borrow, join queue, return, next person notified) can be demonstrated.
/// TODO: add [Authorize(Roles = ...)] when Person A's roles are merged (Reception on behalf of a patron, Admin).
/// </summary>
[Route("holds")]
public class HoldsController(
    DemoLibraryStore store,
    IReservationService reservations,
    ILendingService lending,
    StubInMemoryLendingService demoLending) : Controller
{
    private const int DeskBranchId = 1;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var rows = new List<HoldRow>();
        foreach (var item in store.Items)
        {
            var loan = store.ActiveLoanForItem(item.Id);
            var holder = loan is null ? null : store.FindPatron(loan.BorrowerId)?.Name;
            var queue = await reservations.GetQueueAsync(item.Id);
            rows.Add(new HoldRow(item, holder, loan?.DueOn, queue));
        }
        return View(new HoldsVm(rows, store.Patrons));
    }

    [HttpPost("place")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Place(int itemId, int borrowerId)
    {
        var result = await reservations.PlaceAsync(itemId, borrowerId, ReservationSource.Desk);
        TempData[result.Success ? "Msg" : "Err"] = result.Success ? "Hold placed." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int reservationId, int borrowerId)
    {
        var result = await reservations.CancelAsync(reservationId, borrowerId);
        TempData[result.Success ? "Msg" : "Err"] = result.Success ? "Hold cancelled." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("borrow")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Borrow(string code, int borrowerId)
    {
        var result = await lending.BorrowAsync(code, borrowerId, DeskBranchId);
        TempData[result.Success ? "Msg" : "Err"] = result.Success
            ? $"Borrowed. Due on {result.Receipt!.DueOn:d MMM yyyy}."
            : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("return")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnItem(string code, bool damaged)
    {
        var result = await lending.ReturnAsync(code, DeskBranchId, damaged);
        TempData[result.Success ? "Msg" : "Err"] = result.Success
            ? $"Returned. Days late: {result.DaysLate}. Fine: ${result.FineAmount:0.00}."
            : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("available")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAvailable(int itemId)
    {
        var error = await demoLending.MarkAvailableAsync(itemId);
        TempData[error is null ? "Msg" : "Err"] = error ?? "Item is available again.";
        return RedirectToAction(nameof(Index));
    }
}
