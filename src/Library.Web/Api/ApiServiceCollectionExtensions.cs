using System.Globalization;
using System.Threading.RateLimiting;
using Library.Core.Configuration;
using Library.Web.Api;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class ApiServiceCollectionExtensions
{
    private const string CorsPolicy = "LibraryApi";

    public static IServiceCollection AddLibraryApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiOptions>()
            .Bind(configuration.GetSection(ApiOptions.Section))
            .Validate(options => options.RateLimitPerMinute > 0, "API rate limit must be greater than zero.")
            .ValidateOnStart();
        services.AddControllers(options => options.Filters.Add<ApiProblemDetailsFilter>());
        services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
            .AllowAnyOrigin()
            .WithMethods("GET", "HEAD", "OPTIONS")
            .WithHeaders("X-Api-Key")
            .WithExposedHeaders("Retry-After")));
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<ApiOptions>>((options, api) =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = api.Value.RateLimitPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.OnRejected = async (context, ct) =>
            {
                var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                    : 60;
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                await ApiProblems.WriteAsync(context.HttpContext, 429, "The API request limit has been reached. Try again later.", ct);
            };
        });
        return services;
    }

    public static WebApplication UseLibraryApi(this WebApplication app)
    {
        app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), api =>
        {
            api.UseMiddleware<ApiCorsHeadersMiddleware>();
            api.UseMiddleware<ApiExceptionMiddleware>();
            api.UseStatusCodePages(context => ApiProblems.WriteAsync(context.HttpContext,
                context.HttpContext.Response.StatusCode));
            api.Use(async (context, next) =>
            {
                var method = context.Request.Method;
                if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method))
                {
                    context.Response.Headers.Allow = "GET, HEAD, OPTIONS";
                    await ApiProblems.WriteAsync(context, 405, "This API is read-only. Allowed methods: GET, HEAD, OPTIONS.");
                    return;
                }
                await next(context);
            });
            api.UseRateLimiter();
            // Preflight requests carry the header name, not the API key itself.
            api.UseCors(CorsPolicy);
            api.UseMiddleware<ApiKeyMiddleware>();
            api.Use(async (context, next) =>
            {
                if (HttpMethods.IsOptions(context.Request.Method))
                {
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                    return;
                }
                await next(context);
            });
        });
        app.MapFallback("/api/{**path}", context => ApiProblems.WriteAsync(context, 404));
        return app;
    }
}
