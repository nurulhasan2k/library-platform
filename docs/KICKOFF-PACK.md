# Kickoff pack for Meeting 1

Everything Person A prepared before the kickoff. Read these before the meeting.

| Document | What it is |
|---|---|
| [`DECISIONS.md`](DECISIONS.md) | The 14 decisions plus 2 contract additions to agree, the client questions, and the chair's checklist |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Solution layout, roles matrix, the shared services and the hook rule, how to replace a stub |
| [`ER-DIAGRAM.md`](ER-DIAGRAM.md) | Proposed data model (diagram and source) |
| [`SEED-SCENARIOS.md`](SEED-SCENARIOS.md) | Exactly what demo data will exist and which demo uses it |
| [`CONFIGURATION.md`](CONFIGURATION.md) | Every setting, its default and its owner |
| [`../CONTRIBUTING.md`](../CONTRIBUTING.md) | Branch names, reviews, migrations, standards, definition of done |
| `../src/Library.Core/Contracts/` | The service interfaces (with comments) that B and C code against |
| `../src/Library.Core/Stubs/` | Safe stubs so the solution builds today |

## Try it in two minutes

```bash
dotnet run --project src/Library.Web
# open http://localhost:5080 and http://localhost:5080/dev/contracts
```

`/dev/contracts` lists each shared service and whether it is a stub or real. At Meeting 2 every row owned by A and B should say "Real".
