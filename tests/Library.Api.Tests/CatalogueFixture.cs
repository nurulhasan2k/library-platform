using Library.Core.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Library.Api.Tests;

public sealed class CatalogueFixture : IAsyncLifetime
{
    private readonly ApiFactory parent = new(limit: 1000);
    private WebApplicationFactory<Program> factory = null!;
    public RecordingCatalogue Catalogue { get; } = new();

    public Task InitializeAsync()
    {
        factory = parent.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<ICatalogueQuery>(Catalogue)));
        return Task.CompletedTask;
    }

    public HttpClient CreateClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiFactory.TestKey);
        return client;
    }

    public async Task DisposeAsync()
    {
        await factory.DisposeAsync();
        await parent.DisposeAsync();
    }
}

public sealed class RecordingCatalogue : ICatalogueQuery
{
    public CatalogueFilter? LastFilter { get; private set; }

    public IReadOnlyList<CatalogueItem> Items { get; } = [
        new(1, "BK-A", "Dune", ItemType.Book, "Desert novel", ItemStatus.Available,
            11, "Science Fiction", 1, "CENTRAL", "Central Library", false,
            new Dictionary<string, string> { ["Author"] = "Frank Herbert", ["Genre"] = "Science Fiction" }),
        new(2, "MU-A", "Kind of Blue", ItemType.Music, "Jazz album", ItemStatus.Available,
            22, "Jazz", 2, "RIVER", "River Library", false,
            new Dictionary<string, string> { ["Artist"] = "Miles Davis", ["Year"] = "1959" }),
        new(3, "TY-A", "Wooden Blocks", ItemType.Toy, "Building set", ItemStatus.Damaged,
            33, "Construction", 2, "RIVER", "River Library", false,
            new Dictionary<string, string> { ["ToyType"] = "Blocks", ["RecommendedAge"] = "3" }),
        new(4, "BK-T", "Travelling Book", ItemType.Book, "Being transferred", ItemStatus.Available,
            11, "Science Fiction", 1, "CENTRAL", "Central Library", true,
            new Dictionary<string, string> { ["Author"] = "Alex Writer", ["Genre"] = "Science Fiction" }),
        new(5, "BK-B", "Borrowed Book", ItemType.Book, "On loan", ItemStatus.Borrowed,
            11, "Science Fiction", 2, "RIVER", "River Library", false,
            new Dictionary<string, string> { ["Author"] = "Sam Writer", ["Genre"] = "Science Fiction" })];

    public Task<PagedResult<CatalogueItem>> SearchAsync(CatalogueFilter filter, CancellationToken ct = default)
    {
        LastFilter = filter;
        // Simulate the query contract while recording exactly what the API passes to it.
        IEnumerable<CatalogueItem> rows = Items;
        if (filter.Type is not null)
            rows = rows.Where(item => item.Type == filter.Type);
        if (filter.CategoryId is not null)
            rows = rows.Where(item => item.CategoryId == filter.CategoryId);
        if (filter.BranchId is not null)
            rows = rows.Where(item => item.BranchId == filter.BranchId);
        if (!string.IsNullOrEmpty(filter.Text))
            rows = rows.Where(item => new[] { item.Name, item.LibraryCode, item.Description }
                .Concat(item.Details.Values).Any(value => value?.Contains(filter.Text, StringComparison.OrdinalIgnoreCase) == true));
        if (filter.AvailableOnly)
            rows = rows.Where(item => item.Status == ItemStatus.Available && !item.InTransit);
        var sorted = rows.OrderBy(item => item.Name, StringComparer.Ordinal).ThenBy(item => item.LibraryCode).ToArray();
        var offset = (long)(filter.Page - 1) * filter.PageSize;
        var page = offset >= sorted.Length ? [] : sorted.Skip((int)offset).Take(filter.PageSize).ToArray();
        return Task.FromResult(new PagedResult<CatalogueItem>(page, sorted.Length, filter.Page, filter.PageSize));
    }

    public Task<CatalogueItem?> FindByCodeAsync(string libraryCode, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(item => item.LibraryCode.Equals(libraryCode, StringComparison.OrdinalIgnoreCase)));
    public Task<bool> CodeExistsAsync(string libraryCode, CancellationToken ct = default)
        => Task.FromResult(Items.Any(item => item.LibraryCode.Equals(libraryCode, StringComparison.OrdinalIgnoreCase)));
    public Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CategoryInfo>>([
            new(33, "Construction", ItemType.Toy, 1), new(11, "Science Fiction", ItemType.Book, 3),
            new(12, "Fantasy", ItemType.Book, 0), new(22, "Jazz", ItemType.Music, 1)]);
    public Task<IReadOnlyList<BranchSummary>> GetBranchesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<BranchSummary>>([
            new(1, "CENTRAL", "Central Library", "1 Main Street", "555-0101", true, Hours()),
            new(2, "RIVER", "River Library", "2 River Road", "555-0102", true, Hours()),
            new(3, "OLD", "Closed Library", "3 Old Road", "555-0103", false, Hours())]);

    private static OpeningHours[] Hours() => Enum.GetValues<DayOfWeek>().Reverse()
        .Select(day => new OpeningHours(day, day == DayOfWeek.Sunday ? null : new TimeOnly(9, 0),
            day == DayOfWeek.Sunday ? null : new TimeOnly(18, 0))).ToArray();
}
