using System.Security.Claims;
using System.Text.Encodings.Web;
using Library.Core.Contracts;
using Library.Core.Import;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Library.Import.Tests;

public sealed class ImportWebFactory : WebApplicationFactory<Program>
{
    public ImportTestClock Clock { get; } = new();
    public ImportWebCatalogue Catalogue { get; } = new();
    public ImportTestWriter Writer { get; }

    public ImportWebFactory() => Writer = new ImportTestWriter(Catalogue);

    public HttpClient Client(string? role = "Admin", int userId = 1)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (role is not null) client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-User", userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddSingleton<IClock>(Clock);
            services.AddSingleton<ICatalogueQuery>(Catalogue);
            services.AddSingleton<IImportItemWriter>(Writer);
            services.AddAuthentication("ImportTests").AddScheme<AuthenticationSchemeOptions, ImportTestAuthentication>("ImportTests", _ => { });
            services.AddAuthorization(options => options.DefaultPolicy = new AuthorizationPolicyBuilder("ImportTests")
                .RequireAuthenticatedUser().Build());
        });
    }
}

public sealed class ImportTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].ToString();
        if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Request.Headers["X-Test-User"].ToString()),
            new Claim(ClaimTypes.Name, "Import tester"), new Claim(ClaimTypes.Role, role)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        return Task.CompletedTask;
    }
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Task.CompletedTask;
    }
}

public sealed class ImportTestClock : IClock
{
    public DateTime Now { get; set; } = new(2026, 10, 15, 12, 0, 0);
    public DateOnly Today => DateOnly.FromDateTime(Now);
}

public sealed class ImportWebCatalogue : ICatalogueQuery
{
    internal object Gate { get; } = new();
    internal HashSet<string> Codes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public void AddCode(string code) { lock (Gate) Codes.Add(code); }
    public Task<bool> CodeExistsAsync(string libraryCode, CancellationToken ct = default)
    {
        lock (Gate) return Task.FromResult(Codes.Contains(libraryCode));
    }
    public Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken ct = default)
        => Task.FromResult(ImportTestData.Lookups.Categories);
    public Task<IReadOnlyList<BranchSummary>> GetBranchesAsync(CancellationToken ct = default)
        => Task.FromResult(ImportTestData.Lookups.Branches);
    public Task<PagedResult<CatalogueItem>> SearchAsync(CatalogueFilter filter, CancellationToken ct = default)
        => throw new InvalidOperationException("Not used by the importer.");
    public Task<CatalogueItem?> FindByCodeAsync(string libraryCode, CancellationToken ct = default)
        => throw new InvalidOperationException("Not used by the importer.");
}

public sealed class ImportTestWriter(ImportWebCatalogue catalogue) : IImportItemWriter
{
    public int Calls { get; private set; }
    public bool ThrowOnWrite { get; set; }
    public Task<ImportWriteResult> CreateManyAsync(IReadOnlyList<ValidItemRow> rows, int targetBranchId, CancellationToken ct = default)
    {
        lock (catalogue.Gate)
        {
            Calls++;
            if (ThrowOnWrite) throw new InvalidOperationException("Private database failure");
            if (rows.Any(row => catalogue.Codes.Contains(row.LibraryCode)))
                return Task.FromResult(new ImportWriteResult(false, 0, "A code already exists. Nothing was imported."));
            foreach (var row in rows) catalogue.Codes.Add(row.LibraryCode);
            return Task.FromResult(new ImportWriteResult(true, rows.Count, null));
        }
    }
}
