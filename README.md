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

## 8. Access the kiosk *(B, to do)*

## 9. Test the public API, with example requests *(C, to do)*

## 10. Test notifications *(B, to do)*

## 11. Test reservations and waitlists *(B, to do)*

## 12. Perform an import *(C, to do)*

## 13. Generate and export reports *(C, to do)*

## 14. Known limitations and assumptions *(A compiles; everyone contributes)*

- "Branch" means a physical library branch inside the application.
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
