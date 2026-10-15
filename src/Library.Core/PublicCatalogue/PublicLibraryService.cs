using Library.Core.Contracts;

namespace Library.Core.PublicCatalogue;

public sealed record BranchOperatingStatus(string Code, string Name, BranchOpeningStatus OpeningStatus);

public sealed record LibraryOperatingStatus(DateTime ServerTime, IReadOnlyList<BranchOperatingStatus> Branches);

public sealed class PublicLibraryService(ICatalogueQuery catalogue, IClock clock)
{
    public Task<IReadOnlyList<CategoryInfo>> GetCategoriesAsync(CancellationToken ct = default)
        => catalogue.GetCategoriesAsync(ct);

    public async Task<IReadOnlyList<BranchSummary>> GetBranchesAsync(CancellationToken ct = default)
        => (await catalogue.GetBranchesAsync(ct)).Where(branch => branch.IsActive).ToArray();

    public async Task<LibraryOperatingStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var branches = await GetBranchesAsync(ct);
        var today = clock.Today;
        var now = clock.Now;
        var time = TimeOnly.FromDateTime(now);
        return new LibraryOperatingStatus(now, branches.Select(branch =>
            new BranchOperatingStatus(branch.Code, branch.Name,
                BranchStatusCalculator.Calculate(branch.Hours, today, time))).ToArray());
    }
}
