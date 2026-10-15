using System.Text.Json;
using Library.Core.Contracts;
using Library.Core.Services;
using Library.Web.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

/// <summary>
/// Patron-facing holds page (client 3.5): sign in with library card + PIN, place a hold on a borrowed item
/// by its library code, see the queue position, and cancel a hold.
/// TODO: when Person A's item detail page and patron login are merged, link "Place hold" from the item page too.
/// </summary>
[Route("my-holds")]
public class MyHoldsController(
    IPatronAuthService auth,
    IReservationService reservations,
    ReservationService holds,
    DemoLibraryStore store,
    IDataProtectionProvider dataProtection) : Controller
{
    private const string CookieName = ".Library.Patron";
    private static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(15);

    private sealed record PatronState(int PatronId, string PatronName, DateTime LastSeenUtc);

    private readonly IDataProtector protector = dataProtection.CreateProtector("Library.MyHolds.v1");

    private PatronState? Load()
    {
        var raw = Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(raw)) return null;
        try
        {
            var s = JsonSerializer.Deserialize<PatronState>(protector.Unprotect(raw));
            return s is null || DateTime.UtcNow - s.LastSeenUtc > IdleLimit ? null : s;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void Save(PatronState s)
        => Response.Cookies.Append(CookieName, protector.Protect(JsonSerializer.Serialize(s with { LastSeenUtc = DateTime.UtcNow })),
            new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, IsEssential = true });

    [HttpGet("")]
    public IActionResult Index()
    {
        if (Load() is not { } s) return View(new MyHoldsVm(null, Array.Empty<HoldInfo>()));
        Save(s);
        return View(new MyHoldsVm(s.PatronName, holds.GetHoldsForBorrower(s.PatronId)));
    }

    [HttpPost("signin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(string? cardNumber, string? pin)
    {
        var result = await auth.SignInAsync(cardNumber ?? "", pin ?? "");
        if (!result.Success || result.Patron is null)
        {
            TempData["Err"] = result.Error ?? "Sign-in failed. Please try again.";
            return RedirectToAction(nameof(Index));
        }

        Save(new PatronState(result.Patron.BorrowerId, result.Patron.FullName, DateTime.UtcNow));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("place")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Place(string? code)
    {
        if (Load() is not { } s) return RedirectToAction(nameof(Index));

        var item = store.FindItemByCode(code);
        if (item is null)
        {
            TempData["Err"] = "We could not find an item with that code. Please check the code and try again.";
        }
        else
        {
            var result = await reservations.PlaceAsync(item.Id, s.PatronId, ReservationSource.Web);
            if (result.Success) TempData["Msg"] = $"Your hold on \"{item.Name}\" is placed. We will tell you when it is ready.";
            else TempData["Err"] = result.Error;
        }

        Save(s);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int reservationId)
    {
        if (Load() is not { } s) return RedirectToAction(nameof(Index));

        var result = await reservations.CancelAsync(reservationId, s.PatronId);
        if (result.Success) TempData["Msg"] = "Your hold is cancelled.";
        else TempData["Err"] = result.Error;

        Save(s);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("signout")]
    public IActionResult EndVisit()
    {
        Response.Cookies.Delete(CookieName);
        return RedirectToAction(nameof(Index));
    }
}
