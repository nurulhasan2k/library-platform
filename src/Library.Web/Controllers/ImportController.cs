using System.Text;
using System.Text.Json;
using Library.Core.Configuration;
using Library.Core.Data;
using Library.Core.Import;
using Library.Core.Services;
using Library.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Library.Web.Controllers;

public sealed class ImportPreviewPage
{
    public required ImportPreview Preview { get; init; }
    public required string Token { get; init; }
    public required string BranchName { get; init; }
}

/// <summary>Data importer (feature 3.6): upload a CSV (or pull the mock external catalogue), review the checked rows, then import.</summary>
[Route("admin/import"), Authorize(Roles = Auth.Admin)]
public sealed class ImportController(
    ImportService import, IExternalCatalogueProvider provider, InventoryService inventory, IOptions<ImportOptions> options,
    IOptions<DatabaseOptions> database) : AppController
{
    private sealed record Staged(string Kind, string Source, int BranchId);

    private string StagingDir
    {
        get
        {
            var dir = Path.Combine(Path.GetDirectoryName(DataLocation.Resolve(database.Value.DataFile))!, "import-staging");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewBag.Branches = inventory.GetBranches(true);
        ViewBag.History = import.History();
        ViewBag.AllBranches = inventory.GetBranches().ToDictionary(b => b.Id);
        ViewBag.Provider = provider;
        ViewBag.Here = Here.CurrentBranch?.Id;
        ViewBag.MaxMb = options.Value.MaxFileSizeMb;
        return View();
    }

    [HttpGet("template.csv")]
    public IActionResult Template() => File(Encoding.UTF8.GetBytes(ImportService.CsvTemplate), "text/csv", "import-template.csv");

    [HttpPost("upload"), RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Upload(IFormFile? file, int branchId)
    {
        if (file is null || file.Length == 0) { Fail("Choose a CSV file first."); return RedirectToAction(nameof(Index)); }
        if (file.Length > options.Value.MaxFileSizeMb * 1024L * 1024L) { Fail($"That file is larger than {options.Value.MaxFileSizeMb} MB."); return RedirectToAction(nameof(Index)); }
        if (inventory.GetBranch(branchId) is not { IsActive: true } branch) { Fail("Choose the branch the items should go to."); return RedirectToAction(nameof(Index)); }

        string text;
        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, true)) text = await reader.ReadToEndAsync();
        var preview = import.PreviewCsv(text, file.FileName, branchId);
        if (preview.FileProblem != null) { Fail(preview.FileProblem); return RedirectToAction(nameof(Index)); }

        var token = Guid.NewGuid().ToString("N");
        await System.IO.File.WriteAllTextAsync(Path.Combine(StagingDir, token + ".csv"), text);
        await System.IO.File.WriteAllTextAsync(Path.Combine(StagingDir, token + ".json"), JsonSerializer.Serialize(new Staged("csv", file.FileName, branchId)));
        return View("Preview", new ImportPreviewPage { Preview = preview, Token = token, BranchName = branch.Name });
    }

    [HttpPost("provider")]
    public async Task<IActionResult> FromProvider(int branchId)
    {
        if (inventory.GetBranch(branchId) is not { IsActive: true } branch) { Fail("Choose the branch the items should go to."); return RedirectToAction(nameof(Index)); }
        var preview = import.PreviewProvider(provider, branchId);
        var token = Guid.NewGuid().ToString("N");
        await System.IO.File.WriteAllTextAsync(Path.Combine(StagingDir, token + ".json"), JsonSerializer.Serialize(new Staged("provider", provider.Name, branchId)));
        return View("Preview", new ImportPreviewPage { Preview = preview, Token = token, BranchName = branch.Name });
    }

    [HttpPost("commit")]
    public async Task<IActionResult> Commit(string token)
    {
        var staged = await LoadAsync(token);
        if (staged is null) { Fail("That import has expired. Please upload the file again."); return RedirectToAction(nameof(Index)); }

        // Check again at the moment of importing, in case the catalogue changed while the preview was open.
        var preview = staged.Value.Kind == "provider"
            ? import.PreviewProvider(provider, staged.Value.Staged.BranchId)
            : import.PreviewCsv(await System.IO.File.ReadAllTextAsync(Path.Combine(StagingDir, token + ".csv")), staged.Value.Staged.Source, staged.Value.Staged.BranchId);
        var batch = import.Commit(preview, User.Identity?.Name ?? "?");
        Discard(token);
        Flash($"Imported {batch.ImportedRows} of {batch.TotalRows} rows from {batch.Source}." + (batch.SkippedRows > 0 ? $" {batch.SkippedRows} skipped - see the history below." : ""));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cancel")]
    public IActionResult Cancel(string token)
    {
        Discard(token);
        Flash("Import cancelled. Nothing was added.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("problems/{token}")]
    public async Task<IActionResult> Problems(string token)
    {
        var staged = await LoadAsync(token);
        if (staged is null || staged.Value.Kind != "csv") return NotFound();
        var preview = import.PreviewCsv(await System.IO.File.ReadAllTextAsync(Path.Combine(StagingDir, token + ".csv")), staged.Value.Staged.Source, staged.Value.Staged.BranchId);
        return File(Encoding.UTF8.GetBytes(import.ProblemsCsv(preview)), "text/csv", "import-problems.csv");
    }

    private async Task<(string Kind, Staged Staged)?> LoadAsync(string token)
    {
        if (token is null || token.Length != 32 || !token.All(Uri.IsHexDigit)) return null; // never build a path from user text
        var meta = Path.Combine(StagingDir, token + ".json");
        if (!System.IO.File.Exists(meta)) return null;
        var s = JsonSerializer.Deserialize<Staged>(await System.IO.File.ReadAllTextAsync(meta));
        return s is null ? null : (s.Kind, s);
    }

    private void Discard(string token)
    {
        if (token is null || token.Length != 32 || !token.All(Uri.IsHexDigit)) return;
        foreach (var ext in new[] { ".csv", ".json" })
        {
            var p = Path.Combine(StagingDir, token + ext);
            if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
        }
    }
}
