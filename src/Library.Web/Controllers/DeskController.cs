using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Services;
using Library.Web.Infrastructure;
using Library.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

/// <summary>Reception work (brief 2): lend and take back items by library code at the chosen branch, list loans, record fine payments.</summary>
[Route("desk"), Authorize(Roles = Auth.AdminOrReception)]
public sealed class DeskController(
    ILendingService lending, LoanQueryService loans, BorrowerService borrowers, InventoryService inventory, WebBranchContext context) : AppController
{
    [HttpGet("")]
    public IActionResult Index(string? lastCode, int? lastBorrower)
    {
        if (Here.CurrentBranch is not { } here) return NeedBranch();
        return View(new DeskPage
        {
            Branch = inventory.GetBranch(here.Id)!, DeskName = context.CurrentDeskName, Borrowers = borrowers.ListBorrowers(),
            RecentLoans = loans.GetLoans(new LoanFilter(BranchId: here.Id)).Take(10).ToList(),
            Overdue = loans.GetLoans(new LoanFilter(OverdueOnly: true, BranchId: here.Id)).Take(5).ToList(),
            LastBorrowCode = lastCode, LastBorrowerId = lastBorrower
        });
    }

    [HttpPost("borrow")]
    public async Task<IActionResult> Borrow(string code, string borrower)
    {
        if (Here.CurrentBranch is not { } here) return NeedBranch();
        var b = FindBorrower(borrower);
        if (b is null)
        {
            Fail($"No borrower matches '{borrower}'. Type a card number or choose from the list.");
            return RedirectToAction(nameof(Index), new { lastCode = code });
        }
        var result = await lending.BorrowAsync(code, b.Id, here.Id);
        if (result.Success)
            Flash($"Lent \"{result.Receipt!.ItemName}\" to {b.Name}. Due back {Fmt.Date(result.Receipt.DueOn)}.");
        else
            Fail(result.Error ?? "Could not lend that item.");
        return RedirectToAction(nameof(Index), new { lastBorrower = b.Id, lastCode = result.Success ? null : code });
    }

    [HttpPost("return")]
    public async Task<IActionResult> Return(string code, bool damaged)
    {
        if (Here.CurrentBranch is not { } here) return NeedBranch();
        var result = await lending.ReturnAsync(code, here.Id, damaged);
        if (!result.Success) { Fail(result.Error ?? "Could not take that item back."); return RedirectToAction(nameof(Index)); }

        var parts = new List<string> { $"Returned {code.Trim().ToUpperInvariant()}." };
        if (result.DaysLate > 0) parts.Add($"{result.DaysLate} day{(result.DaysLate == 1 ? "" : "s")} late: fine {Fmt.Money(result.FineAmount)} to collect.");
        if (damaged) parts.Add("Marked as damaged - it won't be lent again until an administrator checks it.");
        if (result.ItemMovedBranch) parts.Add($"It now belongs to {result.MovedToBranchName} (it was from {result.MovedFromBranchName}).");
        Flash(string.Join(" ", parts));
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("loans")]
    public IActionResult Loans(string? q, bool overdue = false, bool active = false, bool unpaid = false, bool allBranches = false, int page = 1)
    {
        int? branchId = allBranches || Here.CurrentBranch is null ? null : Here.CurrentBranch.Id;
        var all = loans.GetLoans(new LoanFilter(active, null, branchId, overdue, unpaid, null, q));
        ViewBag.Q = q; ViewBag.Overdue = overdue; ViewBag.Active = active; ViewBag.Unpaid = unpaid; ViewBag.All = allBranches;
        ViewBag.Page = Math.Max(1, page); ViewBag.Total = all.Count; ViewBag.Pages = (int)Math.Ceiling(all.Count / 25.0);
        return View(all.Skip((Math.Max(1, page) - 1) * 25).Take(25).ToList());
    }

    [HttpPost("fine-paid")]
    public IActionResult FinePaid(int loanId, string? returnUrl)
    {
        if (Try(() => loans.MarkFinePaid(loanId))) Flash("Fine marked as paid.");
        return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/desk/loans?unpaid=true");
    }

    private Library.Core.Models.Borrower? FindBorrower(string? text)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0) return null;
        var byCard = borrowers.FindByCard(text);
        if (byCard != null) return byCard;
        // The datalist fills "Name (LIB-1001)": take what is inside the brackets.
        var open = text.LastIndexOf('(');
        if (open >= 0 && text.EndsWith(')') && borrowers.FindByCard(text[(open + 1)..^1]) is { } fromList) return fromList;
        var matches = borrowers.ListBorrowers(text);
        return matches.Count == 1 ? matches[0] : null;
    }
}
