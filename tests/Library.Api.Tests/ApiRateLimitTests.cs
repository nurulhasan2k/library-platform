using System.Net;
using System.Text.Json;
using Xunit;

namespace Library.Api.Tests;

public sealed class ApiRateLimitTests
{
    [Fact]
    public async Task LimitOfThreeRejectsTheFourthRequestWithRetryAfter()
    {
        await using var factory = new ApiFactory(limit: 3);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiFactory.TestKey);
        for (var request = 0; request < 3; request++)
        {
            using var accepted = await client.GetAsync("/api/v1/items");
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }
        using var rejected = await client.GetAsync("/api/v1/items");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.InRange(rejected.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, 60);
        using var json = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal(429, json.RootElement.GetProperty("status").GetInt32());
        foreach (var name in new[] { "title", "detail", "traceId" })
            Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty(name).GetString()));
    }
}
