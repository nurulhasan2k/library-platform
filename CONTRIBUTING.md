# Contributing

Three people work in this repository. These rules keep `main` working so nobody is blocked.

## Branches

- `main` must always build and run. Never commit straight to it.
- Work on a Git branch named `feature/<area>-<short-name>`, for example `feature/branches-transfer-service`,
  `feature/kiosk-checkout`, `feature/api-items-endpoint`.
  Use `fix/<short-name>` for bug fixes and `docs/<short-name>` for documentation only.
- Keep branches short-lived (a few days). Pull `main` into your branch often.
- In this project **"branch" in the application always means a library branch.** A Git branch is always called a *Git branch*.

## Pull requests and reviews

- Open a pull request for every change to `main`, using the template in `.github/`.
- Every pull request is reviewed by one teammate before merging: **Mohammed reviews Rakibul, Rakibul reviews Modasser, Modasser reviews Mohammed.**
- The author merges after approval (squash merge keeps history readable).
- Review within one working day. If a review is blocking you, say so in the team chat.

## Commit messages

- Short imperative first line (about 70 characters), then a blank line and details if needed.
  Good: `Add transfer state machine and rules`. Not good: `stuff`, `fix`, `wip`.
- One logical change per commit.

## Never commit

`bin/`, `obj/`, the data file (`data/library.json`) and its `.lock` file, import staging files, `appsettings.Local.json`, API keys, passwords or anything from `dotnet user-secrets`.
`.gitignore` covers most of this; check `git status` before you commit.

## Data changes

- The data model lives in `src/Library.Core/Models` and is stored as JSON (see `docs/ER-DIAGRAM.md`). Adding a field needs a default value so older data files still load.
- Update the seed data (`SeedRunner`) and `docs/SEED-SCENARIOS.md` when the demo data changes.
- Run `scripts/reset-db.sh` after pulling a change to the seed data.

## Code standards

- Business rules live in services (`src/Library.Core`), never in controllers or views.
- Controllers stay thin. Views use view models, not database entities.
- Every POST action has an anti-forgery token and validates input on the server.
- Every staff page has `[Authorize(Roles = "...")]` (see the roles matrix in `docs/ARCHITECTURE.md`).
- Never show a stack trace. A missing, deleted or invalid item gets a friendly message.
- Use `IClock` for dates, never `DateTime.Now` or `DateTime.Today` in business code.
- Public pages and the public API never expose patron, loan or fine data.
- Add or update tests for the rules you change (`scripts/run-tests.sh` must pass).

## Settings

Add new settings to `appsettings.json`, to the options class in `src/Library.Core/Configuration/Options.cs`, and to
`docs/CONFIGURATION.md`. Secrets go in `dotnet user-secrets`, never in the repository.

## Definition of done

Merged to `main`, works against the seed data, validated and authorised, has tests for its rules,
has its README section and user guide section written, and has been demonstrated on the VM.

## Meetings

We have three meetings (kickoff, midpoint, final). Chair and note-taker rotate; minutes are posted within 24 hours.
Details: the work briefs, section 6.
