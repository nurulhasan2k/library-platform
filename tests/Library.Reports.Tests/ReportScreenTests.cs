using System.Net;
using System.Text;
using CsvHelper;
using Library.Core.Configuration;
using Library.Core.Reports;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Library.Reports.Tests;

public sealed class ReportScreenTests(ReportsWebFactory factory) : IClassFixture<ReportsWebFactory>
{
    [Theory]
    [InlineData(null, 401)]
    [InlineData("Reception", 403)]
    [InlineData("Manager", 200)]
    [InlineData("Admin", 200)]
    public async Task Report_pages_and_exports_enforce_roles(string? role, int expected)
    {
        using var client = factory.Client(role);
        foreach (var path in new[] { "/Reports", "/Reports/Borrowing", "/Reports/FineRevenue", "/Reports/Inventory" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(expected, (int)response.StatusCode);
        }
        foreach (var report in new[] { "Borrowing", "FineRevenue", "Inventory" })
        foreach (var format in new[] { "csv", "pdf" })
        {
            using var response = await client.GetAsync($"/Reports/{report}?from=2026-08-01&to=2026-10-05&format={format}");
            Assert.Equal(expected, (int)response.StatusCode);
        }
    }

    [Fact]
    public async Task Default_range_and_dashboard_use_clock_and_sample_data()
    {
        using var client = factory.Client();
        var html = await client.GetStringAsync("/Reports");
        Assert.Contains("value=\"2026-04-09\"", html);
        Assert.Contains("value=\"2026-10-05\"", html);
        Assert.Contains("Demonstration data", html);
        Assert.Contains("data-summary=\"Loans in period\">9", html);
        Assert.Contains("data-summary=\"Items as at today\">6", html);
        Assert.Contains("Borrowing statistics", html);
    }

    [Fact]
    public async Task Date_and_branch_filters_change_borrowing_numbers_and_keep_export_values()
    {
        using var client = factory.Client();
        var all = await client.GetStringAsync("/Reports/Borrowing?from=2026-08-01&to=2026-09-30");
        var central = await client.GetStringAsync("/Reports/Borrowing?from=2026-08-01&to=2026-09-30&branchId=1");
        var september = await client.GetStringAsync("/Reports/Borrowing?from=2026-09-01&to=2026-09-30");
        Assert.Contains("data-summary=\"Loans\">9", all);
        Assert.Contains("data-summary=\"Loans\">4", central);
        Assert.Contains("data-summary=\"Loans\">6", september);
        Assert.Contains("name=\"from\" value=\"2026-08-01\"", central);
        Assert.Contains("name=\"to\" value=\"2026-09-30\"", central);
        Assert.Contains("name=\"branchId\" value=\"1\"", central);
        Assert.Contains("selected=\"selected\" value=\"1\"", central);
    }

    [Fact]
    public async Task Fine_screen_reconciles_and_shows_accrual_separately()
    {
        using var client = factory.Client();
        var all = await client.GetStringAsync("/Reports/FineRevenue?from=2026-08-01&to=2026-10-05");
        var central = await client.GetStringAsync("/Reports/FineRevenue?from=2026-08-01&to=2026-10-05&branchId=1");
        Assert.Contains("data-summary=\"Charged\">$9.50", all);
        Assert.Contains("data-summary=\"Collected\">$4.50", all);
        Assert.Contains("data-summary=\"Outstanding\">$5.00", all);
        Assert.Contains("Reconciliation passed.", all);
        Assert.Contains("Accruing on overdue loans (not yet charged)", all);
        Assert.Contains("$0.50", all);
        Assert.Contains("Borrower 3", all);
        Assert.Contains("CARD-3", all);
        Assert.Contains("data-summary=\"Charged\">$2.00", central);
    }

    [Fact]
    public async Task Inventory_branch_and_idle_days_change_counts_without_date_range()
    {
        using var client = factory.Client();
        var thirty = await client.GetStringAsync("/Reports/Inventory?idleDays=30&branchId=2");
        var twenty = await client.GetStringAsync("/Reports/Inventory?idleDays=20&branchId=2");
        Assert.Contains("data-summary=\"Items\">4", thirty);
        Assert.Contains("data-summary=\"Idle\">1", thirty);
        Assert.Contains("data-summary=\"Idle\">2", twenty);
        Assert.Contains("name=\"idleDays\" value=\"20\"", twenty);
        Assert.DoesNotContain("name=\"from\"", twenty);
        Assert.Contains("Ready", twenty);
    }

    [Theory]
    [InlineData("Borrowing", "csv", "text/csv", "borrowing-statistics-2026-08-01_2026-10-05-1.csv")]
    [InlineData("Borrowing", "pdf", "application/pdf", "borrowing-statistics-2026-08-01_2026-10-05-1.pdf")]
    [InlineData("FineRevenue", "csv", "text/csv", "fine-revenue-2026-08-01_2026-10-05-1.csv")]
    [InlineData("FineRevenue", "pdf", "application/pdf", "fine-revenue-2026-08-01_2026-10-05-1.pdf")]
    [InlineData("Inventory", "csv", "text/csv", "inventory-health-2026-10-05_2026-10-05-1.csv")]
    [InlineData("Inventory", "pdf", "application/pdf", "inventory-health-2026-10-05_2026-10-05-1.pdf")]
    public async Task Export_returns_filtered_file_with_correct_type_and_name(string report, string format, string type, string name)
    {
        using var client = factory.Client();
        using var response = await client.GetAsync($"/Reports/{report}?from=2026-08-01&to=2026-10-05&branchId=1&idleDays=20&format={format}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(type, response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(name, response.Content.Headers.ContentDisposition!.FileNameStar);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
        if (format == "pdf") Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes));
        else
        {
            var text = Encoding.UTF8.GetString(bytes);
            Assert.Contains("CENTRAL", text);
            Assert.DoesNotContain("NORTH", text);
        }
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    [Theory]
    [InlineData("Borrowing?from=2026-10-05&to=2026-08-01", "The from date must be on or before the to date.")]
    [InlineData("Borrowing?from=invalid", "not valid")]
    [InlineData("Borrowing?branchId=999", "Choose a known branch.")]
    [InlineData("Inventory?idleDays=-1", "Idle days cannot be negative.")]
    [InlineData("FineRevenue?format=exe", "Choose CSV or PDF")]
    public async Task Invalid_filters_show_validation_and_disable_exports(string path, string message)
    {
        using var client = factory.Client();
        using var response = await client.GetAsync("/Reports/" + path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(message, html);
        Assert.Contains("disabled=\"disabled\"", html);
        Assert.DoesNotContain("System.", html);
    }

    [Fact]
    public async Task Empty_report_disables_exports_and_direct_export_shows_friendly_message()
    {
        using var client = factory.Client();
        foreach (var report in new[] { "Borrowing", "FineRevenue" })
        {
            var html = await client.GetStringAsync($"/Reports/{report}?from=2025-01-01&to=2025-01-31");
            Assert.Contains("No data for this period", html);
            Assert.Contains("disabled=\"disabled\"", html);
            using var response = await client.GetAsync($"/Reports/{report}?from=2025-01-01&to=2025-01-31&format=csv");
            Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
            Assert.Contains("No data for this period", await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public void Existing_data_source_takes_precedence_over_fallback()
    {
        var services = new ServiceCollection();
        IReportDataSource source = new InMemoryReportDataSource(FixedReportData.Data);
        services.AddSingleton(source);
        services.AddLibraryReportScreens();
        using var provider = services.BuildServiceProvider();
        Assert.Same(source, provider.GetRequiredService<IReportDataSource>());
    }

    [Fact]
    public async Task Csv_exports_use_the_same_date_branch_and_idle_filters_as_screens()
    {
        using var client = factory.Client();
        var screen = await client.GetStringAsync("/Reports/Borrowing?from=2026-09-01&to=2026-09-30&branchId=1");
        Assert.Contains("data-summary=\"Loans\">1", screen);
        var rows = await Rows("/Reports/Borrowing?from=2026-09-01&to=2026-09-30&branchId=1&format=csv");
        Assert.Single(rows);
        Assert.Equal("9", rows[0][0]);
        var thirty = await Rows("/Reports/Inventory?branchId=2&idleDays=30&format=csv");
        var twenty = await Rows("/Reports/Inventory?branchId=2&idleDays=20&format=csv");
        Assert.Equal("No", thirty.Single(r => r[0] == "MU-A")[8]);
        Assert.Equal("Yes", twenty.Single(r => r[0] == "MU-A")[8]);

        async Task<List<string[]>> Rows(string url)
        {
            using var stream = await client.GetStreamAsync(url);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var csv = new CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);
            csv.Read();
            var result = new List<string[]>();
            while (csv.Read()) result.Add(csv.Parser.Record!.ToArray());
            return result;
        }
    }

    [Fact]
    public async Task Empty_real_source_disables_inventory_exports_without_demo_label()
    {
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IReportDataSource>();
            services.AddSingleton<IReportDataSource>(new InMemoryReportDataSource(new([], [], [], [])));
        }));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Manager");
        var html = await client.GetStringAsync("/Reports/Inventory");
        Assert.Contains("No data for this period", html);
        Assert.Contains("disabled=\"disabled\"", html);
        Assert.DoesNotContain("Demonstration data", html);
    }

    [Fact]
    public async Task Configured_default_range_ends_on_clock_today()
    {
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<ReportOptions>(options => options.DefaultRangeDays = 10)));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        var html = await client.GetStringAsync("/Reports/Borrowing");
        Assert.Contains("value=\"2026-09-26\"", html);
        Assert.Contains("value=\"2026-10-05\"", html);
        Assert.Contains("No data for this period", html);
    }
}
