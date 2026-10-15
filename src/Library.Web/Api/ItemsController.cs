using Library.Core.PublicCatalogue;
using Library.Web.Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Api;

[ApiController]
[Route("api/v1/items")]
[Produces("application/json")]
public sealed class ItemsController(PublicItemService items) : ControllerBase
{
    [AcceptVerbs("GET", "HEAD")]
    public async Task<ActionResult<ItemListDto>> List(CancellationToken ct)
    {
        string? Query(string key) => Request.Query.TryGetValue(key, out var value) ? value.ToString() : null;
        var result = await items.SearchAsync(new ItemSearchRequest(
            Query("q"), Query("type"), Query("category"), Query("branch"),
            Query("availableOnly"), Query("page"), Query("pageSize")), ct);
        if (result.Page is null)
            return InvalidParameters(result.Errors);
        var page = result.Page;
        return new ItemListDto(page.Items.Select(ItemDtoMapper.Map).ToArray(),
            page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }

    [AcceptVerbs("GET", "HEAD")]
    [Route("{code}")]
    public async Task<ActionResult<ItemDto>> Find(string code, CancellationToken ct)
    {
        var result = await items.FindAsync(code, ct);
        if (result.Error is not null)
            return InvalidParameters(new Dictionary<string, string[]> { ["code"] = [result.Error] });
        if (result.Item is null)
            return NotFound();
        return ItemDtoMapper.Map(result.Item);
    }

    private BadRequestObjectResult InvalidParameters(IReadOnlyDictionary<string, string[]> errors)
    {
        var problem = new ValidationProblemDetails(errors.ToDictionary(pair => pair.Key, pair => pair.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Detail = "One or more request parameters are invalid."
        };
        return BadRequest(problem);
    }
}
