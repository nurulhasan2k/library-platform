using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Library.Core.Configuration;
using Library.Core.Contracts;
using Microsoft.Extensions.Options;

namespace Library.Core.Import;

public sealed class ImportWorkflowService(
    CatalogueCsvValidator validator, ImportLookupAdapter lookups, ICatalogueCodeLookup codeLookup,
    ICatalogueQuery catalogue, ImportPreviewStore previews, IImportItemWriter writer,
    IImportBatchStore batches, IClock clock, IOptions<ImportOptions> options)
{
    public long MaximumBytes => options.Value.MaxFileSizeMb * 1024L * 1024L;

    public string? CheckUpload(string fileName, long length, string contentType)
    {
        if (fileName.Length > 255 || !Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            return "Choose a CSV file with a file name of at most 255 characters.";
        if (length <= 0 || length > MaximumBytes)
            return $"Choose a non-empty file no larger than {options.Value.MaxFileSizeMb} MB.";
        if (!MediaTypeHeaderValue.TryParse(contentType, out var parsed) ||
            parsed.MediaType?.ToLowerInvariant() is not ("text/csv" or "application/csv" or "application/vnd.ms-excel"))
            return "The upload must have a CSV content type (text/csv, application/csv or application/vnd.ms-excel).";
        return null;
    }

    public async Task<IReadOnlyList<BranchSummary>> GetBranchesAsync(CancellationToken ct = default)
        => (await catalogue.GetBranchesAsync(ct)).Where(branch => branch.IsActive).ToArray();

    public async Task<ImportStartResult> StartAsync(ImportSubmission submission, int userId, CancellationToken ct = default)
    {
        if (userId <= 0 || !Enum.IsDefined(submission.Source))
            return new(null, "The import details are invalid. Sign in again and choose a source.");
        var error = CheckUpload(submission.FileName, submission.Content.LongLength, submission.ContentType);
        if (error is not null) return new(null, error);
        if (!(await GetBranchesAsync(ct)).Any(branch => branch.Id == submission.TargetBranchId))
            return new(null, "Choose an active target branch.");
        var validation = await ValidateFreshAsync(submission, ct);
        if (validation.FileErrors.Count > 0)
            return new(null, string.Join(" ", validation.FileErrors));
        if (validation.ValidRows.Count + validation.RejectedRows.Count == 0)
            return new(null, "The file contains no data rows.");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        previews.Save(new PendingImport(token, userId, submission, validation, clock.Now.AddMinutes(30)));
        return new(token, null);
    }

    public PendingImport? GetPreview(string token, int userId) => previews.Get(token, userId, clock.Now);

    public bool Cancel(string token, int userId) => previews.Get(token, userId, clock.Now, consume: true) is not null;

    public async Task<ImportConfirmResult> ConfirmAsync(string token, int userId, ImportMode mode, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(mode)) return new(null, "Choose a valid import mode.");
        var pending = previews.Get(token, userId, clock.Now, consume: true);
        if (pending is null) return new(null, "This preview has expired, was cancelled or has already been used. Upload again.");
        var validation = pending.Validation;
        var created = 0;
        string? error;
        try
        {
            validation = await ValidateFreshAsync(pending.Submission, ct);
            error = validation.FileErrors.Count > 0 ? string.Join(" ", validation.FileErrors) : null;
            if (!(await GetBranchesAsync(ct)).Any(branch => branch.Id == pending.Submission.TargetBranchId))
                error = "The target branch is no longer active. Nothing was imported.";
            if (error is null && mode == ImportMode.AllOrNothing && validation.RejectedRows.Count > 0)
                error = "Nothing was imported because all or nothing mode found rejected rows.";
            if (error is null && validation.ValidRows.Count == 0)
                error = "There are no valid rows to import.";
            if (error is null)
            {
                var result = await writer.CreateManyAsync(validation.ValidRows, pending.Submission.TargetBranchId, ct);
                if (result.Success) created = result.CreatedCount;
                else error = result.Error ?? "Item creation could not be completed. Nothing was imported.";
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            error = "The import could not be completed. Upload again or try later.";
        }
        var total = pending.Validation.ValidRows.Count + pending.Validation.RejectedRows.Count;
        var report = validation.RejectedRows.Count == 0 ? null : Encoding.UTF8.GetString(ImportErrorCsvWriter.Write(validation));
        var summary = error ?? (validation.RejectedRows.Count > 0 ? $"{validation.RejectedRows.Count} rows were rejected." : null);
        var batch = await batches.AddAsync(new ImportBatch(0, pending.Submission.Source.ToString(), pending.Submission.FileName,
            userId, clock.Now, total, created, validation.RejectedRows.Count, summary,
            pending.Submission.TargetBranchId, report), CancellationToken.None);
        return new(batch.Id, null);
    }

    private async Task<ImportValidationResult> ValidateFreshAsync(ImportSubmission submission, CancellationToken ct)
    {
        var current = await lookups.LoadAsync([], ct);
        using var first = new MemoryStream(submission.Content, writable: false);
        var parsed = validator.Validate(first, submission.FileName, current, submission.TargetBranchId);
        if (parsed.FileErrors.Count > 0) return parsed;
        var codes = parsed.ValidRows.Select(row => row.LibraryCode)
            .Concat(parsed.RejectedRows.Select(row => row.Fields.GetValueOrDefault("LibraryCode", "")))
            .Where(code => code.Length <= 30);
        var existing = await codeLookup.GetExistingCodesAsync(codes, ct);
        using var second = new MemoryStream(submission.Content, writable: false);
        return validator.Validate(second, submission.FileName, current with { ExistingCodes = existing }, submission.TargetBranchId);
    }
}
