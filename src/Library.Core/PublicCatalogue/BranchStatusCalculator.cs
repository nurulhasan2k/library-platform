using Library.Core.Contracts;

namespace Library.Core.PublicCatalogue;

public sealed record BranchOpeningStatus(bool IsOpenNow, TimeOnly? TodayOpens, TimeOnly? TodayCloses);

public static class BranchStatusCalculator
{
    public static BranchOpeningStatus Calculate(IEnumerable<OpeningHours> hours, DateOnly today, TimeOnly now)
    {
        var current = hours.FirstOrDefault(entry => entry.Day == today.DayOfWeek);
        if (current is null || current.IsClosed)
            return new BranchOpeningStatus(false, null, null);

        return new BranchOpeningStatus(current.Open <= now && now < current.Close, current.Open, current.Close);
    }
}
