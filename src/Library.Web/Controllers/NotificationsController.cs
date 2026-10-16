using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Services;
using Library.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

/// <summary>The simulated email/SMS dashboard (feature 3.3), plus the admin controls for the simulated date and the daily checks.</summary>
[Route("notifications"), Authorize(Roles = Auth.AnyStaff)]
public sealed class NotificationsController(NotificationService notifications, INotificationService service, ILibraryStore store, IClock clock) : AppController
{
    [HttpGet("")]
    public IActionResult Index(NotificationType? type, NotificationChannel? channel, string? q, bool allBranches = false, int page = 1)
    {
        int? branch = allBranches || IsManager || IsAdmin || Here.CurrentBranch is null ? null : Here.CurrentBranch.Id;
        var rows = notifications.List(branch, type, channel)
            .Where(n => string.IsNullOrWhiteSpace(q) || (n.BorrowerName + " " + n.Subject + " " + n.Recipient).Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        ViewBag.Type = type; ViewBag.Channel = channel; ViewBag.Q = q; ViewBag.All = allBranches;
        ViewBag.Page = Math.Max(1, page); ViewBag.Total = rows.Count; ViewBag.Pages = (int)Math.Ceiling(rows.Count / 30.0);
        ViewBag.Today = clock.Today;
        ViewBag.Simulated = store.Read(d => d.Settings.SimulatedToday);
        ViewBag.ScopedToBranch = branch is not null;
        return View(rows.Skip((Math.Max(1, page) - 1) * 30).Take(30).ToList());
    }

    [HttpPost("run"), Authorize(Roles = Auth.Admin)]
    public async Task<IActionResult> Run()
    {
        var n = await service.RunScheduledChecksAsync(clock.Today);
        Flash(n == 0 ? $"Checks run for {Fmt.Date(clock.Today)}: nothing new to send." : $"Checks run for {Fmt.Date(clock.Today)}: {n} new notification{(n == 1 ? "" : "s")} created.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("date"), Authorize(Roles = Auth.Admin)]
    public IActionResult SetDate(DateOnly? simulatedToday, string? action)
    {
        if (action == "clear") { store.Write(d => d.Settings.SimulatedToday = null); Flash("Using the real date again."); }
        else if (simulatedToday is { } day) { store.Write(d => d.Settings.SimulatedToday = day); Flash($"The system now thinks today is {Fmt.Date(day)}."); }
        else Fail("Choose a date.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("advance"), Authorize(Roles = Auth.Admin)]
    public IActionResult Advance(int days)
    {
        var next = clock.Today.AddDays(Math.Clamp(days, -365, 365));
        store.Write(d => d.Settings.SimulatedToday = next);
        Flash($"The system now thinks today is {Fmt.Date(next)}. Press \"Run checks now\" to create the notifications for that day.");
        return RedirectToAction(nameof(Index));
    }
}
