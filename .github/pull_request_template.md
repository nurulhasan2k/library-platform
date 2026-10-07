## What does this change?

<!-- One or two sentences. Link the task on the project board. -->

## Area

- [ ] Foundation / branches / access (Person A)
- [ ] Kiosk / notifications / reservations (Person B)
- [ ] API / import / analytics (Person C)
- [ ] Documentation only

## Checklist (see CONTRIBUTING.md)

- [ ] `dotnet build` and `dotnet test` pass locally
- [ ] Branch is up to date with `main` and named `feature/<area>-<short-name>`
- [ ] Business rules are in a service, not in a controller or view
- [ ] Every POST has an anti-forgery token and server-side validation
- [ ] Staff pages have an `[Authorize(Roles = ...)]` attribute
- [ ] No patron, loan or fine data is exposed on public pages or the API
- [ ] Added or updated tests for the rules I changed
- [ ] If I added an EF Core migration: I told Person A and it follows the latest one on `main`
- [ ] If I added a setting: it is in `appsettings.json` and `docs/CONFIGURATION.md`
- [ ] README / user-guide notes updated if behaviour changed
- [ ] No `bin/`, `obj/`, database files or secrets are committed

## How I tested it

<!-- Steps, seeded accounts used, screenshots for UI changes. -->

## Reviewer

<!-- A reviews B, B reviews C, C reviews A. -->
