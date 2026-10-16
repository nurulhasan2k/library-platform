# Configuration reference

Settings are read from `src/Library.Web/appsettings.json`. Each section is bound to an options class in
`src/Library.Core/Configuration/Options.cs`. Override a value for your machine without editing the shared file by using
`appsettings.Development.json`, environment variables (use `__` for nesting, for example `Lending__LoanDays=21`) or
`dotnet user-secrets`. The defaults are the demo settings: **nothing needs changing to run the demo.**

| Key | Default | Meaning |
|---|---|---|
| `Database:DataFile` | `data/library.json` | The data file. A relative path is placed next to `LibraryPlatform.sln`; the environment variable `LIBRARY_DATA_FILE` overrides it. Not committed. |
| `Database:SeedOnStartup` | `true` | Create the demo data on first start when the file is empty. |
| `Lending:LoanDays` | `14` | Length of a loan in days. |
| `Lending:FinePerDay` | `0.50` | Fine per full day late. |
| `Auth:MaxPinAttempts` | `5` | Wrong PIN attempts before a patron is locked out. |
| `Auth:LockoutMinutes` | `5` | Length of the lock-out. |
| `Kiosk:BranchCode` | `CENTRAL` | The branch this kiosk belongs to. |
| `Kiosk:IdleTimeoutSeconds` | `60` | A kiosk session ends after this many idle seconds. |
| `Notifications:DueSoonDays` | `2` | A "due soon" notice is created this many days (or fewer) before the due date. |
| `Notifications:EnableScheduler` | `true` | Run the daily notification checks as a background job. |
| `Reservations:MaxActiveHoldsPerPatron` | `5` | Most Waiting + Ready holds one patron may have. |
| `Reservations:ReadyHoldDays` | `3` | Days a Ready hold is kept before it expires. |
| `Api:Key` | `demo-api-key-2026` | Key clients send in `X-Api-Key`. **Demo value**; set your own with user-secrets or `Api__Key`. If empty, the API answers 503. |
| `Api:RateLimitPerMinute` | `60` | Requests per minute per client before HTTP 429. |
| `Api:MaxPageSize` | `100` | Largest page size the API accepts. |
| `Api:EnableDocs` | `true` | Serve the interactive API documentation page (`/api/docs`) and the OpenAPI JSON (`/api/openapi.json`). |
| `Import:MaxFileSizeMb` | `5` | Largest CSV file the importer accepts. |
| `Import:MaxRows` | `2000` | Most data rows in one import. |
| `Reports:IdleItemDays` | `90` | An item not borrowed for this many days is "idle" in the inventory-health report. |

## Command-line options

| Option | Effect |
|---|---|
| `--reset-db` | Delete all data and create the demo data again before starting. |
| `--exit` | Exit after start-up work (used with `--reset-db`). |
| `--urls http://localhost:5090` | Listen on another address (standard ASP.NET Core option). |

## Setting a secret (API key example)

```bash
cd src/Library.Web
dotnet user-secrets init
dotnet user-secrets set "Api:Key" "choose-a-long-random-value"
```

## Adding a setting

1. Add the key to `appsettings.json`.
2. Add the property (with a default) to the matching class in `Options.cs`.
3. Add a row to the table above.
