using Library.Core.Contracts;
using Library.Core.Services;
using Library.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

/// <summary>Inter-branch transfers (feature 3.4): request, dispatch, receive and cancel. Managers can look but not change.</summary>
[Route("transfers"), Authorize(Roles = Auth.AnyStaff)]
public sealed class TransfersController(TransferService transfers, ITransferService service, InventoryService inventory) : AppController
{
    [HttpGet("")]
    public IActionResult Index(bool history = false)
    {
        ViewBag.Branches = inventory.GetBranches().ToDictionary(b => b.Id);
        ViewBag.Active = inventory.GetBranches(true);
        ViewBag.History = history;
        ViewBag.Here = Here.CurrentBranch?.Id;
        ViewBag.CanChange = !IsManager;
        return View(transfers.ListAll(null, activeOnly: !history));
    }

    [HttpPost("request"), Authorize(Roles = Auth.AdminOrReception)]
    public async Task<IActionResult> RequestTransfer(string code, int toBranchId, string? note)
    {
        var item = inventory.FindItemByCode(code);
        if (item is null) { Fail($"No item has the library code '{code}'."); return RedirectToAction(nameof(Index)); }
        if (!MayAct(item.BranchId) && !MayAct(toBranchId)) { Fail("Reception staff can request transfers to or from the branch they are working at."); return RedirectToAction(nameof(Index)); }
        var r = await service.RequestAsync(item.Id, toBranchId, UserId, note);
        if (r.Success) Flash($"Transfer requested for \"{r.Transfer!.ItemName}\". Dispatch it from {inventory.GetBranch(r.Transfer.FromBranchId)?.Name} when it is packed.");
        else Fail(r.Error ?? "Could not request that transfer.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/dispatch"), Authorize(Roles = Auth.AdminOrReception)]
    public async Task<IActionResult> Dispatch(int id)
    {
        var t = transfers.ListAll().FirstOrDefault(x => x.Id == id);
        if (t is null || !MayAct(t.FromBranchId)) { Fail("Only the sending branch can dispatch a transfer."); return RedirectToAction(nameof(Index)); }
        var r = await service.DispatchAsync(id, UserId);
        if (r.Success) Flash($"\"{r.Transfer!.ItemName}\" is now in transit."); else Fail(r.Error ?? "Could not dispatch.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/receive"), Authorize(Roles = Auth.AdminOrReception)]
    public async Task<IActionResult> Receive(int id)
    {
        var t = transfers.ListAll().FirstOrDefault(x => x.Id == id);
        if (t is null || !MayAct(t.ToBranchId)) { Fail("Only the receiving branch can receive a transfer."); return RedirectToAction(nameof(Index)); }
        var r = await service.ReceiveAsync(id, UserId);
        if (r.Success) Flash($"\"{r.Transfer!.ItemName}\" received. It can now be borrowed at {inventory.GetBranch(r.Transfer.ToBranchId)?.Name}."); else Fail(r.Error ?? "Could not receive.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/cancel"), Authorize(Roles = Auth.AdminOrReception)]
    public async Task<IActionResult> Cancel(int id)
    {
        var t = transfers.ListAll().FirstOrDefault(x => x.Id == id);
        if (t is null || !(MayAct(t.FromBranchId) || MayAct(t.ToBranchId))) { Fail("Only the sending or receiving branch can cancel a transfer."); return RedirectToAction(nameof(Index)); }
        var r = await service.CancelAsync(id, UserId);
        if (r.Success) Flash("Transfer cancelled."); else Fail(r.Error ?? "Could not cancel.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Admins may act for any branch; reception only for the branch they are working at.</summary>
    private bool MayAct(int branchId) => IsAdmin || Here.CurrentBranch?.Id == branchId;
}
