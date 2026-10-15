using System.Collections.Concurrent;
using Library.Core.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Library.Api.Tests;

public sealed class ApiFactory(string key = ApiFactory.TestKey, int limit = 60, string environment = "Production")
    : WebApplicationFactory<Program>
{
    public const string TestKey = "api-regression-secret";
    public ConcurrentQueue<string> Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Api:Key"] = key,
            ["Api:RateLimitPerMinute"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }));
        builder.ConfigureLogging(logging => logging.ClearProviders().AddProvider(new CaptureProvider(Logs)));
        builder.ConfigureTestServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddSingleton<ICatalogueQuery, TestCatalogue>();
            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
        });
    }

    private sealed class CaptureProvider(ConcurrentQueue<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(logs);
        public void Dispose() { }
    }

    private sealed class CaptureLogger(ConcurrentQueue<string> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => logs.Enqueue(formatter(state, exception) + exception?.ToString());
    }
}

[ApiController]
[Route("api/v1/probe")]
public sealed class ProbeController : ControllerBase
{
    [HttpGet("fault")]
    public IActionResult Fault() => throw new InvalidOperationException(ApiFactory.TestKey);

    [HttpGet("json-error")]
    public IActionResult JsonError() => new JsonResult(new { internalMessage = "private data" }) { StatusCode = 400 };

    [HttpGet("text-error")]
    public IActionResult TextError() => new ContentResult { Content = "private data", StatusCode = 500 };
}

internal sealed class TestCatalogue : ICatalogueQuery
{
    private static readonly CatalogueItem Item = new(1, "BK-001", "Dune", ItemType.Book, "A book",
        ItemStatus.Available, 1, "Science Fiction", 1, "CENTRAL", "Central", false,
        new Dictionary<string, string>
        {
            ["Author"] = "Frank Herbert", ["Genre"] = "Science Fiction",
            ["PatronName"] = "Hidden patron", ["LoanId"] = "123", ["FineAmount"] = "99",
            ["StaffUser"] = "Hidden staff", ["Notification"] = "Hidden notice", ["Reservation"] = "Hidden hold"
        });

    public Task<PagedResult<CatalogueItem>> SearchAsync(CatalogueFilter filter, CancellationToken ct = default)
        => Task.FromResult(new PagedResult<CatalogueItem>([Item], 1, filter.Page, filter.PageSize));
    public Task<CatalogueItem?> FindByCodeAsync(string libraryCode, CancellationToken ct = default)
        => Task.FromResult<CatalogueItem?>(libraryCode.Equals(Item.LibraryCode, StringComparison.OrdinalIgnoreCase) ? Item : null);
    public Task<bool> CodeExistsAsync(string libraryCode, CancellationToken ct = default) => Task.FromResult(false);
    public Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CategoryInfo>>([new(1, "Science Fiction", ItemType.Book, 1)]);
    public Task<IReadOnlyList<BranchSummary>> GetBranchesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BranchSummary>>([
            new(1, "CENTRAL", "Central", "1 Main Street", "555-0101", true,
                Enum.GetValues<DayOfWeek>().Select(day => new OpeningHours(day,
                    day == DayOfWeek.Sunday ? null : new TimeOnly(9, 0),
                    day == DayOfWeek.Sunday ? null : new TimeOnly(18, 0))).ToArray()),
            new(2, "CLOSED", "Inactive", "", "", false, [])]);
}
