using Library.Core.Contracts;
using Library.Core.PublicCatalogue;
using Xunit;

namespace Library.Api.Tests;

public sealed class BranchStatusCalculatorTests
{
    [Theory]
    [InlineData(8, 59, false)]
    [InlineData(9, 0, true)]
    [InlineData(12, 0, true)]
    [InlineData(18, 0, false)]
    [InlineData(19, 0, false)]
    public void OpeningTimeIsInclusiveAndClosingTimeIsExclusive(int hour, int minute, bool expected)
    {
        var result = BranchStatusCalculator.Calculate(
            [new OpeningHours(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0))],
            new DateOnly(2026, 10, 5), new TimeOnly(hour, minute));
        Assert.Equal(expected, result.IsOpenNow);
        Assert.Equal(new TimeOnly(9, 0), result.TodayOpens);
        Assert.Equal(new TimeOnly(18, 0), result.TodayCloses);
    }

    [Fact]
    public void ClosedDayHasNoOpeningOrClosingTime()
    {
        var result = BranchStatusCalculator.Calculate([
            new OpeningHours(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0)),
            new OpeningHours(DayOfWeek.Sunday, null, null)], new DateOnly(2026, 10, 11), new TimeOnly(12, 0));
        Assert.False(result.IsOpenNow);
        Assert.Null(result.TodayOpens);
        Assert.Null(result.TodayCloses);
    }
}
