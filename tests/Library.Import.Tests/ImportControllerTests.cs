using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Library.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Library.Import.Tests;

public sealed class ImportControllerTests
{
    [Theory]
    [InlineData(null, 401)]
    [InlineData("Reception", 403)]
    [InlineData("Manager", 403)]
    public async Task EveryImportRouteRequiresAdmin(string? role, int status)
    {
        await using var factory = new ImportWebFactory();
        using var client = factory.Client(role);
        foreach (var path in new[] { "/Import", "/Import/Template", "/Import/Preview?token=abc", "/Import/History", "/Import/Result/1", "/Import/Errors/1" })
            Assert.Equal(status, (int)(await client.GetAsync(path)).StatusCode);
        foreach (var path in new[] { "/Import", "/Import/Confirm", "/Import/Cancel" })
            Assert.Equal(status, (int)(await client.PostAsync(path, new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(0, factory.Writer.Calls);
    }

    [Fact]
    public async Task PreviewDoesNotSaveAndConfirmCreatesThenRedirectsToResult()
    {
        await using var factory = new ImportWebFactory();
        using var client = factory.Client();
        var preview = await Upload(client);
        Assert.Contains("20 valid", preview.Html);
        Assert.Contains("0 rejected", preview.Html);
        Assert.Equal(0, factory.Writer.Calls);
        Assert.Empty(await Batches(factory));
        using var confirmed = await Confirm(client, preview);
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        Assert.Contains("/Import/Result/", confirmed.Headers.Location!.OriginalString);
        var result = await client.GetStringAsync(confirmed.Headers.Location);
        Assert.Contains("20 created", result);
        var batch = Assert.Single(await Batches(factory));
        Assert.Equal(20, batch.RowsCreated);
        Assert.Equal(20, batch.RowsTotal);
        Assert.Equal(1, batch.ByUserId);
        Assert.Equal(factory.Clock.Now, batch.At);
        Assert.Equal(2, batch.TargetBranchId);
        Assert.Contains("items-valid.csv", await client.GetStringAsync("/Import/History"));
    }

    [Fact]
    public async Task CancelConsumesThePreviewWithoutWriting()
    {
        await using var factory = new ImportWebFactory();
        using var client = factory.Client();
        var preview = await Upload(client);
        using var cancel = await client.PostAsync("/Import/Cancel", Form(preview));
        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(preview.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Confirm(client, preview)).StatusCode);
        Assert.Equal(0, factory.Writer.Calls);
        Assert.Empty(await Batches(factory));
    }

    [Fact]
    public async Task PreviewExpiresAfterThirtyMinutes()
    {
        await using var factory = new ImportWebFactory();
        using var client = factory.Client();
        var preview = await Upload(client);
        factory.Clock.Now = factory.Clock.Now.AddMinutes(30);
        using var response = await Confirm(client, preview);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("expired", await client.GetStringAsync(response.Headers.Location));
        Assert.Equal(0, factory.Writer.Calls);
        Assert.Empty(await Batches(factory));
    }

    [Theory]
    [InlineData("AllOrNothing", 0, 0)]
    [InlineData("SkipBadRows", 19, 1)]
    public async Task BothModesHandleRejectedRows(string mode, int created, int writerCalls)
    {
        await using var factory = new ImportWebFactory();
        using var client = factory.Client();
        var mixed = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(ImportTestData.Sample("items-valid.csv")).Replace("BK-501,book", "BK-501,cd"));
        var preview = await Upload(client, mixed);
        Assert.Contains("19 valid", preview.Html);
        Assert.Contains("1 rejected", preview.Html);
        await Confirm(client, preview, mode);
        var batch = Assert.Single(await Batches(factory));
        Assert.Equal(created, batch.RowsCreated);
        Assert.Equal(1, batch.RowsRejected);
        Assert.Equal(writerCalls, factory.Writer.Calls);
        Assert.Contains("ItemType must be book, music or toy.", batch.ErrorReport);
        using var errors = await client.GetAsync($"/Import/Errors/{batch.Id}");
        Assert.Equal(HttpStatusCode.OK, errors.StatusCode);
        Assert.Equal("text/csv", errors.Content.Headers.ContentType?.MediaType);
        Assert.Contains("RowNumber,Reason,LibraryCode", await errors.Content.ReadAsStringAsync());
        Assert.Contains("BK-501", await errors.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReuploadCannotCreateDuplicates()
    {
        await using var factory = new ImportWebFactory();
        using var client = factory.Client();
        await Confirm(client, await Upload(client));
        var duplicate = await Upload(client);
        Assert.Contains("0 valid", duplicate.Html);
        Assert.Contains("20 rejected", duplicate.Html);
        await Confirm(client, duplicate, "SkipBadRows");
        var batches = await Batches(factory);
        Assert.Equal(2, batches.Count);
        Assert.Equal(20, batches.Sum(batch => batch.RowsCreated));
        Assert.Equal(20, batches[0].RowsRejected);
        Assert.Equal(0, batches[0].RowsCreated);
        Assert.Equal(1, factory.Writer.Calls);
    }

    [Fact]
    public async Task ConfirmRevalidatesCodesAddedAfterPreview()
    {
        await using var factory = new ImportWebFactory();
        using var client = factory.Client();
        var preview = await Upload(client);
        factory.Catalogue.AddCode("bk-500");
        await Confirm(client, preview);
        var batch = Assert.Single(await Batches(factory));
        Assert.Equal(0, batch.RowsCreated);
        Assert.Equal(1, batch.RowsRejected);
        Assert.Contains("already exists", batch.ErrorReport);
        Assert.Equal(0, factory.Writer.Calls);
    }

    [Fact]
    public async Task TokenCanOnlyBeConfirmedOnce()
    {
        await using var factory = new ImportWebFactory();
        using var client = factory.Client();
        var preview = await Upload(client);
        await Confirm(client, preview);
        using var replay = await Confirm(client, preview);
        Assert.Contains("already been used", await client.GetStringAsync(replay.Headers.Location));
        Assert.Equal(1, factory.Writer.Calls);
        Assert.Single(await Batches(factory));
    }

    [Fact]
    public async Task AnotherAdminCannotReadConsumeOrCancelTheToken()
    {
        await using var factory = new ImportWebFactory();
        using var owner = factory.Client();
        using var other = factory.Client(userId: 2);
        var preview = await Upload(owner);
        Assert.Equal(HttpStatusCode.Redirect, (await other.GetAsync(preview.Location)).StatusCode);
        var otherToken = await Antiforgery(other);
        var stolen = preview with { Antiforgery = otherToken };
        await other.PostAsync("/Import/Cancel", Form(stolen));
        await Confirm(other, stolen);
        Assert.Equal(0, factory.Writer.Calls);
        await Confirm(owner, preview);
        Assert.Equal(20, Assert.Single(await Batches(factory)).RowsCreated);
    }

    [Theory]
    [InlineData("/Import")]
    [InlineData("/Import/Confirm")]
    [InlineData("/Import/Cancel")]
    public async Task PostsRequireAntiforgery(string path)
    {
        await using var factory = new ImportWebFactory();
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(path, new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(0, factory.Writer.Calls);
    }

    [Fact]
    public async Task MockProviderUsesTheSelectedBranchWithoutUploadingAFile()
    {
        await using var factory = new ImportWebFactory();
        using var client = factory.Client();
        using var response = await client.PostAsync("/Import", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Antiforgery(client), ["Source"] = "MockProvider", ["TargetBranchId"] = "1"
        }));
        var html = await client.GetStringAsync(response.Headers.Location);
        var preview = new Preview(response.Headers.Location!, Input(html, "Token"), Input(html, "__RequestVerificationToken"), html);
        Assert.Contains("21 valid", html);
        Assert.Contains("4 rejected", html);
        await Confirm(client, preview, "SkipBadRows");
        var batch = Assert.Single(await Batches(factory));
        Assert.Equal(21, batch.RowsCreated);
        Assert.Equal(1, batch.TargetBranchId);
        Assert.Equal("MockProvider", batch.Source);
    }

    [Theory]
    [InlineData("items.txt", "text/csv")]
    [InlineData("items.csv", "application/json")]
    public async Task UploadMetadataIsRejectedBeforeParsing(string fileName, string contentType)
    {
        await using var factory = new ImportWebFactory();
        using var client = factory.Client();
        using var response = await UploadResponse(client, [0xFF, 0xFE], fileName, contentType);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = await client.GetStringAsync(response.Headers.Location);
        Assert.DoesNotContain("UTF-8", html);
        Assert.Contains(fileName.EndsWith(".txt") ? "Choose a CSV file" : "CSV content type", html);
        Assert.Equal(0, factory.Writer.Calls);
    }

    [Fact]
    public async Task OversizedUploadIsRejectedBeforeParsing()
    {
        await using var factory = new ImportWebFactory();
        using var client = factory.Client();
        using var response = await UploadResponse(client, new byte[5 * 1024 * 1024 + 1]);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("no larger than 5 MB", await client.GetStringAsync(response.Headers.Location));
        Assert.Equal(0, factory.Writer.Calls);
    }

    [Fact]
    public async Task WriterExceptionsProduceFriendlyResultWithoutStackTrace()
    {
        await using var factory = new ImportWebFactory();
        using var client = factory.Client();
        factory.Writer.ThrowOnWrite = true;
        using var response = await Confirm(client, await Upload(client));
        var html = await client.GetStringAsync(response.Headers.Location);
        Assert.Contains("could not be completed", html);
        Assert.DoesNotContain("Private database failure", html);
        Assert.DoesNotContain("InvalidOperationException", html);
        Assert.Equal(0, Assert.Single(await Batches(factory)).RowsCreated);
    }

    [Fact]
    public async Task TemplateContainsTheCanonicalHeader()
    {
        await using var factory = new ImportWebFactory();
        using var client = factory.Client();
        using var response = await client.GetAsync("/Import/Template");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(string.Join(',', Library.Core.Import.CatalogueCsvValidator.Columns), await response.Content.ReadAsStringAsync());
    }

    private static async Task<IReadOnlyList<Library.Core.Import.ImportBatch>> Batches(ImportWebFactory factory)
        => await factory.Services.GetRequiredService<IImportBatchStore>().ListAsync();

    private static async Task<string> Antiforgery(HttpClient client) => Input(await client.GetStringAsync("/Import"), "__RequestVerificationToken");

    private static string Input(string html, string name)
    {
        var input = Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase).Cast<Match>()
            .First(match => match.Value.Contains($"name=\"{name}\"", StringComparison.Ordinal));
        return WebUtility.HtmlDecode(Regex.Match(input.Value, "value=\"([^\"]*)\"").Groups[1].Value);
    }

    private sealed record Preview(Uri Location, string Token, string Antiforgery, string Html);

    private static async Task<Preview> Upload(HttpClient client, byte[]? bytes = null)
    {
        using var response = await UploadResponse(client, bytes ?? ImportTestData.Sample("items-valid.csv"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Import/Preview", response.Headers.Location!.OriginalString);
        var html = await client.GetStringAsync(response.Headers.Location);
        return new Preview(response.Headers.Location, Input(html, "Token"), Input(html, "__RequestVerificationToken"), html);
    }

    private static async Task<HttpResponseMessage> UploadResponse(HttpClient client, byte[] bytes,
        string fileName = "items-valid.csv", string contentType = "text/csv")
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(await Antiforgery(client)), "__RequestVerificationToken");
        form.Add(new StringContent("Csv"), "Source");
        form.Add(new StringContent("2"), "TargetBranchId");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "File", fileName);
        return await client.PostAsync("/Import", form);
    }

    private static FormUrlEncodedContent Form(Preview preview, string mode = "AllOrNothing") => new(new Dictionary<string, string>
    {
        ["Token"] = preview.Token, ["Mode"] = mode, ["__RequestVerificationToken"] = preview.Antiforgery
    });

    private static Task<HttpResponseMessage> Confirm(HttpClient client, Preview preview, string mode = "AllOrNothing")
        => client.PostAsync("/Import/Confirm", Form(preview, mode));
}
