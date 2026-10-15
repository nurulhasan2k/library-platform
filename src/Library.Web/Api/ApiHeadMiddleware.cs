namespace Library.Web.Api;

internal sealed class ApiHeadMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsHead(context.Request.Method))
        {
            await next(context);
            return;
        }

        var body = context.Response.Body;
        context.Response.Body = Stream.Null;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = body;
        }
    }
}
