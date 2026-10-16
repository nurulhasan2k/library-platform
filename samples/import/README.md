# Import samples

- `items-valid.csv`: 20 RIVER items: eight books, six music items and six toys. Includes blank status (Available) and Damaged.
- `items-bad-rows.csv`: valid quoting examples mixed with missing fields, invalid types/statuses, bad years/ages, unknown branch/category, category/type mismatch, duplicates, oversized fields and formula-like cells. Has a UTF-8 BOM, comma, doubled quote, embedded newline and blank line. `BK-1500` demonstrates a database duplicate when that code is supplied in the lookup.
- `items-large.csv`: 2,000 valid RIVER books for the parse-time check. The generator is not stored here.
- `mock-provider/catalogue.json`: 25 fictional Regional Library Network items: 21 valid and four rejected (bad type, unknown category, duplicate code, missing name). This source is simulated and reads a local file; it does not call a live service. Its codes use 600 and above, outside the other sample ranges.

Choose Regional Library Network (simulated) on the existing import screen. JSON fields map to the same row fields as CSV, then use the same validator, preview and confirmation. Choose skip bad rows to create the 21 valid items; all or nothing creates none while errors remain. Repeating the import rejects the previously created codes. Provider item 1 appears as row 2 in the shared preview/error CSV because row 1 is the generated CSV header. `Import:MockProviderPath` selects the local JSON file; relative paths start at the application's content root. Confirmation revalidates the cached preview against the current catalogue, without replacing it with later file edits.

Codes use 500 and above, apart from deliberately missing or invalid codes. The files use the branch/category names in `docs/SEED-SCENARIOS.md`. The current empty catalogue stub needs real lookups before these files can be imported through an application screen.

The validator counts physical lines from header row 1. Blank lines count towards row numbers, not the row limit. A multiline record uses its starting line. Later duplicate codes are rejected, including when the first occurrence was invalid.

Field limits: code/branch 30; item type/status 20; name/author/artist 200; description 4000; category/genre/toy type 100; year/age input 10 characters. Unrecognised columns also have a 4000-character limit. Codes use letters, digits, `-` and `_`. Values and lookup names are trimmed and compared without case where appropriate. A row branch overrides the selected target; an empty branch needs a valid target.

File-extension, byte-size, encoding, missing-header and row-limit failures are tested by changing the sample input or its settings; one CSV cannot also be invalid UTF-8 or lack its own header. File errors prevent confirmation. Rejected-row downloads use UTF-8 with BOM and prefix formula-like cells with an apostrophe, including when whitespace precedes the formula.
