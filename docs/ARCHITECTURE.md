# Architecture

## Solution layout

| Project | Purpose |
|---|---|
| `src/Library.Core` | All business logic, no user interface: models, services, importer, report builders, CSV and PDF writers, settings classes, and the shared service contracts (`Contracts/`). Targets .NET 10 and references the ASP.NET Core shared framework only for hosting types such as `IOptions` and cookie helpers. |
| `src/Library.Web` | The ASP.NET Core MVC application: controllers, Razor views, the public API, middleware, static files, start-up. |
| `tests/Library.Tests` | Console test runner (117 tests) that exercises the real services against a temporary data file. Run with `dotnet run --project tests/Library.Tests`. |
| `tests/e2e` | Optional Playwright script that walks the 12 demo items in a real browser. |

Rules of the road: controllers are thin; rules live in services; views use view models (never entities); dates come from `IClock`;
every POST has an anti-forgery token (global `AutoValidateAntiforgeryToken`); every staff controller carries `[Authorize(Roles = ...)]`.

```
Browser / kiosk / API client
        |
Library.Web   Controllers (MVC + /api/v1) -> view models -> Razor views
        |       Middleware: ApiMiddleware (key, read-only, JSON errors), rate limiter, CORS, session, auth schemes
        |
Library.Core  Services (lending, inventory, transfers, reservations, notifications, import, reports)
        |       Contracts: IClock, IBranchContext, IPatronAuthService, ILendingService, ITransferService,
        |                  INotificationService, IReservationService, ICatalogueQuery
        |
ILibraryStore (unit of work: Read / Write)  ->  JsonLibraryStore  ->  data/library.json
```

## Roles and access

| Area / action | Public | Patron (card + PIN) | Reception | Admin | Manager |
|---|---|---|---|---|---|
| Search catalogue, item details, branch list and hours | Yes | Yes | Yes | Yes | Yes |
| Public read-only API (API key) | Yes (key) | Yes (key) | Yes | Yes | Yes |
| Kiosk: check out, account summary, holds | Start screen only | Yes | n/a | n/a | n/a |
| Web account: summary, place / cancel own hold | No | Yes | n/a | n/a | n/a |
| Borrow / return at a desk, borrower CRUD, fines paid | No | No | Yes | Yes | No |
| Place / cancel a hold for a patron | No | own only | Yes | Yes | No |
| Item CRUD and status, branch and desk admin, categories, staff users | No | No | No | Yes | No |
| Request / dispatch / receive / cancel transfers | No | No | Own branches | Yes | View only |
| Importer (CSV and mock provider) | No | No | No | Yes | No |
| Notification page | No | No | View | Run checks, simulated date | View |
| Analytics dashboard, reports, CSV/PDF export | No | No | No | Yes | Yes |

Three cookie schemes keep the sign-ins apart: `Staff` (staff roles), `Patron` (web account) and `Kiosk` (short sliding idle timeout).
Staff with several branches pick a branch and desk after signing in; the choice is held in the session and read through `IBranchContext`.

## The shared services

| Contract | What it does | Implementation |
|---|---|---|
| `IClock` | The one source of "today" (supports the admin simulated date). | `SimulatedClock` |
| `IBranchContext` | Current branch and desk for a staff session; fixed branch for a kiosk. | `WebBranchContext` (web) |
| `IPatronAuthService` | Card number + PIN sign-in with lock-out. | `PatronAuthService` |
| `ILendingService` | `Borrow` and `Return`; the only place lending rules and fines live. | `LendingService` |
| `ITransferService` | Request, dispatch, receive, cancel; in-transit check. | `TransferService` |
| `ICatalogueQuery` | Read-only item, category and branch queries; never returns patron, loan or fine data. | `CatalogueQuery` (used by the public pages and the API) |
| `INotificationService` | `Record` an event notification; `RunScheduledChecks(asOf)`. | `NotificationService` |
| `IReservationService` | Place/cancel holds, queue, `CheckBorrowAllowed`, `OnBorrowed`, `OnItemBecameAvailable`. | `ReservationService` |

Other services without a contract because only one place uses them: `InventoryService` (items, categories, branches),
`BorrowerService`, `StaffService`, `LoanQueryService`, `ImportService`, `ReportService`, `SeedRunner`.

### How the seven features connect

- **Kiosk -> lending -> reservations/notifications.** The kiosk calls `ILendingService.Borrow` with channel *Kiosk*; lending asks reservations whether
  the item may be lent, then records a *Borrowed* notification.
- **Return -> reservations -> notifications.** A return offers the item to the first waiting patron, who gets a *Ready* notification.
- **Transfers <-> lending / catalogue / reports.** An item with an active transfer cannot be lent; the catalogue (and API) show it as in transit; reports count transfers.
- **Importer -> catalogue.** Imported items appear in the public search and API immediately.
- **Reports -> everything, read only.** `ReportService` builds one `ReportDocument` that the dashboard, the CSV writer and the PDF writer all render, so the numbers always agree.

### The hook rule

- Lending calls reservations **before** lending, and `OnBorrowed` plus a notification **after**. Returns call `OnItemBecameAvailable` (not when damaged).
- Hooks never make a borrow or return fail: errors are logged and the main action stands.

## Data

See [`ER-DIAGRAM.md`](ER-DIAGRAM.md). `JsonLibraryStore` keeps the whole data set in one JSON file. Every `Read` loads the current file; every `Write` takes a cross-process
file lock, loads the file, applies the change, writes a temporary file and replaces the original atomically, so a crash cannot leave half a file and two
requests (or two running copies of the site) cannot overwrite each other's changes.

## Time

Business code never uses `DateTime.Now`. It injects `IClock`. The admin "simulated today" setting makes due-soon and fine notifications demonstrable.

## Errors

Rule refusals (item already borrowed, held for another patron, in transit, unknown card, ...) are `LibraryException`s with a plain-sentence message that the
controllers show to the user; unexpected errors show a friendly error page without a stack trace. The API returns JSON errors with a code and message.

## Security notes

Anti-forgery tokens on every POST; PBKDF2 for passwords and PINs; PIN lock-out; role checks on every staff controller; the API is read-only, key-protected and rate
limited; CSV exports neutralise formula characters; uploaded import files are size-limited and never executed; public pages never show patron data.
