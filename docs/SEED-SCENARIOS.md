# Demo data (seed)

The application creates this data automatically the first time it starts with an empty data file, and again after `--reset-db`.
Dates are relative to the day the data is created, so there are always overdue and due-soon items. Random loan history uses a fixed
seed (2026), so two resets give the same shape of data.

Credentials and PINs below are **demo values** and are written here on purpose.

## 1. Branches

| Code | Name | Desks | Hours |
|---|---|---|---|
| CENTRAL | Central Library | 2 | Mon-Fri 09:00-18:00, Sat 10:00-16:00, Sun closed |
| NORTH | Northside Library | 2 | Mon closed, Tue-Fri 10:00-17:00, Sat 10:00-14:00, Sun closed |
| RIVER | Riverside Library | 2 | Mon-Fri 09:00-17:00, Sat closed, Sun 12:00-16:00 |

The kiosk belongs to CENTRAL by default (`Kiosk:BranchCode`).

## 2. Staff users

| Username | Password | Role | Branches |
|---|---|---|---|
| admin | admin123 | Admin | all |
| reception | reception123 | Reception | CENTRAL, NORTH |
| reception.river | reception123 | Reception | RIVER |
| manager | manager123 | Manager | all (reports only) |

A reception user with two branches is asked to choose a branch and desk after signing in; `reception.river` has one branch and goes straight in.

## 3. Categories

- **Book**: Science Fiction, Fantasy, Programming, History
- **Music**: Rock, Jazz, Classical
- **Toy**: Puzzle, Building

## 4. Items (45)

Per branch: CENTRAL 18, NORTH 14, RIVER 13. Types: Book 24, Music 12, Toy 9.

| Code | Name | Branch | Category | Status |
|---|---|---|---|---|
| BK-001 | Dune | CENTRAL | Science Fiction | Available |
| BK-002 | Neuromancer | CENTRAL | Science Fiction | Available |
| BK-003 | The Name of the Wind | CENTRAL | Fantasy | Borrowed |
| BK-005 | Sapiens | NORTH | History | Borrowed |
| BK-006 | Clean Architecture | NORTH | Programming | Available |
| BK-007 | The Fellowship of the Ring | NORTH | Fantasy | Available |
| BK-008 | Snow Crash | NORTH | Science Fiction | Available |
| BK-009 | Guns, Germs, and Steel | NORTH | History | Damaged |
| BK-010 | The Pragmatic Programmer | CENTRAL | Programming | Borrowed |
| BK-011 | Clean Code | CENTRAL | Programming | Available |
| BK-012 | Eloquent JavaScript | NORTH | Programming | Available |
| BK-013 | The Left Hand of Darkness | RIVER | Science Fiction | Available |
| BK-014 | Mistborn: The Final Empire | RIVER | Fantasy | Available |
| BK-015 | Refactoring | RIVER | Programming | Borrowed |
| BK-016 | The Silk Roads | RIVER | History | Available |
| BK-017 | Hyperion | RIVER | Science Fiction | Available |
| BK-018 | Introduction to Algorithms | RIVER | Programming | Available |
| BK-020 | The Hobbit | CENTRAL | Fantasy | Borrowed |
| BK-021 | Foundation | CENTRAL | Science Fiction | Borrowed |
| BK-030 | Piranesi | CENTRAL | Fantasy | Available |
| BK-031 | A Short History of Nearly Everything | CENTRAL | History | Available |
| BK-032 | Project Hail Mary | CENTRAL | Science Fiction | Available |
| BK-033 | The Martian | NORTH | Science Fiction | Available |
| BK-040 | An old atlas | RIVER | History | Destroy |
| MU-001 | Rumours | CENTRAL | Rock | Available |
| MU-002 | A Love Supreme | CENTRAL | Jazz | Available |
| MU-003 | The Dark Side of the Moon | CENTRAL | Rock | Available |
| MU-004 | The Four Seasons | NORTH | Classical | Borrowed |
| MU-005 | Symphony No. 5 | CENTRAL | Classical | Available |
| MU-006 | Bitches Brew | NORTH | Jazz | Borrowed |
| MU-007 | Led Zeppelin IV | NORTH | Rock | Available |
| MU-008 | The Planets | RIVER | Classical | Available |
| MU-009 | Nevermind | RIVER | Rock | Available |
| MU-010 | Kind of Blue | CENTRAL | Jazz | Damaged |
| MU-011 | Time Out | RIVER | Jazz | Available |
| MU-012 | Abbey Road | NORTH | Rock | Available |
| TY-001 | Speed cube | CENTRAL | Puzzle | Destroy |
| TY-002 | Classic brick box | CENTRAL | Building | Available |
| TY-003 | 1000-piece map puzzle | CENTRAL | Puzzle | Borrowed |
| TY-004 | Solar system jigsaw | NORTH | Puzzle | Borrowed |
| TY-005 | Wooden building blocks | NORTH | Building | Available |
| TY-006 | Tangram set | RIVER | Puzzle | Available |
| TY-007 | Magnetic construction set | RIVER | Building | Damaged |
| TY-008 | Wooden train set | RIVER | Building | Available |
| TY-010 | Marble run set | NORTH | Building | Available |

Items with status *Destroy* never appear in the public search or the API. Items with status *Damaged* are listed but cannot be lent.

## 5. Patrons (10)

| Card | Name | PIN | Prefers | Story |
|---|---|---|---|---|
| LIB-1001 | Ava Thompson | 1111 | Email | Main kiosk demo patron; one normal loan; paid up |
| LIB-1002 | Ben Carter | 2222 | Sms | Has a **Ready** hold |
| LIB-1003 | Chloe Nguyen | 3333 | Both | Prefers *Both*: two notifications per event; had an expired hold |
| LIB-1004 | Daniel Okafor | 4444 | Email | **Two overdue loans** with fines accruing and an earlier unpaid fine |
| LIB-1005 | Emma Rossi | 5555 | Email | **Five active loans**: full account summary |
| LIB-1006 | Farid Hassan | 6666 | Sms | Loan **due in 2 days** (due-soon notification) |
| LIB-1007 | Grace Liu | 7777 | Email | Waitlist position 1 for BK-010 |
| LIB-1008 | Hugo Martin | 8888 | Sms | Waitlist position 2 for BK-010; one cancelled hold |
| LIB-1009 | Isla Brown | 9999 | Both | Waitlist position 3 for BK-010 |
| LIB-1010 | Jack Wilson | 0000 | Email | Brand new: no loans, no holds (empty states); used for the wrong-PIN lock-out demo |

## 6. Loans

About 151 returned loans spread over the last 180 days across all branches (about 1 in 5 lent at a kiosk, some returned at a different branch, some late with fines, some paid), so the reports have real shapes. 10 loans are active:

| Patron | Item | Branch | Borrowed | Due | Channel |
|---|---|---|---|---|---|
| Ava Thompson (LIB-1001) | BK-003 The Name of the Wind | CENTRAL | 2026-10-10 | 2026-10-24 | Desk |
| Daniel Okafor (LIB-1004) | BK-020 The Hobbit | CENTRAL | 2026-09-19 | 2026-10-03 | Desk |
| Daniel Okafor (LIB-1004) | BK-021 Foundation | CENTRAL | 2026-09-26 | 2026-10-10 | Desk |
| Emma Rossi (LIB-1005) | BK-010 The Pragmatic Programmer | CENTRAL | 2026-10-07 | 2026-10-21 | Desk |
| Emma Rossi (LIB-1005) | BK-005 Sapiens | NORTH | 2026-10-07 | 2026-10-21 | Desk |
| Emma Rossi (LIB-1005) | BK-015 Refactoring | RIVER | 2026-10-07 | 2026-10-21 | Desk |
| Emma Rossi (LIB-1005) | MU-006 Bitches Brew | NORTH | 2026-10-07 | 2026-10-21 | Desk |
| Emma Rossi (LIB-1005) | TY-004 Solar system jigsaw | NORTH | 2026-10-07 | 2026-10-21 | Desk |
| Farid Hassan (LIB-1006) | TY-003 1000-piece map puzzle | CENTRAL | 2026-10-03 | 2026-10-17 | Desk |
| Farid Hassan (LIB-1006) | MU-004 The Four Seasons | NORTH | 2026-10-06 | 2026-10-20 | Desk |

## 7. Transfers (one in every state)

| Item | From | To | State | Note |
|---|---|---|---|---|
| BK-030 Piranesi | CENTRAL | RIVER | Requested | Requested by a Riverside patron. |
| MU-012 Abbey Road | NORTH | CENTRAL | InTransit | For the Beatles display. |
| TY-010 Marble run set | RIVER | NORTH | Received |  |
| BK-031 A Short History of Nearly Everything | CENTRAL | NORTH | Cancelled | Cancelled: the patron found a copy elsewhere. |

## 8. Reservations

| Item | Patron | State |
|---|---|---|
| BK-010 The Pragmatic Programmer | Grace Liu | Waiting |
| BK-010 The Pragmatic Programmer | Hugo Martin | Waiting |
| BK-010 The Pragmatic Programmer | Isla Brown | Waiting |
| BK-011 Clean Code | Ben Carter | Ready |
| TY-005 Wooden building blocks | Chloe Nguyen | Expired |
| MU-003 The Dark Side of the Moon | Hugo Martin | Cancelled |
| MU-010 Kind of Blue | Grace Liu | Waiting |

## 9. Notifications

23 notifications are created, covering Borrowed (10), DueSoon (3), FineAccruing (5), ReservationReady (3), ReservationExpired (2), on both channels. More are generated live when the checks run.

## 10. Sample files

`samples/import/` holds the CSV files for the importer (see its README). The mock external provider has no file; it returns 12 records, 2 invalid.

## 11. Which data each demo item uses

See [`DEMO-WALKTHROUGH.md`](DEMO-WALKTHROUGH.md) for the 12 demo items with exact steps and the data each one uses.
