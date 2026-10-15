using System.Net;
using System.Text.Json;
using Xunit;

namespace Library.Api.Tests;

public sealed class ApiPipelineTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("TRACE")]
    [InlineData("CONNECT")]
    [InlineData("CUSTOM")]
    public async Task WritesAreRejectedBeforeRateLimitAndKey(string method)
    {
        using var factory = new ApiFactory(limit: 1);
        using var client = factory.CreateClient();
        await client.GetAsync("/api/v1/items");
        foreach (var path in new[] { "/api/v1/items", "/api/missing", "/api" })
        {
            using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
            await AssertProblem(response, 405);
            Assert.Equal("GET, HEAD, OPTIONS", string.Join(", ", response.Content.Headers.Allow));
        }
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("wrong", 401)]
    [InlineData(ApiFactory.TestKey, 200)]
    public async Task KeyIsRequired(string? supplied, int status)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        if (supplied is not null)
            client.DefaultRequestHeaders.Add("X-Api-Key", supplied);
        using var response = await client.GetAsync("/api/v1/items");
        Assert.Equal(status, (int)response.StatusCode);
        if (status == 401)
        {
            await AssertProblem(response, status);
            Assert.Equal("ApiKey", response.Headers.WwwAuthenticate.Single().Scheme);
        }
        Assert.DoesNotContain(factory.Logs, log => log.Contains(ApiFactory.TestKey) || log.Contains("wrong"));
    }

    [Fact]
    public async Task EmptyConfiguredKeyFailsClosed()
    {
        using var factory = new ApiFactory(key: "");
        using var client = factory.CreateClient();
        await AssertProblem(await client.GetAsync("/api/v1/items"), 503);
    }

    [Fact]
    public async Task DuplicateKeyHeadersAreRejected()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", new[] { ApiFactory.TestKey, ApiFactory.TestKey });
        await AssertProblem(await client.GetAsync("/api/v1/items"), 401);
    }

    [Fact]
    public async Task MissingKeysCountTowardRateLimitAndRetryAfterDecreases()
    {
        using var factory = new ApiFactory(limit: 1);
        using var client = factory.CreateClient();
        await AssertProblem(await client.GetAsync("/api/v1/items"), 401);
        await Task.Delay(1100);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/items");
        request.Headers.Add("Origin", "https://example.com");
        using var response = await client.SendAsync(request);
        await AssertProblem(response, 429);
        Assert.InRange(response.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, 59);
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("Retry-After", response.Headers.GetValues("Access-Control-Expose-Headers").Single());
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/unknown")]
    [InlineData("/api/v1/items/UNKNOWN")]
    [InlineData("/api/unknown.json")]
    [InlineData("/api/v1/ping")]
    public async Task UnknownResourcesUseProblemDetails(string path)
    {
        using var factory = new ApiFactory();
        using var client = Authorized(factory);
        await AssertProblem(await client.GetAsync(path), 404);
    }

    [Theory]
    [InlineData("/api/v1/items?page=0")]
    [InlineData("/api/v1/probe/json-error")]
    public async Task BadRequestsUseProblemDetails(string path)
    {
        using var factory = new ApiFactory();
        using var client = Authorized(factory);
        await AssertProblem(await client.GetAsync(path), 400);
    }

    [Theory]
    [InlineData("Production", "/api/v1/probe/fault")]
    [InlineData("Development", "/api/v1/probe/fault")]
    [InlineData("Production", "/api/v1/probe/text-error")]
    public async Task ServerErrorsAreGenericAndNeverLogTheKey(string environment, string path)
    {
        using var factory = new ApiFactory(environment: environment);
        using var client = Authorized(factory);
        using var response = await client.GetAsync(path);
        await AssertProblem(response, 500);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ApiFactory.TestKey, body);
        Assert.DoesNotContain("private data", body);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(factory.Logs, log => log.Contains(ApiFactory.TestKey));
    }

    [Theory]
    [InlineData("/api/v1/items", true, 200)]
    [InlineData("/api/v1/categories", true, 200)]
    [InlineData("/api/v1/branches", true, 200)]
    [InlineData("/api/v1/status", true, 200)]
    [InlineData("/api/v1/items/BK-001", true, 200)]
    [InlineData("/api/v1/items", false, 401)]
    [InlineData("/api/missing", true, 404)]
    public async Task HeadReturnsHeadersWithoutBody(string path, bool withKey, int status)
    {
        using var factory = new ApiFactory();
        using var client = withKey ? Authorized(factory) : factory.CreateClient();
        using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, path));
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(status >= 400 ? "application/problem+json" : "application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PreflightIsKeylessButRateLimitedAndDoesNotAllowCredentials()
    {
        using var factory = new ApiFactory(limit: 1);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/items");
        request.Headers.Add("Origin", "https://example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "X-Api-Key");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("GET,HEAD,OPTIONS", response.Headers.GetValues("Access-Control-Allow-Methods").Single());
        Assert.Equal("X-Api-Key", response.Headers.GetValues("Access-Control-Allow-Headers").Single());
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        await AssertProblem(await client.GetAsync("/api/v1/items"), 429);
    }

    [Fact]
    public async Task PlainOptionsRequiresKeyAndReturnsNoContent()
    {
        using var factory = new ApiFactory();
        using var client = Authorized(factory);
        using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Options, "/api/v1/items"));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        await AssertProblem(await client.SendAsync(new HttpRequestMessage(HttpMethod.Options, "/api/v1/items")), 401);
    }

    [Fact]
    public async Task PublicDtoPropertiesHaveNoPrivateNamesOrTypes()
    {
        var types = typeof(Library.Web.Api.Dtos.ItemDto).Assembly.GetTypes()
            .Where(type => type.Namespace == "Library.Web.Api.Dtos");
        foreach (var type in types)
        foreach (var property in type.GetProperties())
        foreach (var forbidden in new[] { "borrower", "patron", "loan", "fine", "notification", "reservation",
                     "user", "card", "pin", "password", "hash", "staff" })
        {
            Assert.DoesNotContain(forbidden, property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(forbidden, property.PropertyType.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task MvcAndSimilarPrefixesRemainOutsideTheApiPipeline()
    {
        using var factory = new ApiFactory(limit: 1);
        using var client = factory.CreateClient();
        using var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Equal("text/html", home.Content.Headers.ContentType?.MediaType);
        using var other = await client.GetAsync("/apiary");
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.NotEqual("application/problem+json", other.Content.Headers.ContentType?.MediaType);
        await AssertProblem(await client.GetAsync("/api/v1/items"), 401);
    }

    [Theory]
    [InlineData("items")]
    [InlineData("items/BK-001")]
    [InlineData("categories")]
    [InlineData("branches")]
    [InlineData("status")]
    public async Task PublicResponsesContainNoPrivateFieldsOrCookies(string route)
    {
        using var factory = new ApiFactory();
        using var client = Authorized(factory);
        using var response = await client.GetAsync("/api/v1/" + route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var body = await response.Content.ReadAsStringAsync();
        foreach (var forbidden in new[] { "patron", "loan", "fine", "notification", "reservation", "staff", "Hidden", ApiFactory.TestKey })
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        if (route.StartsWith("items"))
            Assert.Contains("Frank Herbert", body);
    }

    private static HttpClient Authorized(ApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiFactory.TestKey);
        return client;
    }

    private static async Task AssertProblem(HttpResponseMessage response, int status)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(status, json.RootElement.GetProperty("status").GetInt32());
        foreach (var field in new[] { "title", "detail", "traceId" })
            Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty(field).GetString()));
    }
}
