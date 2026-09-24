# TASKS.md — the work queue

Every pending piece of work in RetailErp, as **one ordered queue** of task cards. Work them top to
bottom; several agents can work at once, as long as they follow section 0.

Compiled from `CLAUDE.md` and `docs/Modules.md`, then checked against the code on
23 September 2026. **A card is a claim about the repository, and so is every other doc here.**
Before starting a card, check what it says against the code (see `AGENTS.md`). If the card is
wrong, fix the card in your claim commit.

---

## 0. Rules

### 0.1 Status markers

Each card's first line is its status. Sub-task boxes use the same markers.

| Marker | Meaning |
|---|---|
| `- [ ] open` | Nobody has it |
| `- [~] working (AI name) — since YYYY-MM-DD` | Claimed and in progress. Only one agent holds a card |
| `- [x] completed (AI name) — YYYY-MM-DD · tests written, not run` | An AI finished the work and wrote its tests, but did not run them (section 0.5) |
| `- [x] completed (AI name) — YYYY-MM-DD · tests passed (owner) — YYYY-MM-DD` | The owner ran the tests and they passed. The card is now checked against its **Done when** line |
| `- [!] blocked — D-xx` or `- [!] blocked — reason` | Can't start yet. Waiting on an owner decision (section 3), or on a problem written in the card's Notes |

- **AI name** is the model that did the work, as `get_session` reports it (e.g. `Claude Opus 5.5`,
  `Claude Sonnet 5`). A human uses their own name.
- **Tick a card in the same commit as the work that finishes it.** Tick sub-tasks as you finish
  them.
- **Never delete a card.** If a card is wrong or not needed, strike it through (`~~…~~`), give
  one line of reason and move it to section 4.
- **Card numbers follow the queue.** TK-01 is the first card to do, TK-02 the next, and so on;
  finished cards (section Z) come after the pending ones. The queue was renumbered this way on
  24 September 2026, and section 4 maps each old number to its new one, for commit messages and
  older notes. A new card takes the next unused number and goes where its priority says; only the
  owner orders a renumber, and a renumber updates every reference in the repository in one commit.

### 0.2 Working in parallel

Two agents can't edit the same files at once. **Lanes** make sure they don't (section 1). Each
lane owns a set of paths, and each card lists every lane it writes to.

**You may claim a card only when all four hold:**
1. its status is `- [ ] open`;
2. every card in **Depends on** is `[x]`;
3. it is not blocked by an open decision (section 3);
4. **none of its lanes appears on a `[~]` card.** One active card per lane. A lane lock covers
   that DbContext's migrations, so two agents never generate migrations for the same context.

**Take the first card in the queue that meets all four.** Once earlier cards are claimed, it's
normal for agent 1 to be on TK-70 while agent 2 is on TK-74.

**Claim protocol**. The claim is a commit on its own, and it lands before any work starts:
1. `git pull --rebase origin main`
2. Re-read the card. Check it still meets the four conditions above.
3. Change its status to `- [~] working (AI name) — since <today>`. Change nothing else in the file.
4. Commit only `docs/TASKS.md` with the message `claim TK-nn (AI name)`, then `git push origin main`.
5. **If the push is rejected**, run `git rebase --abort` if a rebase is in progress, then
   `git reset --hard origin/main`. This throws away only your one-line claim commit. Re-read the
   queue and start again from step 1. Never force-push a claim.
6. Start work only after the claim is on `origin/main`.

**While working**
- Write only to paths in your card's lanes. Shared files (section 1.1) follow their own rules.
- Commit small, and run `git pull --rebase origin main` before every push.
- In `docs/TASKS.md`, edit only your own card. In `docs/Modules.md`, you may tick your own stage's
  box without holding `L-DOC`.
- On a shared machine, give your test databases their own names (e.g. `SALES_TEST_DB=sales_test_agent2`).
  Two suites dropping the same database will break each other's results.

**Releasing a card**
- **Done**: tick it in the same commit as the last piece of work, then push. An AI adds
  `· tests written, not run` to the status line (section 0.5).
- **Stopping unfinished**: don't leave a `[~]` behind. Set it back to `- [ ] open` and add a
  `Handover:` line to Notes: what's done, what's next, and anything surprising.
- **Stale claims**: if a `[~]` card has had no commit to its lanes for **48 hours**, another agent
  may take it over. It writes `taken over from <AI name>` in Notes, and continues from
  whatever that agent pushed.

### 0.3 Git

All work goes to `main`, per `CLAUDE.md` hard rules 11 and 12. **Claims only mean something on
`main`.** A claim on any other branch is invisible to the other agents, and two of them will take
the same card.

If a harness starts your session on another branch:
- make the claim on `main` anyway;
- merge the work into `main` before you stop, and delete the branch.

If you can't push to `main`, say so to the user and take only the cards they assign you by name.

### 0.4 Card format

```
### TK-nn · Title
- [ ] open
- **Lanes:** L-… · **Depends on:** TK-… | — · **Decision:** D-… | —
- **Where:** the files to open first, with line numbers where they help
- **State:** what the code does today (checked on a stated date)
- **Tables:** (feature cards) the tables the card creates, named as in the design
- **Sub-tasks:**
  - [ ] a concrete step: a file, a method, a command
  - [ ] Test: a test to write (never run by an AI; section 0.5)
  - [ ] Owner: a step for the owner, such as running a suite from a dropped database
- **Done when:** the test that proves it
- **Notes:** handovers, findings, decisions made while working
```

**Keep cards useful, so the next agent doesn't have to search again.** When your work shows that
a card's Where or State is wrong, fix it in the same commit. When you find something another card
needs to know, write it in that card's Notes. That counts as editing only your own card.

Cards that build a feature (the HRMS, Payroll and School stages) also carry the **standard
delivery** sub-tasks in section 5.

### 0.5 Testing: AI writes the tests, the owner runs them

**AI agents write unit tests for every change and never run them.** The repository owner runs
the tests.

**An AI writes:**
- a test for every behaviour it adds or changes, following the patterns already in
  `backend/tests/*` and the frontend `*.spec.ts` files;
- a test for each clause of the card's **Done when** line, where a test can prove it;
- the `RlsAudit` and `EndpointGuardAudit` tests for any new schema or controller.

**An AI does not run:** `dotnet test`, `npm run test`, `npm run check` (which includes the tests),
Vitest, Playwright, or any other test runner. It also doesn't drop and recreate test databases to
run a suite.

**An AI still runs:** the build and the static checks, since these compile the tests without
running them. That means `dotnet build`, `npm run lint`, the TypeScript typecheck, the `nx`
builds and `dotnet ef migrations has-pending-model-changes`. A change that doesn't build is not
finished.

**Where a card says to run a suite**, the AI writes the tests that step needs and leaves the run
to the owner. Examples are "suite green from a dropped database", "watch `RlsAudit` fail" and
"confirm `dotnet test` is green". The AI does not tick that sub-task; the owner ticks it after
running the tests.

**Completing a card:**
1. The AI marks it `- [x] completed (AI name) — YYYY-MM-DD · tests written, not run`, and lists
   the test files it wrote under Notes.
2. The owner runs the tests. If they pass, the owner appends
   `· tests passed (owner) — YYYY-MM-DD`.
3. If a test fails, the owner sets the card back to `- [ ] open` and writes the failure under
   Notes. The next agent picks it up from there.

Cards that depend on a `tests written, not run` card may start without waiting for the owner's
run.

**CI still runs the tests on every push to `main`.** That is the owner's check, not the AI
running tests. An AI doesn't wait on CI or react to its results unless the owner asks it to.

---

## 1. Lanes

| Lane | Owns |
|---|---|
| `L-MST` | `backend/Api/Master/**` except contacts: `AdminDbContext`, `mst` migrations, auth, users, roles, organizations; `Master.Api.Tests` admin fixtures |
| `L-CON` | Master's contacts half: `ContactsDbContext`, `con` migrations, contacts controllers and services, `con.PrintTemplates` |
| `L-ACC` | `backend/Api/Accounting/**`, `backend/tests/Accounting.Api.Tests` |
| `L-INV` | `backend/Api/Inventory/**`, `backend/worker/CostingEngine.Worker`, `backend/tests/Inventory.Api.Tests` |
| `L-SAL` | `backend/Api/Sales/**`, `backend/tests/Sales.Api.Tests` |
| `L-PUR` | `backend/Api/Purchase/**`, `backend/tests/Purchase.Api.Tests` |
| `L-CUS` | `backend/Api/Customer/**`, `backend/tests/Customer.Api.Tests` |
| `L-RPT` | `backend/Api/Reporting/**`, `backend/tests/Reporting.Api.Tests` |
| `L-PRT` | `backend/Api/Printing/**`, `backend/tests/Printing.Api.Tests` |
| `L-NTF` | `backend/worker/Notification.Worker` |
| `L-RATE` | `backend/worker/RateSync.Worker` |
| `L-KERNEL` | `backend/shared/Shared.Kernel`, `backend/tests/Shared.Kernel.Tests`, `backend/tests/Shared`, `shared-fixtures` |
| `L-{MODULE}-UI` | `frontend/libs/{module}/**`, e.g. `L-SAL-UI` is `frontend/libs/sales` |
| `L-UI` | `frontend/libs/shared/**`, `frontend/libs/app-shell` |
| `L-WEB` / `L-DSK` / `L-PTL` / `L-ADM` | `frontend/apps/web`, `apps/desktop`, `apps/portal`, `apps/admin` |
| `L-DOC` | `CLAUDE.md`, `AGENTS.md`, `docs/Modules.md`, `docs/Architecture.md`, `docs/Transactions_And_Specs.md` |
| New services | Each new service's first card adds a lane for it, e.g. `L-HRM`, `L-PAY`, `L-SIS` |

### 1.1 Shared files (no lane owns them)

| File | Rule |
|---|---|
| `Directory.Packages.props`, `Bill-Book.sln`, `Directory.Build.*`, `package.json`, `package-lock.json`, `nx.json`, `tsconfig.base.json`, `.github/workflows/*` | Take lane **`L-DEPS`** as well for the commit that changes them. Hold it for that one commit only. `Npgsql` and `Npgsql.EntityFrameworkCore.PostgreSQL` move together |
| `release-notes.md`, `frontend/apps/docs/docs.manifest.ts` | Add lines only. On a rebase conflict, keep both sides |
| `frontend/apps/docs/content/**` | Edit only your module's page. A shared page takes `L-DOC` |
| `docs/TASKS.md` | Edit only your own card (section 0.2) |

---

## 2. The queue

**Every card was checked against the code on 23 September 2026.** Each one tells you where to
start, so you don't have to search the repository again:

- **Where**: the files to open first, with line numbers where they help.
- **State**: what the code does today.
- **Sub-tasks**: the steps, in order.
  - **Test:** steps are tests to write, not run (section 0.5).
  - **Owner:** steps are for the repository owner.

If the code has moved on since a card was written, correct the card in your claim commit.

**Reordered on 24 September 2026.** Pending cards run in priority order, and every card sits
after the cards it depends on. Finished cards are in section Z at the end, in ID order, and stay
there until the owner has run their tests. If a test fails, the owner moves the card back up.

### A · Blockers: security, seeding and data integrity

Nothing else is trustworthy until these land: the rest of RLS, the seeding gap that leaves new branches without purchase numbering or reports, internal endpoints that lose their tenant, and the ledger triggers the squash dropped.

### TK-01 · New branches are never seeded for Purchase or the report catalog
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Lanes:** L-MST (plus L-DEPS for the Bicep commit) · **Depends on:** TK-70 · **Decision:** —
- **Where:**
  - `backend/Api/Master/Master.Api/Services/TenantSeeder.cs:41`: `Services = ["Accounting", "Inventory", "Sales"]`.
  - `backend/Api/Master/Master.Api/appsettings.json:29`: the `Seeding` section.
  - `deploy/azure/modules/settings.bicep:50-52`
  - The seed endpoints: `backend/Api/Purchase/Purchase.Api/Controllers/InternalSeedController.cs` and
    `backend/Api/Reporting/Reporting.Api/Controllers/InternalSeedController.cs`.
  - The startup bootstrap in `backend/Api/Master/Master.Api/Services/DatabaseMigrationService.cs`,
    the block that seeds numbering series.
- **State:**
  - Purchase and Reporting both have a seed endpoint, but `TenantSeeder` never calls either, and
    neither has a `Seeding:` URL in `appsettings.json` or the Bicep settings.
  - So a new branch gets no `PO`, `GRN`, `BIL` or `DBN` numbering series, and no
    `rpt.ReportDetails` rows.
  - The startup bootstrap for org `…0001` seeds the Inventory and Sales numbering series, but not
    Purchase's.
- **Sub-tasks:**
  - [x] Confirm both endpoints take `SeedOrganizationRequest` on the route that
        `TenantSeeder.SeedOneAsync` posts to (`internal/seed/organization`). Align them if not.
  - [x] Add `"Purchase"` and `"Reporting"` to `TenantSeeder.Services`.
  - [x] Add `Seeding:Purchase` and `Seeding:Reporting`:
    - to `appsettings.json` (empty);
    - to `appsettings.Development.json`, with the local ports from each service's `launchSettings.json`;
    - to `settings.bicep`, as `Seeding__Purchase` and `Seeding__Reporting` (hold `L-DEPS` for that commit).
  - [x] Add `Purchase.Repository.SeedData.NumberingSeriesSeed.Build(targetOrgId)` next to the
        Inventory and Sales lines in `DatabaseMigrationService`.
  - [x] Test: a `TenantSeeder` test with a stubbed `IHttpClientFactory`. It asserts that all five
        services are called, and that a missing URL is reported as failed.
  - [ ] Owner: create a branch, then check that `NumberingSeries` holds the Purchase codes and
        `rpt.ReportDetails` has rows for the branch.
- **Done when:** a newly created branch can raise a purchase order and see its reports without any
  seeding by hand.
- **Notes:** once this lands, the retry in `apps/admin` can repair existing branches, since
  seeding is idempotent.
  - Handover (Claude Opus 5.5, 2026-09-24): released unfinished, with no code changed, when the
    owner moved me to TK-84. What I checked:
    - Both seed endpoints already take `SeedOrganizationRequest` on `internal/seed/organization`,
      and `PurchaseSeeder` and `ReportCatalogSeeder` are both registered. So the first sub-task
      needs no change.
    - Local ports: Purchase `http://localhost:4505/`, Reporting `http://localhost:4506/`.
    - `settings.bicep`: add both to the `master` block beside `Seeding__Sales`.
      `serviceUrl.purchase` and `serviceUrl.reporting` exist, because both are in `apiKeys`.
    - `DatabaseMigrationService`: don't just add Purchase's series to the `accDb2` block. That
      block runs only when `STA` is missing, so a database bootstrapped before this change would
      never get `POR`/`GRN`/`BIL`/`DBN`. Give Purchase its own existence check on `POR`.
  - Done (Claude Opus 5.5, 2026-09-24):
    - `TenantSeeder.Services` is `Accounting, Inventory, Sales, Purchase, Reporting, Printing`.
      Printing had been added by TK-81 since the card was written, so the test asserts six.
    - `Seeding:Purchase` / `Seeding:Reporting` in both appsettings files (4505, 4506) and in the
      `master` block of `settings.bicep`.
    - `DatabaseMigrationService` seeds Purchase's series behind its own check on `POR`, inside the
      `accDb2` block, so a database bootstrapped before this change is backfilled on next start.
      The bootstrap still does not seed the report catalog for org `…0001`; the card did not ask
      for it, and a retry from `apps/admin` reaches it through `TenantSeeder`.
    - `CLAUDE.md`'s "Purchase's numbering series never reach a new branch" bullet is now stale; it
      is `L-DOC`, which this card does not hold.
    - **Test written:** `backend/tests/Master.Api.Tests/TenantSeederTests.cs` (three tests, no
      database).

### TK-02 · RLS for `pur`
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Lanes:** L-PUR · **Depends on:** TK-71 · **Decision:** —
- **Where:** `backend/Api/Purchase/Purchase.Repository/Migrations/Tenant/`; the audit is at
  `backend/tests/Purchase.Api.Tests/PurchaseQueryFilterTests.cs:140`.
- **State:** 13 tables: `BillDetailTaxes, BillDetails, Bills, DebitNoteDetailTaxes,
  DebitNoteDetails, DebitNotes, ErrorLogs, GoodsReceiptDetailTaxes, GoodsReceiptDetails,
  GoodsReceipts, PurchaseOrderDetailTaxes, PurchaseOrderDetails, PurchaseOrders`.
- **Sub-tasks:**
  - [x] Write the migration using TK-71's template.
  - [x] Check `PurchaseSeeder`, which uses `IgnoreQueryFilters`.
  - [ ] Owner: run the suite from a dropped `PURCHASE_TEST_DB`.
- **Done when:** `pur`'s RLS assertion passes from a dropped database.
- **Notes:**
  - Done (Claude Opus 5.5, 2026-09-24):
    - `Purchase.Repository/Migrations/Tenant/20260924061415_EnableRowLevelSecurity.cs`, TK-71's
      template over the 13 tables listed above; every one carries `CustomerId` and `OrgId`.
    - `PurchaseSeeder`'s `IgnoreQueryFilters()` read filters on the org its endpoint sets as the
      tenant, and the table it reads (`acc.NumberingSeries`) is already under TK-71's policy, so RLS
      leaves it working. No other `IgnoreQueryFilters` and no hand-built `PurchaseDbContext`.
    - Applied by hand, as a `NOSUPERUSER NOBYPASSRLS` owner, to a scratch database built from the
      scripted chain: every tenant table ENABLEd, FORCEd and on the NULLIF policy, and a query with
      the tenant set to `''` returned 0 rows without throwing. `has-pending-model-changes` is clean
      and `dotnet build backend/Bill-Book.sln` has 0 warnings.
    - The audit call now exempts `__EFMigrationsHistory`, as `acc` and `inv` do.
    - **Test written:** `backend/tests/Purchase.Api.Tests/PurchaseRowLevelSecurityTests.cs` (four
      tests, role `pur_rls_probe`).

### TK-03 · RLS for `sal`
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Lanes:** L-SAL · **Depends on:** TK-71 · **Decision:** —
- **Where:** `backend/Api/Sales/Sales.Repository/Migrations/Tenant/`; the audit is at
  `backend/tests/Sales.Api.Tests/SalesQueryFilterTests.cs:218`.
- **State:**
  - 19 tables, including `SalesRegister` (missed once before), `ReminderLogs` and `ReminderProfiles`.
  - `Notification.Worker`'s `PaymentReminderWorker` reads `sal` with **no tenant** and
    `IgnoreQueryFilters()`. Once RLS is on it sees nothing; TK-20 fixes the worker.
- **Sub-tasks:**
  - [x] Write the migration using TK-71's template. The tables: `CreditNoteDetailTaxes,
        CreditNoteDetails, CreditNotes, DeliveryChallanDetailTaxes, DeliveryChallanDetails,
        DeliveryChallans, ErrorLogs, InvoiceDetailTaxes, InvoiceDetails, Invoices, QuoteDetailTaxes,
        QuoteDetails, Quotes, ReminderLogs, ReminderProfiles, SalesOrderDetailTaxes,
        SalesOrderDetails, SalesOrders, SalesRegister`.
  - [x] Apply TK-71's decision to the Forbid probe at `InvoiceService.cs:1562`.
  - [ ] Owner: run the suite from a dropped `SALES_TEST_DB`.
- **Done when:** `sal`'s RLS assertion passes from a dropped database.
- **Notes:**
  - From TK-71 (2026-09-23): the owner decided on **404**. Delete the `IgnoreQueryFilters()` probe
    at `InvoiceService.cs:1562-1563` and return `NotFound()`. Under RLS it can never see another
    branch's row anyway. `AllocationsController` in `acc` is the worked example.
  - From TK-76 (2026-09-23): the delivery challan follows the 404 decision already and has no probe.
    `InvoiceService.ExistsInOtherOrgAsync` is the only one left in `sal`.
  - Done (Claude Opus 5.5, 2026-09-24):
    - `Sales.Repository/Migrations/Tenant/20260924061419_EnableRowLevelSecurity.cs` over all 19
      tables, `SalesRegister` included.
    - `InvoiceService.ExistsInOtherOrgAsync` is gone, with its interface member and all five call
      sites in `InvoicesController` (get, GL preview, update, post, void). Each now answers 404.
      `InvoicesControllerTests` lost its five 403 tests; the 404 tests stand for both cases.
      `sal` has no other probe.
    - `SalesSeeder`'s `IgnoreQueryFilters()` read is scoped to the tenant its endpoint sets, like
      Purchase's. No hand-built `SalesDbContext` anywhere.
    - Applied by hand, as a `NOSUPERUSER NOBYPASSRLS` owner, to a scratch database built from the
      scripted chain: every tenant table ENABLEd, FORCEd and on the NULLIF policy, and a query with
      the tenant set to `''` returned 0 rows without throwing. `has-pending-model-changes` is clean
      and `dotnet build backend/Bill-Book.sln` has 0 warnings.
    - Notification.Worker still reads `sal` with no tenant, so it now sees nothing; TK-20.
    - **Tests written:** `backend/tests/Sales.Api.Tests/SalesRowLevelSecurityTests.cs` (four tests,
      role `sal_rls_probe`); `InvoicesControllerTests.cs` updated.

### TK-04 · RLS for `rpt`: replace the broken policies
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Lanes:** L-RPT · **Depends on:** TK-71 · **Decision:** —
- **Where:**
  - `backend/Api/Reporting/Reporting.Repository/Migrations/Tenant/20260918204348_InitialReportingDbContextSchema.cs:1151-1165`
  - The audit: `backend/tests/Reporting.Api.Tests/ReportingQueryFilterTests.cs:114`.
- **State:**
  - `rpt` **does** have policies, on `Reports`, `ReportViews`, `ReportDetails` and `ErrorLogs`.
  - But they read `current_setting('tenant.orgid', true)`, which nothing sets. They also ignore
    `CustomerId`.
  - With FORCE and a non-superuser connection, those four tables show no rows to anyone.
  - `ReportMasters` and `ReportColumns` are a documented exemption: they hold the imported
    `reports.json` specification and have no tenant columns.
- **Sub-tasks:**
  - [x] Add a new migration that runs `DROP POLICY "TenantPolicy"` on the four tables, then creates
        TK-71's policy on them.
  - [x] Leave `ReportMasters` and `ReportColumns` without a policy, and keep them passed as the
        exemption argument to `RlsAudit.UnprotectedAsync`.
  - [ ] Owner: run the suite from a dropped `REPORTING_TEST_DB`.
- **Done when:** `rpt`'s RLS assertion passes from a dropped database, and a signed-in user sees
  their branch's reports.
- **Notes:** shares `L-RPT` with TK-09, so the two run one after the other.
  - Done (Claude Opus 5.5, 2026-09-24):
    - `Reporting.Repository/Migrations/Tenant/20260924061423_EnableRowLevelSecurity.cs` drops
      `"TenantPolicy"` on the four tables and creates `{table}_tenant_isolation` with TK-71's
      expression. `Down()` restores the old policy as it was.
    - **The snapshot changed with an empty migration.** `ReportingDbContextModelSnapshot` had fallen
      behind the model: `AccountRead.IsSales` and four fixed-asset read models (`acc` tables mapped
      with `ExcludeFromMigrations`) were never snapshotted. None of it is Reporting's schema, so the
      migration's `Up()` is only the RLS block. Worth knowing for TK-09, which shares `L-RPT`.
    - No `IgnoreQueryFilters()` and no hand-built `ReportingDbContext` in Reporting.
    - Applied by hand, as a `NOSUPERUSER NOBYPASSRLS` owner, to a scratch database built from the
      scripted chain: every tenant table ENABLEd, FORCEd and on the NULLIF policy, and a query with
      the tenant set to `''` returned 0 rows without throwing. `has-pending-model-changes` is clean
      and `dotnet build backend/Bill-Book.sln` has 0 warnings.
    - **Test written:** `backend/tests/Reporting.Api.Tests/ReportingRowLevelSecurityTests.cs` (five
      tests, role `rpt_rls_probe`, one asserting no `tenant.orgid` policy survives and the
      specification tables stay unpoliced). The audit call now also exempts `__EFMigrationsHistory`.

### TK-05 · RLS for `prt`: align it with the template
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Lanes:** L-PRT · **Depends on:** TK-71 · **Decision:** —
- **Where:** `backend/Api/Printing/Printing.Repository/Migrations/20260918205343_InitialPrintingSchema.cs:160-171`.
- **State:** `prt` casts `current_setting(…)::uuid` without `NULLIF`, so a request with no tenant
  throws instead of seeing no rows.
- **Sub-tasks:**
  - [x] If TK-71 adopted `NULLIF`, add a migration that recreates both `prt` policies with it.
        Otherwise strike this card.
  - [ ] Owner: run `Printing.Api.Tests` from a dropped database.
- **Done when:** `prt` uses the same expression as the other six schemas.
- **Notes:**
  - Done (Claude Opus 5.5, 2026-09-24):
    - TK-71 adopted `NULLIF`, so `Printing.Repository/Migrations/20260924061426_EnableRowLevelSecurity.cs`
      recreates both policies under the same names with it. `Down()` restores the bare cast.
    - `PrintTemplateSeeder`'s `IgnoreQueryFilters()` read is scoped to the tenant its caller sets;
      Master's bootstrap builds `PrintingDbContext` with `RlsConnectionInterceptor` already.
    - Applied by hand, as a `NOSUPERUSER NOBYPASSRLS` owner, to a scratch database built from the
      scripted chain: every tenant table ENABLEd, FORCEd and on the NULLIF policy, and a query with
      the tenant set to `''` returned 0 rows without throwing. `has-pending-model-changes` is clean
      and `dotnet build backend/Bill-Book.sln` has 0 warnings.
    - **Test written:** `backend/tests/Printing.Api.Tests/PrintingRowLevelSecurityTests.cs` (five
      tests, role `prt_rls_probe`, one asserting both policies carry the NULLIF form).

### TK-06 · Internal endpoints that set no tenant
- [ ] open
- **Lanes:** L-ACC · **Depends on:** — · **Decision:** —
- **Where:**
  - `backend/Api/Accounting/Accounting.Api/Controllers/InternalTaxController.cs`: `GET internal/tax/rates`.
  - `backend/shared/Shared.Kernel/Tax/ITaxRateProvider.cs:80`: the caller, which sends no org.
  - `backend/Api/Accounting/Accounting.Api/Controllers/InternalBankAccountsController.cs`
- **State (checked 2026-09-23):** every other `Internal*Controller` in Accounting copies
  `CustomerId`/`OrgId` from the request into `TenantContext` before it resolves a service. These
  two don't, and the caller authenticates with the internal key only, so `TenantMiddleware`
  fills nothing. The query filter (and now RLS) sees no tenant, so `internal/tax/rates` returns
  an empty list to Sales and Purchase whatever the branch. `HttpTaxRateProvider` caches per org,
  but the request doesn't carry the org. The bank-account pair would be refused writes under RLS,
  though nothing calls it over HTTP today.
- **Sub-tasks:**
  - [ ] Confirm by reading `HttpTaxRateProvider` and its Sales/Purchase callers.
  - [ ] Carry `customerId` and `orgId` on the rates request (query or header) and set the tenant
        the way `InternalLedgerController` does. Touching the kernel client needs `L-KERNEL`.
  - [ ] Delete `InternalBankAccountsController` if it has no caller, or give it the same treatment.
  - [ ] Test: rates for a seeded branch come back non-empty through the controller.
- **Done when:** a Sales invoice resolves its GST rates from Accounting for its own branch.
- **Notes:** found while doing TK-71.
  - From TK-72: the same gap in Master. `InternalContactNamesController` (`internal/contacts/names`)
    reads `con.Contacts` with no tenant set, and `HttpContactNameLookup` in
    `Shared.Kernel/Documents/INameLookup.cs` sends only the internal key. So every document list's
    contact names come back empty. So do the item names: `InternalItemNamesController` in Inventory
    sets no tenant either (confirmed in TK-74). The fix is the same one: carry the org and set the tenant. That touches `L-CON` / `L-INV`
    and `L-KERNEL`.

### TK-07 · Restore the ledger's deferred balance and allocation triggers
- [ ] open
- **Lanes:** L-ACC · **Depends on:** TK-71 · **Decision:** —
- **Where:**
  - What was dropped: `git show 2c5ed6f^:backend/Api/Accounting/Accounting.Repository/Migrations/20260902151402_InitialAccountingSchema.cs`,
    from `acc.assert_ledger_balanced()` onwards, plus any later pre-squash migration that
    `git log -S "CONSTRAINT TRIGGER" 2c5ed6f^ -- backend` names.
  - `backend/Api/Accounting/Accounting.Repository/Migrations/Tenant/`
- **State (checked 2026-09-23):** no migration creates a function or a trigger. `CLAUDE.md`
  described a deferred balance trigger on `acc.Journals` and `acc.JournalLedger`, plus the
  allocation triggers on the money documents, as built. They were in the chains squashed in
  `2c5ed6f` and did not survive. Of the three balance checks, only the domain guard on Post is
  left. The same squash dropped RLS (TK-71).
- **Sub-tasks:**
  - [ ] Recover every `CREATE FUNCTION` / `CREATE CONSTRAINT TRIGGER` block from the pre-squash chain.
  - [ ] Check each against today's columns: the schema changed since, so do not paste blindly.
  - [ ] Add them in a new `acc` migration, with a matching `Down()`.
  - [ ] Test: an unbalanced posted journal is refused at commit; a draft is not.
  - [ ] Owner: run `Accounting.Api.Tests` from a dropped `ACCOUNTING_TEST_DB`.
- **Done when:** a posted, unbalanced journal cannot be committed, and a draft can.
- **Notes:** found while doing TK-71.
  - From TK-77 (2026-09-24): **this number is also held by "Sale challans post to Goods Delivered
    Not Invoiced, once"**, which was added first (with TK-76, commit `cb7da6c`). One of the two
    needs the next unused number; `CLAUDE.md` cites this one.
  - Renumbered from TK-10 on 2026-09-24: two cards had taken that number, and the GDNI card came first. `CLAUDE.md` is updated to cite TK-07.

### TK-08 · Review of the RLS work
- [ ] open
- **Lanes:** L-DOC · **Depends on:** TK-71, TK-72, TK-73, TK-74, TK-02, TK-03, TK-04, TK-05, TK-06 · **Decision:** —
- **State:** two facts decide whether RLS protects anything at all:
  - **A superuser, or any role with `BYPASSRLS`, ignores RLS even when FORCE is set.** The
    development connection strings use `postgres`, a superuser, so the policies have never been
    exercised in development.
  - The interceptor sets the tenant at **session level**, and `CLAUDE.md` says it must be
    transaction-local. The code overwrites both values each time a connection opens, which
    mitigates this; the document and the code still disagree.
- **Sub-tasks:**
  - [ ] Read each migration from TK-72, TK-73, TK-74, TK-02, TK-03, TK-04 and TK-05 against TK-71's template.
  - [ ] Check that the deployed application connects as a role that is neither a superuser nor
        `BYPASSRLS` (`deploy/azure`, and each service's connection string).
  - [ ] Test: one test that connects as a non-superuser role (created in the fixture) and reads
        another branch's rows. It must get zero.
  - [ ] Reconcile the session-level vs transaction-local statement. Either change the interceptor
        or rewrite the rule in `CLAUDE.md`, and write the reason down.
  - [ ] Rewrite the FORCE bullet in `CLAUDE.md`'s standing caveats to say what is true now.
  - [ ] Owner: drop all seven test databases and run the whole backend suite, expecting 0 RLS
        failures. Then drop one policy by hand and watch it go red.
- **Done when:** a non-superuser connection can't read another branch's rows in any tenant schema.
- **Notes:**
  - **Decided by the owner (2026-09-24): transaction-local.** Change `RlsConnectionInterceptor` to `set_config(…, true)` inside each transaction (the reliability filter's scope), and keep `CLAUDE.md`'s rule as written. Reads outside an explicit transaction need one opened for them, or the setting won't hold — check every read path.
  - Dependency on TK-06 added 2026-09-24: internal endpoints that set no tenant break under RLS; the review needs them fixed.

### TK-09 · `ReportLayerCertificationTests`: likely already fixed
- [ ] open
- **Lanes:** L-RPT · **Depends on:** — · **Decision:** —
- **Where:**
  - `backend/tests/Reporting.Api.Tests/ReportLayerCertificationTests.cs:97`, which expects 48.
  - `backend/tests/Reporting.Api.Tests/ReportSourceTests.cs:78`: the `Sources` list, with 48 entries.
  - `backend/Api/Reporting/Reporting.Api/Program.cs`: 48 `AddScoped<IReportSource, …>` lines.
  - `backend/Api/Reporting/Reporting.Repository/SeedData/ReportCatalogSeeder.cs`
- **State:** commit `3dad51f` (19 September, "complete all 48 unblocked reports") came after the
  18 September count of 4 failures. A static count now agrees across three of the four layers.
- **Sub-tasks:**
  - [ ] Check the fourth layer: every `ReportKey` in `ReportCatalogSeeder.Catalog` has a source,
        and every source has a catalog entry with matching column keys.
  - [ ] If they all agree, hand the card to the owner to run.
  - [ ] Otherwise, fix whichever side is stale. Don't just change the expected number.
  - [ ] Owner: run `Reporting.Api.Tests` from a dropped `REPORTING_TEST_DB`.
- **Done when:** `ReportLayerCertificationTests` has 0 failures.
- **Notes:**
  - TK-83 moved the expected count at `ReportLayerCertificationTests.cs:97` from 48 to 52, and added four sources to the `Sources` list, `Program.cs` and the seeder. There are 52 of each now.

### B · Money posted right: cost of sales and fixed assets

Postings that are wrong today or post nothing. TK-10 comes before POS (TK-39), which reuses the invoice's posting.

### TK-10 · Sale challans post to Goods Delivered Not Invoiced, once
- [ ] open
- **Lanes:** L-ACC, L-INV, L-SAL · **Depends on:** TK-76 · **Decision:** —
- **Where:**
  - `backend/Api/Accounting/Accounting.Repository/SeedData/ChartOfAccountsSeed.cs`: GRNI is 2150; GDNI is missing.
  - `backend/Api/Inventory/Inventory.Api/Services/StockLedgerMapping.cs:86-87`: every sourced `Issue`
    posts Dr COGS / Cr Inventory, whatever document it came from.
  - `backend/Api/Sales/Sales.Api/Services/InvoiceService.cs`: the challan branch near 1192, and
    the COGS legs near 1365 (`GdniAccount` when the invoice names a challan).
  - `docs/Modules.md` §9, "What a delivery challan posts": the recommendation this card builds.
- **State (2026-09-23):**
  - A posted challan's cost reaches the ledger once, through the costing worker, as Dr COGS /
    Cr Inventory **at dispatch**. That is cost with no revenue against it until the invoice.
  - An invoice against a challan posts Dr COGS / Cr GDNI at the challan lines' `UnitCost`, which is
    the request path's provisional figure (TK-85) and names an account that is not seeded, so it
    is refused whenever the value is non-zero.
  - **A direct invoice has the same double posting:** it writes its own Dr COGS / Cr Inventory
    (COGS leg at detail 0, CONTROL leg), and the worker writes the COGS legs per line for the same
    issue. Both stand.
- **Sub-tasks:**
  - [ ] Seed `Goods Delivered Not Invoiced` (Asset, off the manual-journal picker like GRNI), with a
        `SystemAccount` value, and backfill existing branches through the seeder's idempotent path.
  - [ ] Decide who posts cost of sale — the worker (at recosted value) or the document. `docs/Modules.md`
        §7 says Inventory, asynchronously. Write the answer under Notes before changing either side.
  - [ ] If the worker: map an `Issue` sourced from a `Sale` challan to Dr GDNI / Cr Inventory, and
        post nothing for job work, approval, branch transfer or sample. The worker can't see
        `ChallanType`; carry it on the movement (for example, a distinct `SourceType` or a flag on
        `IssueStockRequest`) rather than reading `sal`.
  - [ ] The invoice against a challan then clears GDNI: Dr COGS / Cr GDNI at the challan movements'
        settled cost, never the provisional one.
  - [ ] Remove whichever of the invoice's own COGS legs and the worker's is the duplicate.
  - [ ] Test: challan then invoice leaves GDNI at zero, Inventory credited once, COGS debited once.
  - [ ] Test: a job-work challan posts nothing.
- **Done when:** a sale challan and the invoice raised from it leave Inventory reduced once,
  GDNI at zero, and one cost-of-sales debit.
- **Notes:** raised from TK-76 by the owner's decision of 2026-09-23.
  - From TK-77 (2026-09-24): **another card is also numbered TK-10** (restoring the ledger's
    triggers, cited in `CLAUDE.md`). This one came first. Other facts for this card:
    `SalesAccountNameTests` allows "Goods Delivered Not Invoiced" and "Cash" to be unseeded and
    names this card; seed GDNI and take it off that list. The credit note no longer posts its own
    COGS legs (the worker posts the sales return), which is the arrangement this card is deciding
    for the invoice.
  - **Duplicate number resolved (2026-09-24):** this card keeps TK-10; the trigger card is now TK-07.
  - **Who posts cost of sale — decided by the owner, 2026-09-24:** the document posts a **provisional** cost-of-sale entry when it is posted (at the request path's cost), and flags the item for the worker. `CostingEngine.Worker` recalculates, then **corrects the ledger to the settled value**. So: keep the document's COGS legs as provisional, remove the worker's *duplicate* first posting, and make the worker post only the difference (or replace the provisional rows) after recalculation. A sale challan's provisional entry is Dr GDNI / Cr Inventory; the invoice against it moves GDNI to COGS.

### TK-11 · Fixed assets: a service layer, guards and tests
- [ ] open
- **Lanes:** L-ACC · **Depends on:** — · **Decision:** —
- **Where:**
  - `backend/Api/Accounting/Accounting.Api/Controllers/FixedAssetsController.cs`
  - `Services/DepreciationService.cs`
  - `Accounting.Entity/Enums/{DepreciationMethod,DepreciationScheduleType,AssetTransactionType}.cs`
  - `backend/tests/Accounting.Api.Tests/FixedAssetPostingTests.cs`
  - `frontend/libs/accounting/accounting-ui/src/lib/fixed-assets/`
- **State:** the register is **built**:
  - the tables: `acc.FixedAssets`, `FixedAssetCategories`, `DepreciationSchedules` (Books and Tax)
    and `AssetTransactions`;
  - the methods: `StraightLine` and `WrittenDownValue`;
  - the routes: list, register, capitalise, dispose and a depreciation run;
  - two pages.
  
  The controller breaks house rules in several places:
  - it writes through `_db` directly, with no service;
  - no action takes a `CancellationToken`;
  - `dispose` uses `{id}` without `:long`, and returns `NotFound()` where `Forbid()` is required;
  - `dispose` and `depreciation-run` have no `[PermissionAction]`.
- **Sub-tasks:**
  - [ ] Move register, capitalise and dispose into a `FixedAssetService` that returns outcomes, and
        let the controller only map results.
  - [ ] Add a `CancellationToken` to every action.
  - [ ] Change `dispose` to `{id:long}`, and return `Forbid()` for another branch's asset.
  - [ ] Add `[PermissionAction("approve")]` to `dispose` and `depreciation-run`.
  - [ ] Make running depreciation twice for one period a no-op, if it isn't already
        (`DepreciationService.cs:30` says it checks existing transactions).
  - [ ] Test: straight-line and WDV, one month each.
  - [ ] Test: a second run in the same month posts nothing.
  - [ ] Test: another branch's asset gets `Forbid()`.
- **Done when:** the register follows the house rules, and depreciation is idempotent per period.
- **Notes:**
  - In code, D-08 is answered with both Books and Tax schedules; the owner still has to confirm it.
  - From TK-75: the two pages in `accounting-ui/src/lib/fixed-assets/` are neither exported from
    the lib's `index.ts` nor routed anywhere in `apps/web`, so no user can reach them.

### TK-12 · Fixed assets: capitalisation and disposal postings
- [ ] open
- **Lanes:** L-ACC, L-PUR · **Depends on:** TK-11 · **Decision:** D-19, D-20
- **Where:**
  - `FixedAssetsController.cs:95-170`: the doc comments record both open questions.
  - `backend/Api/Purchase/Purchase.Api/Services/BillService.cs:336, 658`: a `Capital` line posts to
    one shared `"Fixed Asset"` account.
- **State:**
  - A bill's capital line posts to one shared Fixed Asset account, and creates no register row.
  - `capitalize` is called by hand, and moves nothing in the ledger.
  - `dispose` records the sale amount and posts nothing.
- **Sub-tasks:**
  - [ ] Once D-19 is answered: when a bill posts, its capital line creates the register row. Purchase
        calls a new Accounting internal endpoint; it never touches `acc` directly (hard rule 8).
        The posting reclassifies the shared Fixed Asset account to the category's account. A
        migrated asset (no `PurchaseBillId`) debits against Opening Balance Equity.
  - [ ] Once D-20 is answered: add `ProceedsBankAccountId` to `DisposeAssetRequest`, then post four
        legs:
    - the accumulated depreciation written back;
    - the asset removed at cost;
    - the proceeds received;
    - the gain or loss.
  - [ ] Test: bill → register row → one month's depreciation → disposal, with each step's journal
        balanced, and the asset and accumulated-depreciation accounts back at zero afterwards.
- **Done when:** an asset bought on a bill depreciates, is disposed of, and each step posts a
  balanced journal.
- **Notes:**
  - D-19 answered (2026-09-24): reclassify to the category's account; a migrated asset debits against Opening Balance Equity.
  - D-20 answered (2026-09-24): **support both** disposal paths — proceeds to a bank or cash account chosen on the disposal, **or** a sales invoice to the buyer (Dr the buyer's receivable). The disposal request carries one of `ProceedsBankAccountId` or `SalesInvoiceId`.
  - D-09 answered: no new transaction codes; acquisition rides `BIL`/`OPB`, disposal `INV`/`JRN`.

### C · Phase 1: finish what's in flight

### TK-13 · Platform operators: `IsPlatformOperator` on the user (D-01)
- [ ] open
- **Lanes:** L-MST · **Depends on:** TK-70 · **Decision:** D-01 (answered)
- **Where:** `backend/Api/Master/Master.Entity/TableEntities/User.cs`, `Master.Api/Services/JwtTokenService.cs`, `Master.Api/Services/DatabaseMigrationService.cs` (`BootstrapFirstOperatorAsync`), `frontend/apps/admin`.
- **State:** `platform.*` is seeded into the permission catalogue and `apps/admin` checks for it, but nothing grants it, so nobody can sign in to `apps/admin`.
- **Sub-tasks:**
  - [ ] Add `IsPlatformOperator bool` (default false) to `User`, with an admin migration; run `has-pending-model-changes`.
  - [ ] `JwtTokenService` adds every `platform.*` permission to the token when the flag is true — never through a role.
  - [ ] Set the flag only from bootstrap configuration (`Bootstrap:OperatorEmails`) or from an existing operator through a `[RequirePermission("platform.edit")]` endpoint. No tenant screen can set it.
  - [ ] Test: an Owner of a customer never gets `platform.*`; an operator does; a non-operator calling the grant endpoint gets 403.
- **Done when:** an operator signs in to `apps/admin` and sees the customer list; no tenant user can.
- **Notes:**

### TK-14 · Item search: barcode and paging
- [ ] open
- **Lanes:** L-INV · **Depends on:** — · **Decision:** —
- **Where:**
  - `backend/Api/Inventory/Inventory.Api/Services/ItemService.cs:36-78` (`ListAsync`).
  - `backend/Api/Inventory/Inventory.Api/Controllers/ItemsController.cs:26`.
- **State:** `GET /api/items?search=` matches name and code with `ILike` and returns at most 500
  rows. It doesn't match barcodes (`inv.ItemBarcodes`) and can't page, and the POS till needs both.
- **Sub-tasks:**
  - [ ] Also match `ItemBarcodes.Barcode`, exactly. A scanned code should rank first.
  - [ ] Add `skip` and `take`, clamped the way `SalesOrderService`'s list does, and return a total.
        Keep the old response shape when neither is passed, so existing callers don't change.
  - [ ] Test: an exact barcode returns that one item.
  - [ ] Test: paging returns the right total.
  - [ ] Test: another branch's item never appears.
  - [ ] Owner: run `Inventory.Api.Tests`.
- **Done when:** a scanned barcode finds its item through `GET /api/items`.
- **Notes:**

### TK-15 · Item and customer pickers on the sales forms
- [ ] open
- **Lanes:** L-SAL-UI · **Depends on:** — · **Decision:** —
- **Where:**
  - The pattern to copy: `frontend/libs/purchase/purchase-core/src/lib/purchase-lookup.service.ts`
    (`vendors()` at 107, `items()` at 191).
  - The picker logic: `frontend/libs/purchase/purchase-ui/src/lib/bill-form/bill-form.page.ts:238-300`.
  - The shared dialog: `frontend/libs/shared/ui-components/src/lib/lookup-dialog/` (`bb-lookup-dialog`, `LookupRow`).
  - The forms: `frontend/libs/sales/sales-ui/src/lib/{quote-form,sales-order-form,invoice-form,delivery-challan-form,credit-note-form}/*.component.html`.
- **State:**
  - Purchase already has working pickers.
  - All five sales forms take `contactId` and `itemId` as numeric inputs (e.g.
    `invoice-form.component.html:171`).
  - The backend needs nothing new: `/api/contacts?search=&role=customer` and `/api/items?search=`
    already exist.
- **Sub-tasks:**
  - [ ] Add `sales-lookup.service.ts` to `libs/sales/sales-core`, with `customers(search)` and
        `items(search)`. Mirror `PurchaseLookupService`.
  - [ ] In each of the five forms:
    - replace the numeric inputs with a button that opens `bb-lookup-dialog`;
    - add `picker` and `pickerRows` signals, as in `bill-form.page.ts`;
    - show the chosen name, and store the id.
  - [ ] Keep the `contactLabel` or `itemLabel` that edit mode shows when it loads a saved document.
  - [ ] Test: a `sales-lookup.service.spec.ts` that asserts the URLs and the mapping.
  - [ ] Update the docs pages for the five sales screens, and add a release-notes bullet.
  - [ ] `npm run lint`, the typecheck and `nx build web` are all clean.
  - [ ] Owner: `npm run test`, then pick a customer and an item on each form at 360px.
- **Done when:** every sales form picks its customer and items by name.
- **Notes:** TK-14 later improves item search (barcode); this card doesn't wait for it.
  - TK-79 (2026-09-23) built the till's own `PosLookupService` in
    `frontend/apps/desktop/src/app/pos-terminal/pos-lookup.service.ts`, because this card was open
    when TK-79 ran. Once `SalesLookupService` exists, move the till's `customers()` and `items()`
    onto it and keep only the till's own lookups (walk-in, item detail, sales rates, branch).

### TK-16 · Contact picker on the support ticket form
- [ ] open
- **Lanes:** L-CUSTOMER-UI · **Depends on:** — · **Decision:** —
- **Where:** `frontend/libs/customer/customer-ui/src/lib/tickets/ticket-form.component.html`
  (a numeric `contactId`).
- **Sub-tasks:**
  - [ ] Replace the numeric field with `bb-lookup-dialog` over `/api/contacts?search=`, the way TK-15 does.
  - [ ] Update the docs page.
  - [ ] Lint, typecheck and build are clean.
- **Done when:** a ticket is raised by picking the contact by name.
- **Notes:**

### TK-17 · Seed a `WALKIN` contact per branch
- [ ] open
- **Lanes:** L-CON · **Depends on:** — · **Decision:** owner, 2026-09-24 (see TK-40)
- **Where:** `backend/Api/Master/Master.Api/Controllers/InternalSeedController.cs` (Master's own branch seed), the contact service's create path.
- **Sub-tasks:**
  - [ ] Seed one contact per branch: code `WALKIN`, name "Walk-in Customer", role customer, no GSTIN (so B2C place of supply is the branch's state).
  - [ ] Idempotent: seeding twice leaves one; the code can't be reused by a user-created contact.
  - [ ] Test: a new branch has exactly one `WALKIN`, and `pos-lookup.service`'s exact-code lookup finds it.
- **Done when:** the till's default customer resolves on a new branch with no setup.
- **Notes:**

### TK-18 · Customer module seed data (stage C4)
- [ ] open
- **Lanes:** L-CUS · **Depends on:** — · **Decision:** D-18
- **Where:**
  - `backend/shared/Shared.Kernel/Customer/Enums.cs`: `LeadSource`, `LeadStatus`, `TicketStatus`, `TicketPriority`.
  - `backend/Api/Customer/Customer.Api/Controllers/TicketsController.cs:101`: SLA hours hard-coded
    per priority.
- **State:** every lead and ticket list is an enum, so there's no reference data left to seed. The
  only hard-coded business data is the SLA table (Urgent 2 h, High 8 h, Medium 2 days, Low 7
  days). No section in `docs/Modules.md` defines C4.
- **Sub-tasks** (if D-18 answers "per-branch SLA policy"):
  - [ ] `cus.SlaPolicies` (`Priority`, `ResponseHours`, `ResolutionHours`), with its migration and
        TK-71's RLS block.
  - [ ] Seed four rows per branch through a new `POST internal/seed/organization` on Customer, and
        add `"Customer"` to `TenantSeeder.Services`. That needs `L-MST` too.
  - [ ] Replace the `switch` at `TicketsController.cs:101` with a lookup.
  - [ ] Test: seeding twice adds nothing, and a ticket's `SlaDueAt` follows its branch's policy.
- **Done when:** decided by D-18.
- **Notes:**
  - D-18 answered (2026-09-24): build the per-branch `cus.SlaPolicies` table exactly as the sub-tasks above say.

### TK-19 · Notification.Worker takes over email from Master
- [ ] open
- **Lanes:** L-NTF, L-MST · **Depends on:** TK-70 · **Decision:** —
- **Where:**
  - `backend/Api/Master/Master.Api/Services/EmailQueue.cs`: `IEmailQueue`, `InProcessEmailQueue`,
    `QueuedEmailSender` and `EmailDispatchWorker`.
  - `SmtpEmailSender.cs`, and `Program.cs:131-136`.
  - The callers: `AuthService.cs:421` (OTP) and `UserService.cs:217` (invitation).
  - `backend/worker/Notification.Worker/Program.cs`
- **State:**
  - Master queues email in memory and drains it with its own background worker; a restart loses
    the queue.
  - `IEventPublisher` sends to Service Bus when `ServiceBus:Namespace` is set, and only logs
    otherwise. Nothing consumes an event anywhere.
- **Sub-tasks:**
  - [ ] Define an `EmailRequested` event in `Shared.Kernel`, with `MessageId` and the fields of
        `EmailMessage`. This takes `L-KERNEL` for that commit.
  - [ ] Replace `QueuedEmailSender` with a sender that publishes `EmailRequested` through
        `IEventPublisher`.
  - [ ] Keep the in-process path when Service Bus isn't configured, so local development still
        sends mail.
  - [ ] Add a Service Bus consumer to `Notification.Worker` that sends through `SmtpEmailSender`,
        which moves or is shared.
  - [ ] Dedupe on `MessageId`, since delivery is at least once. A `ntf.ProcessedMessages` table
        (its own migration, with RLS) or an idempotency key is enough.
  - [ ] SMTP settings are per customer (`mst.SmtpSettings`). The worker reads them through Master's
        API, not its `DbContext` (hard rule 8).
  - [ ] Test: a redelivered message sends once.
  - [ ] Test: an OTP email still arrives with Service Bus unset.
  - [ ] Owner: send an invitation end to end.
- **Done when:** an invitation email is sent by the worker, and a redelivered message sends once.
- **Notes:** ask the owner before moving anything beyond email.

### TK-20 · `PaymentReminderWorker` sends nothing, and reads across tenants
- [ ] open
- **Lanes:** L-NTF · **Depends on:** TK-19 · **Decision:** —
- **Where:** `backend/worker/Notification.Worker/PaymentReminderWorker.cs:39-100` and `Program.cs`.
- **State:**
  - It loads `ReminderProfiles` and overdue `Invoices` with `IgnoreQueryFilters()` and no tenant,
    then writes a `ReminderLog` row, **but never sends an email**.
  - Once RLS is on (TK-03), it will see no rows at all.
  - It runs once every 24 hours, from whenever the process started.
- **Sub-tasks:**
  - [ ] Iterate branches the way `CostingEngine.Worker/Consumers/CostingWorker.cs:140-150` does:
        list them, then set `TenantContext.CustomerId` and `OrgId` per branch in a new scope.
  - [ ] Drop `IgnoreQueryFilters()`.
  - [ ] Settle the invoice through Accounting's settlement API, so a paid invoice gets no reminder.
  - [ ] Send the reminder through the email path from TK-19, and write `ReminderLog` in the same
        unit of work.
  - [ ] Replace the 7-day constant with a field on the profile, if the entity has one. Otherwise
        record the gap under Notes.
  - [ ] Test: a paid invoice gets no reminder.
  - [ ] Test: a second run on the same day sends nothing.
  - [ ] Test: branch A's profile never reminds branch B's invoice.
- **Done when:** an overdue, unpaid invoice produces exactly one email per reminder window.
- **Notes:**

### TK-21 · A blank optional phone is NULL everywhere (D-04)
- [ ] open
- **Lanes:** L-CON, L-MST, L-INV, L-CUS · **Depends on:** — · **Decision:** D-04 (answered)
- **Where:** the phone columns: `con.ContactAddresses` and `con.ContactPersons` (`PhoneNumber`, `MobileNumber`), `mst.Users.MobileNumber`, `mst.Organizations` (`PhoneNumber`, `MobileNumber`), `inv.Warehouses` (`PhoneNumber`, `MobileNumber`), `cus.Leads.Phone`. No shared normaliser exists.
- **Sub-tasks:**
  - [ ] Add one `PhoneNumbers.NormalizeOptional(string?)` in `Shared.Kernel` (trim; blank → null; keep the leading `+` rule from `CLAUDE.md`). Takes `L-KERNEL` for that commit.
  - [ ] Call it in every service that saves those columns.
  - [ ] One data migration per schema turning `''` into NULL in those columns.
  - [ ] Test: the normaliser (blank, spaces, `+91…`, local); one save per service stores NULL for blank.
- **Done when:** no phone column in any schema holds an empty string.
- **Notes:** the lanes are many but each edit is small; release each lane as soon as its commit lands.

### D · Phase 2

### TK-22 · Document archive: PDF/A, every document, a download link
- [ ] open
- **Lanes:** L-SAL, L-KERNEL · **Depends on:** — · **Decision:** D-11
- **Where:**
  - `backend/Api/Sales/Sales.Api/Services/InvoiceService.cs:1440-1460`
  - `Services/Pdf/PdfSharpInvoiceRenderer.cs` and `IInvoicePdfRenderer.cs`
  - `backend/shared/Shared.Kernel/Storage/StorageKey.cs` (`DocumentKey`)
  - `backend/Directory.Packages.props:40` (`PDFsharp 6.1.1`)
- **State:**
  - Posting an invoice already renders a PDF with PDFsharp and saves it to storage at
    `StorageKey.DocumentKey(scope, "invoices", "{id}.pdf")`, using `FileWriteMode.Replace`.
  - It is **not PDF/A**, and it uses its own layout rather than the print template.
  - It covers invoices only, and no endpoint returns the file.
- **Sub-tasks:**
  - [ ] Once D-11 confirms PDFsharp, emit PDF/A-2b. Check PDFsharp 6.1.1's PDF/A support, and if
        it has none, record that under Notes.
  - [ ] Add `GET api/sales/invoices/{id:long}/pdf`, which streams the stored file through
        `IFileStorage` (or returns a signed link).
  - [ ] Archive credit notes and delivery challans the same way.
  - [ ] Once TK-81 lands, render from the template instead of the fixed layout.
  - [ ] Test: posting archives exactly one file.
  - [ ] Test: a re-post doesn't fail on the leftover file.
  - [ ] Test: another branch's PDF gets `Forbid()`.
- **Done when:** a posted invoice's PDF/A file downloads from the invoice screen.
- **Notes:**
  - D-11 answered (2026-09-24): PDFsharp. Replace every mention of Syncfusion as the intended library (CLAUDE.md is done by TK-86).

### TK-23 · Date input that follows the branch's format
- [ ] open
- **Lanes:** L-UI · **Depends on:** — · **Decision:** —
- **Where:**
  - `frontend/libs/shared/ui-components/src/lib/date-input/date-input.component.ts`: a native `<input type="date">`.
  - `frontend/libs/shared/currency-format/src/lib/format-settings.service.ts:55` (`formatDate`).
  - 26 templates use `bb-date-input`.
- **Sub-tasks:**
  - [ ] Build a text input with a calendar popover that displays
        `FormatSettingsService.settings().datePattern`, parses typed input in that pattern, and
        keeps the value ISO (`yyyy-MM-dd`) so all 26 callers stay unchanged.
  - [ ] Make it keyboard- and screen-reader-accessible, and turn the popover into a full-screen
        sheet at 360px.
  - [ ] Show the proposal to the owner before swapping it in, since it changes every date field.
  - [ ] Test: parse and format round trips for `dd/MM/yyyy`, `MM/dd/yyyy` and `yyyy-MM-dd`.
  - [ ] Owner: check it with Playwright on a `dd/MM/yyyy` branch.
- **Done when:** a branch on `dd/MM/yyyy` sees that format in every date field.
- **Notes:**

### TK-24 · `rat` schema: exchange and metal rate history
- [ ] open
- **Lanes:** L-MST · **Depends on:** TK-70 · **Decision:** —
- **Where:**
  - `CLAUDE.md` Schemas: "Master database: `mst`, `rat`".
  - `backend/worker/RateSync.Worker/Program.cs` (an empty host).
- **State:** no `rat` table exists anywhere, so TK-25 and TK-26 have nowhere to write.
- **Sub-tasks:**
  - [ ] Map two tables on `AdminDbContext`, with schema `rat`:
    - `ExchangeRate`: `ExchangeRateId long`, `FromCurrencyCode string(3)`, `ToCurrencyCode string(3)`,
      `RateDate DateOnly`, `Rate decimal(18,8)`, `Source enum`;
    - `MetalRate`: `MetalRateId long`, `Metal enum`, `PurityCode string(10)`, `RateDate DateOnly`,
      `RatePerGram decimal(18,4)`, `Source enum`.
  - [ ] Put a unique index on (currency pair or metal and purity, `RateDate`, `Source`). Both are
        global rows with no tenant columns, like the rest of the master database.
  - [ ] Add `GET api/rates/exchange?from=&to=&on=` and `GET api/rates/metal?metal=&purity=&on=`,
        which return the latest rate on or before a date. A document stores the rate as a
        snapshot; it never looks it up live.
  - [ ] Add a manual entry endpoint and page, so rates can be entered while D-03 and D-14 are open.
  - [ ] Test: the on-or-before lookup, and the unique index.
- **Done when:** a rate entered for a date is returned for that date and every later date until a
  newer one is entered.
- **Notes:** this would be recreated inside the TK-70 squash if both are done together. Do TK-70 first.

### TK-25 · RateSync.Worker: metals (IBJA)
- [ ] open
- **Lanes:** L-RATE · **Depends on:** TK-24 · **Decision:** D-14
- **Where:** copy `backend/worker/CostingEngine.Worker/Program.cs`, as the note in
  `RateSync.Worker/Program.cs` asks.
- **Sub-tasks:**
  - [ ] Write an IBJA client, with its key from `ISecretStore`.
  - [ ] Run on a daily schedule and upsert `rat.MetalRates` per purity.
  - [ ] Retry with backoff, and make a second run on the same day a no-op.
  - [ ] Test: parse a recorded IBJA response.
  - [ ] Test: a second run on the same day writes nothing.
- **Done when:** the day's metal rates appear in `rat` with their date.
- **Notes:**
  - D-14 answered (2026-09-24): both manual entry (TK-24) and the IBJA API. **Ask the owner for the IBJA credentials before starting**; store them through `ISecretStore`, never in `appsettings`.

### TK-26 · RateSync.Worker: currency (RBI)
- [ ] open
- **Lanes:** L-RATE · **Depends on:** TK-24 · **Decision:** D-03
- **Sub-tasks:** follow D-03's answer (scraping, a paid wrapper, or manual entry through TK-24's page).
  - [ ] Upsert `rat.ExchangeRates` against INR.
  - [ ] Make it idempotent per day.
- **Done when:** the day's exchange rates appear in `rat` with their date.
- **Notes:**
  - D-03 answered (2026-09-24): manual entry (TK-24's page) plus a daily scrape of RBI's reference-rate page. Keep the parser isolated and tested against a saved copy of the page, and let a failed scrape log to `ErrorLogs` with `FollowUpStatus = Open` rather than write a rate.

### TK-27 · Production databases are created by infrastructure (D-02)
- [ ] open
- **Lanes:** L-MST, L-DEPS · **Depends on:** TK-70 · **Decision:** D-02 (answered)
- **Where:** `backend/Api/Master/Master.Api/Services/DatabaseMigrationService.cs:50,140` (`EnsureDatabaseExistsAsync`, which issues `CREATE DATABASE` at 263), `deploy/azure/` (Bicep).
- **Sub-tasks:**
  - [ ] Call `EnsureDatabaseExistsAsync` only when the environment is Development; elsewhere, a missing database fails startup with a clear message.
  - [ ] Declare the admin database and the first tenant shard (`IN000001`) as Bicep resources on the flexible server.
  - [ ] Drop `CREATEDB` from the application role in deployment docs.
  - [ ] Note for TK-46: provisioning a new shard in production must then go through infrastructure (or an operator action), not the app.
- **Done when:** a Production start against an existing server needs no `CREATEDB`, and a Development start still creates its databases.
- **Notes:**

### TK-28 · Settings: one Nx lib per sub-screen (D-05)
- [ ] open
- **Lanes:** L-MASTER-UI, L-DEPS, L-WEB · **Depends on:** — · **Decision:** D-05 (answered)
- **Where:** `frontend/libs/master/master-ui/src/lib/` — today one lib holding `api-clients`, `configurations`, `org-currencies`, `organization-settings`, `organizations`, `print-templates`, `roles`, `smtp-settings`, `users` beside contacts and HSN/SAC.
- **Sub-tasks:**
  - [ ] Agree the target layout before moving code (for example `libs/settings/{users,roles,organizations,organization-settings,currencies,configuration,smtp,api-clients,print-templates}`), and write it in `docs/Modules.md`'s shared-master-pages table, since H0 mounts these pages from every app.
  - [ ] Generate the libs, move each folder, add a path alias per lib in `tsconfig.base.json`, update imports and routes.
  - [ ] Lint, typecheck and every app build are clean.
- **Done when:** each settings screen is its own lib and every app still builds and routes to it.
- **Notes:** do this before TK-44 (H0.3), which mounts the shared pages in several apps.

### TK-29 · API clients get per-action permissions through their role (D-07)
- [ ] open
- **Lanes:** L-MST, L-KERNEL · **Depends on:** — · **Decision:** D-07 (answered)
- **Where:** `backend/Api/Master/Master.Entity/TableEntities/ApiClient.cs` (`RoleId`, stored but unused), `Master.Api/Controllers/InternalApiKeysController.cs` (validation), `backend/shared/Shared.Kernel/Security/ApiKeyAuthenticationHandler.cs:43-47`.
- **State:** a validated API key produces `customer_id`, `org_id`, `sub`, `name` and `role = ApiClient` — **no `permission` claims**, so every `[RequireModulePermission]` endpoint refuses it.
- **Sub-tasks:**
  - [ ] Validation returns the permission codes of the client's `RoleId`; the handler adds one `permission` claim per code.
  - [ ] The API-clients page lets the owner pick the role, and a role holding `platform.*` can never be chosen.
  - [ ] Test: a key whose role has `sales.view` can list invoices and gets 403 on posting one.
- **Done when:** an API client can do exactly what its role's `{module}.{action}` permissions allow.
- **Notes:**

### TK-30 · Seeds and menus follow the branch's trade (D-10)
- [ ] open
- **Lanes:** L-MST, L-INV, L-MASTER-UI · **Depends on:** — · **Decision:** D-10 (answered)
- **Where:** `backend/Api/Master/Master.Entity/TableEntities/Organization.cs:34` (`Vertical`), `Master.Entity/Enums/Vertical.cs`, `Master.Api/Services/TenantSeeder.cs` (`ReadVerticalAsync`), `backend/Api/Inventory/Inventory.Api/Controllers/InternalSeedController.cs:58`, `Master.Api/Services/MenuService.cs`, `docs/Modules.md` §5.14.
- **State:** the trade exists and seeding already receives it. `OrganizationModels.cs:81,208` carries it as a **string** (hard rule 7 wants the enum). Menus ignore it.
- **Sub-tasks:**
  - [ ] Change the request and response models to the `Vertical` enum.
  - [ ] List which seeds and menus belong to Pharma and Jewellery only (drug schedules, metal purities, making charges…), write the list in §5.14, then filter seeding and `MenuService` by it.
  - [ ] Changing a branch's trade later seeds what the new trade needs (idempotently) and hides the other's menus; it never deletes data.
  - [ ] Test: a General branch gets no metal purities; switching it to Jewellery seeds them once.
- **Done when:** a new branch shows only its trade's menus and master data.
- **Notes:**

### E · Approved designs (documents only)

All six have the owner's go-ahead (D-17, 2026-09-24). The card is done when the
design section exists under `docs/` and new cards for it are added to this queue.

All of these share `L-DOC`, so they run one at a time, alongside code work in other lanes. **E-invoicing comes first** because GST law requires it above the turnover threshold; the portal design comes next because the School parent portal (TK-69) builds on it.

### TK-31 · Design: e-invoicing and e-way bill
- [ ] open · **Lanes:** L-DOC · **Decision:** D-17 (answered: go-ahead)
- **Notes:** delivery challans already carry `EwayBillNo` and `EwayBillDate`.

### TK-32 · Design: `apps/portal`, the next screens
- [ ] open · **Lanes:** L-DOC · **Decision:** D-16 (answered)
- **State:** `apps/portal` has a dashboard and a statement list over real endpoints.
- **Notes:**
  - D-16 answered (2026-09-24): design these screens: overall outstanding and overall trade value on the dashboard; invoice list with PDF download (TK-22); online payment; quotes to accept or reject; support tickets (Customer module). Every portal route takes `[RequirePortalAccess]`.

### TK-33 · Design: workflow approvals
- [ ] open · **Lanes:** L-DOC · **Decision:** D-17 (answered: go-ahead)
- **Notes:** TK-49 builds an approval engine for HRMS. Design this on top of it rather than as a second engine.

### TK-34 · Design: project accounting
- [ ] open · **Lanes:** L-DOC · **Decision:** D-17 (answered: go-ahead)

### TK-35 · Design: budgeting
- [ ] open · **Lanes:** L-DOC · **Decision:** D-17 (answered: go-ahead)

### TK-36 · Design: custom fields and custom reports
- [ ] open · **Lanes:** L-DOC · **Decision:** D-17 (answered: go-ahead)

### TK-37 · Design: compliance bundle
- [ ] open · **Lanes:** L-DOC · **Decision:** D-17 (answered: go-ahead)

---

### TK-38 · Design: CRM campaigns and marketing automation (D-06)
- [ ] open · **Lanes:** L-DOC · **Decision:** D-06 (answered: in v1)
- **Sub-tasks:**
  - [ ] Write the design under the Customer (`cus`) section of `docs/Modules.md`: campaigns, audiences built from leads and contacts, scheduled sends through Notification (TK-19), unsubscribe handling, and what a campaign reports.
  - [ ] Add build cards for it to this queue.
- **Done when:** the design is in `docs/Modules.md` and its cards are queued.

### F · Phase 3: POS

### TK-39 · POS till API (T7.1)
- [ ] open
- **Lanes:** L-SAL · **Depends on:** TK-78, TK-14, TK-10 · **Decision:** —
- **Where:** `InvoiceService.cs` (create and post) and `InventoryClient.IssueAsync`.
- **State:** a POS sale is an `sal.Invoices` row with `TransactionTypeCode = 'POS'`, and the invoice
  already has the five POS columns, so no new table is needed.
- **Sub-tasks:**
  - [ ] Add `POST api/sales/pos/sales`, which creates and posts an invoice in one call, with
        tender lines (cash, card or UPI) that are received against the invoice.
  - [ ] Decrement stock **synchronously** with the guarded conditional update, and turn "last unit
        gone" into a 409 that names the item.
  - [ ] Seed a `POS` numbering series in `Sales.Repository.SeedData.NumberingSeriesSeed`.
  - [ ] Test: two concurrent sales of the last unit: exactly one succeeds.
  - [ ] Test: tender that doesn't cover the total is refused.
- **Done when:** two concurrent sales of the last unit leave exactly one sale.
- **Notes:**
  - Dependency on TK-10 added 2026-09-24: the invoice posting POS reuses changes with the provisional-COGS decision; build POS on the corrected posting.

### TK-40 · POS till screen (T7.2)
- [ ] open
- **Lanes:** L-DSK · **Depends on:** TK-79, TK-39, TK-17 · **Decision:** —
- **Sub-tasks:**
  - [ ] Keyboard-driven: F-keys for tender, quantity, void line and hold.
  - [ ] Add an item on a barcode-scanner keystroke burst, through TK-14's barcode search.
  - [ ] Decide the offline behaviour (queue sales locally, or refuse when offline), and write it
        under Notes and in the docs page.
  - [ ] Post through TK-39.
  - [ ] Lint and build are clean.
- **Done when:** a barcode-scanned sale posts from `apps/desktop`.
- **Notes:**
  - From TK-79: the cart is `pos-cart.ts` (pure) plus signals in `pos-terminal.component.ts`.
    `checkout()` still posts the scaffold's plain draft invoice; replace it with TK-39's endpoint.
    Two open questions to settle here: a `WALKIN` contact is not seeded anywhere, and a
    tax-inclusive price can total a paisa under its MRP (see TK-79's Notes).
  - **Decided by the owner (2026-09-24):** a `WALKIN` contact is seeded per branch (TK-17) and the till defaults to it; POS invoices carry a **round-off line** to the rupee, posted to the seeded Round Off account; the till **refuses sales while offline** (no local queue).
  - Dependency on TK-17 added 2026-09-24: the till defaults to the seeded `WALKIN` contact.

### TK-41 · POS receipt, ESC/POS (T7.3)
- [ ] open
- **Lanes:** L-DSK · **Depends on:** TK-40 · **Decision:** —
- **Where:** `frontend/apps/desktop/src/app/pos-terminal/esc-pos.service.ts` (88 lines).
- **Sub-tasks:**
  - [ ] A fixed-width layout for 58 mm and 80 mm paper: header from the branch, lines, GST split,
        tender and change.
  - [ ] Print after a successful sale, and allow a reprint.
  - [ ] Talk to the printer through Electron (USB or serial). A browser can't.
- **Done when:** a completed sale prints a receipt on an ESC/POS printer or an emulator.
- **Notes:**

### G · Platform for several apps (stage H0)

The design is in `docs/Modules.md`, section "One customer, many applications" (from line 1068).
None of it is built.

### TK-42 · H0.1: `App` in `mst`
- [ ] open
- **Lanes:** L-MST · **Depends on:** TK-70 · **Decision:** —
- **Where:**
  - `backend/Api/Master/Master.Entity/TableEntities/{Role,Permission,Menu,License,RefreshToken}.cs`
  - `AdminDbContext.cs`: the `HasData` for roles (617), permissions (641) and grants (708).
  - `SeedData/MenuSeed.cs`
- **Sub-tasks:**
  - [ ] Add `Master.Entity/Enums/App.cs`: `[Flags] RetailErp = 1, School = 2, Hrms = 4, Payroll = 8`.
  - [ ] Add `App App` to `Role`, `License` and `RefreshToken`, and `App Apps` to `Permission` and
        `Menu`. Give `License` a unique index on (`CustomerId`, `App`).
  - [ ] Seed every existing row as `RetailErp`, except that users, roles, organizations, settings,
        currencies, configuration and SMTP permissions and menus get all four apps.
  - [ ] Turn `Customer.PlanTier` and `TenantDatabase.PlanType` from strings into enums.
  - [ ] Add the grant rule in `RoleService`: a permission may be granted only if
        `permission.Apps.HasFlag(role.App)`.
  - [ ] Test: the grant rule asserted over the seeded grants.
  - [ ] Test: granting a Payroll-only permission to a RetailErp role is refused.
  - [ ] Confirm `has-pending-model-changes` is clean.
- **Done when:** granting a Payroll-only permission to a RetailErp role is refused; a Payroll role
  can be granted `users.view`; and `apps/web` is unchanged for every existing user.
- **Notes:**

### TK-43 · H0.2: per-app sign-in and licences
- [ ] open
- **Lanes:** L-MST, L-KERNEL · **Depends on:** TK-42 · **Decision:** —
- **Where:**
  - `backend/Api/Master/Master.Entity/Models/AuthModels.cs` (`SelectOrganizationRequest`)
  - `Master.Api/Services/{AuthService,JwtTokenService,LicenseService}.cs`
  - `backend/shared/Shared.Kernel/Internal/{RequireModulePermissionAttribute,EndpointGuardAudit}.cs`
- **Sub-tasks:**
  - [ ] Add `App` to the login request and to `SelectOrganizationRequest`, and filter the branch
        list to branches where the user holds a role in that app.
  - [ ] `JwtTokenService` adds an `app` claim, the licence claims for that app, and permissions
        from that app's roles only. The refresh-token family is per app.
  - [ ] Add a `[RequireApp(App …)]` attribute in `Shared.Kernel.Internal`, and add an
        `EndpointGuardAudit` question that every controller names its apps. Mark all existing
        controllers `RetailErp`, and Master's shared ones with all four.
  - [ ] Add `GET api/me/context`: name, branch, app, licence status and expiry, and permissions,
        with no internal ids.
  - [ ] Test: an HRMS token calling a RetailErp route gets 403.
  - [ ] Test: an expired RetailErp licence leaves a Payroll token working.
- **Done when:** an HRMS token calling a RetailErp endpoint gets 403; a Payroll token reads
  employees but not recruitment; and an expired RetailErp licence leaves Payroll working.
- **Notes:**

### TK-44 · H0.3: shell, page validation and shared master pages
- [ ] open
- **Lanes:** L-UI, L-MASTER-UI, L-WEB · **Depends on:** TK-43, TK-28 · **Decision:** —
- **Where:**
  - `frontend/libs/app-shell/src/lib/{menu.service.ts,shell-screens.ts}`
  - `frontend/libs/shared/auth/src/lib/{license.guard.ts,token-claims.ts}`
  - `frontend/apps/web/src/app/app.routes.ts`
  - Master's `MenuService.cs`
- **Sub-tasks:**
  - [ ] Add an `APP_ID` injection token. `menu.service.ts` calls `GET /api/menu?app=`, and
        `MenuService` filters by `Menus.Apps`.
  - [ ] Add a `SessionContextService` in `libs/shared/auth` over `GET api/me/context`, and rewrite
        `permissionGuard` and `licenseActiveGuard` on it. Retire `token-claims.ts`.
  - [ ] Add `shellRoutes({ app, children })` to `libs/app-shell`, which attaches the five-step
        page guard.
  - [ ] Add `data.access` to every route, with deny-by-default, plus a no-access page and a
        `*bbIfCan` directive.
  - [ ] Add `auditShellRoutes(routes)`, called from each app's route spec.
  - [ ] Move `apps/web` onto `shellRoutes`: `data.permission` becomes `data.access`, and the
        dashboard becomes `{ signedIn: true }`.
  - [ ] Add an app switcher to the topbar, and an Applications page (licences, **Start trial**).
  - [ ] Move the numbering-series page to `libs/master/master-ui`.
  - [ ] Test: route specs, including removing one `data.access` so the audit fails.
- **Done when:** as H0.3 in `docs/Modules.md`: a typed URL to a forbidden page shows the no-access
  page, and removing `data.access` fails the route spec.
- **Notes:**
  - Dependency on TK-28 added 2026-09-24: the shared settings pages should move into their own libs before H0.3 mounts them in several apps.

### TK-45 · H0.4: signup and seeding per app
- [ ] open
- **Lanes:** L-MST · **Depends on:** TK-43, TK-01 · **Decision:** D-12
- **Where:** `Master.Api/Services/{SignupService,TenantSeeder}.cs`
- **Sub-tasks:**
  - [ ] Add `App` to `SignupRequest`. Signup creates that app's Owner role and a 14-day trial licence.
  - [ ] Add `POST api/applications/{app}/trial`, which creates the licence, grants the owner that
        app's Owner role in every branch, and seeds the app into every branch.
  - [ ] `TenantSeeder` seeds Accounting always, plus the services of every licensed app.
  - [ ] Test: Payroll signup, then starting HRMS, gives one customer, one branch and two licences.
- **Done when:** signing up for Payroll and then starting HRMS gives one customer, one branch, two
  licences and one set of employees.
- **Notes:**
  - D-12 answered (2026-09-24): RetailErp and School licences count users with a branch cap; HRMS and Payroll count active employees per month.

### TK-46 · H0.5: sharding in the multi-app model
- [ ] open
- **Lanes:** L-MST · **Depends on:** TK-42, TK-27 · **Decision:** —
- **Where:**
  - `Master.Api/Services/TenantDatabaseAllocator.cs`
  - `mst.TenantDatabases`
  - `docs/Modules.md` Platform § Sharding.
- **Sub-tasks:**
  - [ ] Count capacity in customers, not organizations: `MaxCustomers` (default 100) and `CurrentCustomers`.
  - [ ] When the last pool fills, provision a new one. Create the database, migrate every tenant
        schema into it (reusing the `DatabaseMigrationService` steps), register it, then allocate.
  - [ ] The Elite plan gets a shard with capacity 1.
  - [ ] Test: the 101st customer lands in a new shard.
  - [ ] Test: two concurrent signups can't both take the last slot.
- **Done when:** a full pool no longer makes signup answer 503.
- **Notes:** shares `L-MST` with TK-45, so the two run one after the other.
  - Dependency on TK-27 added 2026-09-24: production shard provisioning goes through infrastructure (D-02), so decide that path first.

### TK-47 · H0.6: `apps/hrms` and `apps/payroll` scaffolds
- [ ] open
- **Lanes:** L-DEPS, plus new lanes `L-HRMS-APP` and `L-PAY-APP` · **Depends on:** TK-44 · **Decision:** —
- **Sub-tasks:**
  - [ ] Generate two Nx apps modelled on `apps/web`: `app.config.ts`, `APP_ID`, and
        `shellRoutes({ app: 'Hrms' | 'Payroll' })`.
  - [ ] Add each app to `nx.json` and CI's build matrix (`.github/workflows/ci.yml`).
  - [ ] Add dev-server ports and a proxy to the Gateway.
  - [ ] Test: each app's route spec calls `auditShellRoutes`.
- **Done when:** both apps sign in, select a branch, draw their own menus, and switch to each other
  and to `apps/web`.
- **Notes:**

### H · HRMS and Payroll (H1–H12)

Design: `docs/Modules.md` "HRMS & Payroll" (from line 1417). The sections to read are Columns
(1497), Approvals (2117), Endpoints (2234), Frontend (2277) and Tenancy (2305).

**Every card also carries the standard delivery sub-tasks in section 5.** A new service also
needs these scaffold steps:
- `backend/Api/{Service}/{Service}.{Entity,Repository,Api}`, copying `backend/Api/Printing` (the
  newest scaffold);
- `backend/tests/{Service}.Api.Tests`;
- a port from the service map;
- the `.sln` entry;
- a Gateway route;
- a `MigrateContextAsync<…>` line in Master's `DatabaseMigrationService`;
- a `TenantSeeder.Services` entry;
- the `libs/{name}/{name}-core` and `-ui` libs.

**Payroll without HRMS** needs TK-48, TK-51, TK-52, TK-53, the settlement half of TK-54, and TK-55. **The first
sellable HRMS** needs TK-48, TK-49, TK-50, TK-54 and TK-55.

### TK-48 · H1: Core HR, the shared employee master (`Hrm`, `hrm`, port 4509)
- [ ] open
- **Lanes:** L-HRM (new) · **Depends on:** TK-47 · **Decision:** —
- **Tables:**
  - Organisation: `Department`, `Designation`, `Grade`, `CostCentre`, `WorkLocation`.
  - Employee: `Employee`, `EmployeeAddress`, `EmployeeContact`, `EmployeeFamilyMember`,
    `EmployeeNominee`, `EmployeeEducation`, `PreviousEmployment`, `EmployeeBankDetail`,
    `EmploymentHistory`, `EmployeeDocument`, `AssetIssue`.
  - Other: `Announcement`, `PolicyDocument`.
- **Sub-tasks:**
  - [ ] Scaffold the service (above).
  - [ ] Build the entities from the Columns section, then the migration with RLS.
  - [ ] Seed per branch: one department, designation, grade and location, and an `EMP` numbering series.
  - [ ] Add CRUD for the organisation tables. Add the employee master with its children, guarded
        by `[RequireApp(Hrms | Payroll | School)]` and module `employee`.
  - [ ] Mask PAN, Aadhaar and bank numbers on lists.
  - [ ] Add `UserId Guid?` to link an employee to a login.
  - [ ] Build pages in `libs/hrm/hrm-ui`: organisation setup, employee list and employee detail
        with tabs.
- **Done when:** an employee is created with family, nominees and bank details, linked to a user
  and listed; RLS and the guard audit pass from a dropped database.
- **Notes:**

### TK-49 · H2: Leave, and the approval engine (`TimeLeave`, `tla`, port 4510)
- [ ] open
- **Lanes:** L-TLA (new), L-HRM · **Depends on:** TK-48 · **Decision:** —
- **Tables:**
  - Leave: `LeaveType`, `LeavePolicy`, `LeaveBalance`, `LeaveApplication`, `LeaveEncashment`.
  - The engine: `ApprovalWorkflow`, `ApprovalWorkflowLevel`, `ApprovalStep`, with `ApproverKind`.
    It lives in `hrm` so every request kind can reuse it.
- **Sub-tasks:**
  - [ ] Scaffold `TimeLeave`.
  - [ ] Build the approval engine in `Hrm`: workflows matched by department, grade and location;
        levels; a snapshot of the chain at submit time; skip rules; send back; delegation; and
        escalation through a hosted service.
  - [ ] Add leave accrual and rollover as a hosted job, applications with the sandwich rule,
        and encashment.
  - [ ] Guard the balance with a conditional update whose row count is the answer.
  - [ ] Seed leave types and a default policy per branch.
- **Done when:** two simultaneous approvals can't overspend a balance; the sandwich rule counts a
  weekend between two leave days; and changing a workflow leaves requests already in flight on
  their old chain.
- **Notes:**

### TK-50 · H3: Time and attendance (`tla`)
- [ ] open
- **Lanes:** L-TLA · **Depends on:** TK-49 · **Decision:** —
- **Tables:** `HolidayList`, `Shift`, `WeeklyOffPolicy`, `ShiftRoster`, `Punch`,
  `DailyAttendance`, `RegularisationRequest`, `OvertimeRequest`, `CompOffCredit`.
- **Sub-tasks:**
  - [ ] Punch import from biometric devices (push endpoint, per the open question in the design)
        and mobile punch-in with the geofence (`WorkLocation.GeoFenceMetres`).
  - [ ] Daily derivation as a hosted job: late marks, half days and absence.
  - [ ] Regularisation and overtime go through the approval engine; add month locking.
  - [ ] Seed a default shift and a weekly-off policy.
- **Done when:** a biometric import derives a late-marked half day, and a regularisation approval
  corrects it.
- **Notes:**

### TK-51 · H4: Payroll core (`Payroll`, `pay`, port 4511)
- [ ] open
- **Lanes:** L-PAY (new) · **Depends on:** TK-48 · **Decision:** —
- **Tables:**
  - Setup: `PayGroup`, `SalaryComponent`, `SalaryStructure`, `EmployeeSalary`, `SalaryRevision`,
    `OneTimePayment`, `SalaryHold`, `EmployeeLoan`, `LoanRepayment`, `MonthlyAttendanceInput`.
  - The run: `PayrollRun`, `Payslip`, `PayslipLine`.
- **Sub-tasks:**
  - [ ] Scaffold `Payroll`.
  - [ ] Components and structures, with formulas over earnings and deductions.
  - [ ] Revisions with arrears paid in the next run.
  - [ ] Paid days come from `tla` when HRMS is licensed, and from `MonthlyAttendanceInput` when
        not. Record the source on the run.
  - [ ] A run moves through `process` → `approve` → `post` → `markpaid`, or `reverse`. Posting
        sends one balanced journal through Accounting's internal API: Dr Payroll Expense, Cr
        Salary Payable and the deductions.
  - [ ] Bank file export and journal export (Tally XML and CSV).
  - [ ] Payslips come from a print template.
- **Done when:** a run posts one balanced journal; Salary Payable ties to the unpaid net; a
  back-dated revision pays arrears in the next run; a reversal restores both; and the run reads
  monthly input without an HRMS licence and `tla` with one.
- **Notes:**

### TK-52 · H5: Statutory (`pay`)
- [ ] open
- **Lanes:** L-PAY · **Depends on:** TK-51 · **Decision:** —
- **Tables:** `PfSetting`, `EsiSetting`, `ProfessionalTaxSlab`, `LwfSetting`, `GratuitySetting`,
  `BonusSetting`, `StatutoryReturn`. All are effective-dated.
- **Sub-tasks:**
  - [ ] PF and ESI with wage ceilings; PT and LWF per state (`WorkLocation.StateId`).
  - [ ] A monthly gratuity provision, and the bonus register.
  - [ ] Return files: the PF ECR, ESI, and the PT challan data.
  - [ ] Seed the settings, and the PT and LWF slabs for every state.
- **Done when:** a month's ECR file matches the posted payslips to the rupee.
- **Notes:**

### TK-53 · H6: Income tax on salary (`pay`)
- [ ] open
- **Lanes:** L-PAY · **Depends on:** TK-51 · **Decision:** —
- **Tables:** `TaxSlab`, `TaxRule`, `TaxDeclaration`, `TaxDeclarationLine`, `RentDetail`,
  `PreviousEmployerIncome`.
- **Sub-tasks:**
  - [ ] Old and new regimes, declarations and proofs (with `lock` and `unlock`), and a projection
        with monthly TDS.
  - [ ] Form 16 Part B, Form 12BA and the 24Q data.
  - [ ] Seed the year's slabs and rules.
- **Done when:** a mid-year joiner with income from a previous employer is taxed the same by a
  monthly run and by the year-end recomputation.
- **Notes:**

### TK-54 · H7: Lifecycle and exit
- [ ] open
- **Lanes:** L-HRM, L-PAY · **Depends on:** TK-48, TK-51 · **Decision:** —
- **Tables:** `ChecklistTemplate`, `EmployeeChecklist`, `Separation`, `Letters` (in `hrm`), and
  `FullAndFinalSettlement` (in `pay`).
- **Sub-tasks:**
  - [ ] Onboarding and exit checklists, separation with clearance, and letters from print templates.
  - [ ] F&F settlement through a `FullAndFinal` run. A Payroll-only customer records the last
        working day on the settlement itself.
  - [ ] Deactivate the linked `mst.Users` login on settlement, through Master's API.
- **Done when:** settling an exit pays through a `FullAndFinal` run, and the employee's login stops
  working.
- **Notes:**

### TK-55 · H8: Self-service and approvals
- [ ] open
- **Lanes:** L-HRMS-APP, L-PAY-APP · **Depends on:** TK-49, TK-51 · **Decision:** —
- **Sub-tasks:**
  - [ ] `/api/me/...` routes: profile, leave, attendance, punches, claims, documents and
        announcements (HRMS), plus payslips, Form 16 and tax declarations (Payroll). Each resolves
        the employee through `sub` → `Employee.UserId`, **never** an id in the URL.
  - [ ] `/api/team/...` routes for a manager's direct and indirect reports.
  - [ ] An approvals inbox.
  - [ ] Mobile-first pages.
- **Done when:** an employee applies for leave and a manager approves it, each from their own
  screens, and the employee downloads a payslip.
- **Notes:**

### TK-56 · H9: Expense claims (`Claims`, `clm`, port 4514)
- [ ] open
- **Lanes:** L-CLM (new) · **Depends on:** TK-49 · **Decision:** —
- **Tables:** `ClaimCategory`, `ClaimLimit`, `ExpenseClaim`, `ExpenseClaimLine`.
- **Sub-tasks:**
  - [ ] Categories with limits per grade, and claims with receipts stored through `IFileStorage`.
  - [ ] Approval through the engine.
  - [ ] Payout: as a line in a payroll run when Payroll is licensed, and as a Spend Money through
        Accounting otherwise.
  - [ ] Seed claim categories and a `CLM` numbering series.
- **Done when:** a claim is submitted, approved and paid, and the payment posts a balanced journal.
- **Notes:**

### TK-57 · H10: Recruitment and onboarding (`Recruitment`, `rec`, port 4512)
- [ ] open
- **Lanes:** L-REC (new) · **Depends on:** TK-49 · **Decision:** —
- **Tables:** `JobRequisition`, `JobOpening`, `Candidate`, `Application`, `InterviewRound`, `Offer`.
- **Sub-tasks:**
  - [ ] Requisitions through the approval engine; openings; candidates and a pipeline board (ask
        before building a new board component); interviews.
  - [ ] Offer `accept` creates the employee through `Hrm`'s API, **idempotently**: the offer id
        becomes the idempotency key.
- **Done when:** accepting an offer twice creates one employee.
- **Notes:**

### TK-58 · H11: Performance (`Performance`, `prf`, port 4513)
- [ ] open
- **Lanes:** L-PRF (new) · **Depends on:** TK-49 · **Decision:** —
- **Tables:** `ReviewCycle`, `Eligibility`, `RatingScale`, `Competency`, `Goal`,
  `PerformanceReview`, `SelfEvaluation`, `GoalSelfAssessment`, `CompetencySelfAssessment`,
  `LevelReview`, `LevelGoalRating`.
- **Sub-tasks:**
  - [ ] Cycles and eligibility; goals and competencies.
  - [ ] Self-evaluation, frozen at submit.
  - [ ] Level reviews over the Appraisal chain, with send back.
  - [ ] Calibration and release. When Payroll is licensed, raise a salary revision.
- **Done when:** as H11 in `docs/Modules.md`:
  - routing follows each department's chain;
  - send back returns to the level before;
  - the self-evaluation is unchanged after every level acts;
  - a manager who is also the lead is asked only once.
- **Notes:**

### TK-59 · H12: HRMS and Payroll reports
- [ ] open
- **Lanes:** L-RPT · **Depends on:** TK-51 · **Decision:** —
- **Where:** `docs/Modules.md` HRMS § Reports (line 2217) lists the groups: People, Time, Leave,
  Pay, Statutory, Recruitment and Claims.
- **Sub-tasks:**
  - [ ] Build each report as an `IReportSource` flagged with `App.Hrms` or `App.Payroll`.
  - [ ] Map the tables it reads read-only on `ReportingDbContext`.
  - [ ] Wire all four layers checked by `ReportLayerCertificationTests`, and update its count.
- **Done when:** each report is flagged with its app, and the certification suite counts them.
- **Notes:**

### I · School (S0–S9)

Design: `docs/Modules.md` "School" (from line 2403). The sections to read are Service map (2454),
Columns (2472), Endpoints (2793) and Stages (2884). Every card also carries section 5's standard
delivery sub-tasks, and a new service needs the scaffold steps listed under H.

### TK-60 · S0: School prerequisites
- [ ] open
- **Lanes:** L-CON, L-MST, L-DEPS, `L-SCH-APP` (new) · **Depends on:** TK-47, TK-48 · **Decision:** —
- **Sub-tasks:**
  - [ ] Add `IsGuardian` to `con.Contact`, with a migration and a role filter on `/api/contacts`.
  - [ ] Seed School's permissions, menus and roles (Principal, Office Admin, Accountant, Teacher,
        Maintenance, Viewer) with `App = School`.
  - [ ] School signup and a trial licence, reusing TK-45.
  - [ ] An empty `apps/school` on `shellRoutes`, mounting the shared master pages and the employee master.
  - [ ] Add numbering series `ADM`, `APL`, `FDM`, `FRC` and `WRK`.
- **Done when:** `apps/school` shows only School menus, and a guardian contact can be created and
  filtered.
- **Notes:**

### TK-61 · S1: Sis (`sis`, port 4515)
- [ ] open
- **Lanes:** L-SIS (new) · **Depends on:** TK-60 · **Decision:** —
- **Tables:** `AcademicYear`, `SchoolClass`, `Section`, `Subject`, `Student`, `StudentGuardian`,
  `Enrolment`, `Exam`.
- **Sub-tasks:**
  - [ ] Scaffold the service; seed `SchoolClass` LKG–XII per branch.
  - [ ] CRUD for years, classes, sections and subjects.
  - [ ] Students with guardians, where each guardian is a `con` contact validated through Master's API.
  - [ ] Enrolment.
- **Done when:** a student is admitted directly, enrolled in a section and listed; RLS and the
  guard audit pass from a dropped database.
- **Notes:**

### TK-62 · S2: Admission (`adm`, port 4516)
- [ ] open
- **Lanes:** L-ADMN (new) · **Depends on:** TK-61 · **Decision:** —
- **Tables:** `Enquiry`, `Application`, `ApplicationDocument`.
- **Sub-tasks:**
  - [ ] Enquiry → application → `admit`.
  - [ ] Admit creates the student through Sis's API and the guardian through Master's API, both
        idempotently.
- **Done when:** admitting twice creates one student.
- **Notes:**

### TK-63 · S3: Student attendance (`att`, port 4517)
- [ ] open
- **Lanes:** L-ATT (new) · **Depends on:** TK-61 · **Decision:** —
- **Tables:** `StudentAttendance`, `AttendanceLock`.
- **Sub-tasks:**
  - [ ] A daily register per section, taking the roll from Sis.
  - [ ] `lock` and `unlock`, where unlocking needs `attendance.unlock`.
  - [ ] The register component: ask before building it if `ui-components` lacks one.
- **Done when:** a locked day refuses an edit from a teacher and accepts one from `attendance.unlock`.
- **Notes:**

### TK-64 · S4: Fee (`fee`, port 4518)
- [ ] open
- **Lanes:** L-FEE (new) · **Depends on:** TK-61 · **Decision:** —
- **Tables:** `FeeHead`, `FeeStructure`, `FeeConcession`, `FeeDemand`, `FeeReceipt`.
- **Sub-tasks:**
  - [ ] Seed fee heads.
  - [ ] Structures per class, concessions, and demand generation per enrolment (idempotent).
  - [ ] Receipts and allocation.
  - [ ] Post the demand (Dr the guardian's AR sub-account, Cr fee income) and the receipt through
        Accounting's internal API.
- **Done when:** a demand and its receipt post balanced journals, and the guardian's AR
  sub-account ties to the open demands.
- **Notes:**

### TK-65 · S5: Facility (`fac`, port 4519)
- [ ] open
- **Lanes:** L-FAC (new) · **Depends on:** TK-60 · **Decision:** —
- **Tables:** `Building`, `Space`, `FacilityAsset`.
- **Sub-tasks:**
  - [ ] CRUD, with the hierarchy: building → space → asset.
- **Done when:** buildings, spaces and assets can be created, listed and deactivated.
- **Notes:**

### TK-66 · S6: WorkOrder (`wrk`, port 4520)
- [ ] open
- **Lanes:** L-WRKO (new) · **Depends on:** TK-65 · **Decision:** —
- **Tables:** `WorkOrder`, `WorkOrderTask`, `WorkOrderPart`.
- **Sub-tasks:**
  - [ ] The lifecycle, through `assign`, `complete` and `close`. An Assigned work order can't be edited.
  - [ ] The assignee is an employee, validated through `Hrm`.
  - [ ] Parts are issued through Inventory's issue API.
- **Done when:** editing an Assigned work order is refused, and issuing a part moves stock.
- **Notes:**

### TK-67 · S7: Preventive (`ppm`, port 4521)
- [ ] open
- **Lanes:** L-PPM (new) · **Depends on:** TK-66 · **Decision:** —
- **Tables:** `PreventivePlan`, `PreventiveOccurrence`.
- **Sub-tasks:**
  - [ ] Plans with a recurrence.
  - [ ] A hosted service generates occurrences and raises work orders, with (plan, due date) as
        the idempotency key.
- **Done when:** running generation twice raises one work order per occurrence.
- **Notes:**

### TK-68 · S8: AMC (`amc`, port 4522)
- [ ] open
- **Lanes:** L-AMC (new) · **Depends on:** TK-65 · **Decision:** —
- **Tables:** `AmcContract`, `AmcCoveredAsset`, `AmcVisit`.
- **Sub-tasks:**
  - [ ] Contracts, where the vendor is a `con` contact validated through Master.
  - [ ] Covered assets.
  - [ ] Visits, which may raise a work order.
  - [ ] Renewal reminders through Notification.
- **Done when:** a contract's covered assets and visits are recorded, and a renewal reminder fires
  before expiry.
- **Notes:**

### TK-69 · S9: Parent portal
- [ ] open
- **Lanes:** L-PTL · **Depends on:** TK-63, TK-64, TK-32 · **Decision:** —
- **Sub-tasks:**
  - [ ] Add routes in `apps/portal` for demands, receipts, attendance and published marks.
  - [ ] Controllers take `[RequirePortalAccess]`, and the guardian's link comes from
        `JwtTokenService.CreatePortalToken` against their `ContactId`.
- **Done when:** a guardian sees their child's demands, receipts, attendance and published marks.
- **Notes:**
  - Dependency on TK-32 added 2026-09-24: the parent portal adds to `apps/portal`, whose next screens TK-32 designs first.

### Z · Done — waiting on the owner's test run

Finished cards, in ID order. A card marked `tests written, not run` needs the owner to run its
tests (section 0.5); the owner then adds `· tests passed (owner) — date`, or moves the card back
into the queue with the failure under its Notes.

### TK-70 · Master fails to start on a fresh database
- [x] completed (Claude Opus 5.5) — 2026-09-23 · tests written, not run
- **Lanes:** L-MST · **Depends on:** — · **Decision:** —
- **Where:**
  - `backend/Api/Master/Master.Repository/AdminDbContext.cs:337-338`: the `Menu` and `MenuPermission` `HasData`.
  - `backend/Api/Master/Master.Repository/SeedData/MenuSeed.cs`
  - `backend/Api/Master/Master.Repository/Migrations/Admin/`: `20260918204306_InitialAdminDbContextSchema`, `20260918205209_UpdateAdminModel` and `AdminDbContextModelSnapshot`.
  - `backend/Api/Master/Master.Api/Services/DatabaseMigrationService.cs:54`: `MigrateAsync`.
- **State:** the menu seed changed after both admin migrations were written, so the model and the
  snapshot disagree and `MigrateAsync` throws `PendingModelChangesWarning`. Adding one more
  migration doesn't fix it. EF emits 379 `UpdateData` calls, and one of them collides on
  `IX_MenuPermissions_MenuId_PermissionCode`.
- **Sub-tasks:**
  - [x] Run `git log -- backend/Api/Master/Master.Repository/SeedData/MenuSeed.cs` (the last change
        was `a3d066c`) and confirm the seed isn't mid-change.
  - [x] Delete the three files in `Migrations/Admin/`, then regenerate one migration:
        `dotnet ef migrations add InitialAdminDbContextSchema --context AdminDbContext --project backend/Api/Master/Master.Repository --startup-project backend/Api/Master/Master.Api --output-dir Migrations/Admin`.
  - [x] Diff the new `Up()` against the deleted one. Every table, index and `HasData` table must
        survive. `mst` has no RLS on purpose: it holds shared reference data.
  - [x] Confirm `dotnet ef migrations has-pending-model-changes --context AdminDbContext …` reports none.
  - [x] Confirm `dotnet build backend/Bill-Book.sln` is clean.
  - [x] Rewrite the "Master cannot start on a fresh database" bullet in `CLAUDE.md` in the same
        commit. You may take `L-DOC` for that one commit.
  - [ ] Owner: drop `ADMIN_TEST_DB` and `CONTACTS_TEST_DB`, run `Master.Api.Tests`, and start
        Master against empty databases.
- **Done when:** Master starts against dropped databases, and `has-pending-model-changes` is clean.
- **Notes:** TK-42 (H0.1) changes the same seed, so this card goes first.
- **Notes (Claude Opus 5.5, 2026-09-23):**
  - The seed was not mid-change: `MenuSeed.cs` last changed in `a3d066c`, the same commit that wrote the
    snapshot. The snapshot was generated before the tree was renumbered, which is the whole drift (32
    deletes and 379 updates across `Menus` and `MenuPermissions`, nothing else). The seed is deterministic
    (fixed ids, no clocks or GUIDs), so the re-squash holds.
  - `UpdateAdminModel` was empty and neither admin migration had any `migrationBuilder.Sql`, so the squash
    lost nothing. It is now `20260923161008_InitialAdminDbContextSchema`, with the same 26 tables and 38
    indexes. The `Up()` diff against the old one is seed rows only.
  - The new id is deliberate. Reusing `20260918204306` would let an old developer database treat the
    migration as applied while it holds stale menu rows. Under a new id that database fails loudly on
    `CREATE TABLE` instead. Drop it.
  - Verified by starting Master (`Migrations:ExitWhenDone=true`) against an empty PostgreSQL 16 with scratch
    database names. Exit 0; `mst` migrated and seeded with 131 menus and 378 menu permissions, matching the
    seed; `IN000001` created and every tenant schema migrated. The one `fail:` line in the log is EF probing
    `__EFMigrationsHistory` before it exists, which is normal on a new database. The scratch databases were
    dropped afterwards. This was a startup check, not a test run.
  - `has-pending-model-changes` is clean for both `AdminDbContext` and `ContactsDbContext`.
    `dotnet build backend/Bill-Book.sln` builds with 0 warnings.
  - Test written: `backend/tests/Master.Api.Tests/MigrationModelTests.cs`. It asserts no pending model
    changes for both Master contexts (no connection is opened, so it cannot skip), plus the menu seed's
    `(MenuId, PermissionCode)` uniqueness and its parent and permission references.
  - TK-42 (H0.1) can start. After it changes the seed, regenerate this one migration rather than adding a
    second one, while nothing is released.

### TK-71 · RLS template: restore it in `acc`
- [x] completed (Claude Opus 5.5) — 2026-09-23 · tests written, not run
- **Lanes:** L-ACC, L-MST (Master's startup bootstrap writes `acc` rows) · **Depends on:** — · **Decision:** —
- **Where:**
  - The template: `backend/Api/Printing/Printing.Repository/Migrations/20260918205343_InitialPrintingSchema.cs:130-172`.
  - The code that sets the tenant: `backend/shared/Shared.Kernel/Tenancy/RlsConnectionInterceptor.cs`.
  - The audit: `backend/tests/Shared/RlsAudit.cs`, called as `UnprotectedAsync(db, "acc")` at
    `backend/tests/Accounting.Api.Tests/AccountingQueryFilterTests.cs:121`.
- **State:**
  - `acc` has 27 tables. All of them carry `CustomerId` and `OrgId`, and none has RLS.
  - The interceptor sets `app.current_customer_id` and `app.current_org_id` at **session level**
    (`set_config(…, false)`) each time a connection opens, and sets `''` when there is no tenant.
  - `prt`'s policy casts `current_setting(…)::uuid`, which **throws** on `''` instead of
    returning no rows.
  - Several services read with `IgnoreQueryFilters()` to tell "another branch's row" apart from "no
    row", so they can return `Forbid()` rather than `NotFound()`. Examples are
    `InvoiceService.cs:1562` and `AllocationService.cs:511`. Under RLS those probes stop seeing
    other branches' rows.
- **Sub-tasks:**
  - [x] Settle the policy expression once, for every schema, and write it under Notes. The
        recommended form lets a request with no tenant see no rows instead of throwing:
        `"CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid`.
  - [x] Create an empty migration:
        `dotnet ef migrations add EnableRowLevelSecurity --context AccountingDbContext --project backend/Api/Accounting/Accounting.Repository --startup-project backend/Api/Accounting/Accounting.Api --output-dir Migrations/Tenant`.
  - [x] In `Up()`, loop over an explicit list of the 27 tables, the way `prt` does. For each one,
        emit `ENABLE`, `FORCE` and `CREATE POLICY {table}_tenant_isolation ON acc."{table}" FOR ALL USING (…)`.
        The tables: `Accounts, AssetTransactions, BankAccounts, BankStatementLines, BankStatements, Banks,
        DepreciationSchedules, ErrorLogs, FixedAssetCategories, FixedAssets, JournalDetails,
        JournalLedger, Journals, NumberingSeries, OpeningBalanceLines, OpeningBalances, PaymentTerms,
        PeriodLocks, ReceiveMoney, ReceiveMoneyDetails, SpendMoney, SpendMoneyDetails,
        StatementImportProfiles, SubAccounts, TaxMasters, TransactionRatios, TransferMoney`.
  - [x] In `Down()`, emit `DROP POLICY IF EXISTS`, `NO FORCE ROW LEVEL SECURITY` and `DISABLE ROW LEVEL SECURITY`.
  - [x] Decide what happens to the Forbid probes. Either accept `NotFound()` under RLS and change
        `CLAUDE.md`'s endpoint rule, or keep `Forbid()` through a `SECURITY DEFINER` function
        (e.g. `acc.row_in_other_org(table, id)`). Record the choice under Notes so TK-72, TK-73, TK-74, TK-02, TK-03, TK-04
        treat their probes the same way.
  - [x] Check whether a view exists in `acc`. A view needs `WITH (security_invoker = true)`, or it
        bypasses RLS.
  - [x] Confirm `dotnet build` is clean and `has-pending-model-changes` is clean.
  - [x] Write the template under Notes for TK-72, TK-73, TK-74, TK-02, TK-03, TK-04 and TK-05: the expression, the loop,
        `Down()`, how to derive the table list, and how to name an exemption.
  - [x] Test: with the tenant cleared, a query on `acc.Accounts` returns zero rows and doesn't throw.
  - [ ] Owner: drop `ACCOUNTING_TEST_DB` and run the suite. Then drop one policy by hand and watch
        the RLS test go red.
- **Done when:** `acc`'s RLS assertion passes from a dropped database, and fails when one policy is
  removed.
- **Notes (Claude Opus 5.5, 2026-09-23):**
  - **Policy expression, settled for every schema:**
    `"CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid`.
    A tenant that was never set, or was cleared to `''`, gives NULL, so the request sees no rows
    and cannot write instead of throwing on `''::uuid`. Checked by hand as a non-superuser table
    owner: never set gives 0, cleared gives 0, own branch 16, another branch 0, and a cross-branch
    insert is refused with `new row violates row-level security policy`.
  - **Forbid probes: decided by the owner on 2026-09-23. Accept 404.** Another branch's row is
    `NotFound()`, because RLS hides it from the service itself. No bypass function and no
    `BYPASSRLS` role. `AllocationService.ExistsInAnotherOrgAsync` and both of its call sites in
    `AllocationsController` are gone. The endpoint rule in `CLAUDE.md` and `docs/Architecture.md`
    now says: 403 only when the token's org differs from an org id the route names, and 404 for a
    row outside the branch. The only other probe in the backend is Sales' (see TK-03's Notes).
  - `acc` has no views (`vw_LedgerDetail` was never built) and no `HasData`, so no migration
    inserts rows after RLS is on.
  - **Found and fixed in `L-MST`.** Master's startup bootstrap (`DatabaseMigrationService`)
    builds `AccountingDbContext` and `InventoryDbContext` by hand without
    `RlsConnectionInterceptor`, so its connection never set the tenant. Under a non-superuser owner
    the seed probes would see nothing and the inserts would be refused, and Master would fail to
    start. It now adds the interceptor to all three hand-built contexts, `inv` included, ahead of
    TK-74. Verified by running Master (`Migrations:ExitWhenDone`) as a
    `NOSUPERUSER NOBYPASSRLS CREATEDB` role against empty databases. It exited 0; `acc` was owned by
    that role with 27 of 27 tables enabled, FORCEd and policied; org `…0001` got 16 accounts and 17
    series.
  - **The `ErrorLogs` write with no tenant is refused, and that is by design.**
    `GlobalExceptionHandler` writes `Guid.Empty` when a request has no tenant. The policy refuses
    it, and `ErrorLogStore` swallows the refusal and logs a warning. The caller still gets its
    curated answer. Nothing to change.
  - The seeders' `IgnoreQueryFilters()` reads (`AccountService`, `TaxMasterService`,
    `PaymentTermService`, `NumberingSeriesService`) all filter on the org the request has already
    set as its tenant, so RLS leaves them working.
  - **Test written:** `backend/tests/Accounting.Api.Tests/RowLevelSecurityTests.cs` (five tests).
    It seeds as the superuser, then switches to `acc_rls_probe` with `SET LOCAL ROLE` inside one
    uncommitted transaction. The suite connects as `postgres`, and a superuser bypasses RLS even
    when FORCEd, so no other test can see a policy work. That is also why none broke.
    `AccountingQueryFilterTests` now exempts `__EFMigrationsHistory`: every service keeps it in its
    own schema in a deployed database, while this fixture leaves it in `public`.
  - **The template for TK-72, TK-73, TK-74, TK-02, TK-03, TK-04 and TK-05.** Copy
    `Accounting.Repository/Migrations/Tenant/20260923173706_EnableRowLevelSecurity.cs`:
    1. `dotnet ef migrations add EnableRowLevelSecurity --context {Ctx} … --output-dir Migrations/Tenant`.
       The generated migration must be empty and the snapshot unchanged. If it isn't, the model has
       drifted: stop and fix that first.
    2. Derive the table list from the migrations, not from memory:
       `grep -A2 "CreateTable(" Migrations/Tenant/*.cs | grep -oE 'name: "[A-Za-z]+"'`. Check that
       every table has both `CustomerId` and `OrgId`, and write the list out as a
       `private static readonly string[] Tables`. Keep it inline. A migration must do the same thing
       forever, and a shared helper changed later would silently rewrite old migrations.
    3. `Up()`, per table: `ENABLE`, `FORCE`, `DROP POLICY IF EXISTS {table}_tenant_isolation`
       (lower-cased, so an un-dropped developer database with the old policy doesn't collide),
       then `CREATE POLICY … FOR ALL USING (<the expression above>)`. There is no `WITH CHECK`,
       because USING doubles as the insert and update check.
    4. `Down()`, per table: `DROP POLICY IF EXISTS`, `NO FORCE ROW LEVEL SECURITY`,
       `DISABLE ROW LEVEL SECURITY`.
    5. **An exemption** is a table with no tenant column, e.g. `rpt.ReportMasters` and
       `rpt.ReportColumns`. Leave it out of `Tables` and name it as an argument to
       `RlsAudit.UnprotectedAsync(db, schema, "Table", "__EFMigrationsHistory")`, with a comment
       saying why.
    6. Grep the service for `IgnoreQueryFilters()` and for any `new {Ctx}(` built without
       `RlsConnectionInterceptor`. Both stop seeing rows under RLS.
    7. Copy `RowLevelSecurityTests.cs` for the schema, changing the role name, schema and seed.
       (Lifting its helpers into `tests/Shared` needs `L-KERNEL`.)
    8. Check `has-pending-model-changes`, and that `dotnet build backend/Bill-Book.sln` has 0 warnings.
  - Two problems found outside this card, now cards of their own: **TK-07**, the journal-balance
    and allocation triggers, which the same squash dropped; and **TK-06**, internal endpoints that
    never set a tenant.

### TK-72 · RLS for `con`
- [x] completed (Claude Opus 5.5) — 2026-09-23 · tests written, not run
- **Lanes:** L-CON · **Depends on:** TK-70, TK-71 · **Decision:** —
- **Where:**
  - The migrations: `backend/Api/Master/Master.Repository/Migrations/Tenant/`, via `--context ContactsDbContext`.
  - The audit: `backend/tests/Master.Api.Tests/ContactsQueryFilterTests.cs:161`.
- **State:**
  - 10 tables: `ApiClients, ContactAddresses, ContactAttachments, ContactBankDetails, ContactLicences,
    ContactPersonRoles, ContactPersons, Contacts, ErrorLogs, PrintTemplates`.
  - **`ApiClients` needs special care.** `InternalApiKeysController.cs:72-75` validates API keys
    with `IgnoreQueryFilters()` across every branch of one customer. A policy that requires
    `OrgId` would hide every key and break internal authentication.
- **Sub-tasks:**
  - [x] Write the migration using TK-71's template.
  - [x] Give `ApiClients` a customer-only policy (`"CustomerId" = …` alone). Or set the branch
        before the lookup, or exempt the table. Write the choice down.
  - [x] Check `PrintTemplateSeeder` and `ContactPersonRoleService`, which use
        `IgnoreQueryFilters`. Both must run with a tenant set.
  - [x] Test: API-key validation still succeeds with RLS on.
  - [ ] Owner: run the suite from a dropped `CONTACTS_TEST_DB`.
- **Done when:** `con`'s RLS assertion passes from a dropped database, and API-key validation still works.
- **Notes (Claude Opus 5.5, 2026-09-23):**
  - Migration: `Master.Repository/Migrations/Tenant/20260923182437_EnableRowLevelSecurity.cs`, from
    TK-71's template. All 10 tables carry both tenant columns and none is exempt. `con` has no
    `HasData`, so nothing is inserted after RLS is on.
  - **`ApiClients`: a variant policy, not an exemption.** It is
    `"CustomerId" = cust AND ("OrgId" = org OR org IS NULL)`. A request that sets a customer and no
    branch sees every branch of that customer's keys. That is exactly what
    `InternalApiKeysController` does, because the key `bb_{customer}_{secret}` names its customer
    but not its branch. Any request with a branch set is held to that branch, as on every other
    table, and nothing crosses customers. Rejected alternatives: exempting the table would leave it
    with no second guard at all, and putting the branch in the key would change a format already
    handed to clients.
  - `PrintTemplateSeeder` and `ContactPersonRoleService.SeedForOrganizationAsync` only run from
    `InternalSeedController` and `TenantSeeder.SeedContactRolesAsync`. Both set the tenant to the
    branch being seeded before resolving the context, so their `IgnoreQueryFilters()` reads still
    see that branch's rows. Nothing to change.
  - Verified by starting Master as a `NOSUPERUSER NOBYPASSRLS` owner against empty databases. It
    exited 0 with 10 of 10 `con` tables enabled, FORCEd and policied. Checked by hand as that owner:
    no tenant 0 keys; customer only 2 (both branches), and the `LastUsedAt` update went through;
    branch A 1 key; a branch with no customer 0. The scratch databases were dropped.
  - **Tests written:** `backend/tests/Master.Api.Tests/ContactsRowLevelSecurityTests.cs` (four
    tests). It logs in as `con_rls_probe`, a password login rather than `SET ROLE`, because the
    API-key test runs the real controller through a DI-built `ContactsDbContext` with
    `RlsConnectionInterceptor`. `ContactsQueryFilterTests` now exempts `__EFMigrationsHistory` in
    its audit.
  - **For TK-08:** `CLAUDE.md`'s RLS bullet says only `acc` is back. Add `con`; this card does not
    hold `L-DOC`.
  - `InternalContactNamesController` also reads `con` with no tenant set. Noted on TK-06.

### TK-73 · RLS for `cus`
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Lanes:** L-CUS · **Depends on:** TK-71 · **Decision:** —
- **Where:** `backend/Api/Customer/Customer.Repository/Migrations/Tenant/`; the audit is at
  `backend/tests/Customer.Api.Tests/CustomerQueryFilterTests.cs:193`.
- **State:** 4 tables: `ErrorLogs, Leads, TicketMessages, Tickets`. Nothing in Customer reads with
  `IgnoreQueryFilters`.
- **Sub-tasks:**
  - [x] Write the migration using TK-71's template.
  - [ ] Owner: run the suite from a dropped database.
- **Done when:** `cus`'s RLS assertion passes from a dropped database.
- **Notes (Claude Opus 5.5, 2026-09-24):**
  - Migration: `Customer.Repository/Migrations/Tenant/20260924015726_EnableRowLevelSecurity.cs`,
    TK-71's template over all four tables. All carry both tenant columns and none is exempt.
    There's no `HasData` and no `IgnoreQueryFilters()`. Customer has no internal endpoints, and its
    only hosted service migrates.
  - Verified by starting Master as a `NOSUPERUSER NOBYPASSRLS` owner against empty databases,
    which migrates every schema. It exited 0 with 4 of 4 `cus` tables enabled, FORCEd and
    policied. By hand as that owner: no tenant 0 leads, own branch 1, and a cross-branch insert
    refused.
  - **Tests written:** `backend/tests/Customer.Api.Tests/CustomerRowLevelSecurityTests.cs` (four
    tests, `SET LOCAL ROLE cus_rls_probe`). `CustomerQueryFilterTests` now exempts
    `__EFMigrationsHistory` in its audit.
  - For TK-08: `CLAUDE.md`'s RLS bullet should now list `acc`, `con` and `cus`.

### TK-74 · RLS for `inv`
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Lanes:** L-INV · **Depends on:** TK-71 · **Decision:** —
- **Where:** `backend/Api/Inventory/Inventory.Repository/Migrations/Tenant/`; the audit is at
  `backend/tests/Inventory.Api.Tests/InventoryQueryFilterTests.cs:133`.
- **State:**
  - 21 tables: `CostLayerConsumptions, CostLayers, ErrorLogs, ItemBarcodes, ItemBatches,
    ItemCategories, ItemJewelleryDetails, ItemPharmaDetails, ItemSerials, ItemStock, Items,
    MetalPurities, PriceListItems, PriceLists, RecostingAdjustments, StockAdjustmentLines,
    StockAdjustments, StockMovements, UnitOfMeasures, UomTypes, Warehouses`.
  - `CostingEngine.Worker` sets a tenant per branch (`Consumers/CostingWorker.cs:144-146`), so
    it keeps working under RLS.
- **Sub-tasks:**
  - [x] Write the migration using TK-71's template.
  - [x] Confirm that `InventorySeeder`, `UomService.cs:336,369` and `MetalPurityService` (all
        using `IgnoreQueryFilters`) always run with the tenant they filter by.
  - [x] Confirm the worker reads its list of branches from somewhere RLS doesn't hide.
  - [ ] Owner: run the suite from a dropped `INVENTORY_TEST_DB`.
- **Done when:** `inv`'s RLS assertion passes from a dropped database, and a costing run still
  costs movements.
- **Notes (Claude Opus 5.5, 2026-09-24):**
  - Migration: `Inventory.Repository/Migrations/Tenant/20260924052802_EnableRowLevelSecurity.cs`,
    TK-71's template over all 21 tables. All carry both tenant columns and none is exempt. No
    migration inserts rows.
  - The `IgnoreQueryFilters()` reads in `InventorySeeder`, `UomService` (all three) and
    `MetalPurityService` are all inside `SeedForOrganizationAsync` / `SeedNumberingSeriesAsync`.
    Only `InternalSeedController` calls them, after setting the tenant to the branch it seeds.
  - All seven `InternalStockController` actions set `CustomerId`/`OrgId` from the request. The
    context reads the same scoped `TenantContext` when it opens a connection, so the stock calls
    from Sales and Purchase keep working.
  - **The costing worker:** `HttpTenantEnumerator` gets its branches from Master's
    `internal/customers/active-organizations`. That endpoint reads `AdminDbContext` (`mst`, no
    RLS), so RLS cannot hide the list. `ProcessOrganizationAsync` opens one scope per branch and
    sets its tenant first, which is the only place the worker touches `inv`.
  - Verified by starting Master as a `NOSUPERUSER NOBYPASSRLS` owner against empty databases. It
    exited 0 with 21 of 21 `inv` tables enabled, FORCEd and policied. The bootstrap branch was
    seeded through the policies (6 unit types, 39 units, 11 purities), which exercises the
    interceptor TK-71 added to Master's hand-built `InventoryDbContext`. By hand as the owner: no
    tenant 0 unit types, own branch 6, another branch 0.
  - **Tests written:** `backend/tests/Inventory.Api.Tests/InventoryRowLevelSecurityTests.cs` (four
    tests, `SET LOCAL ROLE inv_rls_probe`). `InventoryQueryFilterTests` now exempts
    `__EFMigrationsHistory` in its audit.
  - **Owner:** the *Done when* clause "a costing run still costs movements" needs a run under a
    non-bypass role. Every suite connects as `postgres`, which ignores RLS, so no existing costing
    test proves it.
  - `InternalItemNamesController` sets no tenant, so item names on document lists come back empty.
    Confirmed and added to TK-06.
  - For TK-08: `CLAUDE.md`'s RLS bullet should list `acc`, `con`, `cus` and `inv`.

### TK-75 · Correct the stale facts in `CLAUDE.md`
- [x] completed (Claude Opus 5.5) — 2026-09-23 · documentation only, no tests
- **Lanes:** L-DOC · **Depends on:** — · **Decision:** —
- **Where:** `CLAUDE.md` sections "Still not built", "Standing caveats" and "Roadmap"; `docs/Modules.md` §8.2.
- **Sub-tasks:** fix each statement against the code:
  - [x] Reporting: it says "twelve of the 46 not built". It should say five: four fixed-asset
        reports plus *Business Performance*, since 48 sources are wired.
  - [x] Workers: "Notification.Worker … nothing else" is wrong. It has `PaymentReminderWorker`
        (see TK-20).
  - [x] Numbering series: "Sales and Purchase seed theirs when those services land" is wrong. Both
        have `SeedData/NumberingSeriesSeed.cs`, but Purchase's is never called (TK-01).
  - [x] "There is no `Modules.md`" is wrong. `docs/Modules.md` exists and holds every module's file.
  - [x] Fixed assets are described as blocked on two decisions and not started. In fact
        `acc.FixedAssets`, `FixedAssetCategories`, `DepreciationSchedules` (Books and Tax) and
        `AssetTransactions`, the `FixedAssetsController`, `DepreciationService` and two pages are
        built (see TK-11 and TK-12).
  - [x] PDF/A: it says it is "blocked on the Syncfusion licence". In fact
        `Sales.Api/Services/Pdf/PdfSharpInvoiceRenderer.cs` already archives invoice PDFs (TK-22).
  - [x] T7.3: it says "`apps/desktop` declares `targets: {}`". It has real build targets.
- **Done when:** each statement above matches the code.
- **Notes:**
  - Beyond the seven listed, the same commit fixes: the `ReportLayerCertificationTests` sentence
    in the test-count caveat (it expects 48 now; left for TK-09 to confirm by a run); the
    fixed-asset line under Undecided, now pointing at D-19/D-20; `Modules.md`'s T3.4 status row
    and its School table's "`Notification.Worker` is still an empty project"; and the
    weighted-average section of "Inventory & costing", which TK-85 handed here.
  - Two of the card's statements needed sharpening against the code. Purchase's
    `NumberingSeriesSeed` **is** called, by `PurchaseSeeder` behind its seed endpoint; it is
    `TenantSeeder` and the startup bootstrap that never reach it. And the archived invoice PDF is
    plain PDF, not PDF/A.
  - The Undecided line on straight-line versus books-and-tax is reworded, not struck: the code
    built both, but the owner has not confirmed it, so it now points at D-08.
  - Not fixed, out of this card's list: `CLAUDE.md` still cites `docs/modules/Sales.md`,
    `docs/Master.md` and `TRANSACTIONS-ACCOUNTING-BANKING.md`, none of which exist.

### TK-76 · Sales delivery challan: post, void and page
- [x] completed (Claude Opus 5.5) — 2026-09-23 · tests written, not run
- **Lanes:** L-SAL, L-SAL-UI · **Depends on:** — · **Decision:** —
- **Where:**
  - `backend/Api/Sales/Sales.Api/Services/DeliveryChallanService.cs`: `SaveAsync`, `PostAsync`, `VoidAsync`.
  - `backend/Api/Sales/Sales.Api/Controllers/DeliveryChallansController.cs`
  - `backend/tests/Sales.Api.Tests/DeliveryChallanServiceTests.cs`
  - `frontend/libs/sales/sales-ui/src/lib/delivery-challan-form/`, `frontend/libs/sales/sales-core/src/lib/delivery-challan.service.ts`
- **State (as left, 2026-09-23):**
  - Save, post and void return `DeliveryChallanResult`; the controller maps 404 / 409 / 422 / 503.
    Another branch's challan is 404, per TK-71's decision.
  - Each line carries `SalesOrderDetailId`. On a challan against an order every line must name one
    of the order's lines (same item, same customer, order confirmed and open); otherwise none may.
    Posting re-checks the order and refuses a line that would deliver more than is outstanding.
  - Posting issues stock and moves the order's delivered and reserved quantities. It writes **no**
    ledger post and **no** `SalesRegister` rows.
  - Void is draft-only, needs a reason, and moves nothing.
  - The form: *Load order* (fills the customer and the outstanding lines), transporter, e-way bill
    no. and date, GSTIN, place of supply, the five challan types, Post and Void.
- **Sub-tasks:**
  - [x] Check the `SalesRegister` rows the post writes. Confirmed against `docs/Modules.md` §6: the
        register is fed by invoices and credit notes only, and `GstController` reads it with no
        type filter, so a challan's rows double-counted in GSTR-1 and GSTR-3B. Removed.
  - [x] Replace the `throw new InvalidOperationException` guards in save, post and void with
        outcomes the controller maps to 404, 409 or 422 (503 when rates or settings can't be read).
  - [x] Add form controls for `salesOrderId`, `transporterName`, `ewayBillNo` and `ewayBillDate`,
        and give each line its `salesOrderDetailId` (the request had nowhere to carry it, so no
        posted challan ever moved its order).
  - [x] Test: posting a challan against an order moves `DeliveredQuantity` and reduces `ReservedQuantity`.
  - [x] ~~Test: posting a `Sale` challan posts a balanced GDNI/Inventory journal.~~ Withdrawn by the
        owner's decision of 2026-09-23. The challan no longer posts; see TK-10.
  - [x] Test: voiding a draft works; voiding a posted challan is refused.
  - [x] Test: another branch's challan is `NotFound()` on every route (was `Forbid()`, before TK-71).
  - [ ] Owner: run `Sales.Api.Tests`.
- **Done when:** a challan raised from the screen against an order posts, moves stock, and updates
  the order.
- **Notes:**
  - **Why the GDNI post went.** `Goods Delivered Not Invoiced` is not in
    `Accounting.Repository/SeedData/ChartOfAccountsSeed.cs`, so any non-zero post was refused. And
    `StockLedgerMapping` already posts every sourced `Issue` movement, this challan's included, as
    Dr COGS / Cr Inventory under the same `(DLC, id)`. The two keys differ (CONTROL at detail 0,
    COGS per line), so both rows stood and Inventory was credited twice. The owner chose to drop
    the challan's post here and move the GDNI mapping to TK-10.
  - The void used to subtract a draft's quantities from its order, and delete `SalesRegister` rows
    a draft never wrote. Both removed.
  - Other fixes on the way: the currency defaulted to `USD` (now the branch's base currency); the
    form sent no tax group, so every challan saved from the screen was untaxed; its type list sent
    "Transfer" as 2, which the server reads as `Approval`; the list and view sent `Status` as a
    number while the screen compared names. `Status` is now a string, like the invoice's.
  - `ReleaseReservation` is now per line (`SalesOrderDetailId.HasValue`). With every line of an
    order challan required to name an order line, that equals the old header test — see TK-78.
  - Docs: `frontend/apps/docs/content/delivery-challans.md` (new, `partial` in the manifest),
    `sales-orders.md`, and release notes.
  - **Left for `L-DOC`:** `CLAUDE.md` "Still not built" still says the challan has "a scaffold page
    but no verified path".
  - Tests written, not run: `backend/tests/Sales.Api.Tests/DeliveryChallanServiceTests.cs` (20
    tests: save, order-link refusals, post, over-delivery, refused stock, void, cross-branch 404,
    controller mapping); `RecordingInventory` in `Stubs.cs` now records issues and can refuse them.
    Frontend: section 4 of `libs/sales/sales-ui/src/lib/sales-forms.spec.ts` (9 tests) and the
    challan half of `CHAL-SALES-05` in `challenger-m4-m5-verification.spec.ts`.

### TK-77 · Sales credit note: guards, void reason and stock return
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Lanes:** L-SAL, L-SAL-UI, L-INV (one controller line: a return must be recorded as a return), L-ACC (seed Sales Returns and Round Off; owner's decision 2026-09-23) · **Depends on:** — · **Decision:** —
- **Where:**
  - `backend/Api/Sales/Sales.Api/Services/CreditNoteService.cs`, `Controllers/CreditNotesController.cs`
  - `backend/Api/Inventory/Inventory.Api/Controllers/InternalStockController.cs` (`Receipt`)
  - `backend/Api/Accounting/Accounting.Entity/Enums/SystemAccount.cs`, `Accounting.Repository/SeedData/ChartOfAccountsSeed.cs`
  - `frontend/libs/sales/sales-ui/src/lib/credit-note-form/`, `frontend/libs/sales/sales-core/src/lib/credit-note.service.ts`
- **State (as left, 2026-09-24):**
  - Save, post and void return `CreditNoteResult`; the controller maps 404 / 409 / 422 / 503. Post
    needs `sales.approve`, void `sales.void`; every id route is `{id:long}`.
  - A note must correct a posted invoice, for the same customer, line by line and item by item. A
    sales return cannot exceed the invoice line's `Quantity − ReturnedQuantity`.
  - Posting claims the note against its invoice, returns goods (sales returns only), posts AR /
    Sales Returns / Output GST / Round Off, and writes the GST register. A refused stock or ledger
    step releases the claim.
  - Void takes a reason. A posted non-return withdraws its ledger rows, register rows and claim; a
    posted sales return is refused.
- **Sub-tasks:**
  - [x] Add `[PermissionAction("approve")]` to `Post` and `[PermissionAction("void")]` to `Void`,
        and change the routes to `{id:long}`.
  - [x] Add `VoidCreditNoteRequest { [Required(ErrorMessage=…)] string Reason }`, and pass the
        reason through to `VoidAsync` and the stored row.
  - [x] Find and fix the `ReturnsStockMovementId` defect. It was on Inventory's side:
        `InternalStockController.Receipt` recorded every line as `Receipt`, and the costing engine
        only walks a `SalesReturn` back to its layers, so the id was stored and never read. Now a
        line naming `ReturnsStockMovementId` is a `SalesReturn` and reports no value. The credit
        note also sent `UnitPrice` as the cost; it now sends the invoice line's `UnitCost`.
  - [x] Add a void-reason field to the form. The form was rebuilt (below), because its reason codes
        did not match the server's and it could not produce a valid note.
  - [x] Test: posting reverses the revenue and GST legs, and returns stock at the original cost.
  - [x] Test: a void without a reason gets 400 (model validation on `VoidCreditNoteRequest`, and
        the service refuses a blank one).
  - [x] Test: `EndpointGuardAudit` still passes — covered by the existing `EndpointGuardTests`; the
        two new attributes are the documented `PermissionAction` form.
  - [ ] Owner: run `Sales.Api.Tests`, `Inventory.Api.Tests` and `Accounting.Api.Tests`.
- **Done when:** a credit note against a posted invoice posts, returns its stock, and can be voided
  only with a reason.
- **Notes:**
  - **Sales posted to account names the chart does not have.** The ledger resolves
    `AccountSystemName` exactly. The invoice used "Sales", "Tax Payable" and "Round Off"; the
    credit note "Sales Returns" and "Tax Payable". By the owner's decision of 2026-09-23 both now
    use the chart's names (Sales Revenue, Output GST), and the seed gains **Sales Returns** (4200,
    contra Income) and **Round Off** (5900, Expense). Existing branches get them through
    `AccountService`'s idempotent seed, which is what the admin retry runs.
    `Sales.Api.Tests.SalesAccountNameTests` reads every `const string …Account` in
    `Sales.Api.Services` and holds it to `ChartOfAccountsSeed`. Two names are allowed to fail, each
    with a card: "Goods Delivered Not Invoiced" and "Cash" (the POS till).
  - The credit note no longer posts Inventory / COGS legs. The costing worker posts a sourced
    `SalesReturn` as Dr Inventory / Cr COGS (`StockLedgerMapping`), so posting both double-counted.
    The same question for the invoice and the challan is on the GDNI card.
  - The form: reason codes were 1–7 against a server enum of 0–4, so "Sales Return" saved as a price
    correction and no stock ever came back. Lines sent the grid's own row id as `invoiceDetailId`
    and no tax group. An edit always created a new note. Now: *Load invoice*, or pick from the
    customer's open invoices in the allocation grid; lines carry their invoice line; Post and Void.
  - Found, not fixed (`L-PUR`): Purchase posts Input GST with `SubAccountReferenceId =
    line.TaxMasterId`, but `TaxMasterService` provisions tax sub-accounts keyed on `TaxGroupId`.
    Where the two ids differ, the ledger refuses a bill with *SubAccountMissing*.
  - Not atomic, and said so in `PostAsync`'s summary: the claim, the stock return and the ledger
    post are three calls to two services. A refusal after the claim releases it; a retry is safe
    because Inventory treats a repeated receipt as already recorded and the ledger replaces a
    document's rows.
  - **Two cards are numbered TK-10.** The GDNI card was added with TK-76 (commit `cb7da6c`); the
    trigger-restoring card took the same number later, and `CLAUDE.md` now cites it. The comments in
    `SalesAccountNameTests` and `DeliveryChallanService` mean the GDNI card.
  - Docs: `frontend/apps/docs/content/credit-notes.md` (new), the manifest, release notes.
  - **Left for `L-DOC`:** `CLAUDE.md` "Still not built" still calls the credit note a scaffold
    with no verified path.
  - Tests written, not run: `backend/tests/Sales.Api.Tests/CreditNoteServiceTests.cs` (rewritten,
    21 tests), `SalesAccountNameTests.cs` (new), `InvoicePostingTests.cs` and
    `InvoicesControllerTests.cs` (renamed accounts), `Stubs.cs` (receipts and claims recorded,
    refusable); `backend/tests/Inventory.Api.Tests/InternalStockControllerTests.cs` (a return is a
    `SalesReturn`); section 3 of `frontend/libs/sales/sales-ui/src/lib/sales-forms.spec.ts` (14
    tests) and `CHAL-SALES-05` in `challenger-m4-m5-verification.spec.ts`.

### TK-78 · Partial fulfilment (T3.6): what's left
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Lanes:** L-SAL, L-SAL-UI (the billing tag and the From-an-order filter) · **Depends on:** TK-76 · **Decision:** —
- **Where:**
  - `backend/Api/Sales/Sales.Api/Services/InvoiceService.cs`: `PostAsync` (`ReadBilledOrderAsync`,
    `IssueQuantities`), `VoidAsync`, `CreateFromSalesOrderAsync`, `FulfillSalesOrderAsync`.
  - `Services/SalesOrderFulfilment.cs` (new): the one fulfilment-status rule.
  - `Controllers/SalesOrdersController.cs`: `Fulfill` now only maps the result.
  - `SalesOrderDetail.InvoicedQuantity` and migration `20260924054101_SalesOrderInvoicedQuantity`.
- **State (as left, 2026-09-24):**
  - An order line carries Delivered, Invoiced and Reserved (still held). The invoice's post moves
    all three for every line naming an order line; a posted invoice's void gives back only
    Invoiced. `FulfilmentStatus` follows delivery; `IsFullyInvoiced` (list and view) follows billing.
  - An invoice against an order line bills delivered-not-invoiced goods first and issues only the
    rest; against a named challan it issues nothing. Over-billing is refused at post.
- **Sub-tasks:**
  - [x] Move `Fulfill`'s body out of the controller. It went to `InvoiceService.FulfillSalesOrderAsync`
        rather than `SalesOrderService`, beside `CreateFromSalesOrderAsync`, sharing one builder and
        avoiding an invoice dependency in the order service. The controller keeps
        `[Transactional(Serializable)]` for the filter and opens nothing.
  - [x] Add `InvoicedQuantity` (`decimal(18,6)` to match its neighbours, not 18,4) with
        `chk_salesorderdetails_invoiced`; advanced on post, reversed on void.
  - [x] `IsFullyInvoiced` on `SalesOrderListItem` (so the view has it too).
  - [x] Verified: an invoice with `DeliveryChallanId` issues no stock. The real double-issue was an
        invoice against the order that did **not** name the challan, including `Fulfill`; fixed by
        `IssueQuantities`.
  - [x] Test: order 10 → challan 4 gives `PartlyDelivered`, delivered 4 and reserved 6.
  - [x] Test: challan 6 more gives `Closed` and reserved 0.
  - [x] Test: an invoice against the first challan issues no stock and moves `InvoicedQuantity` to 4.
  - [ ] Owner: run `Sales.Api.Tests` (and the frontend suite).
- **Done when:** an order is delivered and billed in two parts, its status goes Open →
  PartlyDelivered → Closed, and its reservation reaches zero.
- **Notes:**
  - Found and fixed on the way: short-close released `Reserved − Delivered`, but every delivery
    already takes its quantity off `Reserved`, so four of ten delivered kept four units reserved
    for good. It releases `Reserved` now. `CreateFromSalesOrderAsync` refused any order with an
    invoice at all; it bills what is left. The *From an order* dialog hid any order with an
    invoice; it filters on `IsFullyInvoiced`. `ReleaseReservation` is per invoice line.
    `InvoiceService.VoidAsync` never loaded the invoice's lines, so its challan reversal walked an
    empty list; it includes them now.
  - Left: the invoice's challan branch still matches challan lines to invoice lines by `ItemId`
    and does not check the challan is posted or the customer's. Its COGS legs name GDNI, which
    the GDNI card (TK-10, first of the two) seeds.
  - Shared `L-SAL` with TK-81 by the owner's decision; TK-81 added no `sal` migration.
  - Tests written, not run: `backend/tests/Sales.Api.Tests/PartialFulfilmentTests.cs` (10 tests),
    the `IInvoiceService` stub in `InvoicesControllerTests.cs`;
    `frontend/libs/sales/sales-ui/src/lib/order-to-invoice/order-to-invoice.dialog.spec.ts` (new)
    and `SOR-T1-07` in `sales-forms.spec.ts`.

### TK-79 · `apps/desktop`: a real cart
- [x] completed (Claude Opus 5.5) — 2026-09-23 · tests written, not run
- **Lanes:** L-DSK · **Depends on:** TK-15 · **Decision:** —
- **Where:**
  - `frontend/apps/desktop/src/app/pos-terminal/pos-terminal.component.{ts,html,scss}`: the till.
  - `pos-cart.ts`: the cart as pure functions over `DocumentLine`, all arithmetic through `line-math.ts`.
  - `pos-lookup.service.ts`: customers, the walk-in contact, items, item detail, sales rates, branch.
  - `esc-pos.service.ts`: receipt bytes, unchanged apart from a typed `generateReceipt`.
  - `frontend/apps/desktop/src/app/app.config.ts`: now carries `authInterceptor`.
- **Sub-tasks:**
  - [x] Add a cart held in signals: lines, quantity, price, line total, and a GST preview from
        `line-math.ts`.
  - [x] Add items by search. ~~Reusing TK-15's `SalesLookupService.items()`~~: that service doesn't
        exist and its lane was held by TK-76, so `PosLookupService` in `apps/desktop` does it for now.
  - [x] Replace `contactId: 1` with the customer picker, defaulting to a walk-in contact looked up
        by code (`WALKIN`) rather than by id.
  - [x] Split the component into `.html` and `.scss` if it's still inline (house rule). It already was.
  - [x] `nx build desktop` and lint are clean. Typecheck is clean too.
  - [x] Test: `pos-cart.spec.ts` (add, merge, quantity, price, remove, reprice across a state line,
        totals) and `pos-lookup.service.spec.ts` (URLs, walk-in exact-code match, MRP fallback,
        sales-rate filter).
  - [ ] Owner: `npm run test`, then build a cart on the desktop app at 360px.
- **Done when:** the terminal builds a cart of real items for a real customer. Posting the sale is TK-39.
- **Notes:**
  - Claimed by name on the owner's instruction while TK-15 was still open.
  - **Nothing seeds a `WALKIN` contact.** The till falls back to a warning and a chosen customer
    until one exists. Seeding one per branch belongs in Master's contacts seed (L-CON). It needs a
    card, and it is a decision too: a walk-in contact is one customer for every counter sale.
  - **The till never sent a bearer token.** `apps/desktop` registered only `apiBaseUrlInterceptor`,
    so every call after sign-in would have been a 401. `authInterceptor` now follows it, in the same
    order as `apps/web`.
  - **A tax-inclusive price can land a paisa off at the till.** Two units at ₹45 inclusive of 18%
    GST total ₹89.99 intra-state and ₹90.00 inter-state, because `line-math.ts` rounds each
    component separately after backing out the taxable value. This matches the C# side and the
    shared fixture, so it's not a till bug. But a customer handed ₹89.99 for a ₹90 MRP will notice.
    A round-off line on POS invoices is the usual answer; TK-40 should decide.
  - Checkout still sends the scaffold's plain draft invoice (now with real lines and customer).
    Replacing it with TK-39's endpoint is TK-40's "Post through TK-39".
  - Checked by screenshot, not tests: `dist/apps/desktop/browser` served with the API mocked in
    Playwright, at 1280px and 360px. Lines stack into cards at 360px. Choosing a customer from
    another state switches the totals from CGST + SGST to IGST.

### TK-80 · Printing.Api: move the template API and renderer into the service
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Lanes:** L-PRT, L-KERNEL · **Depends on:** — · **Decision:** —
- **Where:**
  - `docs/Modules.md` "Printing" (from about line 974): the design, and the list of what moves.
  - `backend/Api/Master/Master.Api/Controllers/PrintTemplatesController.cs`: 10 routes, class-level
    `[RequireModulePermission("settings")]`.
  - `backend/shared/Shared.Kernel/Printing/*`
  - `backend/Api/Printing/Printing.Api/Program.cs`
- **State:**
  - `Printing.Api` starts on port 4508 with no controller, so it answers 401 to everything.
  - The branch question is **decided** in the design: callers push the payload under the user's
    own token.
  - The design also splits `Shared.Kernel.Printing` into two parts:
    - the contract stays: `PrintPayload`, `PrintFormatContext`, `PlaceholderCatalog`,
      `DocumentTypeCatalog`, `PlaceholderType`, `PlaceholderKind` and `MergeTags`;
    - the machinery moves to `Printing.Api`: `PrintRenderer`, `PrintSubstitution`,
      `PrintSettingsValidator`, `SegmentSanitizer`, `PrintGeometry`, `PrintMetrics`,
      `DefaultLayoutGenerator` and `SamplePayload`.
  - **Checked 2026-09-24: none of it can move yet.** Master's `PrintTemplateService`, seeder,
    `PrintTemplate` entity and `ContactsDbContext` use nearly every one of those types, so moving
    them breaks Master, and this card keeps Master working until TK-81.
- **Sub-tasks:**
  - [x] ~~Move the stored shape to `Printing.Entity`~~ and ~~move the machinery to `Printing.Api`~~.
        By the owner's decision (2026-09-24), Printing uses them **in place** from
        `Shared.Kernel.Printing`, and the physical move, with the `HtmlSanitizer` and `AngleSharp`
        references, goes to TK-81, once Master's copy (the last other user) is deleted. No code
        is duplicated in the meantime. The request and response models are Printing's own:
        `Printing.Entity/Models/PrintTemplateModels.cs` and `PrintRenderModels.cs`.
  - [x] Recreate the 10 routes as `api/print-templates` on Printing, with the same guard, and add
        `POST api/print/render`, which takes a `PrintPayload` plus a document type.
        `PrintTemplatesController` and `PrintTemplateService` are ported from Master onto
        `PrintingDbContext`. `PrintController.Render` resolves the layout (the named template,
        then the branch default, then the platform layout) and renders.
  - [x] Add a Gateway route for `/api/print-templates` and `/api/print`, pointing at 4508. Routes
        and cluster are in every `appsettings*.json`, and `printing` is added to
        `gatewayClusters` in `deploy/azure/main.bicep` (L-DEPS, its own commit).
  - [x] Test: carry over Master's print-template tests (`PrintTemplateServiceTests`, all but the
        seeding test, which moves with the seeder in TK-81).
  - [x] Test: render a sample payload (`PrintRenderTests`, sent through JSON as a caller would;
        `PrintPayloadReaderTests`).
  - [x] Test: `EndpointGuardAudit` passes (`Printing.Api.Tests.EndpointGuardTests`).
  - [ ] Owner: run `Printing.Api.Tests` from a dropped `PRINTING_TEST_DB`.
- **Done when:** Printing serves the template API and renders a payload. Master's copy still exists
  until TK-81.
- **Notes:** old card TK-22 is retired (section 4).
  - **The render payload arrives as JSON, and the formatter can't read that.** `MaskFormatter`
    matches on CLR types, so a `JsonElement` amount would print as bare digits and an image URL
    as nothing. `PrintPayloadReader` turns numbers into decimals and strings into strings, and
    matches keys case-insensitively. A render test sends an amount through JSON and asserts the
    Indian grouping.
  - **`api/print/render` is signed-in only, with no module guard**, and is named as an exemption
    in the guard test. Printing is handed the document's data rather than reading it, and the
    service that built the payload already checked the permission. One route prints twelve
    document types across three modules, so no single module fits. The only thing it reads is
    the branch's own template.
  - `prt` has no rows until TK-81 seeds it. Until then a render uses the platform layout and the
    template routes list nothing. Master's `api/print-templates` never had a gateway route, so no
    traffic moved.
  - No release note: no screen calls either service's template API yet.

### TK-81 · Printing cutover: serve from `prt`, drop `con.PrintTemplates`
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Lanes:** L-PRT, L-CON, L-SAL, L-SAL-UI (the invoice print page), L-MST (`TenantSeeder`), L-KERNEL (the machinery move), L-DEPS (package references, per commit), L-DOC (the service count in `CLAUDE.md`, last commit) · **Depends on:** TK-80 · **Decision:** D-13 (answered)
- **Where:**
  - `backend/Api/Master/Master.Api/Services/PrintTemplateSeeder.cs`
  - `con.PrintTemplates`
  - `frontend/libs/sales/sales-ui/src/lib/invoice-print/invoice-print.page.ts`
- **Sub-tasks:**
  - [x] Move branch seeding of templates to Printing, with its own `internal/seed/organization`,
        and add it to `TenantSeeder.Services`. That needs `L-MST`. `PrintTemplateSeed`
        (`Printing.Repository/SeedData`) builds the rows, `PrintTemplateSeeder` writes them
        idempotently, Master's startup bootstrap seeds `prt` for its branch, and
        `Seeding:Printing` is in Master's settings and `settings.bicep`.
  - [ ] ~~Copy existing `con.PrintTemplates` rows to `prt.PrintTemplates`, keeping ids so every
        `PrintTemplateId` on the 14 document headers still resolves.~~ **Re-seed instead, with no
        copy** (owner, 2026-09-24). The copy could only be a raw `INSERT … SELECT` across two
        services' schemas, which is outside hard rule 1's exceptions, and nothing is deployed
        (D-13). Existing branches are re-seeded through the same idempotent path as new ones. An
        old `PrintTemplateId` that no longer resolves falls back to the branch default, then to
        the standard layout, so no document stops printing.
  - [x] Add a per-document print route. Sales builds a `PrintPayload` from its own data and posts
        it to `api/print/render` with the user's token. `GET api/sales/invoices/{id}/print`
        (`sales.print`) → `InvoicePrintService` → `InvoicePrintPayload` (pure) → `PrintingClient`,
        which forwards the caller's `Authorization` header. Not filled yet: the place of supply
        prints as its two-digit state code (Sales stores `PlaceOfSupplyStateId = 0`), and the
        amount in words is blank (there is no C# speller yet).
  - [x] Point the frontend's template calls at Printing (the Gateway route). TK-82's editor calls
        `api/print-templates`, which the gateway sends to Printing, and the invoice print page
        (`invoice-print.page.ts`) now shows Sales' `…/print` result. That page's own hand-built
        layout is gone. Drafts print stamped PROFORMA and voided invoices VOID, through a new
        `Watermark` on the render request that the renderer stamps on every page, whatever the
        template holds.
  - [x] Drop `con.PrintTemplates` in a `con` migration. **Only after D-13** confirms nothing is
        deployed, or migrate the data in the same step. `DropPrintTemplates` (D-13: nothing
        deployed). Master's `PrintTemplatesController`, `PrintTemplateService`,
        `PrintTemplateSeeder`, entity, models and `PrintTemplateServiceTests` are deleted, and its
        seed endpoint no longer seeds templates. `has-pending-model-changes` is clean for both
        Master contexts.
  - [x] The physical move out of `Shared.Kernel.Printing` (from TK-80's handover). The stored shape
        is in `Printing.Entity` (`Models/PrintSettings`, `Models/PrintContent`, `Enums/PrintEnums`).
        `PrintJson` and `DefaultLayoutGenerator` are in `Printing.Repository`, because the seed
        builder and Master's bootstrap need the generator and the Repository can't reach Api. The
        machinery is in `Printing.Api/Rendering`, with `MaskFormatter` and `PrintSegments` split
        out and `TemplateTags` holding `MergeTags`' two `PrintContent` overloads. The contract
        stays: `PrintPayload`, `PrintFormatContext`, both catalogues, `PlaceholderType`/`Kind`, and
        `MergeTags`' text-level half. `AngleSharp` and `HtmlSanitizer` moved to `Printing.Api`
        (L-DEPS). The renderer, settings, sanitiser and layout tests moved to `Printing.Api.Tests`.
  - [x] Change the service count in `CLAUDE.md` from 7 to 8. Also updated: the Printing
        paragraphs, the Master `con` row, a new Printing row, the tenant schema list, and
        `docs/Modules.md`'s Printing status table.
  - [ ] Owner: `Printing.Api.Tests`, `Sales.Api.Tests`, `Master.Api.Tests` and
        `Shared.Kernel.Tests` from dropped databases, since `con` has a new migration and four test
        files moved between projects. Then `npm run test`.
- **Done when:** a sales invoice prints through Printing, and `con.PrintTemplates` no longer exists.
- **Notes:**
  - From TK-80 (owner's decision, 2026-09-24): the physical move out of `Shared.Kernel.Printing`
    happens here, once Master's copy is gone. Move the stored shape (`PrintSettings`,
    `PrintContent`, segment types, `PrintJson`) to `Printing.Entity`, and the machinery
    (`PrintRenderer`, `PrintSubstitution`, `PrintSettingsValidator`, `SegmentSanitizer`,
    `PrintGeometry`, `PrintMetrics`, `DefaultLayoutGenerator`, `SamplePayload`, `MaskFormatter`)
    to `Printing.Api`. Move the `HtmlSanitizer` and `AngleSharp` references off `Shared.Kernel`
    (L-DEPS). Move `Shared.Kernel.Tests`' renderer, sanitiser, settings and layout tests to
    `Printing.Api.Tests`. The contract (`PrintPayload`, `PrintFormatContext`, the two catalogues,
    `MergeTags`) stays.
  - Printing's `PrintTemplateService` was a port of Master's. Master's is deleted now, so there is
    one copy.
  - Done in five commits on `main`: seeding, the Sales print route, the print page with watermark,
    Master's copy dropped, and the kernel move. Then this one.
  - **Drafts printed as clean tax invoices, briefly, in the middle of this card.** The old print
    page stamped PROFORMA itself; the standard layout prints no status. `Watermark` on the render
    request closes that, and the renderer stamps it so a template can't leave it off.
  - Left for later cards: print routes for the other eleven document types; amount in words in C#;
    a state-name lookup for the place of supply (Sales stores `PlaceOfSupplyStateId = 0`
    everywhere, which is its own bug); PDF/A (D-11).
  - Existing branches other than the bootstrap one get their `prt` templates when their setup is
    re-run from `apps/admin`. Until then their documents print with the standard layout.
  - D-13 is answered: nothing is deployed, so the drop can go in the same change as the copy.
    On 2026-09-24 this card was still unclaimable only because TK-77 held L-SAL.
  - From TK-82: the editor reads `prt` through the gateway, so it lists nothing until this card
    seeds `prt` and copies `con`'s rows. When that lands, remove the "ready-made templates" and
    "printing a real document" bullets from `masters.md` "What is not here yet". Then write the
    release note that templates appear in the editor and documents print through them.

### TK-82 · Print-template editor screen
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Lanes:** L-MASTER-UI, L-MST (switch on the print-template menu rows in `MenuSeed`, and the admin migration), L-WEB (the route in `apps/web`), L-UI (the fallback entry in `libs/app-shell/src/lib/shell-screens.ts`) · **Depends on:** TK-80 · **Decision:** —
- **Where:**
  - `libs/master/master-ui`, a new `print-templates/` page; the shared master pages table in
    `docs/Modules.md` Platform says this is where it goes.
  - The API: `GET api/print-templates`, `GET document-types`, `GET {docType}/placeholders`,
    `PUT {id}`, `POST {id}/reset` and `POST {id}/preview`.
- **Sub-tasks:**
  - [x] A list of templates per document type. `libs/master/master-ui/src/lib/print-templates/`,
        over `PrintTemplateService` in `libs/master/master-core`.
  - [x] An editor over the five bands, with a placeholder picker fed by `{docType}/placeholders`.
        The picker inserts `{{Tag}}` at the cursor of the band last focused (`insertTag`, pure).
  - [x] A live preview through `{id}/preview`. One button, **Save and preview**, because the
        endpoint renders the *saved* template; a preview of unsaved edits would be a second
        rendering path.
  - [x] Reset to the default. Also **Make default**, and **Create from the standard layout**,
        because `prt` has no rows until TK-81 and without it there is nothing to edit.
  - [x] At 360px, the editor stacks and the preview becomes a sheet. It replaces the list and
        editor inside the content area; a fixed overlay can't rise above the shell's top bar,
        because the content cell is its own stacking context.
  - [x] ~~Add the route with `data.access: { permission: 'settings.edit' }`~~. The route takes
        `data: { permission: 'settings.view' }`: `data.access` is stage H0 and not built, and the
        menu offers these rows to `settings.view`, which `MenuSeed` says the router must never
        then refuse. The page is read-only without `settings.edit`, and the API refuses a write
        regardless. There are two routes, `settings/print-templates` and
        `settings/print-templates/:docType`.
  - [x] A menu row. The twelve `pt-*` rows under group 117 already existed, inactive and without
        routes. They are switched on in `MenuSeed`, with the migration
        `EnablePrintTemplateMenus` (12 `UpdateData` calls; `has-pending-model-changes` is clean).
        There is also a fallback entry in `shell-screens.ts`.
  - [x] Update the docs page and the release notes (`masters.md` "The editor" and "What is not
        here yet"; two release notes).
  - [x] Lint, typecheck and build are clean (`web`, `docs`, and the backend with `-warnaserror`).
  - [x] Test: `print-template.service.spec.ts` (URLs and verbs, `insertTag`, band order) and
        `Master.Api.Tests.PrintTemplateMenuTests` (the rows are active and name exactly the
        printable document types).
  - [ ] Owner: `npm run test`, and `Master.Api.Tests` from dropped databases (the admin
        migration is new).
- **Done when:** a user changes a template and sees the change in the preview.
- **Notes:**
  - Checked in Chromium against a mocked API at 1400px and 360px: type into a band, insert a
    field, save, and the preview shows it.
  - **The shell made every page wider than a phone.** At ≤860px the shell's single grid column
    was a bare `1fr`, which can't shrink below the top bar's 565px of buttons. Every page's
    content area was 565px wide on a 360px screen, the dashboard included. It is now
    `minmax(0, 1fr)` in `shell.component.scss` (L-UI). The top bar's own buttons past the edge
    are still clipped, as they were before. That needs its own card: the phone design moves
    them into a sheet.
  - `prt` is empty until TK-81, so the editor lists nothing for any branch and a user starts
    from **Create from the standard layout**. The docs say so under "What is not here yet".
  - `CLAUDE.md`'s Master `con` row still says "No editor screen". That's L-DOC; it's stale as of
    this card.

### TK-83 · The four fixed-asset reports
- [x] completed (Claude Opus 5.5) — 2026-09-23 · tests written, not run
- **Lanes:** L-RPT · **Depends on:** TK-11 · **Decision:** —
- **Where:**
  - `docs/Modules.md` §8.2 (the four names).
  - An existing source to copy, e.g. `AccountMovementSource`.
  - `ReportingDbContext.cs`, `ConfigureReadModels`: read-only mappings using `ExcludeFromMigrations`.
- **Sub-tasks:**
  - [x] Map `acc.FixedAssets`, `FixedAssetCategories`, `DepreciationSchedules` and
        `AssetTransactions` read-only on `ReportingDbContext`.
  - [x] Build four sources: Depreciation Schedule, Disposal Schedule, Fixed Asset Reconciliation
        and Fixed Assets Schedule. Take their columns from `reports.json`.
  - [x] For each source, add the `AddScoped<IReportSource, …>` in `Program.cs`, an entry in the
        `ReportSourceTests.Sources` list, and a catalog seed row per column. Then change the
        expected count in `ReportLayerCertificationTests.cs:97` to 52.
  - [x] Test: `FixedAssetReportTests` — the roll-forward, disposals and the gain / capital gain /
        loss split, Books-only depreciation, and both sides of the reconciliation, over lists.
  - [ ] Owner: run `Reporting.Api.Tests`.
  - [ ] Owner: open the four reports on a branch with registered, depreciated and disposed
        assets. The lists prove the arithmetic, not the data a real branch holds.
- **Done when:** `ReportLayerCertificationTests` counts all four.
- **Notes:**
  - Claimed by the owner's instruction by name while TK-11 was still open. TK-11 changes
    Accounting's controller and service only, not the four tables these reports read.
  - **One query behind four reports.** `FixedAssetRegister.Rows` rolls each asset forward over
    the period. Three sources read its row directly, and the reconciliation totals it by account,
    so the four reports cannot disagree about what is opening, what is an addition or what a
    disposal removes. The rules are in its doc comment.
  - **Translation was checked, and behaviour was not.** Every source was executed through
    `ReportSource.ExecuteAsync` against an empty `acc` schema on PostgreSQL 16, in a scratch
    database. That covered with and without dates, a sort with a counted third page, a group-by,
    and a money filter. All of them ran. The reconciliation needed three rewrites to get there:
    - EF cannot translate a `let` over a composed query;
    - it evaluates a null test on an outer-joined aggregate on the client;
    - it will not union projections that assign different members.

    The shape that translates is a union of flat movements, grouped by account and side. Don't
    "simplify" it back into joins.
  - **SQL size.** The per-asset SQL is around 27 KB, because EF inlines the correlated
    subqueries in every expression that uses them. That's fine for a register of hundreds of
    assets. At tens of thousands, it will need a flatter query.
  - **Columns left out.** Brand, Outlet, Warranty Expiry, Cost Limit and Averaging Method /
    Avg Method are in `reports.json`, but the register has no field for them.
  - **Columns added.** The Disposal Schedule also has Disposal Date, Accum Dep and Gain on
    Disposal. The reconciliation has Account Code and Account.
  - **How the Disposal Schedule's columns are read.** Purchased = purchase price,
    Disposed = cost removed, AssetValue = book value on the disposal date, Sale Price = proceeds.
  - **Found, not fixed (outside this card):**
    - `ReportSource<TRow>.FormatRowsAsync` is never called by `ExecuteAsync`. The account-type
      names that `AccountMovementSource`, `TrialBalanceSource` and the others resolve there are
      therefore always empty. This is a one-line fix in `IReportSource.cs`, but it changes the
      output of existing reports, so it needs its own card.
    - The report list prints `ReportModule` as it is named. The heading reads "FixedAssets"
      (L-RPT-UI, `report-list.page.html:14`).
    - `docs/Modules.md` §8.2 and `CLAUDE.md` "Still not built" still say the four reports are
      unbuilt. They now stand at **52 sources wired end to end, 45 of the 46 in `reports.json`**.
      Updating them needs L-DOC.
  - No migration was needed. Read models excluded from migrations don't change the migration
    diff, and `dotnet ef migrations has-pending-model-changes` reports none.

### TK-84 · *Business Performance* report
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Lanes:** L-RPT · **Depends on:** — · **Decision:** D-15 (answered 2026-09-24)
- **Sub-tasks:**
  - [x] Build what D-15 specifies (Xero-style KPI ratios), wired through the same four layers as
        TK-83: `BusinessPerformanceSource`, its `Program.cs` line, a catalog seed entry and the
        `ReportSourceTests.Sources` list. The count in `ReportLayerCertificationTests` is now 53.
  - [x] Test: `BusinessPerformanceTests` — all eight ratios over one hand-worked set of books,
        the annualising of a short period, empty denominators, order, and empty books.
  - [ ] Owner: run `Reporting.Api.Tests`.
  - [ ] Owner: open the report on a branch that has traded, and check two ratios against the
        Profit & Loss and the Balance Sheet using the Numerator and Denominator columns.
- **Done when:** `ReportLayerCertificationTests` counts it.
- **Notes:**
  - **One row per ratio**, with Unit, Numerator, Denominator and a Calculation text, because one
    Value column holds percentages, days, times and an amount. That's also why nothing totals
    and the order is fixed (`ForcesSortOrder`).
  - **Classification choices, all in the source's doc comment:**
    - Revenue is Income accounts with `IsSales`, so FX gains are left out. `AccountRead` gained
      `IsSales` for this.
    - Cost of sales is COGS less Purchase Returns.
    - Term assets are the Fixed Asset account plus every account a fixed-asset category names.
    - Credit sales are debits to AR in the period, and credit purchases credits to AP. Both
      include GST, so they are consistent with the balances they are divided into.
  - **Term assets to liabilities divides by total liabilities.** Xero divides by term
    liabilities, but no account can be marked long-term here, so every liability is current.
    An `IsCurrent`/term flag on `acc.Accounts` (L-ACC) would make both balance-sheet ratios
    exact. That needs its own card and an owner decision.
  - Default period: the twelve months to `to`, and `to` defaults to today.
    `ReportParameter.IsRequired` isn't enforced server-side, so the source defaults the dates
    instead of refusing.
  - **Translation checked, not behaviour.** The report ran through `ExecuteAsync` against an
    empty `acc` schema on PostgreSQL 16: paged with a count, grouped, filtered, with and without
    dates. One ledger pass is grouped by `OrgId` into a single row of conditional sums, then
    eight single-row projections are unioned. Every branch assigns every member, which EF needs
    to union them.
  - **Rounding differs at an exact midpoint.** Values round to 2 places. Postgres rounds half
    away from zero, and the in-memory tests use .NET's half-to-even. They agree everywhere except
    an exact `.xx5`, and none of the tests' figures lands on one.

### TK-85 · Weighted average recalculation in `CostingEngine.Worker`
- [x] completed (Claude Opus 5.5) — 2026-09-23 · tests written, not run
- **Lanes:** L-INV, L-ACC · **Depends on:** — · **Decision:** —
- **Touches:** `backend/worker/CostingEngine.Worker`, `backend/Api/Inventory/Inventory.Api/Services`,
  `inv` migrations (unit-cost precision), one internal read in `backend/Api/Accounting` for the lock date
- **The problem:** a weighted-average item's average is kept in arrival order by the request path,
  so a backdated receipt, or a sale entered before its stock arrived, leaves every later stock-out
  valued at the wrong average, with nothing to restate it. Assigned by the owner on 23 September
  2026; the specification is the owner's "Weighted Average Cost — Calculation Concept".
- **Sub-tasks:**
  - [x] Pure calculator: date order, in before out, entry order; negative-stock reordering; lock
        date; 12-dp average, 2-dp lines with the cumulative cent correction
  - [x] Worker recosts a weighted-average item once per batch, and requeues only the lines whose
        value changed for reposting
  - [x] Lock date from Accounting's period locks (the branch's strictest)
  - [x] Unit cost and average stored to 12 decimals
  - [x] Tests, including the owner's worked example and the negative-stock example
- **Done when:** the worked example recalculates to −20.57, −20.57, −10.99, −32.95 (total −85.08),
  and the negative-stock example values the out at 9.00.
- **Notes:**
  - Claimed as TK-01 (commit `edae6a6`); the number was reused minutes later by the rewrite in
    `58a48e5` for a different card, which other cards already cite. Renumbered here, since only
    this card may be edited. The feature commit `ebccbf7` still says TK-01 and means this card.
  - Tests written, not run: `backend/tests/Inventory.Api.Tests/WeightedAverageCalculatorTests.cs`
    (pure — both owner examples, lock date, ordering, rounding),
    `WeightedAverageRecostingTests.cs` (database — write-back, closed period, repost only on a
    changed value, a posting overtaken by a recalculation), and
    `The_branch_lock_is_the_strictest_whoever_asks` in `Accounting.Api.Tests/PeriodLockTests.cs`.
  - Weighted-average movements are now `Pending` until the worker settles them, so the ledger no
    longer posts the request path's provisional figure. `StockLedgerPoster` reads untracked and
    settles `Posted` only if the amount it posted is still the movement's value.
  - Lock date: the branch's **strictest** period lock (`GET internal/period-locks/branch`). If
    Accounting cannot be reached, weighted-average items are left unclaimed for the next tick
    rather than recalculated with no lock.
  - The worker does not overwrite `ItemStock.QuantityOnHand` — that is the guarded decrement's —
    it logs when the movements disagree with it.
  - A stock-in keeps its stored cost, per the specification, including a sales return; it is not
    re-read from the issue it returns. Say so if returns should come back at the original cost.
  - `CLAUDE.md`'s "Inventory & costing" section still gives only the arrival-order formula. It is
    `L-DOC`, so it is left for TK-75.

### TK-86 · Record the owner's answers of 24 September in `CLAUDE.md`
- [x] completed (Claude Opus 5.5) — 2026-09-24 · documentation only, no tests
- **Lanes:** L-DOC · **Depends on:** — · **Decision:** D-01 … D-20
- **Where:** `CLAUDE.md` sections "Undecided — ask, don't assume", "Printing", "Journal Entry is the only posting mechanism" (cites the trigger card) and "Roadmap".
- **Sub-tasks:**
  - [x] Move every answered item out of "Undecided" and state the decision where it belongs.
  - [x] Printing: PDFsharp replaces Syncfusion as the standard-document library (D-11).
  - [x] The trigger card is TK-07, not TK-10.
- **Done when:** "Undecided" holds only questions nobody has answered, and no line cites a decision the owner reversed.
- **Notes:**
  - "Undecided" now says nothing is open and summarises each answer with its card; the Printing, Rate sync, fixed-asset and PDF notes state the decisions; the operator-permission paragraph points at TK-13.

---

## 3. Decisions from the owner

These aren't tasks, and an agent never answers one itself. When a decision is made, record the
answer and the date here, then change the blocked cards to `- [ ] open`.

**Every decision here was answered by 24 September 2026.** A new question gets the next number (D-21).

| ID | Question | Blocks | Answer |
|---|---|---|---|
| D-01 | How does a platform operator's account get `platform.*`? | `apps/admin` sign-in | **A flag on the user** (owner, 2026-09-24): `mst.Users.IsPlatformOperator`, set only by bootstrap configuration or by another operator; a token carries `platform.*` when it is true. Never a role. TK-13 |
| D-02 | Who holds `CREATEDB` in production? Should the database be auto-created at startup, or provisioned by infra? | deployment, TK-46 | **Infrastructure creates the databases** (owner, 2026-09-24). The app runs without `CREATEDB` in production; auto-create stays for Development only. TK-27 |
| D-03 | RBI rate ingestion: scraping, a paid wrapper, or manual entry? | TK-26 | **Manual entry, plus a daily scrape of RBI's reference-rate page** (owner, 2026-09-24). Manual entry is TK-24's page; the scrape is TK-26 |
| D-04 | Optional phone fields: normalise to an empty string or to null? | — | **NULL** (owner, 2026-09-24). Trim input; a blank optional phone is stored as NULL everywhere. TK-21 |
| D-05 | Does `settings` split into a lib per sub-screen? | — | **Yes, split** (owner, 2026-09-24): one Nx lib per settings sub-screen. TK-28 |
| D-06 | CRM: is campaign and marketing automation in v1? | — | **Yes, in v1** (owner, 2026-09-24): campaigns and marketing automation. Not designed yet, so TK-38 writes the design first |
| D-07 | API client scopes: per module or per action? | — | **Per action** (owner, 2026-09-24): an API client is granted `{module}.{action}` permissions through its role, like a user. TK-29 |
| D-08 | Fixed assets: book **and** tax depreciation? *The code already has both schedule types, and straight-line and WDV methods. Confirm or change.* | TK-11 (confirms) | **Keep Books + Tax** (owner, 2026-09-24), with straight-line and WDV, as built |
| D-09 | Fixed assets: do acquisition and disposal get their own transaction codes, or ride `BIL` and `JRN`? | TK-12 | **Ride existing codes** (owner, 2026-09-24): acquisition under `BIL` or `OPB`, disposal under `INV` or `JRN`; only depreciation has its own (`DEP`) |
| D-10 | Does a branch declare its trade (Pharma, Jewellery or General)? | — | **Yes, a branch declares its trade** (owner, 2026-09-24). `Organization.Vertical` (General, Pharma, Jewellery) already exists and reaches seeding; menus and seeds narrow to it in TK-30 |
| D-11 | PDF library: *PDFsharp 6.1.1 is already in use for invoice PDFs.* Confirm it, or go to Syncfusion. | TK-22 | **PDFsharp** (owner, 2026-09-24). Syncfusion is dropped; archive copies become PDF/A. TK-22 |
| D-12 | Pricing per app: per user, per branch, or per employee? | TK-45 | **Per user with a branch cap** for RetailErp and School (today's `MaxUsers` and `MaxOrganizations`); **per active employee per month** for HRMS and Payroll (owner, 2026-09-24) |
| D-13 | Has anything been deployed with real data? Dropping `con.PrintTemplates` loses templates unless they're migrated. | TK-81 | **Nothing is deployed** (owner, 2026-09-24). TK-81 may drop `con.PrintTemplates` in the same change that copies its rows to `prt`. |
| D-14 | Subscribe to IBJA's paid metals API? | TK-25 | **Both** (owner, 2026-09-24): manual entry now (TK-24's page) **and** IBJA's paid API (TK-25). The owner supplies the IBJA credentials, stored through `ISecretStore` |
| D-15 | What is the *Business Performance* report? | TK-84 | **Xero-style KPI ratios over a period** (owner, 2026-09-24): gross profit margin, net profit margin, return on investment, average days customers take to pay, average days to pay suppliers, current assets to current liabilities, term assets to liabilities, and total cash balance. |
| D-16 | What should the client portal do next? | TK-32 | **All of these** (owner, 2026-09-24): overall outstanding and overall trade value (sales to date) on the dashboard; view and download invoices; pay online; accept or reject quotes; raise and follow support tickets. TK-32 |
| D-17 | Go-ahead, and the order, for each Phase 3 design | TK-34, TK-35, TK-33, TK-36, TK-31, TK-37 | **Go-ahead for all six** (owner, 2026-09-24): e-invoicing and e-way bill, workflow approvals, budgeting, project accounting, custom fields and reports, and the compliance bundle. Order: as listed in section E |
| D-18 | What is Customer stage C4? The proposal is per-branch SLA hours per priority, replacing the hard-coded ones in `TicketsController.cs:101`. | TK-18 | **A per-branch SLA table** (owner, 2026-09-24): `cus.SlaPolicies`, seeded with Urgent 2 h, High 8 h, Medium 2 days, Low 7 days, editable per branch. TK-18 |
| D-19 | Capitalising a fixed asset: does it reclassify the bill's shared Fixed Asset account to the category's account? Does a migrated asset debit against Opening Balance Equity? | TK-12 | **Reclassify to the category** (owner, 2026-09-24): capitalising posts Dr the category's Fixed Asset account / Cr the shared Fixed Asset account the bill used; a migrated asset (no bill) debits the category account against Opening Balance Equity. TK-12 |
| D-20 | Disposing of a fixed asset: which account receives the proceeds? The proposal is a bank account chosen on the disposal. | TK-12 | **Both ways** (owner, 2026-09-24): the disposal either names the bank or cash account the proceeds landed in, or is raised as a sales invoice to the buyer (Dr the buyer's receivable); either way the accumulated depreciation is written back, the asset removed at cost and the gain or loss booked. TK-12 |

---

## 4. Retired

**Renumbered on 24 September 2026.** Card numbers before that date (in commit messages and
older notes) map to the current ones like this. Old TK-16, TK-18 and TK-22 were retired before the
renumber and have no new number.

<details><summary>Old number → new number</summary>

| Old | New |
|---|---|
| TK-01 | TK-70 |
| TK-02 | TK-71 |
| TK-03 | TK-72 |
| TK-04 | TK-73 |
| TK-05 | TK-74 |
| TK-06 | TK-02 |
| TK-07 | TK-03 |
| TK-08 | TK-04 |
| TK-09 | TK-08 |
| TK-10 | TK-09 |
| TK-11 | TK-75 |
| TK-12 | TK-76 |
| TK-13 | TK-77 |
| TK-14 | TK-78 |
| TK-15 | TK-14 |
| TK-17 | TK-15 |
| TK-19 | TK-18 |
| TK-20 | TK-19 |
| TK-21 | TK-79 |
| TK-23 | TK-80 |
| TK-24 | TK-81 |
| TK-25 | TK-82 |
| TK-26 | TK-22 |
| TK-27 | TK-23 |
| TK-28 | TK-25 |
| TK-29 | TK-26 |
| TK-30 | TK-11 |
| TK-31 | TK-83 |
| TK-32 | TK-84 |
| TK-33 | TK-39 |
| TK-34 | TK-40 |
| TK-35 | TK-41 |
| TK-36 | TK-42 |
| TK-37 | TK-43 |
| TK-38 | TK-44 |
| TK-39 | TK-45 |
| TK-40 | TK-46 |
| TK-41 | TK-47 |
| TK-42 | TK-48 |
| TK-43 | TK-49 |
| TK-44 | TK-50 |
| TK-45 | TK-51 |
| TK-46 | TK-52 |
| TK-47 | TK-53 |
| TK-48 | TK-54 |
| TK-49 | TK-55 |
| TK-50 | TK-56 |
| TK-51 | TK-57 |
| TK-52 | TK-58 |
| TK-53 | TK-59 |
| TK-54 | TK-60 |
| TK-55 | TK-61 |
| TK-56 | TK-62 |
| TK-57 | TK-63 |
| TK-58 | TK-64 |
| TK-59 | TK-65 |
| TK-60 | TK-66 |
| TK-61 | TK-67 |
| TK-62 | TK-68 |
| TK-63 | TK-69 |
| TK-64 | TK-32 |
| TK-65 | TK-34 |
| TK-66 | TK-35 |
| TK-67 | TK-33 |
| TK-68 | TK-36 |
| TK-69 | TK-31 |
| TK-70 | TK-37 |
| TK-71 | TK-01 |
| TK-72 | TK-20 |
| TK-73 | TK-24 |
| TK-74 | TK-12 |
| TK-75 | TK-05 |
| TK-76 | TK-16 |
| TK-77 | TK-85 |
| TK-78 | TK-10 |
| TK-79 | TK-06 |
| TK-80 | TK-07 |
| TK-81 | TK-27 |
| TK-82 | TK-13 |
| TK-83 | TK-21 |
| TK-84 | TK-28 |
| TK-85 | TK-38 |
| TK-86 | TK-29 |
| TK-87 | TK-30 |
| TK-88 | TK-86 |
| TK-89 | TK-17 |

</details>

- ~~Seed the Sales and Purchase numbering series~~. Both services have
  `SeedData/NumberingSeriesSeed.cs`. Purchase's is never called, which TK-01 fixes.
- ~~Wire `apps/desktop` into the Nx workspace~~. `apps/desktop/project.json` already has real
  targets. The cart is TK-79.
- ~~old TK-16 · Contact lookup endpoint~~. `GET /api/contacts?search=&role=` already exists
  (`ContactsController.cs:34`). Found 23 September 2026.
- ~~old TK-18 · Picker on the purchase forms~~. All four purchase forms already use
  `bb-lookup-dialog` through `PurchaseLookupService`. Found 23 September 2026.
- ~~old TK-22 · Printing: how an internal call carries its branch~~. Decided in `docs/Modules.md`
  "Printing": callers push the payload under the user's own token. D-13 now asks the question that
  actually blocks the cutover.
- The first version of this file numbered tasks by section, 1.1 to 4.7. Those numbers are gone.
  Use the `TK-nn` IDs.

---

## 5. Standard delivery sub-tasks (feature cards)

Copy these into any card that builds a feature, and tick them as you go:

- [ ] Entity in `{Module}.Entity/TableEntities`, inheriting `AuditableEntity`, with an
      `ErrorMessage` on every annotation. Enums in `Enums/`.
- [ ] `DbSet` and Fluent config. `CustomerId` + `OrgId` and the query filter; `xmin` concurrency.
- [ ] The migration, with an RLS `ENABLE` + `FORCE` + policy block in it (the TK-71 template).
      Run `dotnet ef migrations has-pending-model-changes`.
- [ ] Seed data that is idempotent when a branch is created.
- [ ] Service and controller. Writes happen inside the reliability filter's transaction
      (`BeginScopeAsync`, never `BeginTransactionAsync`). Errors go through `SqlErrorCatalog`. The
      controller carries a guard attribute. A request for another branch's data gets `Forbid()`.
- [ ] `-core` view-model and `-ui` page: standalone components, `inject()`, signals, working at 360px.
- [ ] Write the tests: behaviour, the **Done when** line, and the `RlsAudit` and `EndpointGuardAudit`
      tests for the new schema and controllers. An AI writes them and does not run them (section 0.5).
- [ ] Update the docs page under `frontend/apps/docs/content/` and `docs.manifest.ts`, and add a
      `release-notes.md` bullet under **Unreleased**, all in the same commit.
- [ ] `dotnet build` in `backend/`, and `npm run lint`, the typecheck and the builds in `frontend/`, are
      all clean. Running the tests (`dotnet test`, `npm run test`) is the owner's step (section 0.5).
