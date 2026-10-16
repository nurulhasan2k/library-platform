using System.Security.Claims;
using System.Text.Encodings.Web;
using Library.Core.Contracts;
using Library.Core.Reports;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Library.Reports.Tests;

public sealed class ReportsWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication("ReportTest").AddScheme<AuthenticationSchemeOptions, ReportsTestAuthenticationHandler>("ReportTest", _ => { });
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(new ReportTestClock());
            services.RemoveAll<ICatalogueQuery>();
            services.AddSingleton<ICatalogueQuery, ReportTestCatalogue>();
        });
    }

    public HttpClient Client(string? role = "Manager")
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (role is not null) client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }

    private sealed class ReportTestClock : IClock
    {
        public DateOnly Today => FixedReportData.Today;
        public DateTime Now => new(2026, 10, 5, 14, 15, 16);
    }

    private sealed class ReportTestCatalogue : ICatalogueQuery
    {
        public Task<IReadOnlyList<BranchSummary>> GetBranchesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BranchSummary>>(
            [new(1, "CENTRAL", "Central", "", "", true, []), new(2, "NORTH", "North", "", "", true, [])]);
        public Task<PagedResult<CatalogueItem>> SearchAsync(CatalogueFilter filter, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CatalogueItem?> FindByCodeAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}

public sealed class ReportsTestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var role)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "report-tester"), new Claim(ClaimTypes.Role, role.ToString())], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
