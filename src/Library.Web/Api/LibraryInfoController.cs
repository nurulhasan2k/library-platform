using Library.Core.PublicCatalogue;
using Library.Web.Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Api;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public sealed class LibraryInfoController(PublicLibraryService library) : ControllerBase
{
    [AcceptVerbs("GET", "HEAD")]
    [Route("categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryGroupDto>>> Categories(CancellationToken ct)
        => Ok(LibraryInfoDtoMapper.MapCategories(await library.GetCategoriesAsync(ct)));

    [AcceptVerbs("GET", "HEAD")]
    [Route("branches")]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> Branches(CancellationToken ct)
        => Ok((await library.GetBranchesAsync(ct)).Select(LibraryInfoDtoMapper.MapBranch).ToArray());

    [AcceptVerbs("GET", "HEAD")]
    [Route("status")]
    public async Task<ActionResult<LibraryStatusDto>> Status(CancellationToken ct)
        => LibraryInfoDtoMapper.MapStatus(await library.GetStatusAsync(ct));
}
