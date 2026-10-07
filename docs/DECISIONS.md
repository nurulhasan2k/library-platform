# Decisions to confirm at Meeting 1

Each row is a proposal. At the meeting, mark it **Agreed** or **Changed** and record it in the minutes (numbered D-M1-01 and so on).
Status and date columns are filled in at the meeting.

| # | Decision | Proposed default | Why | Alternative if the team disagrees | Status |
|---|---|---|---|---|---|
| 1 | Database | EF Core with SQLite, one file, migrations committed. The Assignment 2 JSON file is replaced. | Nothing to install on the VM; the marker can reset by deleting one file. | SQL Server LocalDB (needs Windows). | |
| 2 | Authentication | ASP.NET Core cookie authentication; roles Admin, Reception, Manager (as Assignment 2). | Keeps the baseline roles; no extra packages. | ASP.NET Core Identity (more setup, adds user management we do not need). | |
| 3 | Patron identity | Library card number + 4-digit PIN (hashed, lockout after 5 wrong tries). | The kiosk account summary and holds need to know who the patron is without exposing patron data. | Card number only (less secure); full patron accounts with passwords (heavier). | |
| 4 | Reservation granularity | Per item: one library code is one physical copy. | Matches how Assignment 2 identifies items. | Per title across copies (needs a Title entity; larger change). Documented as a limitation. | |
| 5 | Where an item "is" | `Item.BranchId` is where the copy physically is. Lending only at that branch. A return elsewhere moves it. Transfers move items deliberately. | Makes the multi-branch inventory real and simple to demonstrate. | Allow lending anywhere and track home branch (more states). | |
| 6 | In transit | Derived from an active Transfer (Requested or InTransit). Such an item cannot be borrowed or have its status changed. | One source of truth. | A status value on the item. | |
| 7 | Categories | A seeded Category entity per item type (Sci-Fi, Programming, Rock, Puzzle...), used by search, API and importer. | The API needs a categories endpoint; the importer needs a lookup. | Free-text categories. | |
| 8 | Fines | Keep $0.50 per full late day and the 14-day loan, moved to settings. Add FinePaid / FinePaidOn on Loan. | "Fine revenue" needs charged vs collected vs outstanding. | Charged only. | |
| 9 | Notification channels | Email and SMS are simulated. Each patron has a preferred channel; "Both" creates two records. Stored in a table (dashboard) and logged to the console. | Meets "dashboard or console simulator". | Console only. | |
| 10 | Time simulation | An admin-only "simulated today" setting plus a "Run checks now" button. A daily background job runs the same code. | Due-soon and fine notifications can be demonstrated in minutes. | Real clock only. | |
| 11 | CSV / PDF libraries | CsvHelper for CSV; QuestPDF (Community licence) or PDFsharp for PDF. C picks after a spike and tells the team. | Proven libraries; less code to maintain. | Hand-written CSV; another PDF library. | |
| 12 | API documentation | OpenAPI document with Swagger UI, plus curl and `.http` examples in the repo. | "Documented and easy to test." | Markdown only. | |
| 13 | API protection | GET/HEAD/OPTIONS only (anything else returns 405), API key header, built-in rate limiting, DTOs only. | Read-only and not abusable; no sensitive data. | No key (less secure). | |
| 14 | UI | One shared layout and CSS variables for staff and public pages; the kiosk has its own simplified layout using the same colours and fonts. | "Consistent visual design" with a kiosk that is intentionally simpler. | One layout everywhere. | |

## Two additions to the contract (confirm at the meeting)

| # | Addition | Why |
|---|---|---|
| 15 | `IClock`: one source of "today". A registers the real clock; B replaces it with the simulated-date clock. | Decision 10 only works if lending, fines and notifications all read the same date. |
| 16 | `IPatronAuthService`, `ILendingService` and `ITransferService` are published as stubs on day 1, not only the four in the original plan. | B's kiosk needs lending and patron sign-in to compile against from the start. |

## Questions for the client

Record who sends them and the answer in the minutes.

1. In the install instructions, "select the relevant location branch" - does "branch" mean a **library branch** (our assumption) or a **Git branch**?
2. Which Git branch should the marker use (we propose `main`)?
3. Does the provided VM already have the **.NET 10 SDK**? Is internet access available to restore NuGet packages?
4. Is a simulated email/SMS dashboard acceptable for notifications, or should a console simulator also be shown?
5. Is an API key enough for "secure" on the public API, or is something else expected?
6. Should kiosks work with a physical barcode scanner, or is typing a library code acceptable for the demo?
7. Are holds expected per physical copy (our assumption) or per title?

## Meeting 1 checklist for Person A (chair)

- [ ] Walk through sections 3 and 4 of the work briefs (roles, data model, services, chains).
- [ ] Walk through this decisions table; record each outcome.
- [ ] Show the repo: branch rules, PR template, solution layout, `/dev/contracts` page.
- [ ] Show the ER diagram and the seed scenarios; collect B's and C's changes.
- [ ] Confirm PM site, marker access, chat channel and the twice-weekly written check-in.
- [ ] Set dates for Meetings 2 and 3; confirm the chair / minutes rotation.
- [ ] Agree who sends the client questions.
