using System.ComponentModel.DataAnnotations;
using Library.Core.Import;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Library.Web.Models.Import;

public sealed record ImportBranchViewModel(int Id, string Name);

public sealed class ImportStartViewModel
{
    [EnumDataType(typeof(ImportSource))]
    public ImportSource Source { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Choose a target branch.")]
    public int TargetBranchId { get; set; }
    public IFormFile? File { get; set; }
    [BindNever]
    public IReadOnlyList<ImportBranchViewModel> Branches { get; set; } = [];
}

public class ImportTokenViewModel
{
    [Required, StringLength(64, MinimumLength = 64)]
    public string Token { get; set; } = "";
}

public sealed class ImportConfirmViewModel : ImportTokenViewModel
{
    [EnumDataType(typeof(ImportMode))]
    public ImportMode Mode { get; set; }
}

public sealed record ImportPreviewRowViewModel(int RowNumber, string Code, string Name, bool Valid, string Reason);
public sealed record ImportPreviewViewModel(
    string Token, string Source, string FileName, DateTime ExpiresAt, int ValidCount, int RejectedCount,
    IReadOnlyList<ImportPreviewRowViewModel> Rows);
public sealed record ImportBatchViewModel(
    int Id, string Source, string FileName, int ByUserId, DateTime At, int RowsTotal,
    int RowsCreated, int RowsRejected, string? ErrorSummary, int? TargetBranchId, bool HasErrorReport);

internal static class ImportViewModelMapper
{
    public static ImportPreviewViewModel Preview(PendingImport preview)
        => new(preview.Token, preview.Submission.Source == ImportSource.Csv ? "CSV" : "Regional Library Network (simulated)",
            preview.Submission.FileName, preview.ExpiresAt, preview.Validation.ValidRows.Count, preview.Validation.RejectedRows.Count,
            preview.Validation.ValidRows.Select(row => new ImportPreviewRowViewModel(row.RowNumber, row.LibraryCode, row.Name, true, "Ready"))
                .Concat(preview.Validation.RejectedRows.Select(row => new ImportPreviewRowViewModel(row.RowNumber,
                    row.Fields.GetValueOrDefault("LibraryCode", ""), row.Fields.GetValueOrDefault("Name", ""), false, row.Reason)))
                .OrderBy(row => row.RowNumber).ToArray());

    public static ImportBatchViewModel Batch(ImportBatch batch)
        => new(batch.Id, batch.Source == nameof(ImportSource.Csv) ? "CSV" : "Regional Library Network (simulated)", batch.FileName,
            batch.ByUserId, batch.At, batch.RowsTotal, batch.RowsCreated, batch.RowsRejected,
            batch.ErrorSummary, batch.TargetBranchId, batch.ErrorReport is not null);
}
