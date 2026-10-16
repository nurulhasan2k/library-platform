using Library.Core.Contracts;
using Library.Core.Models;
using Library.Core.Services;
using Library.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

/// <summary>Borrower records (brief 2c): add, change, look up and delete borrowers; issue cards and PINs.</summary>
[Route("borrowers"), Authorize(Roles = Auth.AdminOrReception)]
public sealed class BorrowersController(BorrowerService borrowers, LoanQueryService loans) : AppController
{
    [HttpGet("")]
    public IActionResult Index(string? q)
    {
        ViewBag.Q = q;
        return View(borrowers.ListBorrowers(q));
    }

    [HttpGet("new")]
    public IActionResult Create() => View("Edit", new Borrower());

    [HttpPost("new")]
    public IActionResult Create(Borrower form)
    {
        try
        {
            var created = borrowers.AddBorrower(form);
            TempData["NewCard"] = created.Borrower.CardNumber;
            TempData["NewPin"] = created.Pin;
            Flash($"Added {created.Borrower.Name}.");
            return RedirectToAction(nameof(Details), new { id = created.Borrower.Id });
        }
        catch (LibraryException ex) { Fail(ex.Message); return View("Edit", form); }
    }

    [HttpGet("{id:int}")]
    public IActionResult Details(int id)
    {
        var summary = loans.GetAccount(id);
        return summary is null ? NotFound() : View(summary);
    }

    [HttpGet("{id:int}/edit")]
    public IActionResult Edit(int id) => borrowers.GetBorrower(id) is { } b ? View(b) : NotFound();

    [HttpPost("{id:int}/edit")]
    public IActionResult Edit(int id, Borrower form)
    {
        form.Id = id;
        if (!Try(() => borrowers.UpdateBorrower(form))) return View(form);
        Flash("Saved.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/reset-pin")]
    public IActionResult ResetPin(int id)
    {
        try
        {
            var pin = borrowers.ResetPin(id);
            TempData["NewCard"] = borrowers.GetBorrower(id)!.CardNumber;
            TempData["NewPin"] = pin;
            Flash("New PIN created. Tell the patron; it is only shown once.");
        }
        catch (LibraryException ex) { Fail(ex.Message); }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/delete")]
    public IActionResult Delete(int id)
    {
        if (Try(() => borrowers.DeleteBorrower(id))) { Flash("Borrower deleted."); return RedirectToAction(nameof(Index)); }
        return RedirectToAction(nameof(Details), new { id });
    }
}
