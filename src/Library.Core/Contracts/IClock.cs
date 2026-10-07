namespace Library.Core.Contracts;

/// <summary>
/// The ONE source of "today" for lending, fines, due-date checks and notifications.
/// Never call DateTime.Today or DateTime.Now in business code: use this, so the admin
/// "simulated today" setting (decision 10) works everywhere and tests can fix the date.
/// Person A registers SystemClock; Person B replaces it with the simulated-date clock.
/// </summary>
public interface IClock
{
    DateOnly Today { get; }
    DateTime Now { get; }
}
