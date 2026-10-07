using Library.Core.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

/// <summary>
/// Developer diagnostics, available only when ASPNETCORE_ENVIRONMENT=Development.
/// Shows which shared services are still stubs, so the team can see integration progress at a glance.
/// </summary>
public class DevController(IServiceProvider services, IWebHostEnvironment env) : Controller
{
    private static readonly (string Name, Type Contract, string Owner)[] Contracts =
    [
        ("IClock", typeof(IClock), "B (simulated date)"),
        ("IBranchContext", typeof(IBranchContext), "A"),
        ("IPatronAuthService", typeof(IPatronAuthService), "A"),
        ("ILendingService", typeof(ILendingService), "A"),
        ("ITransferService", typeof(ITransferService), "A"),
        ("ICatalogueQuery", typeof(ICatalogueQuery), "A (C extends)"),
        ("INotificationService", typeof(INotificationService), "B"),
        ("IReservationService", typeof(IReservationService), "B")
    ];

    [Route("dev/contracts")]
    public IActionResult ContractsStatus()
    {
        if (!env.IsDevelopment()) return NotFound();

        var rows = Contracts.Select(c =>
        {
            var impl = services.GetService(c.Contract)?.GetType().Name ?? "(not registered)";
            var isStub = impl.StartsWith("Stub", StringComparison.Ordinal) || impl == "SystemClock";
            return new ContractRow(c.Name, c.Owner, impl, isStub);
        }).ToList();

        return View(rows);
    }
}

public sealed record ContractRow(string Contract, string Owner, string Implementation, bool IsStub);
