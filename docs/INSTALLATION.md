# Installation and Running Instructions

For a clean Windows, macOS or Linux machine (including the assessor's virtual machine). Allow about 10 minutes.

**What you need:** the .NET 10 SDK, Git and a web browser. No database server, no Node.js, no other downloads: the solution uses no NuGet packages.

## Step 1. Install the .NET 10 SDK

Check whether it is already there:

```bash
dotnet --list-sdks
```

You need a line starting with `10.`. The **SDK** is required to build and run from source (having only the "runtime" is not enough; if `dotnet --list-sdks` prints nothing
but `dotnet --list-runtimes` shows something, install the SDK).

| System | How |
|---|---|
| Windows | Download the installer from <https://dotnet.microsoft.com/download/dotnet/10.0> (SDK, x64) and run it, or `winget install Microsoft.DotNet.SDK.10` |
| macOS | Installer from the same page, or `brew install --cask dotnet-sdk` |
| Ubuntu / Debian | `sudo apt-get update && sudo apt-get install -y dotnet-sdk-10.0` (or follow Microsoft's instructions for your version) |

Close and reopen the terminal, then run `dotnet --version`. It should print `10.x.x`.

## Step 2. Get the code

```bash
git clone https://github.com/nurulhasan2k/library-platform.git
cd library-platform
git checkout main
```

(If you received a zip file instead, unzip it and open a terminal in the folder that contains `LibraryPlatform.sln`.)

## Step 3. Run it

```bash
dotnet run --project src/Library.Web
```

The first run restores and builds (about a minute) and creates `data/library.json` with the demo data. Wait for:

```
Now listening on: http://localhost:5080
```

Shortcuts: `scripts/run.sh` (macOS/Linux) or `scripts\run.ps1` (Windows PowerShell).
Leave this window open. Stop the site with `Ctrl+C`.

## Step 4. Open the site and sign in

1. Browse to <http://localhost:5080>. The public search page appears (it works on a phone-sized window too).
2. **Select the relevant location branch** (library branch): sign in at <http://localhost:5080/staff/login> as `reception` / `reception123`;
   you are asked to choose **Central Library** or **Northside Library** and a desk. (`reception.river` / `reception123` works at Riverside only.)
3. Other demo accounts:

| Role | Username | Password |
|---|---|---|
| Admin | `admin` | `admin123` |
| Reception | `reception` | `reception123` |
| Reception (Riverside) | `reception.river` | `reception123` |
| Manager | `manager` | `manager123` |
| Patron (kiosk and `/account`) | card `LIB-1001` | PIN `1111` |

4. Kiosk: <http://localhost:5080/kiosk>. API documentation: <http://localhost:5080/api/docs>. API key for the demo: `demo-api-key-2026`.

## Step 5. Check that it works (2 minutes)

| Check | Expected |
|---|---|
| Home page | 43 items found, search box and filters |
| `/kiosk`, card `LIB-1001`, PIN `1111` | "Hello, Ava Thompson" |
| `curl -H "X-Api-Key: demo-api-key-2026" http://localhost:5080/api/v1/status` | `{"status":"ok", ...}` |
| Sign in as `manager`, open **Reports** | Dashboard with charts; CSV and PDF buttons work |

Optional automated checks (the site does not need to be running): `dotnet run --project tests/Library.Tests` prints `117 passed, 0 failed`.

## Resetting the demo data

Stop the site, then:

```bash
scripts/reset-db.sh          # or scripts\reset-db.ps1   (same as: dotnet run --project src/Library.Web -- --reset-db --exit)
```

Or delete `data/library.json` and start the site again.

## Alternative: build once, run anywhere (only the .NET 10 *runtime* needed to run)

```bash
dotnet publish src/Library.Web -c Release -o publish
cd publish
dotnet Library.Web.dll --urls http://localhost:5080
```

## Settings you may want to change

Defaults need no change. To use another port: `dotnet run --project src/Library.Web --urls http://localhost:5090`. All settings are in
`docs/CONFIGURATION.md`. To move the data file: set the environment variable `LIBRARY_DATA_FILE`.

## Troubleshooting

| Problem | Fix |
|---|---|
| `dotnet` is not recognised | Install the SDK (step 1) and reopen the terminal |
| "You must install or update .NET to run this application" | The .NET 10 runtime/SDK is missing or older than 10; install the SDK (step 1) |
| `Address already in use` / port 5080 busy | Close the other program or use `--urls http://localhost:5090` |
| Page shows no items / odd data | Stop the site and run the reset command above |
| "The data file is locked" or save errors | Make sure only one copy of the site is writing; delete `data/library.json.lock` if the site is stopped |
| Kiosk logs me out | By design after 60 idle seconds (`Kiosk:IdleTimeoutSeconds`) |
| API returns 401 / 403 / 429 | Send `X-Api-Key: demo-api-key-2026`; wait a minute if you hit the rate limit (60 per minute) |
| Browser warns about HTTPS | The demo runs on plain `http://localhost:5080`; use that address |

## What was tested

Built and run from a clean copy with the .NET 10 SDK (10.0.112) on Linux without any package download; 117 automated tests pass; a scripted browser walk-through of all 12
demo items passes (67 checks). The same steps apply on Windows and macOS; they have not been tried on the assessor's own VM image, so please report anything unexpected.
