# Library Platform

A multi-branch library platform built on **.NET 10 ASP.NET Core MVC** for the *Distributed Development* group project.
It extends the Assignment 2 library system (items, borrowing, returns, fines, borrowers, staff roles, public search) with:

1. a **self-service kiosk**,
2. a **public read-only REST API**,
3. **simulated email/SMS notifications**,
4. **multi-branch inventory** with inter-branch transfers,
5. **reservations and waitlists**,
6. a **data importer** (CSV and a mock external provider), and
7. **manager analytics** with CSV and PDF export.

New to the project? Start with [`docs/INSTALLATION.md`](docs/INSTALLATION.md) (step-by-step setup on a clean machine) and
[`docs/USER-GUIDE.md`](docs/USER-GUIDE.md). The 12 client demo items are mapped to exact steps in
[`docs/DEMO-WALKTHROUGH.md`](docs/DEMO-WALKTHROUGH.md).

## Contents

1. [Project overview and technology stack](#1-project-overview-and-technology-stack)
2. [Prerequisites](#2-prerequisites)
3. [Get the code](#3-get-the-code)
4. [Configuration](#4-configuration)
5. [Database setup](#5-database-setup)
6. [Seed or reset the multi-branch demo data](#6-seed-or-reset-the-multi-branch-demo-data)
7. [Run the application](#7-run-the-application)
8. [Access the kiosk](#8-access-the-kiosk)
9. [Test the public API](#9-test-the-public-api-with-example-requests)
10. [Test notifications](#10-test-notifications)
11. [Test reservations and waitlists](#11-test-reservations-and-waitlists)
12. [Perform an import](#12-perform-an-import)
13. [Generate and export reports](#13-generate-and-export-reports)
14. [Known limitations and assumptions](#14-known-limitations-and-assumptions)

## 1. Project overview and technology stack

| Part | Choice |
|---|---|
| Platform | .NET 10, ASP.NET Core MVC with Razor views |
| Structure | `Library.Core` (business logic, no UI) and `Library.Web` (controllers, views, API) |
| Data | One JSON data file (`data/library.json`) behind a unit-of-work interface (`ILibraryStore`); safe for several requests at once (file lock, atomic replace) |
| Sign-in | Cookie authentication: staff roles **Admin / Reception / Manager**, patrons by **card number + 4-digit PIN**, kiosk sessions with a short idle timeout |
| Passwords and PINs | PBKDF2 hashes; patron lock-out after repeated wrong PINs |
| API | `/api/v1/...` read-only JSON, API-key header, rate limit, OpenAPI 3 document and an interactive docs page |
| Exports | CSV and PDF are written by the project's own code |
| Tests | 117 automated tests plus an end-to-end browser walk-through of the 12 demo items |
| Packages | **None.** The solution uses only what ships with the .NET 10 SDK, so it restores and builds without any NuGet download. |

Why a JSON file instead of a database server or SQLite, and why no third-party packages, is explained in
[`docs/DECISIONS.md`](docs/DECISIONS.md).

## 2. Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Check with `dotnet --version` (should start with `10.`).
- Git.
- A web browser. Chrome, Edge, Firefox or Safari. Nothing else: no database server, no Node.js, no extra tools.

Works on Windows, macOS and Linux.

## 3. Get the code

```bash
git clone https://github.com/nurulhasan2k/library-platform.git
cd library-platform
git checkout main        # the branch to use for assessment
```

## 4. Configuration

All settings are in `src/Library.Web/appsettings.json` and listed with their meanings in
[`docs/CONFIGURATION.md`](docs/CONFIGURATION.md). The defaults are the demo settings, so **nothing needs to be changed to run the demo**.

The API key for the demo is `demo-api-key-2026`. It is a demo value; in real use set your own with
`dotnet user-secrets set "Api:Key" "..."` or the `Api__Key` environment variable.

## 5. Database setup

There is no installation step. On the first start the application creates `data/library.json` next to the solution file.
No migrations or connection strings are needed. To store the data somewhere else set `Database:DataFile` or the environment
variable `LIBRARY_DATA_FILE`.

## 6. Seed or reset the multi-branch demo data

An empty data file is filled automatically on first start (`Database:SeedOnStartup` is `true`) with 3 branches, 9 categories,
45 items, 10 patrons, about 160 loans, transfers, reservations and notifications. The exact contents are in
[`docs/SEED-SCENARIOS.md`](docs/SEED-SCENARIOS.md).

To wipe everything and recreate the demo data (stop the site first):

```bash
dotnet run --project src/Library.Web -- --reset-db --exit      # or scripts/reset-db.sh / scripts\reset-db.ps1
```

Seeded dates are relative to today, so overdue items and due-soon items are always there when you start.

Demo sign-ins:

| Who | Where | Username / card | Password / PIN |
|---|---|---|---|
| Admin | `/staff/login` | `admin` | `admin123` |
| Reception (Central + Northside) | `/staff/login` | `reception` | `reception123` |
| Reception (Riverside only) | `/staff/login` | `reception.river` | `reception123` |
| Manager | `/staff/login` | `manager` | `manager123` |
| Patron Ava Thompson | `/account` or kiosk | `LIB-1001` | `1111` |
| Patron Daniel Okafor (overdue) | `/account` or kiosk | `LIB-1004` | `4444` |
| Patron Emma Rossi (5 items out) | `/account` or kiosk | `LIB-1005` | `5555` |

All patrons are `LIB-1001` to `LIB-1010` with PINs `1111`, `2222`, ... `9999`, `0000` (see the seed document).

## 7. Run the application

```bash
dotnet run --project src/Library.Web        # or scripts/run.sh / scripts\run.ps1
```

Open <http://localhost:5080>. Stop with `Ctrl+C`.

## 8. Access the kiosk

Open <http://localhost:5080/kiosk> (it is a separate, large-button screen; no staff sign-in needed). Each kiosk belongs to one
branch, set by `Kiosk:BranchCode` (default `CENTRAL`).

1. Type a card number and PIN (for example `LIB-1001` / `1111`) and press **Sign in**.
2. The account page shows items on loan with due dates, fines, holds and a link to check out.
3. **Check out**: type or scan a library code such as `BK-001` and press Enter. Several items can be checked out in one visit.
   A destroyed item or an item held for someone else is refused with a plain message.
4. **Done** shows a receipt. **Sign out** returns to the welcome screen. After 60 idle seconds the kiosk signs the patron out by itself.
5. Five wrong PINs lock that card for five minutes.

## 9. Test the public API, with example requests

Every request needs the header `X-Api-Key: demo-api-key-2026`. The API is read-only (anything other than GET, HEAD or OPTIONS returns 405).
It never returns borrower, loan or fine data, and destroyed items are never listed.

```bash
curl -H "X-Api-Key: demo-api-key-2026" "http://localhost:5080/api/v1/items?q=dune&type=book&availableOnly=true"
curl -H "X-Api-Key: demo-api-key-2026" "http://localhost:5080/api/v1/items/BK-001"
curl -H "X-Api-Key: demo-api-key-2026" "http://localhost:5080/api/v1/categories"
curl -H "X-Api-Key: demo-api-key-2026" "http://localhost:5080/api/v1/branches"
curl -H "X-Api-Key: demo-api-key-2026" "http://localhost:5080/api/v1/branches/CENTRAL"
curl -H "X-Api-Key: demo-api-key-2026" "http://localhost:5080/api/v1/status"
```

| Endpoint | Filters |
|---|---|
| `GET /api/v1/items` | `q`, `type` (book, music, toy), `categoryId`, `branch` (code), `availableOnly`, `page`, `pageSize` |
| `GET /api/v1/items/{code}` | |
| `GET /api/v1/categories` | |
| `GET /api/v1/branches`, `/branches/{code}` | opening hours and whether the branch is open now |
| `GET /api/v1/status` | health check |

Errors are JSON: `401` missing key, `403` wrong key, `404` unknown item or branch, `400` bad parameter, `405` write attempt,
`429` too many requests (more than 60 a minute per key; `Retry-After` header).

Interactive documentation: <http://localhost:5080/api/docs>. Machine-readable OpenAPI 3: <http://localhost:5080/api/openapi.json>.
More examples: [`scripts/api-examples.sh`](scripts/api-examples.sh) and [`scripts/api-examples.http`](scripts/api-examples.http).

## 10. Test notifications

Notifications are **simulated**: each one is stored (and shown on the **Notifications** page for staff) and printed to the console as
`[SIMULATED EMAIL]` or `[SIMULATED SMS]`. Each patron has a preferred channel (email, SMS or both; "both" creates two records;
SMS text is kept to 160 characters).

1. Sign in as `reception` / `reception123`, choose Central Library, open **Desk** and lend `MU-001` to card `LIB-1001`.
   A *Borrowed* notification appears at once.
2. Sign in as `admin`. On **Notifications** press **Run checks now**: due-soon notices (items due in 2 days) and daily fine-accruing
   notices for overdue items are created. Running it twice on the same day does not create duplicates.
3. Change **Simulated today** to a later date (or **+1 day**) and run again to see more notices without waiting.
   Set it back with **Use the real date**.
4. A background job runs the same checks once a day (`Notifications:EnableScheduler`).

## 11. Test reservations and waitlists

Holds are placed on one item (one library code = one physical copy).

1. As patron `LIB-1001` open **My account** (or the kiosk) and place a hold on a borrowed item, for example `BK-010` (it has a queue already).
2. Return the item at the desk. The first person in the queue becomes **Ready**, is notified, and keeps the hold for 3 days
   (`Reservations:ReadyHoldDays`). Nobody else can borrow the item meanwhile.
3. If the hold is not collected in time, the next run of the checks expires it and promotes the next person.
4. Staff can place or cancel holds for a patron under **Holds**.
5. A damaged item that is repaired (admin sets it back to *available*) also serves the queue.

## 12. Perform an import

Sign in as `admin`, open **Import**.

- **CSV**: choose a file, a default branch and press **Preview**. Valid files from [`samples/import/`](samples/import/):
  `valid-river-20-items.csv` (20 ready rows), `bad-rows.csv` (every validation rule; 2 ready, 15 skipped with reasons),
  `larger-150-items.csv`. Press **Import valid rows** to commit; skipped rows are listed and can be downloaded as a CSV to fix.
- **External provider (mock)**: press **Preview** under the provider box: 12 records, 2 deliberately invalid.
- A template with the expected columns can be downloaded from the page. History of every import is kept on the same page.

## 13. Generate and export reports

Sign in as `manager` / `manager123` (or `admin`), open **Reports**.

- The dashboard shows loans per month, by type and by branch plus key figures, filtered by date range and branch.
- Three full reports: **Borrowing statistics**, **Fine revenue** (charged, collected, outstanding) and **Inventory health**
  (stock by branch, idle items, damaged items, transfers).
- Every report has **CSV** and **PDF** buttons (CSV opens in Excel; PDFs are paginated with page numbers).

## 14. Known limitations and assumptions

- "Branch" means a physical library branch inside the application (confirmed by the team; not a Git branch).
- Holds are per physical copy, not per title.
- Email and SMS are simulated; nothing is sent. The kiosk takes a typed code; a barcode scanner that types the code and presses Enter also works.
- One JSON data file is used instead of a database server. It is designed for a demo-sized library (thousands of records), not a very large one.
- The API key is a shared demo key stored in `appsettings.json`; there is no per-client key management or revocation.
- Time-based notifications are generated when the checks run (daily job, or the admin button), not on the exact minute.
- A single installation serves all branches; kiosks are separate browser windows pointed at `/kiosk` with the branch set in configuration.
- Fines are for display and reporting; there is no payment gateway. Reception records a fine as paid.
- Tested with the .NET 10 SDK on Linux in a clean environment and automated browser tests; please see
  [`docs/INSTALLATION.md`](docs/INSTALLATION.md) for the exact VM steps.

## Project layout

```
LibraryPlatform.sln
src/Library.Core/   Business logic: models, services, import, reports, PDF writer, shared contracts (Contracts/)
src/Library.Web/    ASP.NET Core MVC app: controllers, views, API, static files
tests/Library.Tests Automated tests (run: dotnet run --project tests/Library.Tests)
tests/e2e/          Browser walk-through of the 12 demo items (Python + Playwright, optional)
samples/import/     Sample CSV files for the importer
scripts/            Run, reset, test and API example scripts
docs/               Installation, user guide, architecture, decisions, data model, seed data, configuration, demo walkthrough
docs/submission/    Project Plan and Group Portfolio (Markdown sources and PDFs)
data/               Created at run time (git-ignored)
```

## Working on the project

See [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).
