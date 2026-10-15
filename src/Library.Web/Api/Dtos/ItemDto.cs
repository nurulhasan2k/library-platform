namespace Library.Web.Api.Dtos;

public sealed record ItemBranchDto(string Code, string Name);

public sealed record ItemDto(
    string Code, string Name, string Type, string? Category, ItemBranchDto Branch,
    string Status, string? Description, IReadOnlyDictionary<string, object> Details);

public sealed record ItemListDto(
    IReadOnlyList<ItemDto> Items, int Page, int PageSize, int TotalItems, int TotalPages);
