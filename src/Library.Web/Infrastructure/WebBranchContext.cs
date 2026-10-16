using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Data;
using Microsoft.Extensions.Options;

namespace Library.Web.Infrastructure;

/// <summary>
/// Which branch (and desk) this request works for. Staff choose a branch after signing in (kept in the session);
/// a kiosk is fixed to Kiosk:BranchCode; public visitors have none.
/// </summary>
public sealed class WebBranchContext(IHttpContextAccessor http, ILibraryStore store, IOptions<KioskOptions> kiosk) : IBranchContext
{
    public const string BranchKey = "staff.branchId";
    public const string DeskKey = "staff.deskId";

    private HttpContext? Ctx => http.HttpContext;

    public bool IsKiosk => Ctx?.Request.Path.StartsWithSegments("/kiosk") == true;

    public BranchInfo? CurrentBranch
    {
        get
        {
            if (IsKiosk)
                return store.Read(d => d.Branches.Where(b => b.IsActive && string.Equals(b.Code, kiosk.Value.BranchCode, StringComparison.OrdinalIgnoreCase))
                    .Select(b => new BranchInfo(b.Id, b.Code, b.Name)).FirstOrDefault());

            var id = Ctx?.Session.GetInt32(BranchKey);
            return id is null ? null : store.Read(d => d.Branches.Where(b => b.Id == id && b.IsActive).Select(b => new BranchInfo(b.Id, b.Code, b.Name)).FirstOrDefault());
        }
    }

    public int? CurrentDeskId => IsKiosk ? null : Ctx?.Session.GetInt32(DeskKey);

    public string? CurrentDeskName
    {
        get
        {
            var desk = CurrentDeskId;
            var branch = CurrentBranch;
            return desk is null || branch is null ? null : store.Read(d => d.FindBranch(branch.Id)?.Desks.FirstOrDefault(x => x.Id == desk)?.Name);
        }
    }
}
