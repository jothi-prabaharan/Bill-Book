# Backend tests

`dotnet test` from `backend/`.

## Status

**91 tests, passing.** They compiled and ran green the first time an SDK was
available, which is what the scaffolding was written for — the wiring (csproj,
solution entry, package versions) is the tedious part to retrofit, and having it
in place meant one command rather than an afternoon.

If `dotnet` is missing from a container, install it from the distribution
repository — some environments deny `dot.net` by egress policy:

```bash
apt-get update && apt-get install -y dotnet-sdk-10.0
```

## What is covered, and why only this

Everything here is **pure logic**: no `DbContext`, no HTTP, no mocks.

| File | Covers | Why it earns a test |
|---|---|---|
| `NumberFormatTests` | Code composition, financial-year rendering, reset timing | Fails silently — a wrong year segment produces a number that reads perfectly and is only caught at audit |
| `ReorderingTests` | Drag-and-drop display order, including the renumber path | The renumber branch only runs when neighbours have no gap between them, so nobody exercises it by hand |
| `PhoneAttributeTests` | Landline pattern, mobile length | A regex that forgets the leading `+` rejects every overseas number |
| `StockLedgerMappingTests` | What a stock movement means in the general ledger | The clearest case of failing silently in the product: a wrong guard refuses a sale and somebody rings up, but a wrong account produces a balance sheet that still balances and a gross margin that is simply untrue |

That line is what decides whether something belongs in the pure set.
`StockLedgerMapping` qualifies because it is a `static` function over an entity —
it names accounts and does no I/O. `StockLedgerPoster`, which calls it, does not:
everything interesting about it is a guarded claim and an HTTP retry.

## The database-backed set

`Accounting.Api.Tests` is the exception this file used to say was owed, and it
arrived with the general ledger. The interesting behaviour there — the deferred
balance triggers, the `ExecuteDelete` that makes a posting replace rather than
accumulate, the guarded update that keeps a numbering series gapless inside the
caller's transaction — is behaviour of Postgres. Testing it against an in-memory
provider would assert that the mock behaves like the mock, which is exactly why
it was left undone until it could be done properly.

So those tests need **a real PostgreSQL**:

```bash
service postgresql start                 # or point at your own
export ACCOUNTING_TEST_DB="Host=localhost;Port=5432;Database=accounting_tests;Username=postgres;Password=123"
dotnet test
```

The default connection string is the one above, so on a machine with a local
server and those credentials nothing needs setting.

**They skip themselves, with a reason, when no server answers.** A suite that
fails on a machine without Postgres trains people to ignore red; one that passes
without running is worse. Skipped-with-a-reason is the only honest third option.

Each test builds its own branch with a fresh `OrgId`, so the query filter keeps
them apart — which means the tests exercise the isolation rather than working
around it — and the schema comes from `Database.Migrate()`, not
`EnsureCreated()`, because every trigger and RLS policy lives in the migrations
and `EnsureCreated` skips all of them.

| File | Covers |
|---|---|
| `LedgerArithmeticTests` | Running balances and the trial-balance column split. Pure — always runs |
| `LedgerPostingServiceTests` | The posting door: a whole document's legs in one call, two services replacing independently on one invoice, and withdrawal |
| `JournalServiceTests` | The manual journal: draft, post, reverse, line-level reversal pairing, and a refused post leaving the number series where it was |
| `LedgerReportServiceTests` | The account ledger and the trial balance, read back over postings written through the door |

## Adding a test project

One per project under test, named `{Project}.Tests`, under `backend/tests/`.
Add it to `Bill-Book.sln` and reference the project under test — nothing else.
