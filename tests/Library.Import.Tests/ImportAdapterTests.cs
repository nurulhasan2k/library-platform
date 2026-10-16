using Library.Core;
using Library.Core.Contracts;
using Library.Core.Import;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Library.Import.Tests;

public sealed class ImportAdapterTests
{
    [Fact]
    public async Task DefaultCodeLookupTrimsDeduplicatesAndReturnsExistingCodes()
    {
        var catalogue = new LookupCatalogue();
        var lookup = new CatalogueCodeLookup(catalogue);
        var result = await lookup.GetExistingCodesAsync([" bk-500 ", "BK-500", " mu-600 ", "BK-999", " "]);
        Assert.Equal(new[] { "bk-500", "mu-600", "BK-999" }, catalogue.CodeRequests);
        Assert.Equal(2, result.Count);
        Assert.Contains("BK-500", result, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("MU-600", result, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupAdapterLoadsCategoriesBranchesAndExistingCodes()
    {
        var catalogue = new LookupCatalogue();
        var adapter = new ImportLookupAdapter(catalogue, new CatalogueCodeLookup(catalogue));
        var lookups = await adapter.LoadAsync(["BK-500", "BK-501"]);
        Assert.Equal(1, catalogue.CategoryRequests);
        Assert.Equal(1, catalogue.BranchRequests);
        Assert.Same(ImportTestData.Lookups.Branches, lookups.Branches);
        Assert.Same(ImportTestData.Lookups.Categories, lookups.Categories);
        Assert.Equal("BK-500", Assert.Single(lookups.ExistingCodes));
        var validation = ImportTestData.Validate(lookups: lookups);
        Assert.Equal(19, validation.ValidRows.Count);
        Assert.Contains(validation.RejectedRows, row => row.Fields["LibraryCode"] == "BK-500" && row.Reason.Contains("already exists"));
    }

    [Fact]
    public async Task DefaultCodeLookupHonoursCancellation()
    {
        var catalogue = new LookupCatalogue();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CatalogueCodeLookup(catalogue)
            .GetExistingCodesAsync(["BK-500"], cancellation.Token));
        Assert.Empty(catalogue.CodeRequests);
    }

    [Fact]
    public async Task DefaultWriterRefusesWithoutCreatingAnyRows()
    {
        var services = new ServiceCollection();
        services.AddLibraryImports(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IImportItemWriter>();
        var result = await writer.CreateManyAsync(ImportTestData.Validate().ValidRows, 2);
        Assert.False(result.Success);
        Assert.Equal(0, result.CreatedCount);
        Assert.Equal("Item creation is not available yet.", result.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RealImplementationsWinBeforeOrAfterDefaultRegistration(bool before)
    {
        var services = new ServiceCollection();
        var writer = new ReplacementWriter();
        var lookup = new ReplacementLookup();
        void RegisterReal()
        {
            services.AddSingleton<IImportItemWriter>(writer);
            services.AddSingleton<ICatalogueCodeLookup>(lookup);
        }
        if (before) RegisterReal();
        services.AddLibraryImports(new ConfigurationBuilder().Build());
        if (!before) RegisterReal();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Same(writer, scope.ServiceProvider.GetRequiredService<IImportItemWriter>());
        Assert.Same(lookup, scope.ServiceProvider.GetRequiredService<ICatalogueCodeLookup>());
    }

    private sealed class ReplacementWriter : IImportItemWriter
    {
        public Task<ImportWriteResult> CreateManyAsync(IReadOnlyList<ValidItemRow> rows, int targetBranchId, CancellationToken ct = default)
            => Task.FromResult(new ImportWriteResult(false, 0, "No writes in this test."));
    }

    private sealed class ReplacementLookup : ICatalogueCodeLookup
    {
        public Task<IReadOnlyCollection<string>> GetExistingCodesAsync(IEnumerable<string> codes, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyCollection<string>>([]);
    }

    private sealed class LookupCatalogue : ICatalogueQuery
    {
        public List<string> CodeRequests { get; } = [];
        public int CategoryRequests { get; private set; }
        public int BranchRequests { get; private set; }
        public Task<bool> CodeExistsAsync(string libraryCode, CancellationToken ct = default)
        {
            CodeRequests.Add(libraryCode);
            return Task.FromResult(new[] { "BK-500", "MU-600" }.Contains(libraryCode, StringComparer.OrdinalIgnoreCase));
        }
        public Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken ct = default)
        {
            CategoryRequests++;
            return Task.FromResult(ImportTestData.Lookups.Categories);
        }
        public Task<IReadOnlyList<BranchSummary>> GetBranchesAsync(CancellationToken ct = default)
        {
            BranchRequests++;
            return Task.FromResult(ImportTestData.Lookups.Branches);
        }
        public Task<PagedResult<CatalogueItem>> SearchAsync(CatalogueFilter filter, CancellationToken ct = default)
            => throw new InvalidOperationException("Search is not needed for import lookups.");
        public Task<CatalogueItem?> FindByCodeAsync(string libraryCode, CancellationToken ct = default)
            => throw new InvalidOperationException("Item details are not needed for import lookups.");
    }
}
