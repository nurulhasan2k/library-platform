using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Data;
using Microsoft.Extensions.Options;

namespace Library.Core.Services;

/// <summary>Library card number + 4-digit PIN with lockout after repeated wrong PINs (decision 3).</summary>
public sealed class PatronAuthService(ILibraryStore store, IOptions<AuthOptions> options, TimeProvider time) : IPatronAuthService
{
    private const string Generic = "Card number or PIN is not correct.";

    public Task<PatronAuthResult> SignInAsync(string cardNumber, string pin, CancellationToken ct = default)
    {
        var card = (cardNumber ?? "").Trim();
        var now = time.GetUtcNow();
        var rules = options.Value;

        var result = store.Write(data =>
        {
            var b = data.Borrowers.FirstOrDefault(x => string.Equals(x.CardNumber, card, StringComparison.OrdinalIgnoreCase));
            if (b is null)
            {
                PasswordHasher.Verify(pin ?? "", PasswordHasher.Dummy); // same cost as a real check
                return PatronAuthResult.Fail(Generic);
            }

            if (b.LockedUntil is { } until && until > now)
            {
                var mins = Math.Max(1, (int)Math.Ceiling((until - now).TotalMinutes));
                return PatronAuthResult.Fail($"Too many wrong PINs. This card is locked for another {mins} minute{(mins == 1 ? "" : "s")}. Ask a librarian for help.", true, until);
            }

            if (PasswordHasher.Verify(pin ?? "", b.PinHash))
            {
                b.FailedPinAttempts = 0;
                b.LockedUntil = null;
                return PatronAuthResult.Ok(new PatronIdentity(b.Id, b.CardNumber, b.Name, b.PreferredChannel));
            }

            b.FailedPinAttempts++;
            if (b.FailedPinAttempts >= rules.MaxPinAttempts)
            {
                b.FailedPinAttempts = 0;
                b.LockedUntil = now.AddMinutes(rules.LockoutMinutes);
                return PatronAuthResult.Fail($"Too many wrong PINs. This card is locked for {rules.LockoutMinutes} minutes. Ask a librarian for help.", true, b.LockedUntil);
            }

            var left = rules.MaxPinAttempts - b.FailedPinAttempts;
            return PatronAuthResult.Fail($"{Generic} {left} attempt{(left == 1 ? "" : "s")} left.");
        });
        return Task.FromResult(result);
    }
}
