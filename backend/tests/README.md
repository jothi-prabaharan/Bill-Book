# Backend tests

`dotnet test` from `backend/`.

## Read this before trusting anything here

**These tests have never been compiled or run.** No .NET SDK has been available
in any session that worked on this repository — the egress policy blocks
`dot.net` and `builds.dotnet.microsoft.com` — which is the same reason every
migration and Designer file here is hand-written.

So treat a test in this project as a *stated expectation*, not as a passing one.
The first `dotnet test` will almost certainly need corrections: package versions
in `Directory.Packages.props` are unverified, and `TreatWarningsAsErrors` is on
solution-wide, so an xunit analyser warning is a build failure rather than a
note.

That is a real cost, and it was weighed against the alternative. Retrofitting
the project wiring — csproj, solution entry, package versions, folder layout —
is the tedious part; the tests themselves are cheap. Having the wiring in place
means the first person with an SDK runs one command and gets a signal, instead
of spending an afternoon on scaffolding before writing a line.

## What is covered, and why only this

Everything here is **pure logic in `Shared.Kernel`**: no `DbContext`, no HTTP,
no mocks.

| File | Covers | Why it earns a test |
|---|---|---|
| `NumberFormatTests` | Code composition, financial-year rendering, reset timing | Fails silently — a wrong year segment produces a number that reads perfectly and is only caught at audit |
| `ReorderingTests` | Drag-and-drop display order, including the renumber path | The renumber branch only runs when neighbours have no gap between them, so nobody exercises it by hand |
| `PhoneAttributeTests` | Landline pattern, mobile length | A regex that forgets the leading `+` rejects every overseas number |

Services are not covered, deliberately. Almost every one of them is a `DbContext`
away from being testable, and the interesting behaviour — guarded conditional
updates, query filters, deferred constraints — is behaviour of Postgres, not of
C#. Testing it against an in-memory provider would assert that the mock behaves
like the mock. When those are tested it should be against a real Postgres, and
that is a different piece of work: see PLAN 5.7.

## Adding a test project

One per project under test, named `{Project}.Tests`, under `backend/tests/`.
Add it to `Bill-Book.sln` and reference the project under test — nothing else.
