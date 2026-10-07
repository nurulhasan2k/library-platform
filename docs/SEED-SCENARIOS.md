# Seed data scenarios (draft for Meeting 1)

This is the list of demo data the platform will create (client section 6). It is written **before** the code so B and C can say
what they need. The seeder is built by Person A (`SeedRunner`, one seeder class per module); B adds reservations and
notifications, C adds the sample import files.

## Principles

- **Relative dates.** Every date is an offset from "today" (`D`, taken from `IClock` when the data is created), so the due-soon,
  overdue and fine scenarios work whenever the app is reset. `D-12` means twelve days before today.
- **Repeatable.** The same data every time (fixed random seed 2026 for the filler data).
- **Resettable.** `dotnet run --project src/Library.Web -- --reset-db` (and `scripts/reset-db.ps1` / `scripts/reset-db.sh`) deletes
  the database, recreates it and seeds it. Documented in the README.
- **Named scenarios.** The things the client will test are named below, so a demo never depends on luck.
- **Demo credentials only.** Passwords and PINs here are for the demo and are written in the README on purpose.

## 1. Branches, hours and desks

| Code | Name | Address (fictional) | Phone (fictional) | Opening hours | Desks |
|---|---|---|---|---|---|
| CENTRAL | Central Library | 1 Library Square | (08) 5550 0101 | Mon-Fri 09:00-18:00, Sat 10:00-16:00, **Sun closed** | Desk 1, Desk 2 |
| NORTH | Northside Library | 22 Northside Road | (08) 5550 0102 | Tue-Fri 10:00-17:00, Sat 10:00-14:00, **Sun and Mon closed** | Desk 1, Desk 2 |
| RIVER | Riverside Library | 5 Riverbank Walk | (08) 5550 0103 | Mon-Fri 09:00-17:00, Sun 12:00-16:00, **Sat closed** | Desk 1, Desk 2 |

Every branch has at least one closed day so the API's "operating status" has something to show on any day.

## 2. Staff accounts (demo)

| Username | Password | Role | Branches they may choose |
|---|---|---|---|
| `admin` | `admin123` | Admin | all |
| `reception` | `reception123` | Reception | CENTRAL, NORTH (so the "select a branch" demo has a real choice) |
| `reception.river` | `reception123` | Reception | RIVER only (branch chosen automatically) |
| `manager` | `manager123` | Manager | all (read-only) |

`admin`, `reception` and `manager` are the Assignment 2 logins, kept so the baseline still behaves the same.

## 3. Categories

| Type | Categories |
|---|---|
| Book | Science Fiction, Fantasy, Programming, History |
| Music | Rock, Jazz, Classical |
| Toy | Puzzle, Building |

## 4. Items (45)

Library codes are globally unique: `BK-001`..., `MU-001`..., `TY-001`....

| Branch | Items | Books | Music | Toys |
|---|---|---|---|---|
| CENTRAL | 18 | 10 | 5 | 3 |
| NORTH | 14 | 7 | 4 | 3 |
| RIVER | 13 | 7 | 3 | 3 |
| **Total** | **45** | **24** | **12** | **9** |

Statuses across the 45: about 32 Available, 7 Borrowed, 3 Damaged, 2 Destroy (never shown publicly), plus the items that are
Available but in transit or held. Five items are **never borrowed** (idle for the inventory-health report).
About 25 "filler" items are generated from a fixed list of real-sounding titles. The **scenario items** below are fixed.

| Code | Item | Branch | Status / state | Used for |
|---|---|---|---|---|
| BK-001 | Dune (Frank Herbert, Science Fiction) | CENTRAL | Available | Kiosk checkout item 1 |
| BK-002 | Neuromancer (William Gibson, Science Fiction) | CENTRAL | Available | Kiosk checkout item 2 |
| MU-001 | Rumours (Fleetwood Mac, 1977, Rock) | CENTRAL | Available | Desk borrow + notification demo |
| BK-010 | The Pragmatic Programmer (Programming) | CENTRAL | **Borrowed** by Emma, due D+6, **waitlist of 3** | Reservation demo: return it and the next patron is notified |
| BK-011 | Clean Code (Programming) | CENTRAL | Available, **held (Ready)** for Ben until D+2 | Held-item protection: others cannot borrow it |
| MU-010 | Kind of Blue (Miles Davis, 1959, Jazz) | CENTRAL | **Damaged**, Grace waiting | Admin sets it back to Available, so the next patron is notified |
| BK-020 | The Hobbit (Fantasy) | CENTRAL | Borrowed by Daniel, **due D-12 (overdue)** | Fine accruing notification and report |
| BK-021 | Foundation (Science Fiction) | CENTRAL | Borrowed by Daniel, **due D-5 (overdue)** | Fine accruing notification and report |
| TY-003 | 1000-piece map puzzle (Puzzle, age 8+) | CENTRAL | Borrowed by Farid, **due D+2** | Due-soon notification |
| MU-004 | The Four Seasons (Vivaldi, Classical) | NORTH | Borrowed by Farid, due D+5 | Not yet due soon; shows the check is date-based |
| TY-005 | Wooden building blocks (Building, age 3+) | NORTH | Available, **expired hold** (Chloe) | Expired-hold history |
| BK-030 | Piranesi (Fantasy) | CENTRAL | Available, **Requested** transfer to RIVER | Transfer list: requested |
| MU-012 | Abbey Road (The Beatles, 1969, Rock) | CENTRAL (arriving) | **InTransit** from NORTH, dispatched D-2 | Cannot be borrowed; transfer list: in transit |
| TY-010 | Marble run set (Building, age 6+) | NORTH | Available (arrived D-18 from RIVER), transfer **Received** | Transfer history |
| BK-031 | A Short History of Nearly Everything (History) | CENTRAL | Available, transfer to NORTH **Cancelled** D-9 | Transfer history |
| BK-032 | Project Hail Mary (Science Fiction) | CENTRAL | Available, **no transfer** | Live demo: request, dispatch and receive a transfer to NORTH |
| BK-033 | The Martian (Science Fiction) | NORTH | Available | Same demo in the other direction |
| BK-040 | An old atlas (History) | RIVER | Destroy | Must not appear in public search or the API |

## 5. Patrons (10)

| Card | Name | PIN | Prefers | Story |
|---|---|---|---|---|
| LIB-1001 | Ava Thompson | 1111 | Email | Main kiosk demo patron; one normal loan due D+9; paid-up |
| LIB-1002 | Ben Carter | 2222 | SMS | Has a **Ready** hold on BK-011 |
| LIB-1003 | Chloe Nguyen | 3333 | Both | Two notifications per event; had an expired hold |
| LIB-1004 | Daniel Okafor | 4444 | Email | **Two overdue loans with fines accruing** and one earlier unpaid fine of $3.00 |
| LIB-1005 | Emma Rossi | 5555 | Email | **Five active loans** (the limit is not enforced; shows a full account summary); has BK-010 |
| LIB-1006 | Farid Hassan | 6666 | SMS | Loan **due in 2 days** (due-soon notification) |
| LIB-1007 | Grace Liu | 7777 | Email | Waitlist **position 1** for BK-010 and waiting on MU-010 |
| LIB-1008 | Hugo Martin | 8888 | SMS | Waitlist position 2 for BK-010; one cancelled hold |
| LIB-1009 | Isla Brown | 9999 | Both | Waitlist position 3 for BK-010 |
| LIB-1010 | Jack Wilson | 0000 | Email | **Brand new, no loans, no holds** (empty states) and used for the wrong-PIN lockout demo |

## 6. Active loans at seed time

| Patron | Items | Borrowed | Due |
|---|---|---|---|
| Ava | one filler book | D-5 | D+9 |
| Daniel | BK-020 | D-26 | D-12 (overdue 12 days = $6.00) |
| Daniel | BK-021 | D-19 | D-5 (overdue 5 days = $2.50) |
| Emma | BK-010 + 4 filler items | D-8 | D+6 |
| Farid | TY-003 | D-12 | D+2 |
| Farid | MU-004 | D-9 | D+5 |

## 7. Loan history (for manager analytics)

About **150 returned loans** over the last 180 days, generated deterministically:

- Per month (oldest to newest): about 20, 22, 25, 27, 28, 28 loans (gentle growth, so trends show).
- 55% books, 25% music, 20% toys; popular categories (Science Fiction, Rock) borrowed more than the rest.
- About **80% returned on time**; the rest 1 to 14 days late, **fined $0.50 per full day**.
- Fines: about **65% marked paid**, the rest outstanding (the older ones are mostly paid).
- About 15% returned at a **different branch** from where they were borrowed; 4 returned damaged.
- Spread over all branches, all desks and all patrons except Jack.

## 8. Transfers (A)

| State | Item | From | To | Dates |
|---|---|---|---|---|
| Requested | BK-030 | CENTRAL | RIVER | requested D-1 |
| InTransit | MU-012 | NORTH | CENTRAL | requested D-3, dispatched D-2 |
| Received | TY-010 | RIVER | NORTH | requested D-22, received D-18 |
| Cancelled | BK-031 | CENTRAL | NORTH | requested D-10, cancelled D-9 |

## 9. Reservations (seeded by B)

| Item | Patron | Status | Notes |
|---|---|---|---|
| BK-010 | Grace | Waiting, position 1 | created D-4 |
| BK-010 | Hugo | Waiting, position 2 | created D-3 |
| BK-010 | Isla | Waiting, position 3 | created D-1 |
| BK-011 | Ben | **Ready** | created D-6, ready since D-1, expires D+2 |
| TY-005 | Chloe | Expired | expired D-2 |
| MU-003 | Hugo | Cancelled | cancelled D-7 |
| MU-010 | Grace | Waiting, position 1 | item is Damaged |

## 10. Notifications (seeded by B)

About 20 notifications across the last 30 days: Borrowed (8), DueSoon (4), FineAccruing (3), ReservationReady (2),
ReservationExpired (1), plus a couple on each channel, including Chloe's two-record "Both" example. Some patrons' notifications are
generated live when B's scheduler runs against the loans above (for example Farid's due-soon).

## 11. Sample files (seeded by C)

`samples/import/`: a valid CSV (about 20 new items for RIVER), a CSV with bad rows covering every rule, a larger file, and the mock
provider dataset. C also documents the demo API key.

## 12. Which data each demo uses

| # | Client demo item | Data used |
|---|---|---|
| 1 | Fresh install and startup | Whole seed; `admin` login |
| 2 | Selecting a branch | `reception` chooses CENTRAL or NORTH; `reception.river` has one branch |
| 3 | Kiosk checkout and account summary | Ava checks out BK-001 and BK-002; Emma's and Daniel's account summaries |
| 4 | Public API requests | Items across 3 branches, 9 categories, branch hours; BK-040 must not appear |
| 5 | Borrowing and notification logging | Ava borrows MU-001 at a desk and at the kiosk; notification appears |
| 6 | Due-date and fine notification simulation | Farid (due D+2), Daniel (overdue); move simulated today forward |
| 7 | Multi-branch inventory and transfer | Search by branch; BK-032 to NORTH; MU-012 in transit cannot be borrowed |
| 8 | Reservation and next-person notification | Emma returns BK-010 at CENTRAL; Grace becomes Ready and is notified; Hugo cannot borrow it; admin repairs MU-010 |
| 9 | CSV or external import | C's sample files into RIVER |
| 10 | Manager analytics | The 180-day loan history, fines and idle items |
| 11 | CSV and PDF exports | Same data, all three reports |
| 12 | Database seeding/reset | `--reset-db` returns everything to this document |

## 13. Requests to B and C (please confirm at Meeting 1)

- **B:** are the patrons, holds and loan dates above enough for the kiosk, notification and reservation demos? Do you need more
  patrons, a patron with an expired PIN, or a loan on a different branch?
- **C:** is 180 days of history, the fine mix and the idle items enough for the three reports? Which extra categories or
  branches do your sample CSVs need? Which API key value do you want in the demo configuration?

## 14. How it is built (A)

`SeedRunner` runs these seeders in order: Branches (with hours and desks) -> Staff -> Categories -> Items -> Patrons -> Loan history and
active loans -> Transfers -> **B's seeders** (Reservations, Notifications) -> **C's samples**. Each module registers its own
seeder class, so nobody edits another person's seeder. Seeding is skipped when the database already has data, unless
`--reset-db` is used.
