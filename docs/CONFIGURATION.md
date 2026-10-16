# Configuration reference

Settings are read from `src/Library.Web/appsettings.json`. Each section is bound to an options class in
`src/Library.Core/Configuration/Options.cs`. Override a value for your machine without editing the shared file by using
`appsettings.Development.json`, environment variables (use `__` for nesting, for example `Lending__LoanDays=21`) or
`dotnet user-secrets`.

| Key | Default | Meaning | Owner |
|---|---|---|---|
| `Database:ConnectionString` | `Data Source=data/library.db` | SQLite file for the whole app. The `data/` folder is not committed. | A |
| `Database:SeedOnStartup` | `true` | Create the demo data on first start when the database is empty. | A |
| `Lending:LoanDays` | `14` | Length of a loan in days (brief). | A |
| `Lending:FinePerDay` | `0.50` | Fine per full day late (brief). | A |
| `Auth:MaxPinAttempts` | `5` | Wrong PIN attempts before a patron is locked out. | A |
| `Auth:LockoutMinutes` | `5` | Length of the lockout. | A |
| `Kiosk:BranchCode` | `CENTRAL` | The branch this kiosk station belongs to. | B |
| `Kiosk:IdleTimeoutSeconds` | `60` | Kiosk session ends after this many idle seconds. | B |
| `Notifications:DueSoonDays` | `2` | A "due soon" notice is sent this many days before the due date. | B |
| `Notifications:EnableScheduler` | `true` | Run the daily notification checks as a background job. | B |
| `Reservations:MaxActiveHoldsPerPatron` | `5` | Most Waiting + Ready holds one patron may have. | B |
| `Reservations:ReadyHoldDays` | `3` | Days a Ready hold is kept before it expires. | B |
| `Api:Key` | *(empty)* | Key clients send in the `X-Api-Key` header. **Set with user-secrets; never commit.** | C |
| `Api:RateLimitPerMinute` | `60` | Requests per minute per client before HTTP 429. | C |
| `Api:MaxPageSize` | `100` | Largest page size the API accepts. | C |
| `Api:EnableSwagger` | `true` | Serve the OpenAPI document and Swagger UI. | C |
| `Import:MaxFileSizeMb` | `5` | Largest CSV file the importer accepts. | C |
| `Reports:IdleItemDays` | `90` | An item not borrowed for this many days is "idle" in the inventory-health report. | C |
| `Reports:DefaultRangeDays` | `180` | Default borrowing and fine report range, including today. Inventory health remains as at today. | C |

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
