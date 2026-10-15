using System.Text.Json.Serialization;

namespace Library.Web.Api.Dtos;

public sealed record CategoryDto(string Name, int ItemCount);

public sealed record CategoryGroupDto(string Type, IReadOnlyList<CategoryDto> Categories);

public sealed record OpeningHoursDto(
    string Day,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Closed,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Opens,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Closes);

public sealed record BranchDto(
    string Code, string Name, string Address, string Phone, IReadOnlyList<OpeningHoursDto> OpeningHours);

public sealed record BranchStatusDto(
    string Code, string Name, bool IsOpenNow,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? TodayOpens,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? TodayCloses);

public sealed record LibraryStatusDto(string ApiVersion, DateTime ServerTime, IReadOnlyList<BranchStatusDto> Branches);
