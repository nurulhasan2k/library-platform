using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Library.Web.Api;

internal sealed class ApiProblemDetailsFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (!context.HttpContext.Request.Path.StartsWithSegments("/api"))
            return;

        var status = context.Result switch
        {
            ObjectResult result => result.StatusCode ?? (result.Value as ProblemDetails)?.Status,
            StatusCodeResult result => result.StatusCode,
            JsonResult result => result.StatusCode ?? (result.Value as ProblemDetails)?.Status,
            ContentResult result => result.StatusCode,
            _ => null
        };
        if (status is not >= 400)
            return;

        var original = context.Result switch
        {
            ObjectResult result => result.Value as ProblemDetails,
            JsonResult result => result.Value as ProblemDetails,
            _ => null
        };
        var problem = ApiProblems.Create(context.HttpContext, status.Value,
            status.Value >= 500 ? null : original?.Detail);
        if (original is ValidationProblemDetails validation)
            problem.Extensions["errors"] = validation.Errors;
        if (status == 401)
            context.HttpContext.Response.Headers.WWWAuthenticate = "ApiKey";
        if (status == 405)
            context.HttpContext.Response.Headers.Allow = "GET, HEAD, OPTIONS";
        context.Result = new JsonResult(problem)
        {
            StatusCode = status,
            ContentType = "application/problem+json"
        };
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
