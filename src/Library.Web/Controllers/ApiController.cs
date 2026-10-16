using Library.Core.Contracts;
using Library.Core.Configuration;
using Library.Core.Services;
using Library.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Library.Web.Controllers;

public sealed record BranchRefDto(string Code, string Name);
public sealed record ItemDto(
    string Code, string Name, string Type, string? Description, string Status, bool Available, string? Category, BranchRefDto Branch,
    bool InTransit, IReadOnlyDictionary<string, string> Details);
public sealed record CategoryDto(int Id, string Name, string Type, int ItemCount);
public sealed record HoursDto(string Day, bool Closed, string? Opens, string? Closes);
public sealed record BranchDto(string Code, string Name, string Address, string Phone, bool OpenNow, string Today, IReadOnlyList<HoursDto> OpeningHours);
public sealed record PagedDto<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize, int TotalPages);

/// <summary>
/// The public read-only REST API (feature 3.2). It reads through <see cref="ICatalogueQuery"/> and returns DTOs only: no patron,
/// loan, fine or staff data can leave through it. GET/HEAD/OPTIONS only; an API key is required (see ApiMiddleware).
/// </summary>
[ApiController, Route("api/v1")]
public sealed class ApiController(ICatalogueQuery catalogue, IClock clock, IOptions<ApiOptions> options) : ControllerBase
{
    [HttpGet("items")]
    public async Task<IActionResult> Items(string? q, string? type, int? categoryId, string? branch, bool availableOnly = false, int page = 1, int pageSize = 20)
    {
        ItemType? t = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<ItemType>(type, true, out var parsed)) return Bad("invalid_type", "type must be book, music or toy.");
            t = parsed;
        }
        int? branchId = null;
        if (!string.IsNullOrWhiteSpace(branch))
        {
            var b = (await catalogue.GetBranchesAsync()).FirstOrDefault(x => string.Equals(x.Code, branch, StringComparison.OrdinalIgnoreCase));
            if (b is null) return Bad("invalid_branch", $"No branch has the code '{branch}'.");
            branchId = b.Id;
        }
        if (page < 1) return Bad("invalid_page", "page must be 1 or more.");
        if (pageSize < 1) return Bad("invalid_page_size", "pageSize must be 1 or more.");
        pageSize = Math.Min(pageSize, Math.Max(1, options.Value.MaxPageSize));

        var result = await catalogue.SearchAsync(new CatalogueFilter(q, t, categoryId, branchId, availableOnly, page, pageSize));
        return Ok(new PagedDto<ItemDto>(result.Items.Select(ToDto).ToList(), result.TotalCount, result.Page, result.PageSize, result.TotalPages));
    }

    [HttpGet("items/{code}")]
    public async Task<IActionResult> Item(string code)
    {
        var item = await catalogue.FindByCodeAsync(code);
        return item is null ? NotFoundError($"No item has the library code '{code}'.") : Ok(ToDto(item));
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories() =>
        Ok((await catalogue.GetCategoriesAsync()).Select(c => new CategoryDto(c.Id, c.Name, c.ItemType.ToString().ToLowerInvariant(), c.ItemCount)));

    [HttpGet("branches")]
    public async Task<IActionResult> Branches() =>
        Ok((await catalogue.GetBranchesAsync()).Where(b => b.IsActive).Select(ToDto).ToList());

    [HttpGet("branches/{code}")]
    public async Task<IActionResult> Branch(string code)
    {
        var b = (await catalogue.GetBranchesAsync()).FirstOrDefault(x => x.IsActive && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
        return b is null ? NotFoundError($"No branch has the code '{code}'.") : Ok(ToDto(b));
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var branches = (await catalogue.GetBranchesAsync()).Where(b => b.IsActive).Select(ToDto).ToList();
        var all = await catalogue.SearchAsync(new CatalogueFilter(PageSize: 1));
        return Ok(new
        {
            status = "ok",
            apiVersion = "v1",
            serverTime = clock.Now.ToString("s"),
            totalItems = all.TotalCount,
            branches = branches.Select(b => new { b.Code, b.Name, b.OpenNow, b.Today })
        });
    }

    // ---------------------------------------------------------------- mapping

    private static ItemDto ToDto(CatalogueItem i) => new(
        i.LibraryCode, i.Name, i.Type.ToString().ToLowerInvariant(), i.Description,
        i.InTransit ? "in-transit" : i.Status.ToString().ToLowerInvariant(),
        i.Status == ItemStatus.Available && !i.InTransit, i.CategoryName, new BranchRefDto(i.BranchCode, i.BranchName), i.InTransit,
        i.Details);

    private BranchDto ToDto(BranchSummary b)
    {
        var now = clock.Now;
        var hours = b.Hours.Select(h => new HoursDto(h.Day.ToString(), h.IsClosed, h.Open?.ToString("HH:mm"), h.Close?.ToString("HH:mm"))).ToList();
        var today = b.Hours.FirstOrDefault(h => h.Day == now.DayOfWeek);
        var open = today is { IsClosed: false } && TimeOnly.FromDateTime(now) >= today.Open && TimeOnly.FromDateTime(now) < today.Close;
        var todayText = today is null || today.IsClosed ? "Closed today" : $"{today.Open:HH\\:mm}-{today.Close:HH\\:mm}";
        return new BranchDto(b.Code, b.Name, b.Address, b.Phone, open, todayText, hours);
    }

    private IActionResult Bad(string code, string message) => StatusCode(400, new { error = new { code, message, status = 400 } });
    private IActionResult NotFoundError(string message) => StatusCode(404, new { error = new { code = "not_found", message, status = 404 } });
}
