using System.Globalization;
using System.Text.Json;
using Library.Core.Contracts;
using Library.Web.Api.Dtos;

namespace Library.Web.Api;

internal static class ItemDtoMapper
{
    public static ItemDto Map(CatalogueItem item)
    {
        var details = new Dictionary<string, object>();
        string[] allowedKeys = item.Type switch
        {
            ItemType.Book => ["author", "genre"],
            ItemType.Music => ["artist", "year"],
            ItemType.Toy => ["toyType", "recommendedAge"],
            _ => []
        };
        foreach (var pair in item.Details)
        {
            // No real query exists yet; assume detail keys are property names that can be camel-cased.
            var key = JsonNamingPolicy.CamelCase.ConvertName(pair.Key);
            if (!allowedKeys.Contains(key, StringComparer.Ordinal))
                continue;
            details[key] = (key is "year" or "recommendedAge") &&
                int.TryParse(pair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                    ? number : pair.Value;
        }
        return new ItemDto(item.LibraryCode, item.Name, item.Type.ToString().ToLowerInvariant(),
            item.CategoryName, new ItemBranchDto(item.BranchCode, item.BranchName),
            item.InTransit ? "InTransit" : item.Status.ToString(), item.Description, details);
    }
}
