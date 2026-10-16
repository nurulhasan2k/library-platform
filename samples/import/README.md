# Sample import files

Use these with **Admin -> Import** (sign in as `admin` / `admin123`).

| File | What it shows | Expected result |
|---|---|---|
| `valid-river-20-items.csv` | 20 new items for the RIVER branch (8 books, 6 music, 6 toys) | 20 ready, 0 skipped |
| `bad-rows.csv` | One example of every validation rule (missing type, unknown type, missing code, bad code, duplicate of an existing item, missing name, unknown branch, unknown category, missing author/artist, bad year, bad age, duplicate inside the file) | 2 ready, 15 skipped, each with a plain-language reason; the *Download problems as CSV* link lists them |
| `larger-150-items.csv` | 150 rows across all three branches | 150 ready (shows the preview paging and speed) |

Columns: `type, code, name, description, branch, category, author, artist, year, age`.
`branch` is optional: when it is empty the branch picked on the import page is used.

The **external provider (mock)** source on the same page needs no file: it returns 12 records, 2 of which are deliberately invalid.
