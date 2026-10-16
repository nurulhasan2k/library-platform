using System.Security.Cryptography;
using Library.Core.Contracts;
using Library.Core.Data;
using Library.Core.Models;

namespace Library.Core.Services;

public sealed record NewBorrower(Borrower Borrower, string Pin);

/// <summary>Reception's borrower records (brief 2c), now with a library card number and PIN (decision 3).</summary>
public sealed class BorrowerService(ILibraryStore store)
{
    public NewBorrower AddBorrower(Borrower borrower)
    {
        Normalise(borrower);
        InventoryService.EnsureValid(borrower.GetValidationErrors());
        var pin = NewPin();
        var saved = store.Write(data =>
        {
            borrower.Id = data.TakeId();
            borrower.CardNumber = NextCardNumber(data);
            borrower.PinHash = PasswordHasher.Hash(pin);
            data.Borrowers.Add(borrower);
            return borrower;
        });
        return new NewBorrower(saved, pin);
    }

    public Borrower UpdateBorrower(Borrower updated)
    {
        Normalise(updated);
        InventoryService.EnsureValid(updated.GetValidationErrors());
        return store.Write(data =>
        {
            var b = data.FindBorrower(updated.Id) ?? throw new LibraryException($"No borrower with id {updated.Id}.");
            b.Name = updated.Name; b.Phone = updated.Phone; b.Email = updated.Email;
            b.Address = updated.Address; b.PreferredChannel = updated.PreferredChannel;
            return b;
        });
    }

    /// <summary>Gives the patron a new random PIN, clears any lockout, and returns the PIN so reception can tell them.</summary>
    public string ResetPin(int borrowerId)
    {
        var pin = NewPin();
        store.Write(data =>
        {
            var b = data.FindBorrower(borrowerId) ?? throw new LibraryException($"No borrower with id {borrowerId}.");
            b.PinHash = PasswordHasher.Hash(pin);
            b.FailedPinAttempts = 0;
            b.LockedUntil = null;
        });
        return pin;
    }

    public void DeleteBorrower(int borrowerId)
    {
        store.Write(data =>
        {
            var b = data.FindBorrower(borrowerId) ?? throw new LibraryException($"No borrower with id {borrowerId}.");
            if (data.Loans.Any(l => l.BorrowerId == borrowerId && l.IsActive))
                throw new LibraryException($"{b.Name} still has items on loan and can't be deleted.");
            if (data.Loans.Any(l => l.BorrowerId == borrowerId) || data.Reservations.Any(r => r.BorrowerId == borrowerId))
                throw new LibraryException($"{b.Name} has loan or hold history that the reports rely on, so the record can't be deleted.");
            data.Borrowers.Remove(b);
        });
    }

    public Borrower? GetBorrower(int id) => store.Read(d => d.FindBorrower(id));

    public Borrower? FindByCard(string card) =>
        store.Read(d => d.Borrowers.FirstOrDefault(b => string.Equals(b.CardNumber, (card ?? "").Trim(), StringComparison.OrdinalIgnoreCase)));

    public IReadOnlyList<Borrower> ListBorrowers(string? search = null)
    {
        var terms = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return store.Read(data =>
        {
            IEnumerable<Borrower> q = data.Borrowers;
            foreach (var term in terms)
            {
                var t = term;
                q = q.Where(b => $"{b.Name} {b.Phone} {b.Email} {b.Address} {b.CardNumber}".Contains(t, StringComparison.OrdinalIgnoreCase));
            }
            return (IReadOnlyList<Borrower>)q.OrderBy(b => b.Name).ToList();
        });
    }

    private static string NewPin() => RandomNumberGenerator.GetInt32(10000).ToString("D4");

    private static string NextCardNumber(LibraryData data)
    {
        var max = data.Borrowers
            .Select(b => b.CardNumber.StartsWith("LIB-") && int.TryParse(b.CardNumber[4..], out var n) ? n : 1000)
            .DefaultIfEmpty(1000).Max();
        return $"LIB-{max + 1}";
    }

    private static void Normalise(Borrower b)
    {
        b.Name = b.Name?.Trim() ?? ""; b.Phone = b.Phone?.Trim() ?? "";
        b.Email = b.Email?.Trim() ?? ""; b.Address = b.Address?.Trim() ?? "";
    }
}
