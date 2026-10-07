namespace Library.Core.Contracts;

public sealed record BranchInfo(int Id, string Code, string Name);

/// <summary>
/// Which branch (and desk) the current request is working for.
/// Staff: chosen after login and stored in the session. Kiosk: fixed by configuration (Kiosk:BranchCode).
/// Public visitors have no current branch.
/// </summary>
public interface IBranchContext
{
    /// <summary>The branch this request works for, or null if none has been chosen.</summary>
    BranchInfo? CurrentBranch { get; }

    /// <summary>The reception desk, or null (kiosks and public pages have no desk).</summary>
    int? CurrentDeskId { get; }

    /// <summary>True when the request comes from a kiosk station.</summary>
    bool IsKiosk { get; }
}
