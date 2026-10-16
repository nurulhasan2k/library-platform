using System.Security.Claims;
using System.Text.Json;
using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Services;
using Library.Web.Infrastructure;
using Library.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Library.Web.Controllers;

/// <summary>
/// The self-service kiosk (feature 3.1): a patron signs in with card number + PIN, sees their account, checks items out,
/// and manages holds. It is fixed to one branch (Kiosk:BranchCode) and signs the patron out after a period of inactivity.
/// </summary>
[Route("kiosk")]
public sealed class KioskController(
    IPatronAuthService auth, ILendingService lending, IReservationService reservations, ICatalogueQuery catalogue,
    LoanQueryService loans, IBranchContext branch, IOptions<KioskOptions> options) : Controller
{
    private const string SessionKey = "kiosk.receipt";
    private int Idle => Math.Max(15, options.Value.IdleTimeoutSeconds);

    private async Task<int?> PatronIdAsync()
    {
        var r = await HttpContext.AuthenticateAsync(Auth.Kiosk);
        return r.Succeeded ? int.Parse(r.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!) : null;
    }

    private IActionResult KioskView(string? name = null, object? model = null)
    {
        ViewData["IdleSeconds"] = Idle;
        return View(name, model);
    }

    private List<KioskReceiptLine> Receipt() =>
        JsonSerializer.Deserialize<List<KioskReceiptLine>>(HttpContext.Session.GetString(SessionKey) ?? "[]") ?? new();

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (await PatronIdAsync() is not null) return RedirectToAction(nameof(Home));
        if (branch.CurrentBranch is null) return KioskView("Misconfigured");
        return KioskView(null, new PatronLoginForm());
    }

    [HttpPost("signin")]
    public async Task<IActionResult> SignIn(PatronLoginForm form)
    {
        var result = await auth.SignInAsync(form.CardNumber, form.Pin);
        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return KioskView("Index", new PatronLoginForm { CardNumber = form.CardNumber });
        }
        HttpContext.Session.Remove(SessionKey);
        await HttpContext.SignInAsync(Auth.Kiosk, Auth.Identity(Auth.Kiosk, result.Patron!.BorrowerId, result.Patron.FullName));
        return RedirectToAction(nameof(Home));
    }

    [HttpGet("home")]
    public async Task<IActionResult> Home()
    {
        if (await PatronIdAsync() is not { } id) return RedirectToAction(nameof(Index));
        var summary = loans.GetAccount(id);
        if (summary is null) return RedirectToAction(nameof(Index));
        return KioskView(null, summary);
    }

    [HttpGet("checkout")]
    public async Task<IActionResult> Checkout()
    {
        if (await PatronIdAsync() is null) return RedirectToAction(nameof(Index));
        return KioskView(null, Receipt());
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(string code)
    {
        if (await PatronIdAsync() is not { } id) return RedirectToAction(nameof(Index));
        var here = branch.CurrentBranch;
        if (here is null) return KioskView("Misconfigured");
        if (string.IsNullOrWhiteSpace(code))
        {
            TempData["Error"] = "Scan or type the item's library code.";
            return RedirectToAction(nameof(Checkout));
        }

        var result = await lending.BorrowAsync(code, id, here.Id);
        if (result.Success)
        {
            var list = Receipt();
            list.Add(new KioskReceiptLine { Code = result.Receipt!.LibraryCode, Name = result.Receipt.ItemName, DueOn = result.Receipt.DueOn });
            HttpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(list));
            TempData["Ok"] = $"\"{result.Receipt.ItemName}\" is yours until {Fmt.Date(result.Receipt.DueOn)}.";
        }
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Checkout));
    }

    [HttpGet("holds")]
    public async Task<IActionResult> Holds()
    {
        if (await PatronIdAsync() is not { } id) return RedirectToAction(nameof(Index));
        return KioskView(null, loans.GetAccount(id));
    }

    [HttpPost("holds")]
    public async Task<IActionResult> PlaceHold(string code)
    {
        if (await PatronIdAsync() is not { } id) return RedirectToAction(nameof(Index));
        var item = string.IsNullOrWhiteSpace(code) ? null : await catalogue.FindByCodeAsync(code);
        if (item is null) TempData["Error"] = "We couldn't find that library code.";
        else
        {
            var r = await reservations.PlaceAsync(item.Id, id, ReservationSource.Kiosk);
            TempData[r.Success ? "Ok" : "Error"] = r.Success ? $"Hold placed on \"{item.Name}\"." : r.Error;
        }
        return RedirectToAction(nameof(Holds));
    }

    [HttpPost("holds/cancel")]
    public async Task<IActionResult> CancelHold(int id)
    {
        if (await PatronIdAsync() is not { } me) return RedirectToAction(nameof(Index));
        var r = await reservations.CancelAsync(id, me);
        TempData[r.Success ? "Ok" : "Error"] = r.Success ? "Hold cancelled." : r.Error;
        return RedirectToAction(nameof(Holds));
    }

    [HttpGet("receipt")]
    public async Task<IActionResult> ReceiptPage()
    {
        if (await PatronIdAsync() is null) return RedirectToAction(nameof(Index));
        return KioskView("Receipt", Receipt());
    }

    [HttpPost("signout")]
    public async Task<IActionResult> SignOutKiosk(bool idle = false)
    {
        await HttpContext.SignOutAsync(Auth.Kiosk);
        HttpContext.Session.Remove(SessionKey);
        if (idle) TempData["Ok"] = "You were signed out because the kiosk was idle.";
        return RedirectToAction(nameof(Index));
    }
}
