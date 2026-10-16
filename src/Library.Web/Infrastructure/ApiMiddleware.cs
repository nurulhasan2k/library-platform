using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Library.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Library.Web.Infrastructure;

/// <summary>
/// Protects the public API (feature 3.2, decision 13): read-only (anything but GET/HEAD/OPTIONS gets 405), API key in the
/// X-Api-Key header (401 without it), and JSON errors. Rate limiting is applied before this by the global limiter.
/// </summary>
public sealed class ApiMiddleware(RequestDelegate next, IOptions<ApiOptions> options)
{
    public const string KeyHeader = "X-Api-Key";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!ctx.Request.Path.StartsWithSegments("/api/v1"))
        {
            await next(ctx);
            return;
        }

        var method = ctx.Request.Method;
        if (!(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)))
        {
            ctx.Response.Headers.Allow = "GET, HEAD, OPTIONS";
            await Error(ctx, 405, "method_not_allowed", "This API is read-only. Only GET, HEAD and OPTIONS are allowed.");
            return;
        }

        if (HttpMethods.IsOptions(method)) { await next(ctx); return; } // CORS preflight cannot carry the key

        var configured = options.Value.Key;
        if (string.IsNullOrWhiteSpace(configured))
        {
            await Error(ctx, 503, "api_not_configured", "The API is switched off because no key is configured (Api:Key).");
            return;
        }

        var supplied = ctx.Request.Headers[KeyHeader].ToString();
        if (string.IsNullOrEmpty(supplied))
        {
            ctx.Response.Headers.WWWAuthenticate = "ApiKey realm=\"Library API\"";
            await Error(ctx, 401, "missing_api_key", $"Send your API key in the {KeyHeader} header.");
            return;
        }
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(configured)))
        {
            await Error(ctx, 403, "invalid_api_key", "That API key is not valid.");
            return;
        }

        await next(ctx);
    }

    public static async Task Error(HttpContext ctx, int status, string code, string message)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = new { code, message, status } }, Json));
    }
}
