namespace Library.Web.Api;

internal sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            // Exception messages may contain request headers or configuration values.
            logger.LogError("API request failed ({ExceptionType}). Trace identifier: {TraceId}",
                exception.GetType().Name, context.TraceIdentifier);
            if (context.Response.HasStarted)
            {
                context.Abort();
                return;
            }
            context.Response.Clear();
            await ApiProblems.WriteAsync(context, 500);
        }
    }
}
