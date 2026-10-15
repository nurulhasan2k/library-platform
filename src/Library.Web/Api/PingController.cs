using Library.Web.Api.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Api;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public sealed class PingController : ControllerBase
{
    [AcceptVerbs("GET", "HEAD", "OPTIONS")]
    [Route("ping")]
    public ActionResult<ApiHealthDto> Get() => new ApiHealthDto(true);
}
