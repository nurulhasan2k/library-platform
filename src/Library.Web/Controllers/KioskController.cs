using System.Text.Json;
using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Services;
using Library.Web.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Library.Web.Controllers;

/// <summary>
/// Self-service kiosk (client 3.1). Own simplified layout, touch friendly, no staff navigation.
/// Checkout always goes through ILendingService; sign-in through IPatronAuthService.
/// The signed-in patron is kept in a small encrypted cookie (no session middleware needed, so Program.cs stays untouched).
/// </summary>
[Route("kiosk")]
public class KioskController(
    ILendingService lending,
    IPatronAuthService auth,
    IBranchContext branch,
    IClock clock,
    IOptions<KioskOptions> kioskOptions,
    IOptions<LendingOptions> lendingOptions,
    DemoLibraryStore store,
    ReservationService reservations,
    IDataProtectionProvider dataProtection) : Controller
{
    private const string CookieName = ".Library.Kiosk";
    private const int MaxItemsPerVisit = 12;
    private static readonly TimeSpan ServerIdleLimit = TimeSpan.FromMinutes(5);

    private sealed record KioskState(int PatronId, string PatronName, List<LoanReceipt> Items, DateTime LastSeenUtc);

    private readonly IDataProtector protector = dataProtection.CreateProtector("Library.Kiosk.v1");
    private KioskState? state;
    private bool stateLoaded;

    private KioskState? State
    {
        get
        {
            if (stateLoaded) return state;
            stateLoaded = true;
            var raw = Request.Cookies[CookieName];
            if (string.IsNullOrEmpty(raw)) return null;
            try
            {
                var loaded = JsonSerializer.Deserialize<KioskState>(protector.Unprotect(raw));
                if (loaded is null || DateTime.UtcNow - loaded.LastSeenUtc > ServerIdleLimit) return null;
                state = loaded;
            }
            catch (Exception)
            {
                state = null; // a damaged or old cookie simply means "not signed in"
            }
            return state;
        }
    }

    private void Save(KioskState s)
    {
        state = s with { LastSeenUtc = DateTime.UtcNow };
        stateLoaded = true;
        Response.Cookies.Append(CookieName, protector.Protect(JsonSerializer.Serialize(state)), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true
        });
    }

    private void Clear()
    {
        state = null;
        stateLoaded = true;
        Response.Cookies.Delete(CookieName);
    }

    private int BranchId => branch.CurrentBranch?.Id ?? 1;
    private string BranchName => branch.CurrentBranch?.Name ?? kioskOptions.Value.BranchCode;

    private void Prepare(bool signedIn)
    {
        ViewData["BranchName"] = BranchName;
        if (signedIn) ViewData["Idle"] = kioskOptions.Value.IdleTimeoutSeconds;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        Prepare(false);
        return View();
    }

    [HttpGet("signin")]
    public IActionResult SignIn()
    {
        Prepare(false);
        return View();
    }

    [HttpPost("signin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(string? cardNumber, string? pin)
    {
        var result = await auth.SignInAsync(cardNumber ?? "", pin ?? "");
        if (!result.Success || result.Patron is null)
        {
            Prepare(false);
            ViewData["Error"] = result.Error ?? "Sign-in failed. Please try again.";
            return View();
        }

        Save(new KioskState(result.Patron.BorrowerId, result.Patron.FullName, new List<LoanReceipt>(), DateTime.UtcNow));
        return RedirectToAction(nameof(Home));
    }

    [HttpGet("home")]
    public IActionResult Home()
    {
        if (State is not { } s) return RedirectToAction(nameof(Index));
        Save(s);
        Prepare(true);
        ViewData["PatronName"] = s.PatronName;
        return View();
    }

    [HttpGet("checkout")]
    public IActionResult Checkout()
    {
        if (State is not { } s) return RedirectToAction(nameof(Index));
        Save(s);
        Prepare(true);
        return View(new KioskCheckoutVm(s.Items, null, null));
    }

    [HttpPost("checkout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(string? code)
    {
        if (State is not { } s) return RedirectToAction(nameof(Index));
        Prepare(true);

        if (string.IsNullOrWhiteSpace(code))
        {
            Save(s);
            return View(new KioskCheckoutVm(s.Items, "Please scan or type the code on the item.", null));
        }

        if (s.Items.Count >= MaxItemsPerVisit)
        {
            Save(s);
            return View(new KioskCheckoutVm(s.Items, "That is the most items for one visit. Please finish and take your receipt.", null));
        }

        var result = await lending.BorrowAsync(code.Trim(), s.PatronId, BranchId);
        if (!result.Success || result.Receipt is null)
        {
            Save(s);
            return View(new KioskCheckoutVm(s.Items, result.Error ?? "This item could not be borrowed.", null));
        }

        s.Items.Add(result.Receipt);
        Save(s);
        return View(new KioskCheckoutVm(s.Items, null, result.Receipt.ItemName));
    }

    [HttpGet("receipt")]
    public IActionResult Receipt()
    {
        if (State is not { } s) return RedirectToAction(nameof(Index));
        if (s.Items.Count == 0)
        {
            Save(s);
            return RedirectToAction(nameof(Home));
        }

        Prepare(false);
        var vm = new KioskReceiptVm(s.PatronName, BranchName, s.Items.ToList());
        Clear(); // the session ends after the receipt is shown
        return View(vm);
    }

    [HttpGet("account")]
    public IActionResult Account()
    {
        if (State is not { } s) return RedirectToAction(nameof(Index));
        Save(s);
        Prepare(true);

        var today = clock.Today;
        var finePerDay = lendingOptions.Value.FinePerDay;
        var loans = store.LoansSnapshot()
            .Where(l => l.BorrowerId == s.PatronId && !l.Returned)
            .OrderBy(l => l.DueOn)
            .Select(l =>
            {
                var daysLeft = l.DueOn.DayNumber - today.DayNumber;
                var fine = daysLeft < 0 ? -daysLeft * finePerDay : 0m;
                return new AccountLoan(store.FindItem(l.ItemId)?.Name ?? "Unknown item", l.DueOn, daysLeft, fine);
            })
            .ToList();

        return View(new KioskAccountVm(s.PatronName, loans, reservations.GetHoldsForBorrower(s.PatronId), loans.Sum(l => l.Fine)));
    }

    [HttpGet("finish")]
    public IActionResult Finish()
    {
        Clear();
        return RedirectToAction(nameof(Index));
    }
}
