using System.Net;
using System.Text.Json;
using Library.Core.Contracts;
using Xunit;

namespace Library.Api.Tests;

public sealed class CatalogueEndpointTests(CatalogueFixture fixture) : IClassFixture<CatalogueFixture>
{
    public static IEnumerable<object[]> InvalidParameters()
    {
        yield return ["?pageSize=0", "pageSize", "Must be an integer of 1 or more."];
        yield return ["?page=0", "page", "Must be an integer of 1 or more."];
        yield return ["?type=cd", "type", "Must be book, music or toy."];
        yield return ["?category=Unknown", "category", "Unknown category."];
        yield return ["?branch=Unknown", "branch", "Unknown branch."];
        yield return ["?availableOnly=maybe", "availableOnly", "Must be true or false."];
        yield return ["/" + new string('A', 31), "code", "Must be 1 to 30 characters using only letters, digits, - and _."];
        yield return ["/BAD%21CODE", "code", "Must be 1 to 30 characters using only letters, digits, - and _."];
    }

    [Theory]
    [MemberData(nameof(InvalidParameters))]
    public async Task ValidationIdentifiesTheParameterAndExplainsTheProblem(string suffix, string parameter, string message)
    {
        using var client = fixture.CreateClient();
        using var response = await client.GetAsync("/api/v1/items" + suffix);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(message, json.RootElement.GetProperty("errors").GetProperty(parameter)[0].GetString());
        foreach (var name in new[] { "title", "detail", "traceId" })
            Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty(name).GetString()));
    }

    [Fact]
    public async Task OversizedPageIsCappedInTheQueryAndResponse()
    {
        using var json = await Get("items?pageSize=101");
        Assert.Equal(100, fixture.Catalogue.LastFilter!.PageSize);
        Assert.Equal(100, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("totalItems").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task PagePastTheEndIsAnEmptySuccessfulPage()
    {
        using var json = await Get("items?page=999");
        Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(999, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("totalItems").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("totalPages").GetInt32());
    }

    [Theory]
    [InlineData("type=mUsIc", "MU-A")]
    [InlineData("category=science%20fiction", "BK-A,BK-B,BK-T")]
    [InlineData("branch=rIvEr", "BK-B,MU-A,TY-A")]
    [InlineData("q=%20miles%20", "MU-A")]
    public async Task FiltersAreResolvedAndForwardedToTheCatalogue(string query, string expectedCodes)
    {
        using var json = await Get("items?availableOnly=false&" + query);
        Assert.Equal(expectedCodes.Split(','), Codes(json.RootElement));
        var filter = fixture.Catalogue.LastFilter!;
        Assert.False(filter.AvailableOnly);
        if (query.StartsWith("type=")) Assert.Equal(ItemType.Music, filter.Type);
        if (query.StartsWith("category=")) Assert.Equal(11, filter.CategoryId);
        if (query.StartsWith("branch=")) Assert.Equal(2, filter.BranchId);
        if (query.StartsWith("q=")) Assert.Equal("miles", filter.Text);
    }

    [Theory]
    [InlineData("", true, "BK-A,MU-A")]
    [InlineData("?availableOnly=true", true, "BK-A,MU-A")]
    [InlineData("?availableOnly=false", false, "BK-A,BK-B,BK-T,MU-A,TY-A")]
    public async Task AvailabilityDefaultsToTrueAndExcludesTransfersUnlessFalse(string query, bool availableOnly, string expectedCodes)
    {
        using var json = await Get("items" + query);
        Assert.Equal(availableOnly, fixture.Catalogue.LastFilter!.AvailableOnly);
        Assert.Equal(expectedCodes.Split(','), Codes(json.RootElement));
        if (!availableOnly)
            Assert.Equal("InTransit", json.RootElement.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("code").GetString() == "BK-T").GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("BK-A")]
    [InlineData("MU-A")]
    [InlineData("TY-A")]
    [InlineData("BK-T")]
    [InlineData("BK-B")]
    public async Task ItemMappingIncludesAllDocumentedFields(string code)
    {
        using var json = await Get("items/" + code);
        var item = json.RootElement;
        var expected = fixture.Catalogue.Items.Single(row => row.LibraryCode == code);
        Assert.Equal(8, item.EnumerateObject().Count());
        Assert.Equal(expected.LibraryCode, item.GetProperty("code").GetString());
        Assert.Equal(expected.Name, item.GetProperty("name").GetString());
        Assert.Equal(expected.Type.ToString().ToLowerInvariant(), item.GetProperty("type").GetString());
        Assert.Equal(expected.CategoryName, item.GetProperty("category").GetString());
        Assert.Equal(expected.Description, item.GetProperty("description").GetString());
        Assert.Equal(expected.InTransit ? "InTransit" : expected.Status.ToString(), item.GetProperty("status").GetString());
        var branch = item.GetProperty("branch");
        Assert.Equal(2, branch.EnumerateObject().Count());
        Assert.Equal(expected.BranchCode, branch.GetProperty("code").GetString());
        Assert.Equal(expected.BranchName, branch.GetProperty("name").GetString());
        var details = item.GetProperty("details");
        Assert.Equal(2, details.EnumerateObject().Count());
        switch (code)
        {
            case "MU-A":
                Assert.Equal("Miles Davis", details.GetProperty("artist").GetString());
                Assert.Equal(1959, details.GetProperty("year").GetInt32());
                break;
            case "TY-A":
                Assert.Equal("Blocks", details.GetProperty("toyType").GetString());
                Assert.Equal(3, details.GetProperty("recommendedAge").GetInt32());
                break;
            default:
                Assert.Equal(expected.Details["Author"], details.GetProperty("author").GetString());
                Assert.Equal("Science Fiction", details.GetProperty("genre").GetString());
                break;
        }
    }

    [Fact]
    public async Task ActiveBranchesHaveMondayToSundayHoursWithCorrectShapes()
    {
        using var json = await Get("branches");
        var branches = json.RootElement.EnumerateArray().ToArray();
        Assert.Equal(new[] { "CENTRAL", "RIVER" }, branches.Select(branch => branch.GetProperty("code").GetString()));
        foreach (var branch in branches)
        {
            var expected = branch.GetProperty("code").GetString() == "CENTRAL"
                ? ("Central Library", "1 Main Street", "555-0101") : ("River Library", "2 River Road", "555-0102");
            Assert.Equal(expected.Item1, branch.GetProperty("name").GetString());
            Assert.Equal(expected.Item2, branch.GetProperty("address").GetString());
            Assert.Equal(expected.Item3, branch.GetProperty("phone").GetString());
            var hours = branch.GetProperty("openingHours").EnumerateArray().ToArray();
            Assert.Equal(new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" },
                hours.Select(day => day.GetProperty("day").GetString()));
            foreach (var day in hours.Take(6))
            {
                Assert.Equal(3, day.EnumerateObject().Count());
                Assert.False(day.TryGetProperty("closed", out _));
                Assert.Equal("09:00", day.GetProperty("opens").GetString());
                Assert.Equal("18:00", day.GetProperty("closes").GetString());
            }
            Assert.Equal(2, hours[6].EnumerateObject().Count());
            Assert.True(hours[6].GetProperty("closed").GetBoolean());
            Assert.False(hours[6].TryGetProperty("opens", out _));
            Assert.False(hours[6].TryGetProperty("closes", out _));
        }
    }

    [Fact]
    public async Task CategoriesAreGroupedByTypeWithNamesAndCounts()
    {
        using var json = await Get("categories");
        var groups = json.RootElement.EnumerateArray().ToArray();
        Assert.Equal(new[] { "book", "music", "toy" }, groups.Select(group => group.GetProperty("type").GetString()));
        var expected = new Dictionary<string, (string Name, int Count)[]>
        {
            ["book"] = [("Science Fiction", 3), ("Fantasy", 0)],
            ["music"] = [("Jazz", 1)], ["toy"] = [("Construction", 1)]
        };
        foreach (var group in groups)
            Assert.Equal(expected[group.GetProperty("type").GetString()!],
                group.GetProperty("categories").EnumerateArray()
                    .Select(category => (category.GetProperty("name").GetString()!, category.GetProperty("itemCount").GetInt32())).ToArray());
    }

    private async Task<JsonDocument> Get(string route)
    {
        using var client = fixture.CreateClient();
        using var response = await client.GetAsync("/api/v1/" + route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string[] Codes(JsonElement page) => page.GetProperty("items").EnumerateArray()
        .Select(item => item.GetProperty("code").GetString()!).OrderBy(code => code, StringComparer.Ordinal).ToArray();
}
