using Library.Core.Contracts;
using Library.Core.Data;

namespace Library.Core.Services;

/// <summary>
/// The platform clock. If the admin has set a "simulated today" (decision 10) every service sees that date;
/// otherwise it is the real date. The time of day is always real.
/// </summary>
public sealed class SimulatedClock(ILibraryStore store, TimeProvider time) : IClock
{
    public DateOnly Today => store.Read(d => d.Settings.SimulatedToday) ?? RealToday;
    public DateTime Now
    {
        get
        {
            var real = time.GetLocalNow().DateTime;
            return store.Read(d => d.Settings.SimulatedToday) is { } sim ? sim.ToDateTime(TimeOnly.FromDateTime(real)) : real;
        }
    }

    private DateOnly RealToday => DateOnly.FromDateTime(time.GetLocalNow().DateTime);
}

/// <summary>A fixed clock for tests and for seeding.</summary>
public sealed class FixedClock(DateOnly today) : IClock
{
    public DateOnly Today { get; set; } = today;
    public DateTime Now => Today.ToDateTime(new TimeOnly(10, 0));
}
