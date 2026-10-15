using Library.Core.Contracts;
using Library.Core.Services;
using Library.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

/// <summary>
/// Notification dashboard (client 3.3) with the simulated-today setting and "Run checks now".
/// TODO: add [Authorize(Roles = ...)] when Person A's roles are merged (Reception own branch, Admin all, Manager view).
/// </summary>
[Route("notifications")]
public class NotificationsController(
    NotificationService notifications,
    INotificationService checks,
    SimulatedClock clock) : Controller
{
    [HttpGet("")]
    public IActionResult Index(string? type, string? patron)
    {
        var all = notifications.GetAll();
        IEnumerable<NotificationRecord> rows = all;

        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<NotificationType>(type, out var parsed))
            rows = rows.Where(r => r.Type == parsed);
        if (!string.IsNullOrWhiteSpace(patron))
            rows = rows.Where(r => r.BorrowerName.Contains(patron, StringComparison.OrdinalIgnoreCase));

        var counts = all.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Count());
        return View(new NotificationsVm(rows.ToList(), counts, type, patron, clock.Today, clock.SimulatedToday is not null));
    }

    [HttpPost("run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run()
    {
        var created = await checks.RunScheduledChecksAsync(clock.Today);
        TempData["Msg"] = $"Checks ran for {clock.Today:d MMM yyyy}. {created} new notification(s) created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("date")]
    [ValidateAntiForgeryToken]
    public IActionResult SetDate(string? date)
    {
        if (DateOnly.TryParse(date, out var parsed))
        {
            clock.SetSimulatedToday(parsed);
            TempData["Msg"] = $"Simulated today is now {parsed:d MMM yyyy}.";
        }
        else
        {
            TempData["Msg"] = "Please choose a valid date.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cleardate")]
    [ValidateAntiForgeryToken]
    public IActionResult ClearDate()
    {
        clock.SetSimulatedToday(null);
        TempData["Msg"] = "Back to the real date.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("clear")]
    [ValidateAntiForgeryToken]
    public IActionResult ClearAll()
    {
        notifications.Clear();
        TempData["Msg"] = "Demo notifications cleared.";
        return RedirectToAction(nameof(Index));
    }
}
