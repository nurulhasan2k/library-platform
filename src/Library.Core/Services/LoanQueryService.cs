using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Models;
using Microsoft.Extensions.Options;

namespace Library.Core.Services;

/// <summary>A loan with the names a person needs to read it.</summary>
/// <param name="DaysLate">Active loan: days overdue today. Returned loan: days late when it came back.</param>
/// <param name="Fine">Active loan: the fine if it were returned today. Returned loan: the fine charged.</param>
public sealed record LoanDetails(
    Loan Loan, int ItemId, string ItemName, string LibraryCode, ItemType ItemType, int BorrowerId, string BorrowerName,
    string CardNumber, string BranchName, string? ReturnBranchName, int DaysLate, decimal Fine)
{
    public bool IsOverdue => Loan.IsActive && DaysLate > 0;
}

public sealed record LoanFilter(
    bool ActiveOnly = false, int? BorrowerId = null, int? BranchId = null, bool OverdueOnly = false,
    bool UnpaidFinesOnly = false, int? ItemId = null, string? Text = null);

/// <summary>What the kiosk and the patron's web account show: everything about one patron's library use.</summary>
public sealed record AccountSummary(
    int BorrowerId, string Name, string CardNumber, NotificationChannel PreferredChannel,
    IReadOnlyList<LoanDetails> ActiveLoans, IReadOnlyList<LoanDetails> RecentHistory, IReadOnlyList<HoldDetails> Holds,
    decimal UnpaidFines, decimal AccruingFines)
{
    public decimal TotalOwing => UnpaidFines + AccruingFines;
}

/// <summary>Reading loans, fines and a patron's account summary.</summary>
public sealed class LoanQueryService(ILibraryStore store, IClock clock, IOptions<LendingOptions> options, ReservationService reservations)
{
    public IReadOnlyList<LoanDetails> GetLoans(LoanFilter? filter = null)
    {
        filter ??= new LoanFilter();
        var today = clock.Today;
        var terms = (filter.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return store.Read(d =>
        {
            var rows = d.Loans
                .Where(l => !filter.ActiveOnly || l.IsActive)
                .Where(l => filter.BorrowerId is null || l.BorrowerId == filter.BorrowerId)
                .Where(l => filter.BranchId is null || l.BranchId == filter.BranchId)
                .Where(l => filter.ItemId is null || l.ItemId == filter.ItemId)
                .Where(l => !filter.OverdueOnly || (l.IsActive && l.DaysLate(today) > 0))
                .Where(l => !filter.UnpaidFinesOnly || l.HasOutstandingFine)
                .Select(l => Describe(d, l, today))
                .Where(x => terms.All(t => $"{x.ItemName} {x.LibraryCode} {x.BorrowerName} {x.CardNumber}".Contains(t, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(x => x.Loan.BorrowedOn).ThenByDescending(x => x.Loan.Id)
                .ToList();
            return (IReadOnlyList<LoanDetails>)rows;
        });
    }

    public void MarkFinePaid(int loanId)
    {
        var today = clock.Today;
        store.Write(d =>
        {
            var l = d.Loans.FirstOrDefault(x => x.Id == loanId) ?? throw new LibraryException("No such loan.");
            if (l.IsActive) throw new LibraryException("Fines are charged when the item is returned.");
            if (l.FineAmount <= 0) throw new LibraryException("This loan has no fine.");
            if (l.FinePaid) throw new LibraryException("This fine is already marked as paid.");
            l.FinePaid = true;
            l.FinePaidOn = today;
        });
    }

    public AccountSummary? GetAccount(int borrowerId)
    {
        var today = clock.Today;
        var perDay = options.Value.FinePerDay;
        var core = store.Read(d =>
        {
            var b = d.FindBorrower(borrowerId);
            if (b is null) return null;
            var mine = d.Loans.Where(l => l.BorrowerId == borrowerId).ToList();
            var active = mine.Where(l => l.IsActive).OrderBy(l => l.DueOn).Select(l => Describe(d, l, today)).ToList();
            var history = mine.Where(l => !l.IsActive).OrderByDescending(l => l.ReturnedOn).ThenByDescending(l => l.Id).Take(10)
                .Select(l => Describe(d, l, today)).ToList();
            var unpaid = mine.Where(l => l.HasOutstandingFine).Sum(l => l.FineAmount);
            var accruing = active.Sum(a => a.Fine);
            return new { b, active, history, unpaid, accruing };
        });
        if (core is null) return null;
        _ = perDay;
        var holds = reservations.ListHolds(openOnly: true, borrowerId: borrowerId);
        return new AccountSummary(core.b.Id, core.b.Name, core.b.CardNumber, core.b.PreferredChannel, core.active, core.history, holds, core.unpaid, core.accruing);
    }

    private LoanDetails Describe(LibraryData d, Loan l, DateOnly asOf)
    {
        var item = d.FindItem(l.ItemId);
        var b = d.FindBorrower(l.BorrowerId);
        var late = l.DaysLate(l.ReturnedOn ?? asOf);
        var fine = l.IsActive ? late * options.Value.FinePerDay : l.FineAmount;
        return new LoanDetails(l, l.ItemId, item?.Name ?? "(deleted)", item?.LibraryCode ?? "?", item?.Type ?? ItemType.Book, l.BorrowerId,
            b?.Name ?? "?", b?.CardNumber ?? "", d.FindBranch(l.BranchId)?.Name ?? "?",
            l.ReturnBranchId is { } rb ? d.FindBranch(rb)?.Name : null, late, fine);
    }
}
