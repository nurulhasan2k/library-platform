using System.Security.Claims;
using Library.Core.Contracts;
using Library.Core.Services;
using Library.Web.Infrastructure;
using Library.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

/// <summary>A patron's web account: card number + PIN, then see loans, fines and holds, and place or cancel holds.</summary>
[Route("account")]
public sealed class AccountController(IPatronAuthService auth, LoanQueryService loans, IReservationService reservations, ICatalogueQuery catalogue) : Controller
{
    private int PatronId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    [HttpGet("signin")]
    public async Task<IActionResult> SignIn(string? returnUrl)
    {
        if ((await HttpContext.AuthenticateAsync(Auth.Patron)).Succeeded) return RedirectToAction(nameof(Index));
        return View(new PatronLoginForm { ReturnUrl = returnUrl });
    }

    [HttpPost("signin")]
    public async Task<IActionResult> SignIn(PatronLoginForm form)
    {
        var result = await auth.SignInAsync(form.CardNumber, form.Pin);
        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return View(new PatronLoginForm { CardNumber = form.CardNumber, ReturnUrl = form.ReturnUrl });
        }
        await HttpContext.SignInAsync(Auth.Patron, Auth.Identity(Auth.Patron, result.Patron!.BorrowerId, result.Patron.FullName));
        return Redirect(Url.IsLocalUrl(form.ReturnUrl) ? form.ReturnUrl! : "/account");
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var p = await HttpContext.AuthenticateAsync(Auth.Patron);
        if (!p.Succeeded) return RedirectToAction(nameof(SignIn));
        var id = int.Parse(p.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var summary = loans.GetAccount(id);
        if (summary is null) return RedirectToAction(nameof(SignIn));
        return View(summary);
    }

    [HttpPost("hold")]
    public async Task<IActionResult> Hold(string code)
    {
        var p = await HttpContext.AuthenticateAsync(Auth.Patron);
        if (!p.Succeeded) return RedirectToAction(nameof(SignIn), new { returnUrl = "/items/" + code });
        var item = await catalogue.FindByCodeAsync(code);
        if (item is null) { TempData["Error"] = "That item isn't available for holds."; return Redirect("/"); }
        var result = await reservations.PlaceAsync(item.Id, int.Parse(p.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!), ReservationSource.Web);
        TempData[result.Success ? "Ok" : "Error"] = result.Success ? $"Hold placed on \"{item.Name}\". We will notify you when it is ready." : result.Error;
        return Redirect("/items/" + item.LibraryCode);
    }

    [HttpPost("cancel-hold")]
    public async Task<IActionResult> CancelHold(int id)
    {
        var p = await HttpContext.AuthenticateAsync(Auth.Patron);
        if (!p.Succeeded) return RedirectToAction(nameof(SignIn));
        var result = await reservations.CancelAsync(id, int.Parse(p.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!));
        TempData[result.Success ? "Ok" : "Error"] = result.Success ? "Hold cancelled." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("signout")]
    public async Task<IActionResult> SignOutPatron()
    {
        await HttpContext.SignOutAsync(Auth.Patron);
        return Redirect("/");
    }
}
