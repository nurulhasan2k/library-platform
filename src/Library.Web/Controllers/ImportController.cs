using System.Security.Claims;
using System.Text;
using Library.Core.Contracts;
using Library.Core.Import;
using Library.Web.Import;
using Library.Web.Models.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.Web.Controllers;

[Authorize(Roles = "Admin")]
[TypeFilter(typeof(ImportExceptionFilter))]
public sealed class ImportController(ImportWorkflowService imports, IImportBatchStore batches) : Controller
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(new ImportStartViewModel { Branches = (await imports.GetBranchesAsync(ct))
            .Select(branch => new ImportBranchViewModel(branch.Id, $"{branch.Code} - {branch.Name}")).ToArray() });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ImportStartViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Notice("Choose a valid source and target branch.");
        byte[]? content;
        string fileName;
        string contentType;
        if (model.Source == ImportSource.MockProvider)
        {
            var external = await imports.StartExternalAsync(model.TargetBranchId, UserId, ct);
            return external.Token is null ? Notice(external.Error!) : RedirectToAction(nameof(Preview), new { token = external.Token });
        }
        else
        {
            if (model.File is null) return Notice("Choose a CSV file to upload.");
            var error = imports.CheckUpload(model.File.FileName, model.File.Length, model.File.ContentType);
            if (error is not null) return Notice(error);
            content = await ImportUploadReader.ReadAsync(model.File, imports.MaximumBytes, ct);
            if (content is null) return Notice("The file is too large. Choose a smaller CSV file.");
            fileName = Path.GetFileName(model.File.FileName.Replace('\\', '/'));
            contentType = model.File.ContentType;
        }
        var result = await imports.StartAsync(new ImportSubmission(model.Source, fileName, contentType, content, model.TargetBranchId), UserId, ct);
        return result.Token is null ? Notice(result.Error!) : RedirectToAction(nameof(Preview), new { token = result.Token });
    }

    [HttpGet]
    public IActionResult Template()
        => File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(string.Join(',', CatalogueCsvValidator.Columns) + "\r\n")).ToArray(),
            "text/csv; charset=utf-8", "items-template.csv");

    [HttpGet]
    public IActionResult Preview(string? token)
    {
        var preview = imports.GetPreview(token ?? "", UserId);
        return preview is null ? Notice("This preview is unavailable or has expired. Upload again.") : View(ImportViewModelMapper.Preview(preview));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(ImportConfirmViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Notice("The confirmation details are invalid. Upload again.");
        var result = await imports.ConfirmAsync(model.Token, UserId, model.Mode, ct);
        return result.BatchId is null ? Notice(result.Error!) : RedirectToAction(nameof(Result), new { id = result.BatchId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Cancel(ImportTokenViewModel model)
    {
        if (!ModelState.IsValid) return Notice("The cancellation details are invalid.");
        return Notice(imports.Cancel(model.Token, UserId) ? "Import cancelled. Nothing was created." : "This preview is unavailable or has already expired.");
    }

    [HttpGet]
    public async Task<IActionResult> Result(int id, CancellationToken ct)
    {
        var batch = await batches.FindAsync(id, ct);
        return batch is null ? Notice("The import batch was not found.") : View(ImportViewModelMapper.Batch(batch));
    }

    [HttpGet]
    public async Task<IActionResult> History(CancellationToken ct)
        => View((await batches.ListAsync(ct)).Select(ImportViewModelMapper.Batch).ToArray());

    [HttpGet]
    public async Task<IActionResult> Errors(int id, CancellationToken ct)
    {
        var batch = await batches.FindAsync(id, ct);
        return batch?.ErrorReport is null ? Notice("No error report was found for that batch.")
            : File(Encoding.UTF8.GetBytes(batch.ErrorReport), "text/csv; charset=utf-8", $"import-{id}-errors.csv");
    }

    private RedirectToActionResult Notice(string message)
    {
        TempData["ImportMessage"] = message;
        return RedirectToAction(nameof(Index));
    }
}
