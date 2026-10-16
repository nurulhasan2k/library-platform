namespace Library.Core.Import;

public sealed record ImportBatch(
    int Id, string Source, string FileName, int ByUserId, DateTime At,
    int RowsTotal, int RowsCreated, int RowsRejected, string? ErrorSummary,
    int? TargetBranchId, string? ErrorReport);

public enum ImportSource { Csv, MockProvider }
public enum ImportMode { AllOrNothing, SkipBadRows }

public sealed record ImportSubmission(ImportSource Source, string FileName, string ContentType, byte[] Content, int TargetBranchId);
public sealed record ImportStartResult(string? Token, string? Error);
public sealed record ImportConfirmResult(int? BatchId, string? Error);

public sealed record PendingImport(
    string Token, int ByUserId, ImportSubmission Submission, ImportValidationResult Validation, DateTime ExpiresAt);
