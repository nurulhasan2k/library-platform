using System.Globalization;
using Library.Core.Contracts;
using Library.Core.PublicCatalogue;
using Library.Web.Api.Dtos;

namespace Library.Web.Api;

internal static class LibraryInfoDtoMapper
{
    public static IReadOnlyList<CategoryGroupDto> MapCategories(IEnumerable<CategoryInfo> categories)
        => categories.GroupBy(category => category.ItemType).OrderBy(group => group.Key)
            .Select(group => new CategoryGroupDto(group.Key.ToString().ToLowerInvariant(),
                group.Select(category => new CategoryDto(category.Name, category.ItemCount)).ToArray())).ToArray();

    public static BranchDto MapBranch(BranchSummary branch)
        => new(branch.Code, branch.Name, branch.Address, branch.Phone,
            branch.Hours.OrderBy(hours => ((int)hours.Day + 6) % 7)
                .Select(hours => hours.IsClosed
                    ? new OpeningHoursDto(hours.Day.ToString(), true, null, null)
                    : new OpeningHoursDto(hours.Day.ToString(), null, Format(hours.Open), Format(hours.Close)))
                .ToArray());

    public static LibraryStatusDto MapStatus(LibraryOperatingStatus status)
        => new("1.0", status.ServerTime, status.Branches.Select(branch => new BranchStatusDto(
            branch.Code, branch.Name, branch.OpeningStatus.IsOpenNow,
            Format(branch.OpeningStatus.TodayOpens), Format(branch.OpeningStatus.TodayCloses))).ToArray());

    private static string? Format(TimeOnly? time) => time?.ToString("HH:mm", CultureInfo.InvariantCulture);
}
