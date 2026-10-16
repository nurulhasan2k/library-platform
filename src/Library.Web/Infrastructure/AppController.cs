using Library.Core.Contracts;
using Library.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Infrastructure;

/// <summary>Shared helpers for controllers: flash messages, the signed-in staff user and the chosen branch.</summary>
public abstract class AppController : Controller
{
    protected int UserId => User.GetUserId();
    protected bool IsAdmin => User.IsInRole(Auth.Admin);
    protected bool IsManager => User.IsInRole(Auth.Manager);
    protected IBranchContext Here => HttpContext.RequestServices.GetRequiredService<IBranchContext>();

    protected void Flash(string message) => TempData["Ok"] = message;
    protected void Fail(string message) => TempData["Error"] = message;

    /// <summary>Send the user to choose a branch first (reception and admin desk work needs one).</summary>
    protected IActionResult NeedBranch() =>
        RedirectToAction("Branch", "Staff", new { returnUrl = Request.Path + Request.QueryString });

    /// <summary>Runs an action that may break a library rule; shows the rule's message instead of crashing.</summary>
    protected bool Try(Action action)
    {
        try { action(); return true; }
        catch (LibraryException ex) { Fail(ex.Message); return false; }
    }
}
