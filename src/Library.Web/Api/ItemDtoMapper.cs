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
        foreach (var pair in item.Details)
        {
            // No real query exists yet; assume detail keys are property names that can be camel-cased.
            var key = JsonNamingPolicy.CamelCase.ConvertName(pair.Key);
            details[key] = (key is "year" or "recommendedAge") &&
                int.TryParse(pair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                    ? number : pair.Value;
        }
        return new ItemDto(item.LibraryCode, item.Name, item.Type.ToString().ToLowerInvariant(),
            item.CategoryName, new ItemBranchDto(item.BranchCode, item.BranchName),
            item.InTransit ? "InTransit" : item.Status.ToString(), item.Description, details);
    }
}
