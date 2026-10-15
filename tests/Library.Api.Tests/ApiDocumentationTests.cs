using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Library.Api.Tests;

public sealed class ApiDocumentationTests
{
    [Fact]
    public async Task DocumentIsPublicAndContainsOnlyVersionedApiPathsWithKeySecurity()
    {
        await using var factory = new ApiFactory(key: "");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var document = json.RootElement;
        var paths = document.GetProperty("paths").EnumerateObject().Select(path => path.Name).ToArray();
        Assert.All(paths, path => Assert.StartsWith("/api/v1/", path));
        foreach (var path in new[] { "/api/v1/items", "/api/v1/items/{code}", "/api/v1/categories", "/api/v1/branches", "/api/v1/status" })
            Assert.Contains(path, paths);
        Assert.DoesNotContain(paths, path => path.Contains("ping", StringComparison.OrdinalIgnoreCase));
        var scheme = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("ApiKey");
        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal("X-Api-Key", scheme.GetProperty("name").GetString());
        Assert.Empty(document.GetProperty("security")[0].GetProperty("ApiKey").EnumerateArray());
        Assert.False(response.Headers.Contains("Set-Cookie"));
        using var unavailableApi = await client.GetAsync("/api/v1/items");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailableApi.StatusCode);
    }

    [Fact]
    public async Task SwaggerLoadsTheDocumentAndSupportsKeyAuthorizationWithReadOnlyMethods()
    {
        await using var factory = new ApiFactory(key: "");
        using var client = factory.CreateClient();
        using var ui = await client.GetAsync("/swagger");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        Assert.Contains("swagger-ui", await ui.Content.ReadAsStringAsync());
        var initializer = await client.GetStringAsync("/swagger/index.js");
        Assert.Contains("/openapi/v1.json", initializer);
        Assert.Contains("\"supportedSubmitMethods\":[\"get\",\"head\",\"options\"]", initializer);
        var bundle = await client.GetStringAsync("/swagger/swagger-ui-bundle.js");
        Assert.Contains("authorizeApiKey", bundle);
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task DocumentationIsUnavailableWhenDisabled(string path)
    {
        await using var parent = new ApiFactory();
        await using var factory = parent.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Api:EnableSwagger"] = "false" })));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DocumentationRejectsWritesWithoutAKey()
    {
        await using var factory = new ApiFactory(key: "");
        using var client = factory.CreateClient();
        foreach (var path in new[] { "/swagger/index.html", "/openapi/v1.json" })
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
        {
            using var response = await client.SendAsync(new HttpRequestMessage(method, path));
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
            Assert.Equal("GET, HEAD", string.Join(", ", response.Content.Headers.Allow));
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(405, json.RootElement.GetProperty("status").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
        }
    }
}
