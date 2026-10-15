using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Library.Web.Api;

internal sealed class ApiCorsHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICorsService cors, IOptions<CorsOptions> options)
    {
        var policy = options.Value.GetPolicy("LibraryApi")!;
        var result = cors.EvaluatePolicy(context, policy);
        // Early errors also need CORS headers so browser clients can read Retry-After.
        context.Response.OnStarting(() =>
        {
            cors.ApplyResult(result, context.Response);
            return Task.CompletedTask;
        });
        await next(context);
    }
}
