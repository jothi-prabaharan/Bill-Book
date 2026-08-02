# Backend tests

`dotnet test` from `backend/`.

## Status

**58 tests, passing.** They compiled and ran green the first time an SDK was
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

Services are not covered, deliberately. Almost every one of them is a `DbContext`
away from being testable, and the interesting behaviour — guarded conditional
updates, query filters, deferred constraints — is behaviour of Postgres, not of
C#. Testing it against an in-memory provider would assert that the mock behaves
like the mock. When those are tested it should be against a real Postgres, and
that is a different piece of work: see PLAN 5.7.

That line is what decides whether something belongs here. `StockLedgerMapping`
qualifies because it is a `static` function over an entity — it names accounts
and does no I/O. `StockLedgerPoster`, which calls it, does not: everything
interesting about it is a guarded claim and an HTTP retry.

## Adding a test project

One per project under test, named `{Project}.Tests`, under `backend/tests/`.
Add it to `Bill-Book.sln` and reference the project under test — nothing else.
