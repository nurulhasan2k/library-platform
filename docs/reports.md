# Manager reports

After A's login is merged, sign in as Manager or Admin and open `/Reports`.
Reception and anonymous users cannot open reports or download exports.

The dashboard links to borrowing statistics, the fine revenue audit and inventory
health. Date ranges include both dates. Inventory is a snapshot of `IClock.Today`
and uses branch and idle days instead of a date range. Export buttons use the
displayed filters. Empty reports disable exports.

The current fallback source contains the fixed August–October 2026 sample data.
The screen labels it as demonstration data. Branch choices come from
`ICatalogueQuery.GetBranchesAsync`; the catalogue stub has no branches.
Register the real `IReportDataSource` before `AddLibraryReportScreens()` to
replace the fallback. No authentication scheme is registered by the reports.

For A: in `Views/Shared/_Layout.cshtml`, inside the existing header navigation,
add a link to `/Reports` labelled "Reports", visible only when
`User.IsInRole("Manager") || User.IsInRole("Admin")`. The layout is unchanged here.
