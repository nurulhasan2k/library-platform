using System.Net;
using System.Text.RegularExpressions;
using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Import;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Library.Import.Tests;

public sealed class MockImportProviderTests
{
    [Fact]
    public async Task JsonRowsUseTheCsvValidatorAndRejectEachInvalidItemWithAReason()
    {
        await using var factory = new ImportWebFactory();
        using var scope = factory.Services.CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<ImportWorkflowService>();
        var started = await workflow.StartExternalAsync(2, 1);
        Assert.Null(started.Error);
        var preview = workflow.GetPreview(started.Token!, 1)!;
        Assert.Equal(21, preview.Validation.ValidRows.Count);
        Assert.Equal(4, preview.Validation.RejectedRows.Count);
        Assert.Contains(preview.Validation.RejectedRows, row => row.Fields["LibraryCode"] == "BK-625" && row.Reason.Contains("ItemType must be book, music or toy."));
        Assert.Contains(preview.Validation.RejectedRows, row => row.Fields["LibraryCode"] == "BK-626" && row.Reason.Contains("Category does not exist."));
        Assert.Contains(preview.Validation.RejectedRows, row => row.Fields["LibraryCode"] == " bk-600 " && row.Reason.Contains("repeats inside this file"));
        Assert.Contains(preview.Validation.RejectedRows, row => row.Fields["LibraryCode"] == "BK-628" && row.Reason.Contains("Name is required."));
        Assert.Contains(preview.Validation.ValidRows, row => row.ItemType == ItemType.Music && row.Year == 2000);
        Assert.Contains(preview.Validation.ValidRows, row => row.ItemType == ItemType.Toy && row.RecommendedAge == 6);
        Assert.All(preview.Validation.ValidRows, row => Assert.Equal(2, row.BranchId));
        Assert.Equal(0, factory.Writer.Calls);
    }

    [Fact]
    public async Task RepeatingTheJsonImportCannotCreateDuplicates()
    {
        await using var factory = new ImportWebFactory();
        using var scope = factory.Services.CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<ImportWorkflowService>();
        var first = await workflow.StartExternalAsync(2, 1);
        var confirmed = await workflow.ConfirmAsync(first.Token!, 1, ImportMode.SkipBadRows);
        Assert.NotNull(confirmed.BatchId);
        var second = await workflow.StartExternalAsync(2, 1);
        var preview = workflow.GetPreview(second.Token!, 1)!;
        Assert.Empty(preview.Validation.ValidRows);
        Assert.Equal(25, preview.Validation.RejectedRows.Count);
        Assert.Equal(22, preview.Validation.RejectedRows.Count(row => row.Reason.Contains("already exists")));
        await workflow.ConfirmAsync(second.Token!, 1, ImportMode.SkipBadRows);
        var batches = await scope.ServiceProvider.GetRequiredService<IImportBatchStore>().ListAsync();
        Assert.Equal(2, batches.Count);
        Assert.Equal(21, batches.Sum(batch => batch.RowsCreated));
        Assert.Equal(0, batches[0].RowsCreated);
        Assert.Equal(25, batches[0].RowsRejected);
        Assert.Equal(1, factory.Writer.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingFileAndMalformedJsonReturnFriendlyMessagesOnTheStartPage(bool malformed)
    {
        var path = Path.Combine(Path.GetTempPath(), "import-catalogue-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            if (malformed) await File.WriteAllTextAsync(path, "{ not valid JSON");
            await using var parent = new ImportWebFactory();
            await using var factory = parent.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.PostConfigure<ImportOptions>(options => options.MockProviderPath = path)));
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
            client.DefaultRequestHeaders.Add("X-Test-User", "1");
            var start = await client.GetStringAsync("/Import");
            Assert.Contains("simulated", start);
            var token = WebUtility.HtmlDecode(Regex.Match(start,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            Assert.False(string.IsNullOrEmpty(token));
            using var response = await client.PostAsync("/Import", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token, ["Source"] = "MockProvider", ["TargetBranchId"] = "2"
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            using var redirected = await client.GetAsync(response.Headers.Location);
            Assert.Equal(HttpStatusCode.OK, redirected.StatusCode);
            var html = await redirected.Content.ReadAsStringAsync();
            Assert.Contains(malformed ? "simulated catalogue JSON is malformed" : "simulated catalogue file was not found", html);
            Assert.DoesNotContain("JsonException", html);
            Assert.DoesNotContain("stack trace", html, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, parent.Writer.Calls);
            Assert.Empty(await factory.Services.GetRequiredService<IImportBatchStore>().ListAsync());
        }
        finally
        {
            if (malformed) File.Delete(path);
        }
    }
}
