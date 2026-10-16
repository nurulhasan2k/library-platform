# Walkthrough of the 12 demo items

Each item below says what to do and what you should see. Start from fresh demo data
(`scripts/reset-db.sh`, then `scripts/run.sh`). The browser walk-through `tests/e2e/demo.py` performs exactly these steps automatically.
Data used is described in [`SEED-SCENARIOS.md`](SEED-SCENARIOS.md). Site: <http://localhost:5080>.

## 1. Fresh install and startup

1. Run `dotnet run --project src/Library.Web`. The console shows the demo data being created and
   `Now listening on: http://localhost:5080`.
2. Open the site. You see the public search (43 items found, 12 per page) and a footer link to the kiosk.
3. Open `/staff/login` and sign in as `admin` / `admin123`.

*Note:* 45 items are seeded; 2 are *destroyed* and are never shown publicly, so the public search shows 43.

## 2. Selecting a branch

1. Sign in as `reception` / `reception123`. Because this user works at two branches you are asked to pick one:
   choose **Central Library** and a desk. The top bar then shows `reception - Central Library / Desk 1`.
2. Sign out and sign in as `reception.river`: only one branch, so you go straight in.
3. Click the branch name in the top bar at any time to change branch or desk.

## 3. Kiosk checkout and account summary

1. Open `/kiosk`. Enter `LIB-1001` and PIN `1111`. You see "Hello, Ava Thompson", her loans, fines and holds.
2. **Check out**: type `BK-001`, Enter, then `BK-002`, Enter. Both appear under "Checked out this visit (2)" with due dates 14 days ahead.
3. Type `TY-001` (destroyed): a clear refusal message appears. **Done - show my receipt** shows both loans. **Sign out**.
4. Sign in as `LIB-1005` / `5555` (5 items on loan) and `LIB-1004` / `4444` (overdue items and fines).
5. Enter a wrong PIN for `LIB-1010` (real PIN `0000`) five times: the card is locked for five minutes.

## 4. Public API requests

Use the commands in the README section 9 or `scripts/api-examples.sh`. Check: items from 3 branches, 9 categories, 3 branches with seven
opening-hour lines each, `401` without a key, `403` with a wrong key, `405` for POST or DELETE, and that `BK-040` (destroyed) never appears.
Browse <http://localhost:5080/api/docs> for the interactive documentation.

## 5. Borrowing and notification logging

1. Staff sign in as `reception`, branch Central, open **Desk**. Lend `MU-001` to `LIB-1001`. The page confirms the loan and due date.
2. Open **Notifications** (search "Rumours"): a *Borrowed* notification exists for Ava (email). The server console also shows `[SIMULATED EMAIL] to ava.thompson@example.com`.
3. Do the same at the kiosk (item 3): kiosk loans produce the same notifications.
4. Chloe (`LIB-1003`) prefers *Both*, so each event creates an email and an SMS record.

## 6. Due-date and fine notification simulation

1. Sign in as `admin`, open **Notifications**, press **Run checks now**. Farid (`LIB-1006`) gets a due-soon notice (due in 2 days); Daniel (`LIB-1004`) gets
   daily fine-accruing notices for his overdue loans. Press it again: no duplicates are created.
2. Press **+7 days** under *Simulated today*, then **Run checks now**: more notices appear (items now overdue, more fines).
3. Press **Use the real date** to go back.

## 7. Multi-branch inventory and transfer

1. On the public search, pick **Northside Library** in the branch filter: only its items are listed. Item pages show their branch and opening hours.
2. As reception at Central open **Transfers**. Request `BK-032` to Northside, then **Dispatch**: the item is now *in transit*.
3. Try to lend `MU-012` (already in transit from Northside) or `BK-030` (transfer requested) at the desk: refused ("in transit" / transfer in progress).
4. Switch to branch Northside (top bar) and **Receive** `BK-032`. The item page now shows Northside Library and it can be lent there.

## 8. Reservation and next-person notification

1. Open **Holds**: `BK-010` has Grace, Hugo and Isla waiting in that order.
2. At the Central desk return `BK-010` (Emma has it). Grace becomes **Ready** and gets a *ready for you* notification.
3. Try to lend `BK-010` to Hugo (`LIB-1008`): refused, "held for another patron".
4. As `admin` open **Items**, set the damaged `MU-010` (Kind of Blue) to *available*: Grace, who was waiting for it, is notified.

## 9. CSV or external import

1. Sign in as `admin`, open **Import**. Upload `samples/import/bad-rows.csv` with branch Riverside: preview shows 2 ready and 15 skipped rows, each with a reason.
2. Upload `samples/import/valid-river-20-items.csv` and press **Import valid rows**: 20 new items, visible in the public search under Riverside Library.
3. Use the **External provider (mock)** box: 12 records, 2 invalid, 10 imported.
4. The page keeps a history of imports and lets you download the skipped rows as a CSV.

## 10. Manager analytics

Sign in as `manager` / `manager123`. The dashboard shows loans in the last 30 days, items out, overdue items, unpaid fines, loans per month, by type and by branch.
Change the dates or branch and press **Update**. The three full reports (Borrowing statistics, Fine revenue, Inventory health) show tables for the same filter.

## 11. CSV and PDF exports

On any report press **CSV** (opens in Excel with a summary block and one table per section) or **PDF** (paginated, with title, filter, and "Page n of N").
Exports respect the date range and branch chosen on the dashboard.

## 12. Database seeding and reset

Stop the site, run `scripts/reset-db.sh` (or `.ps1`), start again: everything is back to the state in [`SEED-SCENARIOS.md`](SEED-SCENARIOS.md).
Deleting `data/library.json` and starting the site has the same effect.
