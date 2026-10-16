using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Library.Core.Configuration;

namespace Library.Web.Controllers;

/// <summary>Documentation for the API: a machine-readable OpenAPI file and a small interactive page (no outside libraries).</summary>
public sealed class ApiDocsController(IOptions<ApiOptions> options) : Controller
{
    [HttpGet("/api/docs")]
    public IActionResult Docs() => options.Value.EnableDocs ? View() : NotFound();

    [HttpGet("/api/openapi.json")]
    public IActionResult OpenApi() =>
        options.Value.EnableDocs ? Json(OpenApiDocument.Build($"{Request.Scheme}://{Request.Host}", options.Value)) : NotFound();
}

/// <summary>Builds the OpenAPI 3.0 description by hand so the project needs no NuGet packages.</summary>
public static class OpenApiDocument
{
    public static object Build(string server, ApiOptions api)
    {
        object Param(string name, string where, string type, string desc, bool required = false, object? example = null) => new Dictionary<string, object?>
        {
            ["name"] = name, ["in"] = where, ["required"] = required, ["description"] = desc, ["schema"] = new { type }, ["example"] = example
        };
        object Resp(string desc, string? schema = null) => schema is null
            ? new { description = desc }
            : new { description = desc, content = new Dictionary<string, object> { ["application/json"] = new { schema = new Dictionary<string, string> { ["$ref"] = "#/components/schemas/" + schema } } } };
        Dictionary<string, object> Responses(string okDesc, string? schema, params (string code, string desc)[] errors)
        {
            var d = new Dictionary<string, object> { ["200"] = Resp(okDesc, schema) };
            foreach (var (code, desc) in errors) d[code] = Resp(desc, "Error");
            d["401"] = Resp("API key missing", "Error");
            d["403"] = Resp("API key not valid", "Error");
            d["429"] = Resp($"Rate limit exceeded ({api.RateLimitPerMinute} requests per minute)", "Error");
            return d;
        }
        object Get(string summary, string tag, object[] parameters, Dictionary<string, object> responses) => new Dictionary<string, object>
        {
            ["get"] = new Dictionary<string, object>
            {
                ["summary"] = summary, ["tags"] = new[] { tag }, ["parameters"] = parameters,
                ["security"] = new object[] { new Dictionary<string, object> { ["ApiKey"] = Array.Empty<string>() } }, ["responses"] = responses
            }
        };
        Dictionary<string, object> Obj(Dictionary<string, object> props) => new() { ["type"] = "object", ["properties"] = props };
        object Str(string? example = null) => new { type = "string", example };

        return new Dictionary<string, object>
        {
            ["openapi"] = "3.0.3",
            ["info"] = new { title = "Library Platform public API", version = "1.0.0",
                description = "Read-only access to the catalogue, categories, branches and opening hours. Send your key in the X-Api-Key header. Only GET, HEAD and OPTIONS are accepted; anything else returns 405. No borrower, loan or fine data is ever returned." },
            ["servers"] = new[] { new { url = server } },
            ["components"] = new Dictionary<string, object>
            {
                ["securitySchemes"] = new { ApiKey = new { type = "apiKey", @in = "header", name = "X-Api-Key" } },
                ["schemas"] = new Dictionary<string, object>
                {
                    ["Error"] = Obj(new() { ["error"] = Obj(new() { ["code"] = Str("not_found"), ["message"] = Str("No item has the library code 'XX-1'."), ["status"] = new { type = "integer", example = 404 } }) }),
                    ["Item"] = Obj(new()
                    {
                        ["code"] = Str("BK-001"), ["name"] = Str("Dune"), ["type"] = new { type = "string", @enum = new[] { "book", "music", "toy" } }, ["description"] = Str(),
                        ["status"] = new { type = "string", @enum = new[] { "available", "borrowed", "damaged", "in-transit" } }, ["available"] = new { type = "boolean" },
                        ["category"] = Str("Science Fiction"), ["branch"] = Obj(new() { ["code"] = Str("CENTRAL"), ["name"] = Str("Central Library") }),
                        ["inTransit"] = new { type = "boolean" }, ["details"] = new { type = "object", additionalProperties = new { type = "string" }, example = new { Author = "Frank Herbert", Genre = "Science Fiction" } }
                    }),
                    ["ItemPage"] = Obj(new()
                    {
                        ["items"] = new { type = "array", items = new Dictionary<string, string> { ["$ref"] = "#/components/schemas/Item" } },
                        ["totalCount"] = new { type = "integer" }, ["page"] = new { type = "integer" }, ["pageSize"] = new { type = "integer" }, ["totalPages"] = new { type = "integer" }
                    }),
                    ["Category"] = Obj(new() { ["id"] = new { type = "integer" }, ["name"] = Str("Rock"), ["type"] = Str("music"), ["itemCount"] = new { type = "integer" } }),
                    ["Branch"] = Obj(new()
                    {
                        ["code"] = Str("CENTRAL"), ["name"] = Str("Central Library"), ["address"] = Str(), ["phone"] = Str(), ["openNow"] = new { type = "boolean" }, ["today"] = Str("09:00-18:00"),
                        ["openingHours"] = new { type = "array", items = Obj(new() { ["day"] = Str("Monday"), ["closed"] = new { type = "boolean" }, ["opens"] = Str("09:00"), ["closes"] = Str("18:00") }) }
                    }),
                    ["Status"] = Obj(new() { ["status"] = Str("ok"), ["apiVersion"] = Str("v1"), ["serverTime"] = Str(), ["totalItems"] = new { type = "integer" }, ["branches"] = new { type = "array", items = new { type = "object" } } })
                }
            },
            ["paths"] = new Dictionary<string, object>
            {
                ["/api/v1/items"] = Get("Search the catalogue", "Items", new[]
                {
                    Param("q", "query", "string", "Words that must all appear in the name, author, artist, genre or code.", example: "dune"),
                    Param("type", "query", "string", "book, music or toy.", example: "book"),
                    Param("categoryId", "query", "integer", "A category id from /categories."),
                    Param("branch", "query", "string", "A branch code from /branches.", example: "CENTRAL"),
                    Param("availableOnly", "query", "boolean", "Only items that can be borrowed now."),
                    Param("page", "query", "integer", "Page number, starting at 1.", example: 1),
                    Param("pageSize", "query", "integer", $"Items per page (max {api.MaxPageSize}).", example: 20),
                }, Responses("A page of items", "ItemPage", ("400", "A parameter was invalid"))),
                ["/api/v1/items/{code}"] = Get("Get one item by library code", "Items", new[] { Param("code", "path", "string", "Library code.", true, "BK-001") },
                    Responses("The item", "Item", ("404", "No such item (destroyed items are never returned)"))),
                ["/api/v1/categories"] = Get("List categories with item counts", "Reference data", Array.Empty<object>(), Responses("All categories", null)),
                ["/api/v1/branches"] = Get("List branches with opening hours and whether each is open now", "Reference data", Array.Empty<object>(), Responses("All branches", null)),
                ["/api/v1/branches/{code}"] = Get("Get one branch", "Reference data", new[] { Param("code", "path", "string", "Branch code.", true, "NORTH") },
                    Responses("The branch", "Branch", ("404", "No such branch"))),
                ["/api/v1/status"] = Get("System and branch operating status", "Status", Array.Empty<object>(), Responses("Status", "Status")),
            }
        };
    }
}
