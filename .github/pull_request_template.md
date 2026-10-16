## What does this change?

<!-- One or two sentences. Link the task on the project board. -->

## Area

- [ ] Foundation / branches / access (Mohammed Nurul Hasan)
- [ ] Kiosk / notifications / reservations (Md Rakibul Hassan Emon)
- [ ] API / import / analytics (Md Modasser Hossain)
- [ ] Documentation only

## Checklist (see CONTRIBUTING.md)

- [ ] `dotnet build` and `scripts/run-tests.sh` pass locally
- [ ] Branch is up to date with `main` and named `feature/<area>-<short-name>`
- [ ] Business rules are in a service, not in a controller or view
- [ ] Every POST has an anti-forgery token and server-side validation
- [ ] Staff pages have an `[Authorize(Roles = ...)]` attribute
- [ ] No patron, loan or fine data is exposed on public pages or the API
- [ ] Added or updated tests for the rules I changed
- [ ] If I changed the data model: new fields have defaults, and the seed data and `docs/ER-DIAGRAM.md` are updated
- [ ] If I added a setting: it is in `appsettings.json` and `docs/CONFIGURATION.md`
- [ ] README / user-guide notes updated if behaviour changed
- [ ] No `bin/`, `obj/`, the data file or secrets are committed

## How I tested it

<!-- Steps, seeded accounts used, screenshots for UI changes. -->

## Reviewer

<!-- Mohammed reviews Rakibul, Rakibul reviews Modasser, Modasser reviews Mohammed. -->
