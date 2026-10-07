namespace Library.Core.Contracts;

/// <summary>Who a signed-in patron is. Contains no PIN or hash.</summary>
public sealed record PatronIdentity(int BorrowerId, string CardNumber, string FullName, NotificationChannel PreferredChannel);

public sealed record PatronAuthResult(
    bool Success,
    PatronIdentity? Patron,
    string? Error,
    bool LockedOut = false,
    DateTimeOffset? LockedUntil = null)
{
    public static PatronAuthResult Ok(PatronIdentity patron) => new(true, patron, null);
    public static PatronAuthResult Fail(string error, bool lockedOut = false, DateTimeOffset? until = null) => new(false, null, error, lockedOut, until);
}

/// <summary>
/// Library card number + 4-digit PIN (hashed). Used by the kiosk sign-in and by the screens
/// where a patron places or cancels a hold. Lockout after repeated wrong PINs (Auth:MaxPinAttempts).
/// Built by Person A. Never put a card number or PIN in a log, URL or notification body.
/// </summary>
public interface IPatronAuthService
{
    Task<PatronAuthResult> SignInAsync(string cardNumber, string pin, CancellationToken ct = default);
}
