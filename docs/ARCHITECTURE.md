# Architecture and shared contract

This document is the **contract** the three of us code against. It is proposed for Meeting 1 and frozen after it.
Changes after Meeting 1 are agreed in the team chat and recorded in the next minutes.

## Solution layout

| Project | Purpose |
|---|---|
| `src/Library.Core` | Business logic with no user interface: shared service contracts (`Contracts/`), safe stubs (`Stubs/`), settings classes (`Configuration/`). EF Core entities, `DbContext` and the real services are added here from Meeting 2. |
| `src/Library.Web` | The ASP.NET Core MVC application: controllers, views, static files, startup. Areas for the kiosk and staff screens are added as each feature lands. |
| `tests/` | Automated tests (from Meeting 2). |

Rules of the road: controllers are thin; rules live in services; views use view models, never entities; dates come from `IClock`.

## Roles and access

| Area / action | Public | Patron (card + PIN) | Reception | Admin | Manager |
|---|---|---|---|---|---|
| Search catalogue, item details, branch list and hours | Yes | Yes | Yes | Yes | Yes |
| Public read-only API (API key) | Yes (key) | Yes (key) | Yes | Yes | Yes |
| Kiosk: check out, account summary | Start screen only | Yes | n/a | n/a | n/a |
| Place / cancel own hold | No | Yes | On behalf of patron | Yes | No |
| Borrow / return at a desk, borrower CRUD | No | No | Yes | Yes | No |
| Item CRUD, branch and desk admin | No | No | No | Yes | No |
| Request / dispatch / receive transfers | No | No | Own branch | All branches | View only |
| Importer (CSV and mock provider) | No | No | No | Yes | No |
| Notification dashboard | No | No | Own branch | Yes | View |
| Analytics dashboard and CSV/PDF export | No | No | No | View | Yes |
| Reset/reseed demo data | No | No | No | Dev only | No |

Every staff controller carries `[Authorize(Roles = "...")]`. The roles are `Admin`, `Reception`, `Manager` (the Assignment 2 roles).
Patrons are not staff users: they identify themselves with a card number and PIN through `IPatronAuthService`.

## The shared services

All of these are interfaces in `Library.Core.Contracts`. Each has a **stub** in `Library.Core.Stubs` so the solution compiles
and runs from day one. The developer page `/dev/contracts` (Development only) shows which are still stubs.

| Contract | What it does | Built by | Called by |
|---|---|---|---|
| `IClock` | The one source of "today" (supports the admin simulated date). | A registers `SystemClock`; B replaces it | Lending, fines, notifications, reports |
| `IBranchContext` | Current branch and desk for a staff session; fixed branch for a kiosk. | A | Everyone |
| `IPatronAuthService` | Card number + PIN sign-in with lockout. | A | B's kiosk and holds screens |
| `ILendingService` | `Borrow` and `Return`. The only place lending rules live. | A | Desk screens (A), kiosk (B) |
| `ITransferService` | Request, dispatch, receive, cancel; inbound/outbound lists; in-transit check. | A | A's screens; C's reports |
| `ICatalogueQuery` | Read-only item, category and branch queries, never with patron/loan/fine data. | A (C extends) | Public pages, API, importer |
| `INotificationService` | `Record` an event notification; `RunScheduledChecks(asOf)`. | B | A's lending hooks; B's scheduler and reservations |
| `IReservationService` | Place/cancel holds, queue, `CheckBorrowAllowed`, `OnBorrowed`, `OnItemBecameAvailable`. | B | A's lending and status-change hooks |

Report services (`IReportService` and the CSV/PDF exporters) are Person C's and are added by C inside `Library.Core`; they only read data.

### The hook rule

- A's lending and status-change code **calls** B's `INotificationService` and `IReservationService`. Nobody else sends notifications or edits a queue directly.
- Lending asks `IReservationService.CheckBorrowAllowed` **before** lending: an item held (Ready) for one patron cannot be borrowed by someone else.
- Hooks are called from: borrow (`OnBorrowed`, then `Record(Borrowed)`), return, an admin changing a damaged item back to available
  (`OnItemBecameAvailable`), and a hold being fulfilled.
- Hooks must never make a borrow or return fail because a notification failed: log and carry on.

### Replacing a stub

1. Write your real class in `Library.Core` (or `Library.Web` if it needs web types) implementing the interface.
2. In `src/Library.Web/Program.cs`, register it **before** `AddLibraryStubs()`:

   ```csharp
   builder.Services.AddScoped<INotificationService, NotificationService>();
   builder.Services.AddLibraryStubs();   // keeps only the stubs that are still missing
   ```
3. Open `/dev/contracts` and check it now says "Real".
4. Do not change an interface signature without telling the team. Adding a method is fine; changing or removing one breaks others.

## Data and migrations

EF Core with SQLite (decision 1). See `docs/ER-DIAGRAM.md`. Person A owns the migration order; see `CONTRIBUTING.md`.

## Time

Never use `DateTime.Now` / `DateTime.Today` in business code. Inject `IClock`. The admin "simulated today" setting
(decision 10) makes due-soon and fine notifications demonstrable without waiting days.

## Errors

Services return result objects with plain-sentence error messages (see `BorrowResult`, `ReservationResult`, `TransferResult`).
Controllers show them to the user. Exceptions are for bugs, not for rule refusals.
