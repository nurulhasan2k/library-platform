using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Services;
using Library.Web.Infrastructure;
using Library.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

[Route("staff")]
public sealed class StaffController(
    StaffService staff, ILibraryStore store, IClock clock, LoanQueryService loans, ReservationService reservations,
    TransferService transfers, InventoryService inventory) : AppController
{
    [HttpGet("login"), AllowAnonymous]
    public IActionResult Login(string? returnUrl) =>
        User.Identity?.IsAuthenticated == true ? RedirectToAction(nameof(Index)) : View(new LoginForm { ReturnUrl = returnUrl });

    [HttpPost("login"), AllowAnonymous]
    public async Task<IActionResult> Login(LoginForm form)
    {
        var user = staff.Authenticate(form.Username, form.Password);
        if (user is null)
        {
            Fail("Username or password is not correct.");
            return View(new LoginForm { Username = form.Username, ReturnUrl = form.ReturnUrl });
        }

        await HttpContext.SignInAsync(Auth.Staff, Auth.Identity(Auth.Staff, user.Id, user.Username, user.Role.ToString()));
        HttpContext.Session.Clear();

        // A staff member who may only use one branch doesn't need to choose.
        if (user.Role != Library.Core.Models.UserRole.Manager)
        {
            var allowed = inventory.GetBranches(activeOnly: true).Where(b => user.MayUseBranch(b.Id)).ToList();
            if (allowed.Count == 1)
            {
                HttpContext.Session.SetInt32(WebBranchContext.BranchKey, allowed[0].Id);
                HttpContext.Session.SetInt32(WebBranchContext.DeskKey, allowed[0].Desks.First().Id);
                return Redirect(Url.IsLocalUrl(form.ReturnUrl) ? form.ReturnUrl! : "/staff");
            }
            if (allowed.Count > 1)
                return RedirectToAction(nameof(Branch), new { returnUrl = Url.IsLocalUrl(form.ReturnUrl) ? form.ReturnUrl : "/staff" });
        }
        return Redirect(Url.IsLocalUrl(form.ReturnUrl) ? form.ReturnUrl! : "/staff");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(Auth.Staff);
        HttpContext.Session.Clear();
        return Redirect("/");
    }

    [HttpGet("denied")]
    public IActionResult Denied() => View();

    [HttpGet("branch"), Authorize(Roles = Auth.AdminOrReception)]
    public IActionResult Branch(string? returnUrl)
    {
        var user = staff.GetUser(UserId);
        var allowed = inventory.GetBranches(activeOnly: true).Where(b => user is not null && user.MayUseBranch(b.Id)).ToList();
        return View(new BranchChoice
        {
            Branches = allowed, ReturnUrl = returnUrl,
            BranchId = HttpContext.Session.GetInt32(WebBranchContext.BranchKey) ?? allowed.FirstOrDefault()?.Id ?? 0,
            DeskId = HttpContext.Session.GetInt32(WebBranchContext.DeskKey) ?? 0
        });
    }

    [HttpPost("branch"), Authorize(Roles = Auth.AdminOrReception)]
    public IActionResult Branch(BranchChoice form)
    {
        var user = staff.GetUser(UserId);
        var branch = inventory.GetBranch(form.BranchId);
        if (user is null || branch is null || !branch.IsActive || !user.MayUseBranch(branch.Id))
        {
            Fail("You can't work at that branch.");
            return RedirectToAction(nameof(Branch), new { form.ReturnUrl });
        }
        var desk = branch.Desks.FirstOrDefault(d => d.Id == form.DeskId) ?? branch.Desks.First();
        HttpContext.Session.SetInt32(WebBranchContext.BranchKey, branch.Id);
        HttpContext.Session.SetInt32(WebBranchContext.DeskKey, desk.Id);
        Flash($"Working at {branch.Name}, {desk.Name}.");
        return Redirect(Url.IsLocalUrl(form.ReturnUrl) ? form.ReturnUrl! : "/staff");
    }

    [HttpGet(""), Authorize(Roles = Auth.AnyStaff)]
    public IActionResult Index()
    {
        var model = new StaffDashboard { Today = clock.Today };
        model.Simulated = store.Read(d => d.Settings.SimulatedToday) is not null;
        model.Branches = inventory.GetBranches().ToDictionary(b => b.Id);

        var ctx = Branch_();
        var branchId = ctx?.Id;
        model.Branch = branchId is { } b ? inventory.GetBranch(b) : null;
        model.Overdue = loans.GetLoans(new LoanFilter(OverdueOnly: true, BranchId: branchId)).Take(8).ToList();
        model.ActiveLoans = loans.GetLoans(new LoanFilter(ActiveOnly: true, BranchId: branchId)).Count;
        model.ReadyHolds = reservations.ListHolds(true, branchId).Where(h => h.Status == ReservationStatus.Ready).Take(8).ToList();
        if (branchId is { } id)
        {
            model.Inbound = TestWait(transfers.GetInboundAsync(id));
            model.Outbound = TestWait(transfers.GetOutboundAsync(id));
        }
        else
        {
            model.Inbound = transfers.ListAll(null, true).Where(t => t.Status == TransferStatus.InTransit).ToList();
        }
        model.NotificationsToday = store.Read(d => d.Notifications.Count(n => DateOnly.FromDateTime(n.CreatedAt) == clock.Today));
        return View(model);
    }

    private Library.Core.Models.Branch? Branch_() => Here.CurrentBranch is { } c ? inventory.GetBranch(c.Id) : null;
    private static T TestWait<T>(Task<T> t) => t.GetAwaiter().GetResult();
}
