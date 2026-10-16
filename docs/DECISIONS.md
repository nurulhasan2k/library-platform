# Design decisions

Each decision records what we chose, why, and what we gave up. Decisions D1-D14 were the proposals for the kickoff; the status column
shows what was actually built. Where the build differs from the first proposal the reason is stated.

| # | Decision | What was built | Why | Trade-off / alternative |
|---|---|---|---|---|
| D1 | Data storage | **One JSON file behind `ILibraryStore`** (unit of work: `Read` / `Write`), with a lock file between processes and atomic replace on save. *Changed from EF Core + SQLite.* | The Assignment 2 baseline already used a JSON file, nothing needs installing on the marker's VM, resetting is deleting one file, and the build needs no NuGet download (the package feed was unreachable while the app was being built). | Not suitable for very large libraries. Because all data access goes through `ILibraryStore`, swapping in EF Core/SQLite later changes one class, not the services. |
| D2 | Authentication | ASP.NET Core cookie authentication, three schemes: **Staff**, **Patron**, **Kiosk**. Roles Admin / Reception / Manager. | Keeps the baseline roles; no extra packages; separate cookies stop a patron session being mistaken for staff. | ASP.NET Core Identity would add user management we do not need. |
| D3 | Patron identity | Library card number + 4-digit PIN, PBKDF2 hashed, locked for 5 minutes after 5 wrong tries. | Kiosk and holds need to know the patron without exposing data. | Card-only is weaker; full accounts are heavier. |
| D4 | Reservation granularity | Per item (one library code = one physical copy). | Matches how Assignment 2 identifies items. | Per title would need a Title entity. Listed as a limitation. |
| D5 | Where an item "is" | `Item.BranchId` is the physical location. Lending only at that branch. A return elsewhere moves it. | Makes multi-branch inventory real and easy to demonstrate. | Lend-anywhere with a home branch means more states. |
| D6 | In transit | Derived from an active `Transfer`. The item stays at the sending branch until the receiving branch confirms. | One source of truth; cannot get out of step with an item flag. | A status value on the item. |
| D7 | Categories | A `Category` entity per item type, used by search, API and importer. | The API needs a categories endpoint; the importer needs a lookup. | Free text. |
| D8 | Fines | 14-day loan and $0.50 per full late day from settings; `FinePaid`/`FinePaidOn` on the loan. | "Fine revenue" needs charged vs collected vs outstanding. | |
| D9 | Notification channels | Email and SMS **simulated**. Preferred channel per patron; "Both" creates two records; SMS capped at 160 characters. Stored and shown on a page, and logged to the console. | Meets "dashboard or console simulator" and is testable. | |
| D10 | Time simulation | Admin "simulated today" and "Run checks now" buttons; a daily background job runs the same code. `IClock` is the only source of today. | Due-soon and fine notices can be demonstrated in a minute. | |
| D11 | CSV and PDF | **Written by our own code** (RFC 4180 reader/writer; a small PDF 1.4 writer with pagination and page numbers). *Changed from CsvHelper and QuestPDF.* | No NuGet access during the build; zero third-party code to install or licence. CSV exports guard against spreadsheet formula injection. | A library would give richer PDF layout. |
| D12 | API documentation | Hand-built **OpenAPI 3 document** (`/api/openapi.json`) and an interactive docs page (`/api/docs`), plus `curl` and `.http` examples. *Changed from Swagger UI.* | Same reason as D11; the docs page needs no external script. | |
| D13 | API protection | GET/HEAD/OPTIONS only (405 otherwise), `X-Api-Key` header, fixed-window rate limit per key (429 + Retry-After), DTOs only, CORS policy. | Read-only and not abusable; no sensitive data. | One shared demo key; no per-client keys. |
| D14 | UI | One responsive layout and stylesheet for public and staff pages; the kiosk has its own large-button layout in the same colours. | Consistent design; the kiosk is intentionally simpler. | |
| D15 | `IClock` contract | One clock registered for the whole app (`SimulatedClock`, which falls back to the system clock). | D10 only works if lending, fines and notifications read the same date. | |
| D16 | Single solution, one team | The three planned work-streams were integrated into one codebase built against the shared contracts in `Library.Core/Contracts`. | One coherent product; contracts stopped the parts drifting apart. | |

## The hook rule (how the features talk to each other)

- Lending calls `IReservationService.CheckBorrowAllowed` **before** lending: an item held (Ready) for someone else cannot be lent.
- After a successful borrow: `OnBorrowed` (closes the borrower's own hold) and then a *Borrowed* notification is recorded.
- After a return (not when returned damaged), or when an admin changes a damaged item back to available: `OnItemBecameAvailable` offers the item
  to the first person waiting, who is notified.
- Hooks never make the main action fail: errors are logged and the borrow or return still succeeds.

## Assumptions agreed with the client / team

- "Branch" in the install instructions means a library branch (not a Git branch).
- The baseline is the team's Assignment 2 system.
- A shared demo API key is acceptable for the demo.
