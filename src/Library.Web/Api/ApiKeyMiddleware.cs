using System.Security.Cryptography;
using System.Text;
using Library.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Library.Web.Api;

internal sealed class ApiKeyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IOptions<ApiOptions> options)
    {
        var key = options.Value.Key;
        if (string.IsNullOrWhiteSpace(key))
        {
            await ApiProblems.WriteAsync(context, 503);
            return;
        }

        var supplied = context.Request.Headers["X-Api-Key"];
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied.ToString()));
        var matches = CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
        if (supplied.Count != 1 || !matches)
        {
            context.Response.Headers.WWWAuthenticate = "ApiKey";
            await ApiProblems.WriteAsync(context, 401);
            return;
        }

        await next(context);
    }
}
