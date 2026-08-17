# Antigravity brief — RetailErp reporting, stages R1 and R2

You are continuing work already in progress on a multi-tenant retail ERP for
Indian SMBs. The reporting engine, the grid and three worked example reports
already exist. **Your job is to add ten more reports on top of them, not to design
anything.** If a task seems to need a design decision, it has almost certainly
already been made in one of the files named here; if it genuinely has not, stop
and ask rather than inventing one.

---

## 1. Repository and branch

| | |
|---|---|
| Repository | `github.com/jothi-prabaharan/Bill-Book` |
| **Branch — work on this and only this** | **`Report`** |
| Base of your work | `a8b0737` or later on `Report` |
| Default branch | `main` — **do not commit to it** |

```bash
git clone https://github.com/jothi-prabaharan/Bill-Book.git
cd Bill-Book
git fetch origin Report
git checkout Report
git pull origin Report          # always start from the latest
```

**Why not `main`.** `CLAUDE.md` says the product has one branch and it is `main`.
Reporting is a written exception, recorded in `CLAUDE.md` under *"The one standing
exception: reporting"*, because two agents build it in parallel. Everything under
`docs/architecture/REPORTS.md` goes to `Report`.

**Never open a pull request unless explicitly asked.**

---

## 2. Git workflow

**Pull before you start each task.** Another agent is on this branch.

```bash
git pull origin Report
```

**Commit when a task stands up**, not when the whole stage is finished. One task,
one commit. A commit that does not build blocks whoever is working in parallel
with you.

**Commit message format** (from `docs/standards/commit-rules.md`):

```
<type>(<scope>): <subject>

<body — why, not what. The diff already says what.>
```

- `type` — `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `build`, `chore`
- `scope` — the module: `reporting`, `accounting`, `inventory`
- `subject` — imperative, present tense, **no capital, no full stop**

Good: `feat(reporting): add the general ledger summary`
Bad: `Added GL Summary report.`

**Push:**

```bash
git push -u origin Report
```

If a push fails on a network error, retry up to four times with 2s, 4s, 8s, 16s
backoff. If it fails because the branch moved, `git pull --rebase origin Report`
and push again.

---

## 3. Environment

**.NET 10 SDK** is required. If missing:

```bash
apt-get update && apt-get install -y dotnet-sdk-10.0
```

**PostgreSQL 16** for the database-backed tests. Without one they skip with a
reason rather than failing, which is acceptable — but the reports you write are
worth running against real data.

```bash
apt-get install -y postgresql postgresql-contrib
pg_ctlcluster 16 main start
su postgres -c "psql -c \"ALTER USER postgres PASSWORD '123';\" -c 'CREATE DATABASE accounting_tests;'"
```

The tests read `ACCOUNTING_TEST_DB`, defaulting to
`Host=localhost;Port=5432;Database=accounting_tests;Username=postgres;Password=123`.
**Reporting uses the same database** — `rpt` sits beside `acc` and `con` in the
per-customer database, so there is no second one to create.

**EF Core tools**, if you need to generate a migration:

```bash
dotnet tool install --global dotnet-ef --version 10.0.0
export PATH="$PATH:/root/.dotnet/tools"
```

---

## 4. Project layout

```
backend/
├── Bill-Book.sln
├── Directory.Packages.props        ← the closed package list
├── Api/{Module}/                   ← Master, Inventory, Accounting, Sales,
│   ├── {Module}.Entity/            ←   Purchase, Customer, Reporting
│   │   ├── TableEntities/
│   │   ├── Models/
│   │   └── Enums/
│   ├── {Module}.Repository/        ← DbContext, repositories, seed data
│   └── {Module}.Api/               ← controllers, services, DI
├── shared/Shared.Kernel/
├── worker/
├── Gateway/                        ← YARP
└── tests/{Module}.Api.Tests/
frontend/
├── apps/{web, portal, admin, desktop, docs}
└── libs/
    ├── {module}/{module}-core   ← view-models, no templates, Ionic-safe
    ├── {module}/{module}-ui     ← pages
    └── shared/{auth, api-client, ui-components, ...}
```

Dependency direction: `Api` → `Repository` → `Entity` → `Shared.Kernel`. **Never
backwards.**

Your work is almost entirely in:

```
backend/Api/Reporting/Reporting.Api/Services/Sources/       ← new report sources
backend/Api/Reporting/Reporting.Repository/ReadModels/      ← new read models (R2.1)
backend/Api/Reporting/Reporting.Repository/SeedData/ReportCatalogSeeder.cs
backend/Api/Reporting/Reporting.Api/Program.cs              ← register each source
backend/tests/Reporting.Api.Tests/
```

Plus one Accounting migration for R1.1.

---

## 5. Hard rules

These are non-negotiable. Code that breaks one gets rejected.

1. **LINQ only. Never write raw SQL.** The only exceptions, because no LINQ
   equivalent exists: `CREATE DATABASE`, RLS policies, triggers, `set_config`.
2. **Entities are plain property bags.** No constructors, no methods, no
   validation logic, no computed properties. Just
   `public X Y { get; set; }` with Data Annotations.
3. **Every Data Annotation carries an `ErrorMessage`.**
4. **PascalCase table and column names**, matching the C# property names exactly.
5. **PostgreSQL only.** Never add SQL Server compatibility.
6. **All table entities inherit `Shared.Kernel.Entities.AuditableEntity`**, and
   per-customer ones inherit `Shared.Kernel.Tenancy.OrgScopedEntity`. Never set
   audit fields by hand.
7. **Enums, not magic strings**, for any fixed set of values.
8. **Never reference another service's `DbContext`.** Reporting has a recorded
   exception — read-only models mapped with `ExcludeFromMigrations` — described in
   `REPORTS.md` §2. It is specific to reporting and does not generalize.
9. **Ask before expanding scope.**
10. **Ship documentation in the same commit as the feature.**
11. **Do not add a package.** Not one, backend or frontend.
    `Directory.Packages.props` and `frontend/package.json` are closed lists for
    this work. If a task appears to need one, the task is wrong — say so.

---

## 6. Tenancy — the thing most likely to be got wrong

**Two levels, not three.**

- **Customer** = the head office. Owns **one physical database**.
- **Organization** = a branch. One place the business trades from, one complete
  set of books. A Customer owns many, all sharing that database.

| Boundary | Enforced by |
|---|---|
| Customer ↔ Customer | Separate physical databases |
| Organization ↔ Organization | `OrgId` + EF query filter + Postgres RLS |

**There is no `Branches` table and no `BranchId` column.** `OrgId` *is* the branch.

**Every per-customer table needs `OrgId` and a query filter.** Inherit
`OrgScopedEntity` and `TenantDbContext` applies the filter by reflection — which is
why a read model cannot be added without one.

Schemas: `mst` and `rat` in the shared master database; `con inv sal pur acc cus
rpt ntf` in each customer's own.

**`mst.Users`, `mst.AccountTypes`, countries and states are in a different
database and cannot be joined.** Resolve them in C#, batched. This is what R1.2
builds.

---

## 7. How the reporting engine works

You do not need to modify any of this. You need to know it exists so you do not
rebuild it.

- **`IReportSource` / `ReportSource<TRow>`** — one per report. Declares a key, a
  title, a permission, a column list, parameters, and a LINQ query that **executes
  nothing**. Everything else is the engine's.
- **`ReportQueryBuilder<TRow>`** — filtering, multi-key sorting, paging, composite
  group keys. All expression trees; no SQL anywhere.
- **Group footers and grand totals** — computed over the whole filtered result,
  never the page, so a subtotal is right when its group spans a page boundary.
- **`ReportCatalogService`** — merges what a column *means* (the source) with how
  it *appears* (the seeded `rpt.ReportDetails` row), and substitutes `%CurCode%`
  with the branch's currency.
- **`ExcelReportWriter`** — the export, over the full result set.
- **`PivotBuilder<TRow>`**, **saved views**, **`bb-report-grid`** and its panels —
  all done. Every report you add gets all of it for free.

**Adding a report is: a row type, a column list, a LINQ projection, a seed entry,
and one line of DI registration.** Nothing more.

---

## 8. The recipe — how to add a report

Full version with the rationale is `REPORTS.md` §9.5. The short form:

**Step 1 — a row type.** One per report, in the same file, holding exactly what
its columns read. Not an entity, not shared with another report: the projection
becomes EF's `SELECT` list, so a shared row type fetches columns nobody asked for.

**Step 2 — a source class** deriving from `ReportSource<TRow>` in
`Reporting.Api/Services/Sources/`:

```csharp
public sealed class GeneralLedgerSummarySource : ReportSource<GeneralLedgerSummaryRow>
{
    public override string ReportKey => "general-ledger-summary";
    public override string Title => "General Ledger Summary";
    public override ReportModule Module => ReportModule.Accounting;

    // The MODULE's permission, never reports.view. reports.view gets you the
    // catalog; reading the ledger through a report still needs accounting.view,
    // or the engine becomes a way round the permission on the screens it reports
    // from.
    public override string RequiredPermission => "accounting.view";

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        new() { Name = "from", Label = "From", DataType = ColumnDataType.Date },
        new() { Name = "to", Label = "To", DataType = ColumnDataType.Date },
    ];

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<GeneralLedgerSummaryRow, string>(
            "accountCode", ColumnDataType.Text, r => r.AccountCode, groupable: true),

        ReportColumn.Of<GeneralLedgerSummaryRow, decimal>(
            "debit", ColumnDataType.Money, r => r.Debit, AggregateFunction.Sum,
            alignment: default),
    ];

    protected override IQueryable<GeneralLedgerSummaryRow> Build(
        ReportParameters parameters, ReportingDbContext db)
    {
        // Executes NOTHING. Return the IQueryable — materialising here pages in
        // memory and reads the whole ledger to show fifty rows.
        DateOnly? start = parameters.Date("from");   // not named `from`: it is a
        DateOnly? end = parameters.Date("to");       // query-expression keyword

        return from a in db.Accounts
               where a.IsActive
               select new GeneralLedgerSummaryRow { /* ... */ };
    }

    // The tie-break, and it MUST be unique. Postgres promises no order without
    // ORDER BY, so two rows equal on every sort key can swap between pages —
    // which reads as a row going missing, only under paging, never in a test
    // with four rows.
    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<GeneralLedgerSummaryRow, long>>)(r => r.AccountId);
}
```

**Step 3 — the column flags.** Each has a wrong answer:

| Flag | Give it to | Never give it to |
|---|---|---|
| `AggregateFunction.Sum` | money worth totalling | a rate, a code, a date, a running balance — the footer would look like an answer |
| `groupable: true` | **text columns only** — it throws otherwise | anything else; grouping concatenates the key in SQL and a date would render in a format nobody chose |
| `filterable: false` | internal ids | ordinary columns |
| `sortable: false` | rarely | anything a person might order by |

**Step 4 — a seed entry** in `ReportCatalogSeeder.Catalog`, with the report and
**every** column it declares. Headers may carry `%CurCode%`, substituted per branch
at render time. `IsDefault` marks the columns shown before anybody chooses;
`IsHidden` carries a column fetched but never offered, like an id a row links by.

**Step 5 — register it** in `Reporting.Api/Program.cs`:

```csharp
builder.Services.AddScoped<IReportSource, GeneralLedgerSummarySource>();
```

**The check that will catch you:** the catalog service compares the source's
column keys against the seeded ones and **refuses the report by name** when they
disagree. A column added to a source but not the seeder breaks that report rather
than quietly dropping the column. That is deliberate, and it is the error you will
hit most often.

---

## 9. Decisions already taken — do not re-make them

- **Base-currency amounts** (`DebitAmountBase`, not `DebitAmount`) unless the
  report explicitly offers a `(Source)` column — in which case it must also offer
  `Currency` and `ExchangeRate` beside it. A total mixing a rupee row and a dollar
  row foots to a number in no currency.
- **Date ranges are parameters, not filters.** A report filtered to April is
  April's rows hidden from a report of everything; a report *for* April is a
  different report, and every opening figure downstream depends on which it is.
- **Cross-database values** — a user's name from `mst.Users`, an account type's
  name from `mst.AccountTypes` — cannot be joined. Resolve in C#, **batched**. A
  200-row page must not be 200 lookups. R1.2 builds the mechanism; until it lands,
  a report carries the id and not the name.
- **Groupable means text.** Expose an account type's *name* as a column and group
  by that.
- **A left join for optional relations.** The contact is denormalized onto a ledger
  leg, so a bank leg with no contact must still appear.

---

## 10. Your tasks, in order

### R1.1 — indexes on `acc.JournalLedger`
Three composite indexes: `(OrgId, LedgerDate)`, `(OrgId, AccountId, LedgerDate)`,
`(OrgId, SubAccountId, LedgerDate)`. **An Accounting migration, not a Reporting
one** — Accounting owns that table. Account Transaction is unusable at volume
without them.

### R1.2 — batched user-name resolver
`mst.Users` is in the master database and cannot be joined. Build a resolver that
takes a set of user ids and returns their display names in one call, cached.
Then use it for the six audit columns on Journal Report.

**This also unblocks the Account Type column**, which `REPORTS.md` §8.1 lists on
Account Movement and which is currently absent for exactly this reason. Add it
once the resolver exists.

### R1.4 — General Ledger Summary
Columns in §8.1. Copy `TrialBalanceSource`. Opening balance is everything before
the period — the `OpeningBalanceAsync` hook on `ReportSource<TRow>` already exists;
see `AccountTransactionSource` for how it is used.

### R1.5 — Journal Report
Columns in §8.1. Reads `acc.Journals` and `acc.JournalDetails`, **not**
`acc.JournalLedger`, because it reports on the document including drafts, which
have no ledger rows at all. Six audit columns resolved through R1.2.

### R1.6 — Bank Summary
Columns in §8.1. `acc.BankAccounts` joined to the ledger through
`LedgerAccountId` — each bank account provisions its own account in the chart, so
opening, received, spent and closing all come from ledger rows against it.

### R1.7 — Reconciliation
Columns in §8.1. `acc.BankStatements` and `acc.BankStatementLines`. Note:
*GroupBy* in the source column list is **a parameter, not a column**.

### R2.1 — the item reports
**Start by adding the `inv` read models** to
`Reporting.Repository/ReadModels/`, following the ten already there exactly:
`ItemRead`, `ItemStockRead`, `ItemCategoryRead`, `StockMovementRead`,
`CostLayerRead`, `ItemBatchRead`, `ItemSerialRead`, `WarehouseRead`,
`UnitOfMeasureRead`.

Each inherits `OrgScopedEntity`, is mapped in `ReportingDbContext` with
`ToTable(name, "inv", t => t.ExcludeFromMigrations())`, and declares **only** the
columns the reports need — a read model is not a copy of an entity. **Then run the
empty-migration check in §11.**

Then the reports: Inventory Aging (ages by cost-layer receipt date), Inventory Item
List, Item Detail, Item Summary. The last three declare their sales/purchase
columns and return null for them until those services exist.

### R2.2 — Batch Tracking, status and detail
### R2.3 — Serial Tracking, status and detail
### R2.4 — Warehouse Tracking, status and detail

Columns for all of these are in `REPORTS.md` §8.3.

---

## 11. Verify before every commit

```bash
cd backend
dotnet build          # MUST be clean — TreatWarningsAsErrors is on
dotnet test

cd ../frontend
npx nx lint <project>
npx tsc --noEmit -p tsconfig.eslint.json
```

**Do not run `npm run check`** — it chains lint → typecheck → tests → builds and
stops on a pre-existing failure that is not yours (§13). Run the steps directly.

**After adding read models**, prove they added nothing to the schema:

```bash
cd backend
export PATH="$PATH:/root/.dotnet/tools"
dotnet ef migrations add VerifyNoDrift \
  -p Api/Reporting/Reporting.Repository -s Api/Reporting/Reporting.Api -o Migrations

# The generated Up() and Down() must BOTH be empty. If they are not,
# ExcludeFromMigrations is missing somewhere. Then remove the probe:
dotnet ef migrations remove --force \
  -p Api/Reporting/Reporting.Repository -s Api/Reporting/Reporting.Api
```

---

## 12. Definition of done, per task

A task is finished when **all** of these are true:

1. `dotnet build` is clean with zero warnings.
2. `dotnet test` passes, with new tests covering what the report gets wrong
   quietly — a total that should not exist, a column that should not be
   filterable, a null that should still appear.
3. The report appears in `GET /api/reports` and runs through
   `POST /api/reports/{key}/query`.
4. Its box is ticked in `docs/architecture/REPORTS.md` §9.2 **in the same commit**.
5. Any user-visible change ships its documentation in the same commit — the page
   under `frontend/apps/docs/content/`, its status in `docs.manifest.ts`, and a
   bullet under **Unreleased** in `release-notes.md`.
6. Committed and pushed to `Report`.

---

## 13. Known-broken things that are not yours

Do not fix these, do not be surprised by them, and do not let them block you.

1. **`npm run check` fails** on a pre-existing typecheck error in
   `libs/sales/sales-ui/.../delivery-challan-form.component.ts` — a nine-field
   literal assigned to a twenty-five-field type. The chain stops there. `REPORTS.md`
   §9.6.
2. **Two `Accounting.Api.Tests` fail** when PostgreSQL is running —
   `An_under_allocated_payment_cannot_be_posted` and
   `A_posted_payment_cannot_be_knocked_out_of_allocation`. A migration squash
   dropped the allocation triggers. `REPORTS.md` §9.8.
3. **Row-level security does not apply at runtime.** `set_config` runs outside a
   transaction so the value is discarded, and the application connects as the table
   owner, which bypasses RLS anyway. Branch isolation currently rests on the EF
   query filter alone — which your read models inherit automatically, so your work
   is as safe as everything else. `REPORTS.md` §9.9.

---

## 14. What is already done

**R0 entirely** — `rpt` schema and migration with RLS, ten read models over `acc`
and `con`, the query contract, the generic engine with group footers and the
catalog, the API host and controllers, Excel export, `bb-report-grid`, the filter
bar, the column chooser, the group panel, the report list and host pages, routes
and documentation.

**R1.3** — Account Transaction, including the running-balance hooks on
`ReportSource<TRow>`.

**R3 entirely** — saved views API and dialog, `PivotBuilder`, pivot panel.

**Three reports render today:** Account Movement, Account Transaction, Trial
Balance. Your ten tasks take that to seventeen.

---

## 15. When to stop and ask

Ambiguity is a question, not a judgement call. An agent that guesses at the
tenancy model writes code that passes its own tests and leaks data between
customers.

Stop and ask when:

- a task seems to need a package that is not pinned;
- a spec does not say what a column means and you would have to invent it;
- something in the codebase contradicts this brief;
- you are about to write raw SQL for anything other than the four permitted cases;
- you are about to add a table without `OrgId`;
- a report needs a figure that no table holds.

Where an instruction says "copy `AccountMovementSource.cs`", that is an
instruction, not a hint. Consistency is what keeps forty-five reports maintainable
by one person.
