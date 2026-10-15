using System.Globalization;
using Library.Core.Configuration;
using Library.Core.Contracts;
using Microsoft.Extensions.Options;

namespace Library.Core.PublicCatalogue;

public sealed class PublicItemService(ICatalogueQuery catalogue, IOptions<ApiOptions> options)
{
    public async Task<ItemSearchResult> SearchAsync(ItemSearchRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var text = request.Text?.Trim();
        if (text?.Length > 100)
            errors["q"] = ["Must be at most 100 characters."];

        ItemType? type = request.Type?.Trim().ToLowerInvariant() switch
        {
            "book" => ItemType.Book,
            "music" => ItemType.Music,
            "toy" => ItemType.Toy,
            _ => null
        };
        if (request.Type is not null && type is null)
            errors["type"] = ["Must be book, music or toy."];

        var availableOnly = true;
        if (request.AvailableOnly is not null && !bool.TryParse(request.AvailableOnly, out availableOnly))
            errors["availableOnly"] = ["Must be true or false."];
        var page = ParsePositive(request.Page, 1, "page", errors);
        var pageSize = ParsePositive(request.PageSize, 20, "pageSize", errors);
        pageSize = Math.Min(pageSize, options.Value.MaxPageSize);

        int? categoryId = null;
        if (request.Category is not null)
        {
            var categories = await catalogue.GetCategoriesAsync(ct);
            categoryId = categories.FirstOrDefault(category =>
                string.Equals(category.Name, request.Category.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;
            if (categoryId is null)
                errors["category"] = ["Unknown category."];
        }

        int? branchId = null;
        if (request.Branch is not null)
        {
            var branches = await catalogue.GetBranchesAsync(ct);
            branchId = branches.FirstOrDefault(branch =>
                string.Equals(branch.Code, request.Branch.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;
            if (branchId is null)
                errors["branch"] = ["Unknown branch."];
        }

        if (errors.Count > 0)
            return new ItemSearchResult(null, errors);

        var result = await catalogue.SearchAsync(new CatalogueFilter(
            Text: text, Type: type, CategoryId: categoryId, BranchId: branchId,
            AvailableOnly: availableOnly, Page: page, PageSize: pageSize), ct);
        return new ItemSearchResult(result, errors);
    }

    public async Task<ItemLookupResult> FindAsync(string code, CancellationToken ct = default)
    {
        code = code.Trim();
        if (code.Length is 0 or > 30 || code.Any(character =>
            !char.IsLetterOrDigit(character) && character != '-' && character != '_'))
            return new ItemLookupResult(null, "Must be 1 to 30 characters using only letters, digits, - and _.");

        var item = await catalogue.FindByCodeAsync(code, ct);
        return new ItemLookupResult(item?.Status == ItemStatus.Destroy ? null : item, null);
    }

    private static int ParsePositive(string? value, int defaultValue, string name, Dictionary<string, string[]> errors)
    {
        if (value is null)
            return defaultValue;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 1)
            return parsed;
        errors[name] = ["Must be an integer of 1 or more."];
        return defaultValue;
    }
}
