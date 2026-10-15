# Library Platform (Distributed Development group project)

A multi-branch library platform built on **.NET 10 ASP.NET Core MVC**. It expands the Assignment 2 library system
(items, borrowing, returns, fines, borrowers, staff roles, public search) with a self-service kiosk, a public
read-only REST API, simulated notifications, multi-branch inventory and transfers, reservations and waitlists,
a data importer, and manager analytics with CSV and PDF export.

> **Status: foundation in progress.** The solution skeleton, shared service contracts and stubs are in place.
> Sections marked *(to do)* are filled in as each part is delivered. Owners in brackets: A, B or C.

## 1. Project overview and technology stack *(A, to do)*

## 2. Prerequisites *(A)*

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`dotnet --version` should print `10.x`)
- Git
- Nothing else for now. The database will be a local SQLite file; no database server is needed.

## 3. Get the code *(A)*

```bash
git clone https://github.com/nurulhasan2k/library-platform.git
cd LibraryPlatform
git checkout main     # the branch to use for assessment
```

## 4. Configuration *(A; B and C add their keys)*

Settings live in `src/Library.Web/appsettings.json` and are listed, with defaults and meanings, in
[`docs/CONFIGURATION.md`](docs/CONFIGURATION.md). Secrets (such as the API key) are set with
`dotnet user-secrets`; see that document.

## 5. Database setup and migrations *(A, to do)*

## 6. Seed or reset the multi-branch demo data *(A, to do)*

Planned data is described in [`docs/SEED-SCENARIOS.md`](docs/SEED-SCENARIOS.md).

## 7. Run the application *(A)*

```bash
dotnet run --project src/Library.Web
```

Open <http://localhost:5080>. In Development, <http://localhost:5080/dev/contracts> shows which shared
services are still stubs.

## 8. Access the kiosk *(B)*

Run the app (section 7), then open <http://localhost:5080/kiosk>. The kiosk has its own large, touch-friendly layout
and no staff navigation. Each kiosk station is tied to one branch with `Kiosk:BranchCode` (default `CENTRAL`); the
session ends on Finish, after `Kiosk:IdleTimeoutSeconds` (default 60) of no touch, or after the receipt is shown.

Demo patrons (card number / PIN `1234` for everyone): `1001` Alice (Email), `1002` Bob (SMS), `1003` Carol (Both),
`1004` Dan (has two loans), `1005` Erin (one overdue loan). Demo item codes: `LIB-0001` to `LIB-0006`
(`LIB-0004` and `LIB-0005` are available to borrow).

> Until Person A's real lending and sign-in services merge, the kiosk uses temporary in-memory demo versions
> (`StubInMemoryLendingService`, `StubInMemoryPatronAuthService`; they show as "Stub" on /dev/contracts). They still call the real reservation and notification hooks.
> Remove `AddLibraryInMemoryLendingStubs()` from `Program.cs` when A's services are registered.

## 9. Test the public API, with example requests *(C, to do)*

## 10. Test notifications *(B)*

1. Open <http://localhost:5080/notifications> (the dashboard). Notifications are simulated: they are stored here and
   written to the console log. Nothing is really sent.
2. Borrow an item at the kiosk or on <http://localhost:5080/holds> ("Borrow at desk"). A `Borrowed` notification appears
   in the patron's preferred channel (Both creates two records).
3. Press **Set simulated today** to move the date forward (for example 3 days), then **Run checks now**. `DueSoon` and
   `FineAccruing` notifications appear. Press **Run checks now** again: no duplicates are created.
4. Use **Use real date** to go back and **Clear demo notifications** to start a clean demo.

Settings: `Notifications:DueSoonDays` (default 2), `Notifications:EnableScheduler` (default true, runs the same checks daily).

## 11. Test reservations and waitlists *(B)*

Open <http://localhost:5080/holds>. Seeded: **Dune** is borrowed by Dan and Alice, Bob and Carol wait in that order.

1. Click **Return item** on Dune. Alice becomes **Ready** (a `ReservationReady` notification is created) and Bob moves up.
2. Try **Borrow at desk** for Bob on Dune: refused ("held for another patron"). Alice can borrow it, which fulfils her hold.
3. Try to place a hold on an available item, or twice on the same item: a clear message is shown.
4. Set simulated today 3 days ahead and press **Run checks now** on the notifications page: a Ready hold expires,
   `ReservationExpired` is created and the next person is promoted.

Patrons can also place and cancel their own holds at <http://localhost:5080/my-holds> (sign in with a demo card and PIN `1234`, type a library code such as `LIB-0002`).

Settings: `Reservations:MaxActiveHoldsPerPatron` (default 5), `Reservations:ReadyHoldDays` (default 3).
Holds are per item (each library code is one physical copy); title-level holds are a known limitation.

Unit tests for these rules: `dotnet test tests/Library.Lending.Tests`.

More from Person B: [User Guide](docs/USER-GUIDE.md), [journeys and diagrams](docs/JOURNEYS-AND-DIAGRAMS.md), [VM test checklist](docs/VM-TEST-CHECKLIST.md), [Project Plan](docs/PROJECT-PLAN.md).

## 12. Perform an import *(C, to do)*

## 13. Generate and export reports *(C, to do)*

## 14. Known limitations and assumptions *(A compiles; everyone contributes)*

- "Branch" means a physical library branch inside the application.
- Holds are per item (each library code is one physical copy). Title-level holds across copies are not supported.
- Notifications (email and SMS) are simulated: they are stored for the dashboard and logged, never sent.
- Until the database is merged, Person B's data (holds, notifications, simulated date) is kept in memory and resets when the app restarts.
- The kiosk checkout borrows each scanned item immediately and shows all items on the receipt.
- Staff pages for notifications and holds have no role checks yet (marked with TODO in the code). Add `[Authorize(Roles = ...)]` when A's login merges.
- There is no "Place hold" button on the item page yet. That page belongs to A. Patrons use /my-holds.
- Seed data has a queue of three patrons but no expired or cancelled hold. These can be created in the demo with the buttons.
- *(more to come)*

## Project layout

```
LibraryPlatform.sln
src/
  Library.Core/   Business logic, shared service contracts (Contracts/), stubs (Stubs/), settings classes
  Library.Web/    ASP.NET Core MVC application (controllers, views, static files)
tests/            Automated tests (added from Meeting 2)
docs/             Architecture, decisions, ER diagram, seed scenarios, configuration
scripts/          Reset and helper scripts (added by Person A)
```

## Working on the project

See [`CONTRIBUTING.md`](CONTRIBUTING.md) for branch names, pull request reviews, migrations and standards, and
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the shared service contract everyone codes against.
