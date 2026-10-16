using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Library.Core;
using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Services;
using Library.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddHttpContextAccessor();

builder.Services.AddLibraryOptions(builder.Configuration);
builder.Services.AddLibraryServices();
builder.Services.AddScoped<IBranchContext, WebBranchContext>();
builder.Services.AddScoped(sp => (WebBranchContext)sp.GetRequiredService<IBranchContext>());
builder.Services.AddHostedService<NotificationScheduler>();

// Sessions hold the staff member's chosen branch and desk.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.Name = ".Library.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.IdleTimeout = TimeSpan.FromHours(8);
});

// Three separate sign-ins: staff (roles), patrons on the web, and the kiosk (short idle timeout).
var kioskSeconds = builder.Configuration.GetSection(KioskOptions.Section).Get<KioskOptions>()?.IdleTimeoutSeconds ?? 60;
builder.Services.AddAuthentication(Auth.Staff)
    .AddCookie(Auth.Staff, o =>
    {
        o.Cookie.Name = ".Library.Staff"; o.LoginPath = "/staff/login"; o.AccessDeniedPath = "/staff/denied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = true; o.Cookie.HttpOnly = true;
    })
    .AddCookie(Auth.Patron, o =>
    {
        o.Cookie.Name = ".Library.Patron"; o.LoginPath = "/account/signin"; o.AccessDeniedPath = "/account/signin";
        o.ExpireTimeSpan = TimeSpan.FromMinutes(20); o.SlidingExpiration = true; o.Cookie.HttpOnly = true;
    })
    .AddCookie(Auth.Kiosk, o =>
    {
        o.Cookie.Name = ".Library.Kiosk"; o.LoginPath = "/kiosk"; o.AccessDeniedPath = "/kiosk";
        o.ExpireTimeSpan = TimeSpan.FromSeconds(Math.Max(15, kioskSeconds)); o.SlidingExpiration = true; o.Cookie.HttpOnly = true;
    });
builder.Services.AddAuthorization();

// Read-only API: limit requests per API key (or per IP when there is no key), 429 when exceeded.
var api = builder.Configuration.GetSection(ApiOptions.Section).Get<ApiOptions>() ?? new ApiOptions();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "60";
        await ApiMiddleware.Error(ctx.HttpContext, 429, "rate_limited", $"Too many requests. The limit is {api.RateLimitPerMinute} per minute. Try again shortly.");
    };
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        if (!ctx.Request.Path.StartsWithSegments("/api/v1")) return RateLimitPartition.GetNoLimiter("web");
        var key = ctx.Request.Headers[ApiMiddleware.KeyHeader].ToString();
        var who = string.IsNullOrEmpty(key) ? "ip:" + ctx.Connection.RemoteIpAddress : "key:" + key;
        return RateLimitPartition.GetFixedWindowLimiter(who, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, api.RateLimitPerMinute), Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        });
    });
});
builder.Services.AddCors(o => o.AddPolicy("api", p => p.AllowAnyOrigin().WithMethods("GET", "HEAD", "OPTIONS").WithHeaders(ApiMiddleware.KeyHeader)));

var app = builder.Build();

// ---- start-up tasks: --reset-db, first-run seeding ------------------------------------------------------------
var seed = app.Services.GetRequiredService<SeedRunner>();
var dbOptions = app.Configuration.GetSection(DatabaseOptions.Section).Get<DatabaseOptions>() ?? new DatabaseOptions();
if (args.Contains("--reset-db"))
{
    seed.Reset();
    app.Logger.LogInformation("Database reset: all data deleted and the demo data created again.");
}
else if (dbOptions.SeedOnStartup && seed.SeedIfEmpty())
{
    app.Logger.LogInformation("First start: demo data created.");
}
if (args.Contains("--exit")) return; // used by scripts/reset-db.* so the reset can run without starting the web server

if (string.IsNullOrWhiteSpace(api.Key)) app.Logger.LogWarning("Api:Key is empty, so the public API will answer 503 until a key is set.");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseCors("api");
app.UseMiddleware<ApiMiddleware>();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program;
