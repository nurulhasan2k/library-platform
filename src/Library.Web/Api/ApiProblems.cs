using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Library.Web.Api;

internal static class ApiProblems
{
    public static ProblemDetails Create(HttpContext context, int status, string? detail = null)
    {
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = ReasonPhrases.GetReasonPhrase(status),
            Status = status,
            Detail = detail ?? DefaultDetail(status)
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        return problem;
    }

    public static Task WriteAsync(HttpContext context, int status, string? detail = null, CancellationToken ct = default)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        if (HttpMethods.IsHead(context.Request.Method))
            return Task.CompletedTask;
        return context.Response.WriteAsJsonAsync(Create(context, status, detail),
            options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json", cancellationToken: ct);
    }

    private static string DefaultDetail(int status) => status switch
    {
        400 => "One or more request parameters are invalid.",
        401 => "A valid X-Api-Key header is required.",
        404 => "The requested API resource was not found.",
        405 => "This API is read-only. Allowed methods: GET, HEAD, OPTIONS.",
        500 => "An unexpected error occurred. Try again later.",
        503 => "The API is unavailable because its key is not configured.",
        _ => "The API request could not be completed."
    };
}
