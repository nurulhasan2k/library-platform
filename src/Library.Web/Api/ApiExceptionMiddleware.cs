namespace Library.Web.Api;

internal sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            logger.LogError(exception, "API request failed. Trace identifier: {TraceId}", context.TraceIdentifier);
            context.Response.Clear();
            await ApiProblems.WriteAsync(context, 500);
        }
    }
}
