using Library.Core.Contracts;

namespace Library.Core.Services;

/// <summary>
/// The real IClock for the platform (decision 10). Normally gives the real date.
/// An admin can set a "simulated today" so due-soon and fine notifications can be shown without waiting days.
/// For now the simulated date is kept in memory (it resets when the app restarts).
/// </summary>
public sealed class SimulatedClock : IClock
{
    private DateOnly? _simulated;

    public DateOnly? SimulatedToday => _simulated;

    public void SetSimulatedToday(DateOnly? date) => _simulated = date;

    public DateOnly Today => _simulated ?? DateOnly.FromDateTime(DateTime.Now);

    public DateTime Now => _simulated is { } d
        ? d.ToDateTime(TimeOnly.FromDateTime(DateTime.Now))
        : DateTime.Now;
}
