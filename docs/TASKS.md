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
4. **Open the card's GitHub issue** (section 0.6) if it has none, and add its `- **Issue:**` line
   to the card under the status line.
5. Commit only `docs/TASKS.md` with the message `claim TK-nn (AI name)` and `Refs #N` in the body,
   then `git push origin main`.
6. **If the push is rejected**, run `git rebase --abort` if a rebase is in progress, then
   `git reset --hard origin/main`. This throws away only your one-line claim commit. Re-read the
   queue and start again from step 1. The issue you opened stays; reuse it if you take the card
   later, or leave it for whoever does. Never force-push a claim.
7. Start work only after the claim is on `origin/main`.

**While working**
- Write only to paths in your card's lanes. Shared files (section 1.1) follow their own rules.
- Commit small, and run `git pull --rebase origin main` before every push.
- In `docs/TASKS.md`, edit only your own card. In `docs/Modules.md`, you may tick your own stage's
  box without holding `L-DOC`.
- On a shared machine, give your test databases their own names (e.g. `SALES_TEST_DB=sales_test_agent2`).
  Two suites dropping the same database will break each other's results.

**Releasing a card**
- **Done**: tick it in the same commit as the last piece of work, with `Closes #N` in that commit's
  body, then push. An AI adds `· tests written, not run` to the status line (section 0.5).
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

### 0.6 GitHub issues: one per card, linked from every commit

**Every card has a GitHub issue, and every commit for a card names it.** The issue is where the
owner follows a card from GitHub, and the commits linked on it are the card's whole history.
(Rule set 25 September 2026 by the repository owner; TK-60 to TK-71 were linked retrospectively
as #2 to #13.)

- **One issue per card**, in `jothi-prabaharan/Bill-Book`, titled exactly as the card's heading
  without the `###`: `TK-nn · Title`. Search for one first; never open a second.
- **When:** at the claim (section 0.2, step 4). A card worked before this rule gets its issue the
  next time anyone touches it.
- **Body:** the card's link (`docs/TASKS.md` › TK-nn), its dependencies by issue number where they
  have one, its sub-tasks as a checklist, and its *Done when*. No credentials, hosts or customer
  data. End with the Claude Code attribution footer when an AI writes it.
- **The card names it**: `- **Issue:** [#N](https://github.com/jothi-prabaharan/Bill-Book/issues/N)`,
  directly under the status line.
- **Every commit for the card references it** in the message body: `Refs #N` on the claim and on
  each piece of work, `Closes #N` on the commit that marks the card completed. GitHub lists each
  referencing commit on the issue, so the mapping needs no upkeep.
- **Close it as completed** when the card is done. `Closes #N` in a commit on `main` does that;
  if it didn't (a merge, a typo), close it by hand. A card handed back open keeps its issue open,
  with the `Handover:` line copied into a comment.
- **Opening an issue for a card that is already completed** — the retrospective case, done for
  TK-01 to TK-80 on 25 September — never closes on its own. A `create` call's `state` argument is
  ignored by GitHub: every new issue opens regardless of what's passed. Close it with a separate
  `update` call (`state: closed`, `state_reason: completed`) right after creating it, in the same
  batch of work. Check the issue actually reads closed before moving on — 64 were opened already
  completed on 25 September and stayed open until this was caught and every one closed by hand.
- **A commit already pushed without the reference is never rewritten** to add one — `main` is
  never force-pushed. Comment on the issue with the missing commit id instead.
- A decision (section 3) doesn't get an issue; the card it blocks carries it.

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
- **Issue:** [#14](https://github.com/jothi-prabaharan/Bill-Book/issues/14)
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
- **Issue:** [#15](https://github.com/jothi-prabaharan/Bill-Book/issues/15)
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
- **Issue:** [#16](https://github.com/jothi-prabaharan/Bill-Book/issues/16)
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
- **Issue:** [#17](https://github.com/jothi-prabaharan/Bill-Book/issues/17)
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
- **Issue:** [#18](https://github.com/jothi-prabaharan/Bill-Book/issues/18)
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
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#19](https://github.com/jothi-prabaharan/Bill-Book/issues/19)
- **Lanes:** L-ACC, L-KERNEL, L-CON, L-INV · **Depends on:** — · **Decision:** —
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
  - [x] Confirm by reading `HttpTaxRateProvider` and its Sales/Purchase callers.
  - [x] Carry `customerId` and `orgId` on the rates request (query or header) and set the tenant
        the way `InternalLedgerController` does. Touching the kernel client needs `L-KERNEL`.
  - [x] Delete `InternalBankAccountsController` if it has no caller, or give it the same treatment.
  - [x] Test: rates for a seeded branch come back non-empty through the controller.
- **Done when:** a Sales invoice resolves its GST rates from Accounting for its own branch.
- **Notes:** found while doing TK-71.
  - From TK-72: the same gap in Master. `InternalContactNamesController` (`internal/contacts/names`)
    reads `con.Contacts` with no tenant set, and `HttpContactNameLookup` in
    `Shared.Kernel/Documents/INameLookup.cs` sends only the internal key. So every document list's
    contact names come back empty. So do the item names: `InternalItemNamesController` in Inventory
    sets no tenant either (confirmed in TK-74). The fix is the same one: carry the org and set the tenant. That touches `L-CON` / `L-INV`
    and `L-KERNEL`.
  - Done (Claude Opus 5.5, 2026-09-24):
    - Confirmed: `HttpTaxRateProvider`, `HttpContactNameLookup` and `HttpItemNameLookup` are
      registered in Sales and Purchase with `InternalKeyHandler` only, so no token went with them.
    - `internal/tax/rates` now takes `customerId` and `orgId` in the query and sets the tenant the
      way `InternalPeriodLocksController` does; `HttpTaxRateProvider.RatesRoute` builds the URL.
    - The names gap from TK-72/TK-74 is fixed here too, since it was the same bug. `NameLookupRequest`
      carries `CustomerId`/`OrgId`, `HttpNameLookup` fills them, and both `internal/contacts/names`
      and `internal/items/names` set the tenant through the new
      `Shared.Kernel.Tenancy.InternalTenant.Apply`. It takes the branch from the body, or from a
      forwarded token when the body names none (`Customer.Api`'s `ContactsClient` does this). A body
      that disagrees with the token gets a 403. All three controllers now resolve their DbContext
      *after* setting the tenant, because the context binds its connection when it is built.
    - `InternalBankAccountsController` had no caller: `BankService` provisions in-process through
      `BankLedgerService`. Deleted, along with `UpdateBankAccountLedgerRequest`.
    - The three fixtures (`Accounting`, `Inventory`, `Master` contacts) gained a
      `CreateContext(TenantContext)` overload, for tests where the controller sets the tenant.
    - **Tests written:** `backend/tests/Accounting.Api.Tests/InternalTaxControllerTests.cs`,
      `backend/tests/Master.Api.Tests/InternalContactNamesControllerTests.cs`,
      `backend/tests/Inventory.Api.Tests/InternalItemNamesControllerTests.cs`,
      `backend/tests/Shared.Kernel.Tests/InternalTenantTests.cs`.

### TK-07 · Restore the ledger's deferred balance and allocation triggers
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#20](https://github.com/jothi-prabaharan/Bill-Book/issues/20)
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
  - [x] Recover every `CREATE FUNCTION` / `CREATE CONSTRAINT TRIGGER` block from the pre-squash chain.
  - [x] Check each against today's columns: the schema changed since, so do not paste blindly.
  - [x] Add them in a new `acc` migration, with a matching `Down()`.
  - [x] Test: an unbalanced posted journal is refused at commit; a draft is not.
  - [ ] Owner: run `Accounting.Api.Tests` from a dropped `ACCOUNTING_TEST_DB`.
- **Done when:** a posted, unbalanced journal cannot be committed, and a draft can.
- **Notes:** found while doing TK-71.
  - From TK-77 (2026-09-24): **this number is also held by "Sale challans post to Goods Delivered
    Not Invoiced, once"**, which was added first (with TK-76, commit `cb7da6c`). One of the two
    needs the next unused number; `CLAUDE.md` cites this one.
  - Renumbered from TK-10 on 2026-09-24: two cards had taken that number, and the GDNI card came first. `CLAUDE.md` is updated to cite TK-07.
  - Done (Claude Opus 5.5, 2026-09-24):
    - The seven triggers and their functions were all in one pre-squash block
      (`2c5ed6f^:…/20260902151402_InitialAccountingSchema.cs`); no later pre-squash migration
      touched them. Every column they read still exists under the same name, and `Status` is still
      stored as the enum's name (`'Draft'`).
    - `Accounting.Repository/Migrations/Tenant/20260924063953_RestoreLedgerTriggers.cs` restores them:
      `trg_ledger_balanced` (JournalLedger, per branch), `trg_journal_balanced` (JournalDetails) and
      `trg_journal_balanced_on_post` (Journals), plus the allocated/allocated-on-post pair on
      SpendMoney and ReceiveMoney. All are `DEFERRABLE INITIALLY DEFERRED`. `Down()` drops them all.
      The snapshot didn't change.
    - **Two changes from the old text**, both in the ledger trigger:
      - **Deletes were never checked.** A DELETE read `NEW."OrgId"`, which is NULL on a delete, so
        the sum ran over no rows and passed. It reads `OLD` now.
      - **The branch-wide sum ran once per changed row, so large postings were quadratic.** An
        opening balance of thousands of lines summed the whole branch ledger thousands of times at
        commit. Every deferred event fires against the same final state, so a transaction-local
        marker (`acc.ledger_checked` = txid + org) now lets it run once per branch per transaction.
        `SET CONSTRAINTS … IMMEDIATE` would defeat that; nothing issues it.
    - Voiding a money document leaves its lines and its amount in place, so the allocation trigger
      still agrees with a voided document.
    - `LedgerPostingService` already refuses an unbalanced request. So the branch trigger only fires
      on a real bug, such as a partial replace that leaves another request's legs behind.
    - `MoneyDocumentSchemaTests` already asserted the allocation triggers (e.g.
      `An_under_allocated_payment_cannot_be_posted`). Those tests have failed on any database built
      since the squash, and should pass now. `ReconciliationMatchingTests` inserts debit-only
      ledger rows, but with the base amounts left at 0, so the trigger (which sums base) passes.
    - Applied the scripted chain to a scratch database: all seven triggers are present, deferrable
      and initially deferred. `has-pending-model-changes` is clean, and the solution builds with
      0 warnings.
    - `CLAUDE.md` still says no migration creates a trigger. That is L-DOC; TK-08 holds it next.
    - **Test written:** `backend/tests/Accounting.Api.Tests/LedgerTriggerTests.cs`. It covers a
      posted unbalanced journal refused at commit, a draft that may be unbalanced, posting an
      unbalanced draft refused, editing a posted line refused, unbalanced ledger rows refused,
      deleting one leg refused, two postings in one transaction checked together, and the catalogue.

### TK-08 · Review of the RLS work
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#21](https://github.com/jothi-prabaharan/Bill-Book/issues/21)
- **Lanes:** L-DOC, L-KERNEL, L-ACC, L-RPT · **Depends on:** TK-71, TK-72, TK-73, TK-74, TK-02, TK-03, TK-04, TK-05, TK-06 · **Decision:** —
- **State:** two facts decide whether RLS protects anything at all:
  - **A superuser, or any role with `BYPASSRLS`, ignores RLS even when FORCE is set.** The
    development connection strings use `postgres`, a superuser, so the policies have never been
    exercised in development.
  - The interceptor sets the tenant at **session level**, and `CLAUDE.md` says it must be
    transaction-local. The code overwrites both values each time a connection opens, which
    mitigates this; the document and the code still disagree.
- **Sub-tasks:**
  - [x] Read each migration from TK-72, TK-73, TK-74, TK-02, TK-03, TK-04 and TK-05 against TK-71's template.
  - [x] Check that the deployed application connects as a role that is neither a superuser nor
        `BYPASSRLS` (`deploy/azure`, and each service's connection string).
  - [x] Test: one test that connects as a non-superuser role (created in the fixture) and reads
        another branch's rows. It must get zero.
  - [x] Reconcile the session-level vs transaction-local statement. Either change the interceptor
        or rewrite the rule in `CLAUDE.md`, and write the reason down.
  - [x] Rewrite the FORCE bullet in `CLAUDE.md`'s standing caveats to say what is true now.
  - [ ] Owner: drop all seven test databases and run the whole backend suite, expecting 0 RLS
        failures. Then drop one policy by hand and watch it go red.
- **Done when:** a non-superuser connection can't read another branch's rows in any tenant schema.
- **Notes:**
  - **Decided by the owner (2026-09-24): transaction-local.** Change `RlsConnectionInterceptor` to `set_config(…, true)` inside each transaction (the reliability filter's scope), and keep `CLAUDE.md`'s rule as written. Reads outside an explicit transaction need one opened for them, or the setting won't hold — check every read path.
  - Dependency on TK-06 added 2026-09-24: internal endpoints that set no tenant break under RLS; the review needs them fixed.
  - Done (Claude Opus 5.5, 2026-09-24):
    - **Migrations reviewed.** `acc`, `con`, `cus`, `inv`, `pur`, `sal`, `rpt` and `prt` all have
      TK-71's NULLIF expression, ENABLE and FORCE, and `FOR ALL` with no `WITH CHECK`. The only
      `tenant.orgid` or bare-cast policy text left is in `Down()` methods. `con.ApiClients` has its
      documented customer-level variant (`OR org IS NULL`).
    - **Transaction-local, as decided.** `RlsConnectionInterceptor` is now a `DbCommandInterceptor`.
      It prefixes every command with `SET LOCAL app.current_customer_id = '…'; SET LOCAL
      app.current_org_id = '…';`, reading the tenant as it is when the command runs. Npgsql sends
      the prefix and the command as one batch, and Postgres runs a batch as one transaction: the
      explicit one if there is one, otherwise an implicit one. So every read path is covered
      without opening transactions for it: GETs, workers, seeders, the error log. That answers
      "check every read path" structurally, not route by route. It sends nothing when there is no
      tenant (null or `Guid.Empty`), so startup and migrations are untouched. I checked it on a
      scratch database before relying on it:
      - the setting holds for a command with no transaction, and is gone from the next command on
        the same connection;
      - parameterised LINQ, a `SaveChanges` batch and `ExecuteUpdate` row counts are unchanged;
      - a tenant change in the middle of a transaction applies to the next command.
      The Tenancy rule in `CLAUDE.md` stands as written; one clause says how it's done.
    - **What the review found: internal writes would have been refused under RLS.**
      `TransactionFilter` resolves every `IUnitOfWork` (and so the DbContext) and opens the
      transaction *before* an internal route copies the branch out of its request. The old
      interceptor set the tenant at connection open, which was `''` at that point. So every
      internal POST (ledger postings, stock issues, allocations, seeding) would have failed the
      policy's write check on any non-superuser connection. The per-command prefix fixes it,
      because the tenant is read when the command runs.
    - **Not fixed, and needs its own card: the shard follows the same ordering.** The DbContext
      takes its connection string from `ITenantDatabaseResolver` when it is built. That happens in
      the filter, before the internal route sets `CustomerId`, so it gets the default shard
      (`IN000001`). This is harmless with one shard, but wrong once a second one exists. The fix
      is to set the tenant from the request before `TransactionFilter` runs, e.g. an ordered
      action filter reading `customerId`/`orgId` from the bound arguments (see TK-06's
      `InternalTenant.Apply`).
    - **Also found and fixed: every allocation failed through the host.** `AllocationService`
      asks `BeginScopeAsync` for Serializable inside the filter's Read Committed transaction, so
      `GuardIsolation` threw. Both `AllocationsController.Allocate` and
      `InternalAllocationsController.Allocate` now carry `[Transactional(IsolationLevel.Serializable)]`.
      The tests call controllers directly with no filter, which is why this went unseen.
      Release note added.
    - **Deployment.** The apps connect as the Flexible Server admin login
      (`deploy/azure/main.bicep`, `connectionBase`). That login is neither a superuser nor
      `BYPASSRLS` by default, so the FORCEd policies bind. But it owns the tables and has
      `CREATEDB`/`CREATEROLE`. **Recommendation for the owner:** a dedicated login with DML rights
      only, used by the services, with the admin kept for migrations. That is a deployment decision
      and wants a D-number, so I didn't change it.
    - `CLAUDE.md`: the FORCE caveat is rewritten to say what is true now. The ledger-trigger line
      reflects TK-07, the Purchase-seeding bullet is struck (TK-01), and the Tenancy rule names the
      mechanism.
    - **Tests written:** `backend/tests/Accounting.Api.Tests/TenantInterceptorTests.cs` (six tests,
      role `acc_tenant_probe`). Through the real interceptor with no explicit transaction: own
      branch visible; another branch zero even past the query filter; no tenant zero; a tenant set
      after `BEGIN` applies; nothing survives on a pooled connection; a write into another branch
      refused. Also `backend/tests/Shared.Kernel.Tests/RlsConnectionInterceptorTests.cs` (the
      prefix text) and `backend/tests/Accounting.Api.Tests/AllocationIsolationTests.cs`. The
      per-schema `*RowLevelSecurityTests` already cover "a non-superuser reads another branch's
      rows and gets zero" in all eight schemas.

### TK-09 · `ReportLayerCertificationTests`: likely already fixed
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#22](https://github.com/jothi-prabaharan/Bill-Book/issues/22)
- **Lanes:** L-RPT · **Depends on:** — · **Decision:** —
- **Where:**
  - `backend/tests/Reporting.Api.Tests/ReportLayerCertificationTests.cs:97`, which expects 48.
  - `backend/tests/Reporting.Api.Tests/ReportSourceTests.cs:78`: the `Sources` list, with 48 entries.
  - `backend/Api/Reporting/Reporting.Api/Program.cs`: 48 `AddScoped<IReportSource, …>` lines.
  - `backend/Api/Reporting/Reporting.Repository/SeedData/ReportCatalogSeeder.cs`
- **State:** commit `3dad51f` (19 September, "complete all 48 unblocked reports") came after the
  18 September count of 4 failures. A static count now agrees across three of the four layers.
- **Sub-tasks:**
  - [x] Check the fourth layer: every `ReportKey` in `ReportCatalogSeeder.Catalog` has a source,
        and every source has a catalog entry with matching column keys.
  - [x] If they all agree, hand the card to the owner to run.
  - [x] ~~Otherwise, fix whichever side is stale.~~ Nothing was stale. Don't just change the expected number.
  - [ ] Owner: run `Reporting.Api.Tests` from a dropped `REPORTING_TEST_DB`.
- **Done when:** `ReportLayerCertificationTests` has 0 failures.
- **Notes:**
  - TK-83 moved the expected count at `ReportLayerCertificationTests.cs:97` from 48 to 52, and added four sources to the `Sources` list, `Program.cs` and the seeder. There are 52 of each now.
  - Done (Claude Opus 5.5, 2026-09-24):
    - **All four layers agree at 53**, one more than the note above says; a source was added
      after TK-83. `ReportLayerCertificationTests.cs` already expects 53. I checked with a
      throwaway program over the built assemblies, using the same reflection and the same
      `Program.cs` regex as the tests, not a test run:
      - 53 concrete `IReportSource` types;
      - 53 `AddScoped<IReportSource, …>` lines, each naming a type that exists;
      - 53 entries in `ReportSourceTests.Sources`;
      - 53 keys in `ReportCatalogSeeder.SeededColumnKeys`.
      No report is missing a layer, no key is duplicated, and no source has an empty column list.
    - **Column keys match on every report**: each source's `Columns` keys equal its catalog entry's
      keys, in both directions.
    - The `ReportingDbContextModelSnapshot` drift TK-04 flagged was fixed in TK-04's migration, and
      `has-pending-model-changes` is clean for `ReportingDbContext`.
    - No code or test change was needed. The owner's run of `Reporting.Api.Tests` from a dropped
      `REPORTING_TEST_DB` is what closes this card.

### B · Money posted right: cost of sales and fixed assets

Postings that are wrong today or post nothing. TK-10 comes before POS (TK-39), which reuses the invoice's posting.

### TK-10 · A direct sale's cost of goods posts once (GDNI moved to TK-90)
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#23](https://github.com/jothi-prabaharan/Bill-Book/issues/23)
- **Lanes:** L-ACC, L-SAL · **Depends on:** TK-76 · **Decision:** —
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
  - [ ] ~~Seed `Goods Delivered Not Invoiced`~~ → TK-90 (Asset, off the manual-journal picker like GRNI), with a
        `SystemAccount` value, and backfill existing branches through the seeder's idempotent path.
  - [x] Decide who posts cost of sale — the worker (at recosted value) or the document. `docs/Modules.md`
        §7 says Inventory, asynchronously. Write the answer under Notes before changing either side.
  - [ ] ~~If the worker: map an `Issue`~~ → TK-90 sourced from a `Sale` challan to Dr GDNI / Cr Inventory, and
        post nothing for job work, approval, branch transfer or sample. The worker can't see
        `ChallanType`; carry it on the movement (for example, a distinct `SourceType` or a flag on
        `IssueStockRequest`) rather than reading `sal`.
  - [ ] ~~The invoice against a challan then clears GDNI~~ → TK-90: Dr COGS / Cr GDNI at the challan movements'
        settled cost, never the provisional one.
  - [x] Remove whichever of the invoice's own COGS legs and the worker's is the duplicate.
  - [ ] ~~Test: challan then invoice leaves GDNI~~ → TK-90 at zero, Inventory credited once, COGS debited once.
  - [ ] ~~Test: a job-work challan posts nothing.~~ → TK-90
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
  - **Rescoped by the owner (2026-09-24): the duplicate fix only.** Asked how an invoice should
    clear GDNI, the owner chose to fix the double posting now and move the challan/GDNI switch to a
    follow-up card (TK-90, blocked on D-21). GDNI stays unseeded, and an invoice naming a challan
    is refused as before whenever its cost is non-zero. The **Done when** above belongs to TK-90.
    This card is done when a direct sale's cost of goods is in the ledger once, at the settled figure.
  - Done (Claude Opus 5.5, 2026-09-24):
    - **The duplicate.** A direct invoice posted one Dr COGS / Cr Inventory pair at line 0 (the Cr
      on the CONTROL leg type). The costing worker posted a pair per line on
      `(INV, invoiceId, lineId, COGS)`. Those keys differ, so both stood.
    - **Now the invoice posts its cost per line, provisionally, on the worker's key.** The key is
      the document, `TransactionDetailId` = the invoice line (the movement's `SourceLineId`), and
      leg type 4. The legs are Dr COGS / Cr Inventory with the item's sub-accounts and ledger
      source 1, exactly the rows the worker writes, at the issue's `LineValue`. The worker's
      settled posting replaces them (the owner's "replace the provisional rows"). The worker is
      unchanged.
    - **Order-independent.** `PostLedgerRequest.ProvisionalLedgerTypeIds` (Accounting and Sales)
      names leg types written only where their (line, type) key is empty. So if the worker settles
      the movement before the invoice's own posting lands, the provisional legs are dropped instead
      of overwriting the settled cost. Each provisional (line, type) group must balance on its own,
      or the posting is refused as Unbalanced. The check runs inside the posting's transaction; a
      worker committing in the milliseconds between that read and the insert could still double a
      line, and closing that fully would need a lock.
    - Unchanged: an invoice naming a challan still posts its aggregate Dr COGS / Cr GDNI at line 0
      (TK-90); the credit note already left cost to the worker (TK-77); POS invoices take the same
      path as direct ones.
    - **Found, not fixed:**
      - **Void leaves stock issued with no Inventory credit.** A void withdraws every COGS row on the
        invoice, settled ones included, but the goods stay issued. That is delivered-not-invoiced,
        which TK-90's GDNI is for.
      - **The worker can post after a void.** If the movement is still pending when the invoice is
        voided, the worker writes COGS onto the voided invoice.
      - **Ledger source.** Sales files the invoice's other legs under ledger source 3 (Invoice
        payment), where 1 (Document posting) is what the worker uses. The new legs use 1.
    - **Tests written:** `backend/tests/Accounting.Api.Tests/LedgerPostingServiceTests.cs` (five
      provisional-leg tests: written when empty; replaced by the settled cost; never overwrites a
      settled cost; only settled lines skipped; an unbalanced provisional line refused) and
      `backend/tests/Sales.Api.Tests/InvoicePostingTests.cs`
      (`A_direct_invoice_posts_its_cost_per_line_provisionally_on_the_workers_key`).

### TK-90 · Sale challans post to Goods Delivered Not Invoiced, and the invoice clears it
- [x] completed (Claude Opus 5.5) — 2026-09-26 · tests written, not run
- **Issue:** [#82](https://github.com/jothi-prabaharan/Bill-Book/issues/82)
- **Lanes:** L-ACC, L-INV, L-SAL · **Depends on:** TK-10 · **Decision:** D-21
- **Where:** as TK-10's Where. `ChartOfAccountsSeed.cs` (GDNI missing), `StockLedgerMapping.cs:86-87`,
  `InvoiceService.cs` (the challan branch and the aggregate Dr COGS / Cr GDNI at line 0),
  `DeliveryChallanService` (posts nothing today), `IssueQuantities` in `InvoiceService.cs` (an
  order-billed invoice bills delivered goods without naming the challan).
- **State (2026-09-24):** after TK-10, a direct sale posts its cost once. A sale challan's cost
  still reaches the ledger through the worker as Dr COGS / Cr Inventory **at dispatch**. An invoice
  naming a challan posts Dr COGS / Cr GDNI, which is refused while GDNI is unseeded. An invoice
  billing delivered goods through an order posts no cost for them, which is consistent only
  because the challan already booked COGS at dispatch.
- **What the owner has decided:** GDNI is seeded, and each document posts a provisional cost the
  worker replaces with the settled one (TK-10's `ProvisionalLedgerTypeIds`). A sale challan's
  provisional entry is Dr GDNI / Cr Inventory. Job-work, approval, transfer and sample challans post
  nothing: issue them with a flag that creates their movements `LedgerStatus.NotApplicable`, and
  check that recosting never requeues a `NotApplicable` movement.
- **What D-21 decided (owner, 2026-09-25): (b), cost at invoice time.** The invoice clears GDNI at
  whatever cost Inventory holds when it posts — only the `sal` table is touched. No
  `sal.InvoiceChallanAllocations` or `inv.StockMovementBillings` link tables; a void reverses the
  invoice's own GDNI-clearing entry rather than walking back to specific challan lines, and a later
  WAC restatement of the challan's dispatch cost can leave a small residual balance in GDNI rather
  than being chased down and zeroed.
- **Sub-tasks:**
  - [x] Seed GDNI (Asset, off the manual-journal picker like GRNI) with a `SystemAccount` value;
        backfill existing branches through the seeder's idempotent path; take it off
        `SalesAccountNameTests`' allow-list.
  - [x] Challan post: provisional Dr GDNI / Cr Inventory per line on `(DLC, challanId, lineId, 4)`
        for `ChallanType.Sale`; the worker maps a `DLC`-sourced `Issue` to Dr GDNI / Cr Inventory.
  - [x] Invoice: Dr COGS / Cr GDNI per line, for challan-named and order-billed delivered goods, at
        Inventory's current cost when the invoice posts — no allocation record naming which challan
        lines were cleared.
  - [x] Test: challan then invoice leaves GDNI at zero (absent a later restatement), Inventory
        credited once, COGS debited once.
  - [x] Test: an order-billed invoice of delivered goods clears GDNI, and its void restores it.
  - [x] Test: a job-work challan posts nothing.
  - [x] Test: a challan cost restatement after its invoice has posted leaves a residual GDNI balance
        rather than erroring — document this as the accepted cost of (b) rather than a bug.
- **Done when:** a sale challan and the invoice raised from it leave Inventory reduced once, GDNI
  at zero, and one cost-of-sales debit.
- **Notes:** split from TK-10 by the owner's choice of 2026-09-24.
- **As built (2026-09-26):**
  - **Accounting:** `SystemAccount.GoodsDeliveredNotInvoiced` (25) is seeded as 1250, an Asset kept off the journal picker. An existing branch gets it from the idempotent seeder (the retry in `apps/admin`).
  - **Master:** ledger type **7 `GDNI`** was added (migration `GdniLedgerType`). The invoice's clearing legs cannot use type 4 (COGS). That type is the key the costing worker replaces per line, so on an invoice line that both clears delivered goods and issues more, the worker's settlement would erase the clearing. Type 3 (CONTROL) was also ruled out, because allocation and aging read it per contact.
  - **Inventory:**
    - `StockMovement.LedgerExempt` (migration `StockMovementLedgerExempt`) marks a movement that posts nothing. It is created `NotApplicable`, `StockLedgerMapping` returns no posting for it, and neither recosting path (`CostingService` requeue, `WeightedAverageRecosting`) puts it back in the ledger queue.
    - A flag was needed because the poster already uses `NotApplicable` for a zero-cost movement, and that movement must still be requeued once recosting gives it a value.
    - A `DLC`-sourced `Issue` maps to Dr GDNI / Cr Inventory, with no item sub-account on GDNI.
    - New endpoint `POST internal/stock/movement-costs` returns a movement's current `TotalCost`.
  - **Sales, challan:** a sale challan posts provisional Dr GDNI / Cr Inventory per line on `(DLC, challan, line, 4)` with `ProvisionalLedgerTypeIds = [4]`. If the ledger refuses, the result is `DeliveryChallanOutcome.PostingRefused` (409) and the challan stays a draft. Every other challan type is issued with `LedgerExempt`.
  - **Sales, invoice:**
    - Posts Dr COGS / Cr GDNI per line under type 7, at the challan movement's current cost. For a named challan the cost is taken in proportion to the quantity billed. For order-billed delivered goods it is the average cost of every posted sale challan line against the order line.
    - A named non-sale challan (approval, say) credits Inventory instead, because its goods never left Inventory in the ledger.
    - If Inventory cannot be reached, the result is `StockRefused` and nothing is posted.
    - Void withdraws type 7, which puts the cost back in GDNI.
  - **Tests:**
    - `Accounting.Api.Tests.GdniClearingTests`: the seed row; challan, worker, then invoice leaves GDNI at zero; void restores GDNI; a restatement leaves a residue rather than failing; the worker's settlement of a line leaves its clearing alone.
    - `Inventory.Api.Tests`: `StockLedgerMappingTests` covers DLC to GDNI and exempt movements posting nothing. `WeightedAverageRecostingTests` covers an exempt movement that is revalued but not requeued, while a zero-cost one is requeued.
    - `Sales.Api.Tests`: challan posting per line, non-sale challans (four types), ledger refusal, clearing at current cost, partial billing, Inventory unreachable, approval challan, void, the order-billed path in `PartialFulfilmentTests`, and the client route. The GDNI entry is off `SalesAccountNameTests`' allow-list.
  - **Found, not fixed:** an order-billed invoice after a *voided direct invoice* posts no cost for the re-billed goods. Those goods were issued by the voided invoice, not a challan, so there is no challan movement to read. This is TK-10's "void leaves stock issued with no Inventory credit" gap, still open.
  - **Owner step:** run the Accounting, Inventory and Sales suites from dropped databases. Branches that already exist need the admin retry to get GDNI before their next sale challan posts. Until then, the challan's post is refused with Accounting's reason.
### TK-11 · Fixed assets: a service layer, guards and tests
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#24](https://github.com/jothi-prabaharan/Bill-Book/issues/24)
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
  - [x] Move register, capitalise and dispose into a `FixedAssetService` that returns outcomes, and
        let the controller only map results.
  - [x] Add a `CancellationToken` to every action.
  - [x] Change `dispose` to `{id:long}`. Another branch's asset answers `NotFound()`, not `Forbid()` — see Notes.
  - [x] Add `[PermissionAction("approve")]` to `dispose` and `depreciation-run`.
  - [x] Make running depreciation twice for one period a no-op, if it isn't already
        (`DepreciationService.cs:30` says it checks existing transactions).
  - [x] Test: straight-line and WDV, one month each.
  - [x] Test: a second run in the same month posts nothing.
  - [x] Test: another branch's asset gets `NotFound()`.
- **Done when:** the register follows the house rules, and depreciation is idempotent per period.
- **Notes:**
  - In code, D-08 is answered with both Books and Tax schedules; the owner still has to confirm it.
  - From TK-75: the two pages in `accounting-ui/src/lib/fixed-assets/` are neither exported from
    the lib's `index.ts` nor routed anywhere in `apps/web`, so no user can reach them.
  - Done (2026-09-24):
    - `FixedAssetService` owns list, register, capitalise and dispose, and returns `FixedAssetOutcome`.
      It checks the category and the asset code first, validates schedules, and writes the asset and
      its schedules in one scope.
    - Dispose refuses an asset that isn't Active (so disposing twice fails) and a disposal dated before
      the purchase.
    - The controller holds no `DbContext`, every action takes a `CancellationToken`, and `dispose`
      answers 204.
  - **`Forbid()` → `NotFound()`**: this card predates the TK-71 rule in `CLAUDE.md` ("When asked
    to add an endpoint", point 4). Under that rule, a row id outside the caller's branch is 404,
    because the filter hides it and a 403 would confirm it exists. The card's wording was
    superseded, so the code follows `CLAUDE.md`.
  - **Written-down value charged nothing.** `DepreciationService` only knew straight line, so a
    WDV schedule's amount stayed 0 and the asset was skipped. It is now
    `DepreciationService.MonthlyCharge`, which is pure:
    - WDV charges the rate on cost less what was charged to date;
    - both methods stop at salvage value;
    - a schedule isn't charged before its `DepreciationStartDate`.
  - The run returns `DepreciationRunResult` and doesn't throw. It posts the journal and the asset
    transactions inside one scope, so a refused journal leaves nothing behind. It answers 409 for
    a closed period. It was already idempotent per month; the guard is unchanged.
  - Tests: `FixedAssetRulesTests.cs` (new, no database) and `FixedAssetPostingTests.cs` (seven
    tests added, and the harness parameterised).
  - Not done here: the pages still aren't routed (TK-75's note above).


### TK-12 · Fixed assets: capitalisation and disposal postings
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#25](https://github.com/jothi-prabaharan/Bill-Book/issues/25)
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
  - [x] Once D-19 is answered: when a bill posts, its capital line creates the register row. Purchase
        calls a new Accounting internal endpoint; it never touches `acc` directly (hard rule 8).
        The posting reclassifies the shared Fixed Asset account to the category's account. A
        migrated asset (no `PurchaseBillId`) debits against Opening Balance Equity.
  - [x] Once D-20 is answered: add `ProceedsBankAccountId` to `DisposeAssetRequest`, then post four
        legs:
    - the accumulated depreciation written back;
    - the asset removed at cost;
    - the proceeds received;
    - the gain or loss.
  - [x] Test: bill → register row → one month's depreciation → disposal, with each step's journal
        balanced, and the asset and accumulated-depreciation accounts back at zero afterwards.
- **Done when:** an asset bought on a bill depreciates, is disposed of, and each step posts a
  balanced journal.
- **Notes:**
  - D-19 answered (2026-09-24): reclassify to the category's account; a migrated asset debits against Opening Balance Equity.
  - D-20 answered (2026-09-24): **support both** disposal paths — proceeds to a bank or cash account chosen on the disposal, **or** a sales invoice to the buyer (Dr the buyer's receivable). The disposal request carries one of `ProceedsBankAccountId` or `SalesInvoiceId`.
  - D-09 answered: no new transaction codes; acquisition rides `BIL`/`OPB`, disposal `INV`/`JRN`.
  - Done (2026-09-24). How each part landed:
    - **Postings go through `JournalService.PostSystemAsync`** (new), not a `BIL` key. It is a JRN
      journal created and posted in one scope, allowed onto control accounts because the register
      is their subledger. Every other hand-entry check still runs. Two keys were ruled out:
      - posting under the bill's own key would replace the bill's ITEM leg (same document, line
        and leg type);
      - there is no free `mst.LedgerTypes` id without an `L-MST` migration.

      Depreciation now uses the same path, filed under ledger source 14 (Depreciation).
    - **Bill → register**:
      - `BillService.PostAsync` calls `IFixedAssetClient` → `POST internal/fixed-assets/capitalise-bill`
        after the ledger post. A refusal leaves the bill Draft, and a retry is safe.
      - A new column `acc.FixedAssets.PurchaseBillDetailId` has a unique filtered index
        (migration `FixedAssetBillLine`), so each line registers once.
      - Asset code `{DocumentNo}-{LineNumber}`, status Active, no schedule. A new
        `PUT api/accounting/fixed-assets/{id:long}/schedules` sets the schedule, and is refused once
        depreciation has been charged.
      - A manual `capitalize` for a bill that registered itself → `AlreadyCapitalised`.
    - **Migrated** (register with no `PurchaseBillId`): Dr category asset / Cr Opening Balance Equity.
    - **Disposal**: `DisposeAssetRequest` carries `ProceedsBankAccountId` **or** `SalesInvoiceId`.
      Neither is allowed only when `SaleAmount` is 0 (scrapped).
      - The invoice path reads the invoice's ITEM credit legs (`INV`/`POS`) from `acc.JournalLedger`
        as the proceeds, and debits them back off those accounts. The invoice already debited the
        buyer's receivable, and an asset sale is not trading income. **Owner: confirm this reading of
        D-20.**
      - The gain or loss goes to a new system account `Gain/Loss on Asset Disposal` (4920, Income,
        `SystemAccount.AssetDisposalGainLoss`). Existing branches get it from the idempotent seed.
      - An asset registered before TK-12 (no Acquisition transaction) is taken off the shared Fixed
        Asset account, where its cost still sits.
  - **Also fixed in `BillService.BuildLegs`**: a capital line on a bill *against a receipt* debited
    GRNI, which the receipt never credited for it, because receipts refuse capital lines. It now
    debits Fixed Asset.
  - **Void**: a posted bill with a capital line is refused. Withdrawing its legs would leave the
    register and the reclassification standing on nothing.
  - **Found, not fixed (needs a card):** Purchase's `LedgerLegRequest` has no `AccountId`, and an
    expense line sends neither a system name nor an id. Accounting refuses it with "A leg has to
    name its account", so **every bill with an expense line fails to post**. A receipt-backed bill
    sends its expense lines to GRNI instead, which is wrong in a different way.
  - Tests:
    - `Accounting.Api.Tests/FixedAssetPostingTests.cs`: eight added, including the full bill →
      register → depreciation → disposal cycle with every journal balanced and the asset,
      accumulated-depreciation and Fixed Asset accounts at zero;
    - `FixedAssetRulesTests.cs`: disposal arithmetic, the asset code, the schedules route;
    - `Purchase.Api.Tests/BillServiceTests.cs`: five added, including the new `RecordingFixedAssets`
      stub.


### C · Phase 1: finish what's in flight

### TK-13 · Platform operators: `IsPlatformOperator` on the user (D-01)
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#26](https://github.com/jothi-prabaharan/Bill-Book/issues/26)
- **Lanes:** L-MST · **Depends on:** TK-70 · **Decision:** D-01 (answered)
- **Where:** `backend/Api/Master/Master.Entity/TableEntities/User.cs`, `Master.Api/Services/JwtTokenService.cs`, `Master.Api/Services/DatabaseMigrationService.cs` (`BootstrapFirstOperatorAsync`), `frontend/apps/admin`.
- **State:** `platform.*` is seeded into the permission catalogue and `apps/admin` checks for it, but nothing grants it, so nobody can sign in to `apps/admin`.
- **Sub-tasks:**
  - [x] Add `IsPlatformOperator bool` (default false) to `User`, with an admin migration; run `has-pending-model-changes`.
  - [x] `JwtTokenService` adds every `platform.*` permission to the token when the flag is true — never through a role.
  - [x] Set the flag only from bootstrap configuration (`Bootstrap:OperatorEmails`) or from an existing operator through a `[RequirePermission("platform.edit")]` endpoint. No tenant screen can set it.
  - [x] Test: an Owner of a customer never gets `platform.*`; an operator does; a non-operator calling the grant endpoint gets 403.
- **Done when:** an operator signs in to `apps/admin` and sees the customer list; no tenant user can.
- **Notes:**
  - Done (2026-09-24):
    - `User.IsPlatformOperator`, with the admin migration `UserIsPlatformOperator` (one column, no
      `HasData` churn). `has-pending-model-changes` is clean for both Master contexts.
    - The permissions are added in `AuthService.IssueAsync`, not `JwtTokenService`. That is where
      sign-in, branch switch and refresh all get their permission list; the token service only
      writes claims. The role query now also excludes `Module == "platform"`, so a `platform.*`
      row on a role grants nothing. The flag is read at every issue, so a revoke bites at the next
      refresh.
    - `PlatformOperatorService.ApplyBootstrapAsync` runs at every Master start from
      `Bootstrap:OperatorEmails`:
      - it accepts a comma/semicolon string or an array, case-insensitive;
      - it is **grant-only**, so a typo can't lock every operator out.
      It is wired into `appsettings.json`, `appsettings.Development.json`, `deploy/local` (defaults
      to the owner's address) and `deploy/azure` (`bootstrapOperatorEmails`).
    - `PlatformOperatorsController`: `GET` (`platform.view`) and `PUT {userId:guid}`
      (`platform.edit`). An operator can't revoke themselves.
  - Tests: `Master.Api.Tests/PlatformOperatorTests.cs`.
  - Owner: set `Bootstrap:OperatorEmails` on each deployment, then sign in to `apps/admin`.
    The operator still needs a branch assignment to get through the two-step login.

### TK-14 · Item search: barcode and paging
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#27](https://github.com/jothi-prabaharan/Bill-Book/issues/27)
- **Lanes:** L-INV · **Depends on:** — · **Decision:** —
- **Where:**
  - `backend/Api/Inventory/Inventory.Api/Services/ItemService.cs:36-78` (`ListAsync`).
  - `backend/Api/Inventory/Inventory.Api/Controllers/ItemsController.cs:26`.
- **State:** `GET /api/items?search=` matches name and code with `ILike` and returns at most 500
  rows. It doesn't match barcodes (`inv.ItemBarcodes`) and can't page, and the POS till needs both.
- **Sub-tasks:**
  - [x] Also match `ItemBarcodes.Barcode`, exactly. A scanned code should rank first.
  - [x] Add `skip` and `take`, clamped the way `SalesOrderService`'s list does, and return a total.
        Keep the old response shape when neither is passed, so existing callers don't change.
  - [x] Test: an exact barcode returns that one item.
  - [x] Test: paging returns the right total.
  - [x] Test: another branch's item never appears.
  - [ ] Owner: run `Inventory.Api.Tests`.
- **Done when:** a scanned barcode finds its item through `GET /api/items`.
- **Notes:**
  - Done (2026-09-24):
    - `ItemService.Filtered` is shared by `ListAsync` (the old bare array, capped at 500) and the new
      `PageAsync` (`ItemListPage` with `Total`, `Skip`, `Take` and `Rows`; skip ≥ 0, take clamped to 1–200).
    - The controller returns a page only when `skip` or `take` is passed, so existing callers see no
      change.
    - A barcode matches only exactly, and only an active one. The order is: scanned barcode, then
      exact item code, then `DisplayOrder`, then name.
  - Tests: `Inventory.Api.Tests/ItemSearchTests.cs`.
  - For TK-15 and the till: to page, pass `take`; the response is then `{ total, skip, take, rows }`.

### TK-15 · Item and customer pickers on the sales forms
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#28](https://github.com/jothi-prabaharan/Bill-Book/issues/28)
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
  - [x] Add `sales-lookup.service.ts` to `libs/sales/sales-core`, with `customers(search)` and
        `items(search)`. Mirror `PurchaseLookupService`.
  - [x] In each of the five forms:
    - replace the numeric inputs with a button that opens `bb-lookup-dialog`;
    - add `picker` and `pickerRows` signals, as in `bill-form.page.ts`;
    - show the chosen name, and store the id.
  - [x] Keep the `contactLabel` or `itemLabel` that edit mode shows when it loads a saved document.
  - [x] Test: a `sales-lookup.service.spec.ts` that asserts the URLs and the mapping.
  - [x] Update the docs pages for the five sales screens, and add a release-notes bullet.
  - [x] `npm run lint`, the typecheck and `nx build web` are all clean.
  - [ ] Owner: `npm run test`, then pick a customer and an item on each form at 360px.
- **Done when:** every sales form picks its customer and items by name.
- **Notes:** TK-14 later improves item search (barcode); this card doesn't wait for it.
  - TK-79 (2026-09-23) built the till's own `PosLookupService` in
    `frontend/apps/desktop/src/app/pos-terminal/pos-lookup.service.ts`, because this card was open
    when TK-79 ran. Once `SalesLookupService` exists, move the till's `customers()` and `items()`
    onto it and keep only the till's own lookups (walk-in, item detail, sales rates, branch).
  - Done (2026-09-24):
    - **`SalesLookupService`** (sales-core) holds `customers(search)` (`role=customer`) and
      `items(search)`, which also matches a whole barcode (TK-14).
    - **`SalesPicker`** (sales-ui) is one helper for all five forms: the dialog's signals, a search
      token so a late answer never overwrites a newer one, and `withItem` to set a line and
      recalculate it. It replaces the `picker`/`pickerRows` fields that were copied per form in
      purchase.
    - Every form now has a customer button in `bb-form-field` in place of the numeric input, one
      `bb-lookup-dialog`, and `contactLabel`, which is set from the loaded document's
      code/name or from its id. Choosing a customer fills the GSTIN only when it is empty.
    - Item picking is wired on quote, order, invoice and challan. A credit note's lines come from
      its invoice, so it has no item picker. Choosing its customer loads their open invoices, as
      the old `valueChange` did.
    - **Quote:** `contactId` defaulted to `1`, so a quote saved untouched went to the branch's first
      contact. It is now `0`, with `min(1)`. `QOT-T1-01` was updated to match.
    - **Till:** `PosLookupService.customers()` and `.items()` now delegate to `SalesLookupService`
      (`apps/desktop`, the card's note above).
  - Checks: lint (0 errors), typecheck and `nx build web desktop` are clean.
  - Tests:
    - `sales-core/.../sales-lookup.service.spec.ts`
    - `sales-ui/.../sales-picker.spec.ts`
    - five picker tests in `invoice-form.component.spec.ts`
    - four in `sales-forms.spec.ts` (which now provides a stub `SalesLookupService`)
  - Owner: `npm run test`, then pick a customer and an item on each form at 360px.


### TK-16 · Contact picker on the support ticket form
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#29](https://github.com/jothi-prabaharan/Bill-Book/issues/29)
- **Lanes:** L-CUSTOMER-UI · **Depends on:** — · **Decision:** —
- **Where:** `frontend/libs/customer/customer-ui/src/lib/tickets/ticket-form.component.html`
  (was a `bb-master-select`, not a numeric `contactId` as first written. It loaded every contact
  in the branch into one dropdown).
- **Sub-tasks:**
  - [x] Replace the numeric field with `bb-lookup-dialog` over `/api/contacts?search=`, the way TK-15 does.
  - [x] Update the docs page.
  - [x] Lint, typecheck and build are clean.
- **Done when:** a ticket is raised by picking the contact by name.
- **Notes:**
  - Done (2026-09-24):
    - The ticket form opens `bb-lookup-dialog` over `CustomerService.searchContacts`, the same call
      the lead conversion picker already used.
    - It keeps a search token, so a late answer can't overwrite a newer one.
    - The dialog sits in a `.picker-layer` stacking context (z-index 1001), because the ticket
      form's overlay is at 1000 and the dialog's own is 100, so it would otherwise open behind
      the form.
  - **Docs:** there is no Support page under `frontend/apps/docs/content/`, so only the release note
    carries this. A Customer/Support page is its own piece of work.
  - Tests: three added to `ticket-form.component.spec.ts`.

### TK-17 · Seed a `WALKIN` contact per branch
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#30](https://github.com/jothi-prabaharan/Bill-Book/issues/30)
- **Lanes:** L-CON (+ L-ACC for the provisioning fix, see Notes) · **Depends on:** — · **Decision:** owner, 2026-09-24 (see TK-40)
- **Where:** `backend/Api/Master/Master.Api/Controllers/InternalSeedController.cs` (Master's own branch seed), the contact service's create path.
- **Sub-tasks:**
  - [x] Seed one contact per branch: code `WALKIN`, name "Walk-in Customer", role customer, no GSTIN (so B2C place of supply is the branch's state).
  - [x] Idempotent: seeding twice leaves one; the code can't be reused by a user-created contact.
  - [x] Test: a new branch has exactly one `WALKIN`, and `pos-lookup.service`'s exact-code lookup finds it.
- **Done when:** the till's default customer resolves on a new branch with no setup.
- **Notes:**
  - Done (2026-09-24):
    - `ContactService.SeedWalkInAsync(baseCurrency)`: code `WALKIN`, "Walk-in Customer", customer,
      `Individual`, `GstRegistrationType.Consumer`, no GSTIN and no place of supply, in the branch's
      base currency.
    - It is idempotent. It also retries the sub-ledger when an earlier provisioning failed.
    - Called from the in-process Contacts seed in `TenantSeeder`, after the HTTP services, so
      Accounting's chart exists first. Also called from `InternalSeedController` (`walkInCustomer`).
    - `CreateAsync` refuses `WALKIN` in any case as `DuplicateCode`, because the till matches it
      case-insensitively.
  - **Fixed on the way (L-ACC):** `internal/sub-accounts/provision` only learned the branch from a
    forwarded user token, so a caller with none (seeding) provisioned into no branch. The walk-in
    would have had no receivable sub-account and could never be invoiced.
    - `ProvisionSubAccountsRequest` now carries an optional `CustomerId`/`OrgId`. The controller uses
      `InternalTenant.Apply` (TK-06) and resolves `SubAccountService` after the tenant is set.
    - Master's `AccountingSubAccounts` sends the tenant in the body.
  - Tests:
    - `Master.Api.Tests/WalkInContactTests.cs`
    - `Accounting.Api.Tests/InternalSubAccountsControllerTests.cs`
    - The till's exact-code lookup is already covered by `pos-lookup.service.spec.ts`.

### TK-18 · Customer module seed data (stage C4)
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#31](https://github.com/jothi-prabaharan/Bill-Book/issues/31)
- **Lanes:** L-CUS, L-MST (the seeder list) · **Depends on:** — · **Decision:** D-18
- **Where:**
  - `backend/shared/Shared.Kernel/Customer/Enums.cs`: `LeadSource`, `LeadStatus`, `TicketStatus`, `TicketPriority`.
  - `backend/Api/Customer/Customer.Api/Controllers/TicketsController.cs:101`: SLA hours hard-coded
    per priority.
- **State:** every lead and ticket list is an enum, so there's no reference data left to seed. The
  only hard-coded business data is the SLA table (Urgent 2 h, High 8 h, Medium 2 days, Low 7
  days). No section in `docs/Modules.md` defines C4.
- **Sub-tasks** (if D-18 answers "per-branch SLA policy"):
  - [x] `cus.SlaPolicies` (`Priority`, `ResponseHours`, `ResolutionHours`), with its migration and
        TK-71's RLS block.
  - [x] Seed four rows per branch through a new `POST internal/seed/organization` on Customer, and
        add `"Customer"` to `TenantSeeder.Services`. That needs `L-MST` too.
  - [x] Replace the `switch` at `TicketsController.cs:101` with a lookup.
  - [x] Test: seeding twice adds nothing, and a ticket's `SlaDueAt` follows its branch's policy.
- **Done when:** decided by D-18.
- **Notes:**
  - D-18 answered (2026-09-24): build the per-branch `cus.SlaPolicies` table exactly as the sub-tasks above say.
  - Done (2026-09-24):
    - Entity `SlaPolicy` (`Priority`, `ResponseHours`, `ResolutionHours`), unique `(OrgId, Priority)`.
    - Migration `AddSlaPolicies`, which carries its own RLS block (ENABLE, FORCE,
      `slapolicies_tenant_isolation`). `has-pending-model-changes` is clean.
    - `SlaPolicySeed.Defaults` holds the hours that were hard-coded: resolution 2/8/48/168, with
      response 1/4/8/24 added.
    - `SlaPolicyService.SeedAsync` adds only the missing priorities and keeps a branch's edits.
    - `DueAtAsync` uses the branch's `ResolutionHours`, and falls back to the default when a branch
      isn't seeded yet.
    - `TicketsController.Create` now uses `DueAtAsync` and `TimeProvider`.
    - New `Customer.Api/Controllers/InternalSeedController`; `"Customer"` added to
      `TenantSeeder.Services`.
    - Wiring for `Seeding:Customer`:
      - `appsettings*.json` (dev `http://localhost:4502/`);
      - `deploy/local` compose;
      - `seed/first-branch.sh`;
      - `deploy/azure/modules/settings.bicep`.
  - Not done: a Settings screen to edit the hours. The table is edited only through the database
    for now. A ticket's due date is also not recalculated when its priority changes (unchanged
    behaviour).
  - Tests: `Customer.Api.Tests/SlaPolicyTests.cs`. `Master.Api.Tests/TenantSeederTests.cs` now
    includes Customer.


### TK-19 · Notification.Worker takes over email from Master
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#32](https://github.com/jothi-prabaharan/Bill-Book/issues/32)
- **Lanes:** L-NTF, L-MST, L-KERNEL, L-DEPS (one commit) · **Depends on:** TK-70 · **Decision:** —
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
  - [x] Define an `EmailRequested` event in `Shared.Kernel`, with `MessageId` and the fields of
        `EmailMessage`. This takes `L-KERNEL` for that commit.
  - [x] Replace `QueuedEmailSender` with a sender that publishes `EmailRequested` through
        `IEventPublisher`.
  - [x] Keep the in-process path when Service Bus isn't configured, so local development still
        sends mail.
  - [x] Add a Service Bus consumer to `Notification.Worker` that sends through `SmtpEmailSender`,
        which moves or is shared.
  - [x] Dedupe on `MessageId`, since delivery is at least once. A `ntf.ProcessedMessages` table
        (its own migration, with RLS) or an idempotency key is enough.
  - [x] SMTP settings are per customer (`mst.SmtpSettings`). The worker reads them through Master's
        API, not its `DbContext` (hard rule 8).
  - [x] Test: a redelivered message sends once.
  - [x] Test: an OTP email still arrives with Service Bus unset.
  - [ ] Owner: send an invitation end to end.
- **Done when:** an invitation email is sent by the worker, and a redelivered message sends once.
- **Notes:** ask the owner before moving anything beyond email.
  - Done (2026-09-24). **Only email moved.**
  - **Shared.Kernel:**
    - `Messaging/EmailRequested` carries a `MessageId` in the body (the dedupe key, chosen once by
      the sender) plus every `EmailMessage` field.
    - `Email/SmtpMailer` and `ResolvedSmtp` moved out of Master, so both processes build a mail
      one way.
  - **Master:**
    - `EventEmailSender` publishes `EmailRequested`. `QueuedEmailSender` wasn't replaced; it is
      kept as the default.
    - `EmailDelivery.UseWorker` picks the path. It needs `ServiceBus:Namespace` **and**
      `Notification:EmailWorker=true`. A namespace alone would have silently stopped Azure's mail,
      because the worker isn't deployed there.
    - New `InternalSmtpController` (`GET internal/smtp/resolved?customerId=`, `[InternalOnly]`).
      **This returns the decrypted SMTP password to the internal-key holder**, a deliberate change
      from "the password never leaves Master". It is used only on the worker path, and the worker
      holds it for one send.
  - **Worker:**
    - `NotificationDbContext` (`ntf`), `ProcessedMessage`, and migration `InitialNotificationSchema`
      (migrated at start).
    - `ProcessedMessageStore` claims before sending (the PK lets one insert through) and marks sent
      after. A claim with no send behind it goes stale after 5 minutes, and a guarded update decides
      the takeover.
    - `EmailRequestHandler` runs claim → resolve → send → mark, and gives the claim back on failure.
    - `EmailRequestedConsumer` (Service Bus processor, manual settle, and only when the namespace is
      set):
      - sent or duplicate → complete;
      - in progress or failed → abandon;
      - unreadable or no mailbox → dead-letter.
  - **RLS:** `ntf.ProcessedMessages` is a *named exemption*, not "with RLS" as first written. It has
    no tenant columns, and platform mail has no customer. The reason is written in the migration and
    added to CLAUDE.md's exemption list.
  - **L-DEPS:** pinned `System.Security.Cryptography.Xml` to 10.0.12. EF Design under the Worker SDK
    pulled 9.0.0 (high-severity advisories), and 10.0.0 is flagged too. Also added the test
    project to `Bill-Book.sln`.
  - **Not done, for the owner:** neither deployment runs the worker. Azure needs:
    - an `EmailRequested` topic and a `notification-worker` subscription;
    - a worker container with an identity holding Data Receiver;
    - `Notification:EmailWorker=true` on Master.

    Steps are in the docs' Deployment page. Until then everything takes the in-process path.
  - Tests:
    - new project `tests/Notification.Worker.Tests` (`NOTIFICATION_TEST_DB`): `ProcessedMessageStoreTests`,
      `EmailRequestHandlerTests`, `EmailRequestedConsumerTests`;
    - `Master.Api.Tests/EmailDeliveryTests.cs`;
    - `Shared.Kernel.Tests/SmtpMailerTests.cs`.


### TK-20 · `PaymentReminderWorker` sends nothing, and reads across tenants
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#33](https://github.com/jothi-prabaharan/Bill-Book/issues/33)
- **Lanes:** L-NTF, L-KERNEL, L-CON, L-INV (the enumerator move) · **Depends on:** TK-19 · **Decision:** —
- **Where:** `backend/worker/Notification.Worker/PaymentReminderWorker.cs:39-100` and `Program.cs`.
- **State:**
  - It loads `ReminderProfiles` and overdue `Invoices` with `IgnoreQueryFilters()` and no tenant,
    then writes a `ReminderLog` row, **but never sends an email**.
  - Once RLS is on (TK-03), it will see no rows at all.
  - It runs once every 24 hours, from whenever the process started.
- **Sub-tasks:**
  - [x] Iterate branches the way `CostingEngine.Worker/Consumers/CostingWorker.cs:140-150` does:
        list them, then set `TenantContext.CustomerId` and `OrgId` per branch in a new scope.
  - [x] Drop `IgnoreQueryFilters()`.
  - [x] Settle the invoice through Accounting's settlement API, so a paid invoice gets no reminder.
  - [x] Send the reminder through the email path from TK-19, and write `ReminderLog` in the same
        unit of work.
  - [x] Replace the 7-day constant with a field on the profile, if the entity has one. Otherwise
        record the gap under Notes.
  - [x] Test: a paid invoice gets no reminder.
  - [x] Test: a second run on the same day sends nothing.
  - [x] Test: branch A's profile never reminds branch B's invoice.
- **Done when:** an overdue, unpaid invoice produces exactly one email per reminder window.
- **Notes:**
  - Done (2026-09-24):
    - **Branches:** `ITenantEnumerator`/`HttpTenantEnumerator`/`ActiveOrganization` moved from
      `CostingEngine.Worker/Consumers` to `Shared.Kernel.Tenancy`, and the costing engine uses them
      from there. `PaymentReminderWorker` runs each branch in its own scope with its tenant set.
    - The worker's `SalesDbContext` now carries the audit and RLS interceptors, as every service's
      does.
    - **`PaymentReminderRun`**, per branch:
      1. Active profiles pick posted invoices at least the profile's trigger days past due.
      2. Invoices with a `ReminderLog` for that profile inside the interval are excluded in the
         query.
      3. Accounting's `internal/ledger/settlements` (INV, CONTROL leg) keeps only invoices with
         something outstanding. If Accounting can't be read, the whole branch is skipped rather
         than risk reminding someone who has paid.
      4. Master's new `internal/contacts/emails` (the default person's email, else the first active
         person with one) gives the address.
      5. `EmailRequestHandler` (TK-19) sends it, under a message id hashed from branch, invoice,
         profile and day. The `ReminderLog` rows are saved in one `SaveChanges` at the end.
    - **No interval field on `sal.ReminderProfiles`:** the 7 days is now
      `PaymentReminderRun.ReminderInterval`, one value for every profile. A per-profile interval
      needs a Sales migration, which is left for a card that edits profiles.
    - Reminders go through the handler directly, not through Service Bus. They're produced inside
      the worker that consumes them, so publishing would be a round trip to itself; the dedupe and
      the Master mailbox are the same either way.
  - Not built: there is still no screen for reminder profiles, so a branch has none until one is
    inserted.
  - Tests: `Notification.Worker.Tests/PaymentReminderRunTests.cs` (the fixture now also migrates
    `sal`).

### TK-21 · A blank optional phone is NULL everywhere (D-04)
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#34](https://github.com/jothi-prabaharan/Bill-Book/issues/34)
- **Lanes:** L-CON, L-MST, L-INV, L-CUS · **Depends on:** — · **Decision:** D-04 (answered)
- **Where:** the phone columns: `con.ContactAddresses` and `con.ContactPersons` (`PhoneNumber`, `MobileNumber`), `mst.Users.MobileNumber`, `mst.Organizations` (`PhoneNumber`, `MobileNumber`), `inv.Warehouses` (`PhoneNumber`, `MobileNumber`), `cus.Leads.Phone`. No shared normaliser exists.
- **Sub-tasks:**
  - [x] Add one `PhoneNumbers.NormalizeOptional(string?)` in `Shared.Kernel` (trim; blank → null; keep the leading `+` rule from `CLAUDE.md`). Takes `L-KERNEL` for that commit.
  - [x] Call it in every service that saves those columns.
  - [x] ~~One data migration per schema~~ A LINQ backfill at Master startup (see Notes) turning `''` into NULL in those columns.
  - [x] Test: the normaliser (blank, spaces, `+91…`, local); one save per service stores NULL for blank.
- **Done when:** no phone column in any schema holds an empty string.
- **Notes:** the lanes are many but each edit is small; release each lane as soon as its commit lands.
- **Outcome (2026-09-24):**
  - `Shared.Kernel.Validation.PhoneNumbers.NormalizeOptional` trims and turns blank into NULL. It keeps the `+` and adds no prefix.
  - It is called at every write of the listed columns: `ContactService` (addresses, people, the quick-create person), `UserService`, `SignupService`, `InProcessSeams`, `InternalUsersController`, `OrganizationService`, `WarehouseService` and `LeadsController`.
  - **No data migration was written.** A migration can only update rows in raw SQL, which hard rule 1 forbids. Instead, `Master.Api/Services/BlankPhoneBackfill.cs` runs `ExecuteUpdate` from `DatabaseMigrationService` on every start: the `mst` columns after the admin migration, and the `con`, `inv` and `cus` columns after each tenant schema migrates. It is idempotent, because only rows still holding `''` match. It reads past the query filter, as the seeding beside it does. Row-level security still applies, so a deployment login that does not bypass RLS would only clear what it can see. Nothing is deployed (D-13), so this matters only for developer databases.
  - Tests: `Shared.Kernel.Tests.PhoneNumbersTests`, `Inventory.Api.Tests.WarehousePhoneTests`, `Customer.Api.Tests.LeadPhoneTests`, `Master.Api.Tests.BlankPhoneBackfillTests`. Master's contact save needs the admin database, numbering and accounting, so it has no round-trip test of its own. The backfill test covers `mst`.

### D · Phase 2

### TK-22 · Document archive: PDF/A, every document, a download link
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run · **PDF/A and template rendering not done, see Outcome**
- **Issue:** [#35](https://github.com/jothi-prabaharan/Bill-Book/issues/35)
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
  - [ ] ~~Once D-11 confirms PDFsharp, emit PDF/A-2b.~~ **Not possible on 6.1.1** — see Outcome. Check PDFsharp 6.1.1's PDF/A support, and if
        it has none, record that under Notes.
  - [x] Add `GET api/sales/invoices/{id:long}/pdf`, which streams the stored file through
        `IFileStorage` (or returns a signed link).
  - [x] Archive credit notes and delivery challans the same way.
  - [ ] ~~Once TK-81 lands, render from the template~~ **Not done** — see Outcome. Render from the template instead of the fixed layout.
  - [x] Test: posting archives exactly one file.
  - [x] Test: a re-post doesn't fail on the leftover file.
  - [x] Test: another branch's PDF gets ~~`Forbid()`~~ `NotFound()` (CLAUDE.md, TK-71: a row outside the caller's branch is not found).
- **Done when:** a posted invoice's PDF/A file downloads from the invoice screen.
- **Notes:**
  - D-11 answered (2026-09-24): PDFsharp. Replace every mention of Syncfusion as the intended library (CLAUDE.md is done by TK-86).
- **Outcome (2026-09-24):**
  - **PDF/A: PDFsharp 6.1.1 has no PDF/A API.** Its assembly and XML documentation have no PDF/A conformance setting, no XMP metadata writer and no output-intent helper. Every "pdfa" match is `PdfAcroField`. Producing PDF/A-2b needs one of two things: a later PDFsharp than the pinned 6.1.1, or hand-building the XMP metadata, the sRGB output intent and the font embedding on the 6.1.1 object model. Either is a change to the owner's D-11 pin, so it is raised as **D-22** in section 3 rather than done here. The archived files are plain PDF.
  - **Template rendering: not done.** A print template is HTML, laid out by Printing's `PrintRenderer`. PDFsharp draws primitives and cannot lay out HTML, so rendering the archive from the template needs an HTML-to-PDF engine. That is part of D-22. The archive keeps one fixed layout, now shared: `Services/Pdf/SalesPdfLayout.cs`.
  - **Built:**
    - `SalesPdfLayout` is the one layout every archived sales PDF uses. It also fixes the VOID stamp: the old renderer compared the status with `"Voided"`, which the enum never produces.
    - `SalesDocumentArchive` owns the key (`{customer}/{branch}/retail-erp/sales/{invoices|credit-notes|delivery-challans}/{id}.pdf`), the credit note and challan write (`FileWriteMode.Replace`, with the scope resolved before any HTTP call), and the read-back. A document the branch cannot see, a draft and a missing file all return null.
    - `GET api/sales/{invoices|credit-notes|delivery-challans}/{id:long}/pdf` requires `sales.print`, as printing does.
    - The invoice print page has a **Download PDF** button for posted and voided invoices.
  - Tests: `SalesPdfLayoutTests`; in `CreditNoteServiceTests`, archive-once and download, retry over leftover, and another branch and draft not found; in `DeliveryChallanServiceTests`, archive-once and a refused post archiving nothing; in `invoice-print.spec.ts`, `downloadPdf`.
  - A voided invoice's archived copy is the one filed at post, with no VOID stamp. Re-filing on void is left for when PDF/A lands.

### TK-23 · Date input that follows the branch's format
- [!] blocked — the owner tries `bb-branch-date-input` before it replaces `bb-date-input` (built by Claude Opus 5.5, 2026-09-24 · tests written, not run)
- **Lanes:** L-UI · **Depends on:** — · **Decision:** —
- **Where:**
  - `frontend/libs/shared/ui-components/src/lib/date-input/date-input.component.ts`: a native `<input type="date">`.
  - `frontend/libs/shared/currency-format/src/lib/format-settings.service.ts:55` (`formatDate`).
  - 26 templates use `bb-date-input`.
- **Sub-tasks:**
  - [x] Build a text input with a calendar popover that displays
        `FormatSettingsService.settings().datePattern`, parses typed input in that pattern, and
        keeps the value ISO (`yyyy-MM-dd`) so all 26 callers stay unchanged.
  - [x] Make it keyboard- and screen-reader-accessible, and turn the popover into a full-screen
        sheet at 360px.
  - [ ] Show the proposal to the owner before swapping it in, since it changes every date field.
  - [x] Test: parse and format round trips for `dd/MM/yyyy`, `MM/dd/yyyy` and `yyyy-MM-dd`.
  - [ ] Owner: check it with Playwright on a `dd/MM/yyyy` branch.
- **Done when:** a branch on `dd/MM/yyyy` sees that format in every date field.
- **Notes:**
  - **Built beside the old one, not swapped (owner's instruction, 2026-09-24): "Build it, don't swap".**
    - `bb-branch-date-input` is in `libs/shared/ui-components/src/lib/branch-date-input/`, exported beside `bb-date-input`.
    - `parseDate` and `daysInMonth` are in `libs/shared/currency-format`, beside `formatDate`. Separators are lenient, single-digit parts are accepted, `yy` is read as 20yy, and the order of the parts is never guessed.
    - The value is ISO in and out, so the swap is one selector change in each of the 26 templates.
    - Accessibility:
      - the field carries `aria-invalid` and `aria-describedby`;
      - the toggle carries `aria-haspopup="dialog"` and `aria-expanded`;
      - the calendar is a `role="dialog"` holding a `role="grid"` with a roving `tabindex`;
      - each day is labelled with its weekday and full date, and today has `aria-current="date"`;
      - Alt+Down opens the calendar from the field, and Escape closes it and returns focus.
    - At 480px and below the calendar is a full-screen sheet.
    - The template compiled under `strictTemplates`: it was mounted briefly in a page, `nx build web` was run, and the mount was reverted. No screen uses it yet.
    - Specs: `format-settings.spec.ts` (`parseDate`: round trips for five patterns, including `dd/MM/yyyy`, `MM/dd/yyyy` and `yyyy-MM-dd`, plus refusals) and `branch-date-input/calendar.spec.ts`. The component itself has no spec, because this workspace's Vitest cannot compile a `templateUrl` component (CLAUDE.md).
  - **To unblock:** the owner tries it (the Playwright sub-task above). If it is right, swap the selector in the 26 templates, delete `bb-date-input`'s "known limitation" note, and add the release note. No release note is written yet, because nothing a user sees has changed.

### TK-24 · `rat` schema: exchange and metal rate history
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#36](https://github.com/jothi-prabaharan/Bill-Book/issues/36)
- **Lanes:** L-MST · **Depends on:** TK-70 · **Decision:** —
- **Where:**
  - `CLAUDE.md` Schemas: "Master database: `mst`, `rat`".
  - `backend/worker/RateSync.Worker/Program.cs` (an empty host).
- **State:** no `rat` table exists anywhere, so TK-25 and TK-26 have nowhere to write.
- **Sub-tasks:**
  - [x] Map two tables on `AdminDbContext`, with schema `rat`:
    - `ExchangeRate`: `ExchangeRateId long`, `FromCurrencyCode string(3)`, `ToCurrencyCode string(3)`,
      `RateDate DateOnly`, `Rate decimal(18,8)`, `Source enum`;
    - `MetalRate`: `MetalRateId long`, `Metal enum`, `PurityCode string(10)`, `RateDate DateOnly`,
      `RatePerGram decimal(18,4)`, `Source enum`.
  - [x] Put a unique index on (currency pair or metal and purity, `RateDate`, `Source`). Both are
        global rows with no tenant columns, like the rest of the master database.
  - [x] Add `GET api/rates/exchange?from=&to=&on=` and `GET api/rates/metal?metal=&purity=&on=`,
        which return the latest rate on or before a date. A document stores the rate as a
        snapshot; it never looks it up live.
  - [x] Add a manual entry endpoint and page, so rates can be entered while D-03 and D-14 are open.
  - [x] Test: the on-or-before lookup, and the unique index.
- **Done when:** a rate entered for a date is returned for that date and every later date until a
  newer one is entered.
- **Notes:** this would be recreated inside the TK-70 squash if both are done together. Do TK-70 first.
- **Outcome (2026-09-24):**
  - `rat.ExchangeRates` and `rat.MetalRates` are mapped on `AdminDbContext`. The enums are `RateSource` (Manual, Rbi, Ibja) and `Metal` (Gold, Silver, Platinum), both stored as strings. The migration is `AddRateHistory`, and `has-pending-model-changes` is clean.
  - **Lookup rule:** the latest rate on or before the date. On one date, a Manual row wins over a fetched one. The same currency on both sides is 1. A pair is directional and is never inverted.
  - **Manual entry is operator-only (`platform.edit`), a narrower reading than the card.** The rows are global, and every customer's documents read them, so a customer's own settings user writing one would change another business's rates. The page is therefore in `apps/admin` (Rates), not in a tenant's Settings. Per-branch rate overrides, such as a jewellery shop's own board rate, would be new scope and would need a decision first.
  - `RatesController` (`api/rates`):
    - the two lookups are open to any signed-in user, like `MasterController`'s reference data, so `RatesController` is added to Master's `EndpointGuardTests` exemptions with its reason;
    - the history needs `platform.view`;
    - `PUT` (upsert of the Manual row) and `DELETE` (Manual rows only) need `platform.edit`.
  - Gateway route `master-rates` added (`/api/rates/**`).
  - Tests: `Master.Api.Tests.RateServiceTests` (on-or-before for both tables, Manual precedence, both unique indexes, upsert correcting, refusals, removal of Manual rows only) and `apps/admin/.../rates.service.spec.ts`.

### TK-25 · RateSync.Worker: metals (IBJA)
- [x] completed (Antigravity) — 2026-09-25 · tests written, not run
- **Issue:** [#37](https://github.com/jothi-prabaharan/Bill-Book/issues/37)
- **Lanes:** L-RATE · **Depends on:** TK-24 · **Decision:** D-14
- **Where:** copy `backend/worker/CostingEngine.Worker/Program.cs`, as the note in
  `RateSync.Worker/Program.cs` asks.
- **Sub-tasks:**
  - [x] Write an IBJA client, with its key from `ISecretStore`.
  - [x] Run on a daily schedule and upsert `rat.MetalRates` per purity.
  - [x] Retry with backoff, and make a second run on the same day a no-op.
  - [x] Test: parse a recorded IBJA response.
  - [x] Test: a second run on the same day writes nothing.
- **Done when:** the day's metal rates appear in `rat` with their date.
- **Notes:**
  - D-14 answered (2026-09-24): both manual entry (TK-24) and the IBJA API. **Ask the owner for the IBJA credentials before starting**; store them through `ISecretStore`, never in `appsettings`.
  - Done (Antigravity, 2026-09-25):
    - Wrote `IIbjaClient` and `HttpIbjaClient` which parse a standard JSON structure from the API and map it to `rat.MetalRates`.
    - Added `IbjaMetalRateSync` that uses `ISecretStore.GetSecretAsync("IbjaApiKey")` to retrieve the key securely without storing in `appsettings`. If it is missing, it logs a warning.
    - Updated `RateSyncWorker` to run both the RBI and IBJA syncs when due.
    - **Tests written**: `backend/tests/RateSync.Worker.Tests/IbjaMetalRateSyncTests.cs` testing success, no-op reruns, network failures, and JSON parsing.

### TK-26 · RateSync.Worker: currency (RBI)
- [!] blocked — the parser needs checking against a real saved copy of RBI's page, which this environment cannot fetch (built by Claude Opus 5.5, 2026-09-24 · tests written, not run)
- **Lanes:** L-RATE · **Depends on:** TK-24 · **Decision:** D-03
- **Sub-tasks:** follow D-03's answer (scraping, a paid wrapper, or manual entry through TK-24's page).
  - [x] Upsert `rat.ExchangeRates` against INR.
  - [x] Make it idempotent per day.
- **Done when:** the day's exchange rates appear in `rat` with their date.
- **Notes:**
  - D-03 answered (2026-09-24): manual entry (TK-24's page) plus a daily scrape of RBI's reference-rate page. Keep the parser isolated and tested against a saved copy of the page, and let a failed scrape log to `ErrorLogs` with `FollowUpStatus = Open` rather than write a rate.
- **Outcome (2026-09-24):**
  - `RateSync.Worker` is built. `Program.cs` is modelled on the costing worker's. It references `Master.Repository`, because `rat` is Master's, the same way the costing worker references Inventory. It writes through `AdminDbContext` with the audit interceptor and `SystemUser`.
  - **The parser is isolated:** `Rbi/RbiReferenceRateParser.cs`, which takes a string and returns the page date and `{code, rate per 1 unit in INR}`.
    - It reads `INR / 1 USD` rows on the tag-stripped text, and a per-100 quote such as `INR / 100 JPY` is divided back down.
    - It reads a day-first date after the words "reference rate".
    - It refuses (`RbiPageFormatException`) when there is no date, no rate, or a non-positive figure.
  - **Idempotent per day, twice over:**
    - a `Succeeded` run in `rat.RateFetchRuns` for the India-local day stops the fetch;
    - a rate already on file for the page's date, pair and source `Rbi` is never rewritten.
    - A holiday page that repeats Friday's rates therefore writes nothing.
  - **Failures go to `rat.RateFetchRuns`, not `ErrorLogs`: a deviation from the card.** `ErrorLog` is an `OrgScopedEntity` with RLS, so a fetch that belongs to no customer and no branch has nowhere to go there. That is the same reason `AdminDbContext` registers with `writesErrorLog: false`. A failed run is written with `FollowUpStatus = Open`, the error text, and no rate. The open rows are the task list, and the next wake retries.
  - Schedule: the worker wakes every `RateSync:CheckIntervalMinutes` (60) and runs after `RateSync:RbiAfter` (13:45 India time). The migration is `AddRateFetchRuns`.
  - Tests: new project `tests/RateSync.Worker.Tests`, added to the sln. It has its own database, `ratesync_tests` (`RATESYNC_TEST_DB`), so it cannot race Master's `admin_tests`.
    - `RbiReferenceRateParserTests` (date forms, per-100 units, first figure wins, refusals).
    - `ExchangeRateSyncTests` (rates land with their date, a second run fetches nothing, a holiday rewrites nothing, a failure leaves an open run and no rate then retries, the due time and the India day).
  - **Why blocked:** the environment's network policy refused `www.rbi.org.in` and `www.fbil.org.in`. The test page, `Fixtures/rbi-reference-rate.synthetic.html`, is an imitation and says so at its top. Since 2018 the INR reference rates are published by FBIL, so the configured URL (`Rbi:ReferenceRateUrl`) may need to point there.
  - **To unblock:** save the live page into `Fixtures/`, point a test at it, and adjust the parser's two regular expressions if they miss. Then deploy the worker; it is in neither `deploy/azure` nor `deploy/local` yet.

### TK-27 · Production databases are created by infrastructure (D-02)
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#38](https://github.com/jothi-prabaharan/Bill-Book/issues/38)
- **Lanes:** L-MST, L-DEPS · **Depends on:** TK-70 · **Decision:** D-02 (answered)
- **Where:** `backend/Api/Master/Master.Api/Services/DatabaseMigrationService.cs:50,140` (`EnsureDatabaseExistsAsync`, which issues `CREATE DATABASE` at 263), `deploy/azure/` (Bicep).
- **Sub-tasks:**
  - [x] Call `EnsureDatabaseExistsAsync` only when the environment is Development; elsewhere, a missing database fails startup with a clear message.
  - [x] Declare the admin database and the first tenant shard (`IN000001`) as Bicep resources on the flexible server.
  - [x] Drop `CREATEDB` from the application role in deployment docs.
  - [x] Note for TK-46: provisioning a new shard in production must then go through infrastructure (or an operator action), not the app.
- **Done when:** a Production start against an existing server needs no `CREATEDB`, and a Development start still creates its databases.
- **Notes:**
- **Outcome (2026-09-24):**
  - `DatabaseMigrationService.EnsureOrRequireDatabaseAsync`: in Development it creates the database as before. Elsewhere it only opens a connection, and on `3D000` (`invalid_catalog_name`) it stops startup with `MissingDatabaseMessage`, which names the database, the host, and where the database is made. The password is never included. The rule is `MayCreateDatabases(env) => env.IsDevelopment()`.
  - **Bicep already declared both databases**, `EP_Admin` and `IN000001`, so only its comment changed: it now says they are the only source, and that a new shard is a new resource here.
  - **`deploy/local` was the gap.** Nothing there created the databases except the app, and its services run as `SelfHosted`. `deploy/local/db/init/01-create-databases.sql` is now mounted at `/docker-entrypoint-initdb.d`, so Postgres creates both on a first start with an empty volume. Existing installs already have them. The README's troubleshooting explains how to create one by hand if an old volume predates the script.
  - `CREATEDB`: no deployment doc granted it, so there was nothing to drop. `deploy/azure/README.md` now says the app needs no `CREATEDB`, that the services still connect as the admin login (which has it through `azure_pg_admin`), and that a DML-only login is the owner's call (TK-08).
  - TK-46 note: a second shard is a new `flexibleServers/databases` resource (or a `CREATE DATABASE` by the operator on a single PC) added **before** its row goes into `mst.TenantDatabases`. It is never created by the app outside Development.
  - Tests: `Master.Api.Tests.DatabaseCreationPolicyTests` (only Development may create; the message names database and host and hides the password; the compose file mounts the init script and the script creates both; the Bicep declares both).

### TK-28 · Settings: one Nx lib per sub-screen (D-05)
- [x] completed (Claude Opus 5.5) — 2026-09-24 · no tests needed (a move); lint, typecheck and all five app builds clean
- **Issue:** [#39](https://github.com/jothi-prabaharan/Bill-Book/issues/39)
- **Lanes:** L-MASTER-UI, L-DEPS, L-WEB · **Depends on:** — · **Decision:** D-05 (answered)
- **Where:** `frontend/libs/master/master-ui/src/lib/` — today one lib holding `api-clients`, `configurations`, `org-currencies`, `organization-settings`, `organizations`, `print-templates`, `roles`, `smtp-settings`, `users` beside contacts and HSN/SAC.
- **Sub-tasks:**
  - [x] Agree the target layout before moving code (for example `libs/settings/{users,roles,organizations,organization-settings,currencies,configuration,smtp,api-clients,print-templates}`), and write it in `docs/Modules.md`'s shared-master-pages table, since H0 mounts these pages from every app.
  - [x] Generate the libs, move each folder, add a path alias per lib in `tsconfig.base.json`, update imports and routes.
  - [x] Lint, typecheck and every app build are clean.
- **Done when:** each settings screen is its own lib and every app still builds and routes to it.
- **Notes:** do this before TK-44 (H0.3), which mounts the shared pages in several apps.
- **Outcome (2026-09-24):**
  - **Layout** (the owner's answer, 2026-09-24): `libs/settings/{users,roles,organizations,organization-settings,currencies,configuration,smtp,api-clients,print-templates}`. Each has a `project.json` (`settings-<screen>`, tags `scope:settings`, `type:ui`), a `src/index.ts` and an alias `@bill-book/settings-<screen>` in `tsconfig.base.json`. It is recorded in `docs/Modules.md`, Shared master pages: rule 1 and the table.
  - The files were moved with `git mv`, so history follows them. None of the nine folders imported another, so no import changed except the eight lazy routes in `apps/web/src/app/app.routes.ts`.
  - **Contacts, contact person roles and HSN/SAC stay in `master-ui`**, as agreed. They are reference data for RetailErp and School, not settings for every app.
  - `api-clients` had no route and no export before this change, and still has no route. It is now exported from its own lib, ready for TK-29's page.
  - Checks: `npm run typecheck` clean; `nx run-many -t lint` 32 projects clean (an `nx reset` was needed first, because the project graph cache hid the ninth lib); `nx run-many -t build` all 5 apps clean. There are no specs in the moved folders.

### TK-29 · API clients get per-action permissions through their role (D-07)
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#40](https://github.com/jothi-prabaharan/Bill-Book/issues/40)
- **Lanes:** L-MST, L-KERNEL · **Depends on:** — · **Decision:** D-07 (answered)
- **Where:** `backend/Api/Master/Master.Entity/TableEntities/ApiClient.cs` (`RoleId`, stored but unused), `Master.Api/Controllers/InternalApiKeysController.cs` (validation), `backend/shared/Shared.Kernel/Security/ApiKeyAuthenticationHandler.cs:43-47`.
- **State:** a validated API key produces `customer_id`, `org_id`, `sub`, `name` and `role = ApiClient` — **no `permission` claims**, so every `[RequireModulePermission]` endpoint refuses it.
- **Sub-tasks:**
  - [x] Validation returns the permission codes of the client's `RoleId`; the handler adds one `permission` claim per code.
  - [x] The API-clients page lets the owner pick the role, and a role holding `platform.*` can never be chosen.
  - [x] Test: a key whose role has `sales.view` can list invoices and gets 403 on posting one.
- **Done when:** an API client can do exactly what its role's `{module}.{action}` permissions allow.
- **Notes:**
- **Outcome (2026-09-24):**
  - `ApiKeyValidationResult.Permissions`: `InternalApiKeysController` fills it from `ApiClientRoles.PermissionsAsync`, which returns the role's codes less any in `platform.*`. It returns nothing for an inactive role, another customer's role, or role 0, which is what every key minted before this change holds.
  - `ApiKeyAuthenticationHandler.PermissionClaims` adds one `permission` claim per code, the claim type users' tokens use, so `[RequireModulePermission]` treats a key the same way it treats a user. It drops `platform.*` again, as a second line of defence.
  - `ApiClientsController`:
    - `GET` lists keys with their role names;
    - `GET roles` returns the assignable roles: active, system or own-customer, holding no `platform.*` permission;
    - `POST` needs a `roleId` it can assign;
    - `PUT {id:guid}/role` changes the role;
    - `DELETE {id:guid}` revokes the key (`IsActive = false`, and the row is kept).
    - Request and response models moved to `Master.Entity/Models/ApiClientModels.cs`, with annotations.
  - The page (`libs/settings/api-clients`, TK-28's lib) has a role picker on create, a role select on each row and **Revoke**. It is routed at `/settings/api-clients` (`settings.view`), and the menu row "API keys" under Users and access is migration `AddApiKeysMenu`, which only inserts rows.
  - Caveat: `HttpApiKeyValidator` caches a valid answer for five minutes, so a role change or a revocation reaches a service that has already seen the key within five minutes. This is documented; it was not changed.
  - Tests:
    - `Shared.Kernel.Tests.ApiKeyPermissionTests`: through the real handler and `RequireModulePermissionAttribute`, a `sales.view` key may GET invoices and is refused both `approve` (post) and `edit` (create); platform codes are dropped and duplicates removed; a key with no role is refused.
    - `Master.Api.Tests.ApiClientRoleTests`: role codes without platform; another customer's, inactive or zero role grants nothing; which roles are assignable.
    - `api-clients.list.spec.ts`.

### TK-30 · Seeds and menus follow the branch's trade (D-10)
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run · one question raised (D-23)
- **Issue:** [#41](https://github.com/jothi-prabaharan/Bill-Book/issues/41)
- **Lanes:** L-MST, L-INV, L-MASTER-UI · **Depends on:** — · **Decision:** D-10 (answered)
- **Where:** `backend/Api/Master/Master.Entity/TableEntities/Organization.cs:34` (`Vertical`), `Master.Entity/Enums/Vertical.cs`, `Master.Api/Services/TenantSeeder.cs` (`ReadVerticalAsync`), `backend/Api/Inventory/Inventory.Api/Controllers/InternalSeedController.cs:58`, `Master.Api/Services/MenuService.cs`, `docs/Modules.md` §5.14.
- **State:** the trade exists and seeding already receives it. `OrganizationModels.cs:81,208` carries it as a **string** (hard rule 7 wants the enum). Menus ignore it.
- **Sub-tasks:**
  - [x] Change the request and response models to the `Vertical` enum.
  - [x] List which seeds and menus belong to Pharma and Jewellery only (drug schedules, metal purities, making charges…), write the list in §5.14, then filter seeding and `MenuService` by it.
  - [x] Changing a branch's trade later seeds what the new trade needs (idempotently) and hides the other's menus; it never deletes data.
  - [x] Test: a ~~General~~ **Pharma** branch gets no metal purities; switching it to Jewellery seeds them once (see D-23).
- **Done when:** a new branch shows only its trade's menus and master data.
- **Notes:**
- **Outcome (2026-09-24):**
  - **Models:** `OrganizationListItem.Vertical` and `SaveOrganizationRequest.Vertical` are the `Vertical` enum, written by name (`JsonStringEnumConverter<Vertical>`), so the frontend is unchanged. The request carries `[EnumDataType]`, and `OrganizationService` no longer parses a string. The wire contracts read by other services stay strings, with the reason written in `docs/Modules.md`: `SeedOrganizationRequest`, the org context and the JWT claim.
  - **The list** is a new section of `docs/Modules.md`, "A branch's trade" (the old §5.14 no longer exists in that file). Today it has metal purities as a seed (Inventory) and as the `mtp` menu. Nothing is Pharma-only yet.
  - **Seeding** already followed the trade (`MetalPurityService`: none for Pharma) and a trade change already re-seeded idempotently (`OrganizationService.SaveAsync`), so no code change was needed there.
  - **Menus:** `TradeScope.MenuTrades` maps a menu code to its trades, and anything unlisted is every trade's. `MenuService` reads the branch's trade (General when unknown) and drops the other trade's items. There is no new column: a `Verticals` column on `mst.Menus` would rewrite every seeded row, which is the UpdateData cascade TK-70 had to squash away.
  - **Hides, never deletes:** switching back to Pharma only hides the menu, and the purities stay.
  - **D-23 raised:** the card's test asked that a General branch get no purities, but the enum's recorded decision (5.14) is that General gets everything. The recorded decision was kept, and the test uses a Pharma branch instead.
  - Tests: `Inventory.Api.Tests.TradeSeedTests` (Pharma none, then Jewellery once, then back to Pharma deletes nothing; General seeded) and `Master.Api.Tests.TradeScopeTests` (menu scope by trade, unlisted codes shown, every scoped code exists in `MenuSeed`, the trade read and written by name, an undefined value refused).

### E · Approved designs (documents only)

All six have the owner's go-ahead (D-17, 2026-09-24). The card is done when the
design section exists under `docs/` and new cards for it are added to this queue.

All of these share `L-DOC`, so they run one at a time, alongside code work in other lanes. **E-invoicing comes first** because GST law requires it above the turnover threshold; the portal design comes next because the School parent portal (TK-69) builds on it.

### TK-31 · Design: e-invoicing and e-way bill
- [x] completed (Claude Opus 5.5) — 2026-09-24 · design only, no code
- **Issue:** [#42](https://github.com/jothi-prabaharan/Bill-Book/issues/42)
- **Notes:** delivery challans already carry `EwayBillNo` and `EwayBillDate`.
- **Outcome (2026-09-24):**
  - The design is `docs/Modules.md`, "Approved designs" → "E-invoicing and e-way bill": what the law asks (thresholds as settings, not literals), nine decisions, `sal.EInvoices` and `sal.EwayBills`, the post-then-register flow, local validation, security.
  - Key calls: register **after** the posting commit, never inside it; a document needing an IRN prints stamped `IRN PENDING` until it has one; void cancels the IRN inside 24 hours and is refused after; duplicate-IRN answers are recovered, not failed.
  - Found while designing: `SalesRegister.UqcCode` is always written null although `inv.UnitsOfMeasure.UqcCode` exists (TK-91 fixes it).
  - Cards: TK-91 (tables, settings, gateway), TK-92 (IRN lifecycle, retry, print), TK-93 (e-way bill).
  - Raised **D-24**: which IRP access to use (a GSP, or NIC direct).

### TK-32 · Design: `apps/portal`, the next screens
- [x] completed (Claude Opus 5.5) — 2026-09-24 · design only, no code
- **Issue:** [#43](https://github.com/jothi-prabaharan/Bill-Book/issues/43)
- **State:** `apps/portal` has a dashboard and a statement list over real endpoints.
- **Notes:**
  - D-16 answered (2026-09-24): design these screens: overall outstanding and overall trade value on the dashboard; invoice list with PDF download (TK-22); online payment; quotes to accept or reject; support tickets (Customer module). Every portal route takes `[RequirePortalAccess]`.
- **Outcome (2026-09-24):**
  - The design is `docs/Modules.md`, "Approved designs" → "Client portal — the next screens": seven decisions, `con.PortalGrants`, the screens and their owning endpoints, `acc.OnlinePayments`, quote responses, internal ticket notes.
  - **Found while designing:** portal access is a 30-day JWT (`JwtTokenService.CreatePortalToken`) that nothing records and nothing can revoke. The design makes access revocable (a hashed link code exchanged for one-hour sessions) before any money action is added, and that is the first card.
  - Also found: the statement prints `{code}-{id}` although the ledger has `DocumentNo`; ticket messages have no internal flag, so staff notes would leak to the portal.
  - Cards: TK-94 (access), TK-95 (dashboard, invoices, PDF), TK-96 (quotes), TK-97 (tickets), TK-98 (pay online).
  - Raised **D-25**: which payment gateway.

### TK-33 · Design: workflow approvals
- [x] completed (Claude Opus 5.5) — 2026-09-24 · design only, no code
- **Issue:** [#44](https://github.com/jothi-prabaharan/Bill-Book/issues/44)
- **Notes:** TK-49 builds an approval engine for HRMS. Design this on top of it rather than as a second engine.
- **Outcome (2026-09-24):**
  - The design is `docs/Modules.md`, "Approved designs" → "Workflow approvals for RetailErp documents".
  - **The engine is shared, not duplicated.** The HRMS engine's state machine and step shape stay in `Shared.Kernel.Approvals`. Its configuration and resolution move from `Employee` to Master (new tenant schema `apr`), because RetailErp is sold without HRMS and has users and roles but no employees. `Employee` becomes a resolver for the employee-based approver kinds. That amends TK-49's plan, so it is raised as **D-26**, and TK-99 is blocked on it.
  - A chain governs the existing `Draft → ReadyToPost` "approve" transition; with no matching workflow, nothing changes. Editing mid-chain returns the document to Draft.
  - Nine document kinds, including two overrides (discount limit and credit limit), which turn today's outright refusals into approvable requests.
  - Cards: TK-99 (engine in Master; blocked on D-26), TK-100 (purchase), TK-101 (accounting), TK-102 (sales and overrides), TK-103 (inbox and settings). A note is added to TK-49.

### TK-34 · Design: project accounting
- [x] completed (Claude Opus 5.5) — 2026-09-24 · design only, no code
- **Issue:** [#45](https://github.com/jothi-prabaharan/Bill-Book/issues/45)
- **Outcome (2026-09-24):**
  - The design is `docs/Modules.md`, "Approved designs" → "Project accounting".
  - A project is a **ledger dimension** (`ProjectId` on `acc.JournalLedger` and journal and document lines), owned by Accounting, so profit by project is a query over the books rather than a second ledger.
  - Three billing methods (fixed fee by milestone, time and materials, non-billable). Time and re-billable expenses are claimed onto an invoice by a guarded update at post and released on void, so the same hours cannot be billed twice.
  - Timesheets are for billing and job cost, logged by users, and do not replace HRMS attendance.
  - Cards: TK-104 (masters and dimension), TK-105 (document lines), TK-106 (timesheets and billing), TK-107 (reports). No decision needed.

### TK-35 · Design: budgeting
- [x] completed (Claude Opus 5.5) — 2026-09-24 · design only, no code
- **Issue:** [#46](https://github.com/jothi-prabaharan/Bill-Book/issues/46)
- **Outcome (2026-09-24):**
  - The design is `docs/Modules.md`, "Approved designs" → "Budgeting".
  - A budget is monthly amounts per account for one financial year of one branch, optionally by project (TK-34), entered positive in the account's normal direction. Several budgets per year are allowed; one approved default is used by reports; an approved budget is locked, and changing it is a revision with a reason.
  - Budgets **warn and do not block** in the first build. Blocking becomes an approvable override once workflow approvals (TK-33) exist.
  - Cards: TK-108 (tables, grid, import, approval), TK-109 (report and warnings). No decision needed.

### TK-36 · Design: custom fields and custom reports
- [x] completed (Claude Opus 5.5) — 2026-09-24 · design only, no code
- **Issue:** [#47](https://github.com/jothi-prabaharan/Bill-Book/issues/47)
- **Outcome (2026-09-24):**
  - The design is `docs/Modules.md`, "Approved designs" → "Custom fields and custom reports".
  - **Custom fields**: values in a `jsonb` column on each entity (mapped as `JsonDocument`, so LINQ still queries them). Definitions are central in Master (new tenant schema `cfd`), and each owning service validates against them through a cached client in `Shared.Kernel`. Keys are permanent; removing a field keeps its values. Fields carry forward through document conversions and print as `{{custom.<key>}}`.
  - **Custom reports**: saved views over six wide **datasets** that include custom fields, running on the existing engine and inheriting the dataset's permission. There is deliberately no user SQL. Scheduled email delivery goes through Notification.
  - Cards: TK-110 (definitions and validator), TK-111 (contacts and items), TK-112 (documents, carry-forward, print), TK-113 (datasets and builder), TK-114 (scheduled email). No decision needed.

### TK-37 · Design: compliance bundle
- [x] completed (Claude Opus 5.5) — 2026-09-24 · design only, no code
- **Issue:** [#48](https://github.com/jothi-prabaharan/Bill-Book/issues/48)

---
- **Outcome (2026-09-24):**
  - The design is `docs/Modules.md`, "Approved designs" → "Compliance bundle". It defines the bundle as five parts: complete GST returns (a new purchase register, GSTR-3B with ITC, a GSTR-1 JSON export, GSTR-2B reconciliation), TDS, an append-only audit trail, MSME 45-day payments, and a compliance calendar.
  - **Found while designing:** GSTR-2 answers 501 and GSTR-3B has no input tax credit, because there is no purchase register; TDS is only a free-text section on the contact and nothing deducts it; there is no edit log, only last-modified columns.
  - Key calls: the edit log is written in the same `SaveChanges` and made append-only by a database trigger, and every bulk `ExecuteUpdate` on an audited table is made to log too; tax rates, thresholds and due dates are seeded, effective-dated, editable tables; returns are prepared and exported, not filed.
  - Cards: TK-115 (purchase register, 3B, GSTR-1 export), TK-116 (2B reconciliation), TK-117 (TDS), TK-118 (audit trail), TK-119 (MSME and calendar). No decision needed.

### TK-38 · Design: CRM campaigns and marketing automation (D-06)
- [x] completed (Claude Opus 5.5) — 2026-09-24 · design only, no code
- **Issue:** [#49](https://github.com/jothi-prabaharan/Bill-Book/issues/49)
- **Sub-tasks:**
  - [x] Write the design under the Customer (`cus`) section of `docs/Modules.md`: campaigns, audiences built from leads and contacts, scheduled sends through Notification (TK-19), unsubscribe handling, and what a campaign reports.
  - [x] Add build cards for it to this queue.
- **Done when:** the design is in `docs/Modules.md` and its cards are queued.
- **Outcome (2026-09-24):**
  - The design is in `docs/Modules.md` under a new **"Customer service (`cus`) — CRM and support"** section beside the Contacts section. The Contacts section's note pointed to a `cus` section that did not exist, so it now does. A pointer is left at the end of the file with the other designs.
  - Email first; sends go through Notification with a message id per campaign and recipient, so a retried send sends nothing twice; consent is per address per branch, and an unsubscribed address is never sent to whatever the audience says; one-click unsubscribe; audiences are saved rules frozen at send; purchase-history rules are answered by Reporting, not Customer; automation is waits and sends with exit conditions.
  - Cards: TK-120 (templates, audiences, consent), TK-121 (sending and tracking), TK-122 (automation), TK-123 (reports).
  - Raised **D-27**: how bulk email is sent, since a branch's own mailbox would be rate-limited and blacklisted.

### E2 · Builds from the approved designs

The build cards each design in section E produced. Each design section in `docs/Modules.md`
("Approved designs") is the specification; a card here names the part it builds.

### TK-91 · E-invoice: tables, branch settings and the IRP gateway
- [x] completed (Claude Opus 5.5) — 2026-09-25 · tests written, not run · the real provider waits on D-24
- **Issue:** [#79](https://github.com/jothi-prabaharan/Bill-Book/issues/79)
- **Lanes:** L-SAL, L-MST, L-KERNEL · **Depends on:** TK-31 · **Decision:** D-24
- **Where:** `docs/Modules.md`, "E-invoicing and e-way bill" (Tables, Decisions 3, 7, 8); `Sales.Repository/SalesDbContext.cs`; `Master.Entity/TableEntities/Organization.cs`; `Shared.Kernel/Secrets`.
- **Tables:** `sal.EInvoices`, `sal.EwayBills`; `mst.Organizations.EInvoiceFrom`, `EwayBillEnabled`
- **Sub-tasks:**
  - [x] The two tables and their enums, with the TK-71 RLS block in the migration.
  - [x] `EInvoiceFrom` and `EwayBillEnabled` on the branch, edited in Settings › Organization, and carried on the org context Sales caches.
  - [x] `IEInvoiceGateway` (authenticate, generate IRN, get IRN by document, cancel IRN, generate/update/cancel e-way bill) with a **sandbox** implementation first, and the provider D-24 names second. Credentials through `ISecretStore`, keyed by GSTIN.
  - [x] The INV-01 mapper from an invoice or credit note, and the local validation list in the design.
  - [x] Carry the line's UQC onto the document line so `SalesRegister.UqcCode` stops being null.
  - [x] Test: the mapper against a recorded INV-01 sample; each validation refusal; RlsAudit for both tables.
  - Standard delivery sub-tasks (section 5).
- **Done when:** a B2B invoice maps to a schema-valid INV-01 document and a sandbox call returns an IRN.
- **As built:**
  - `sal.EInvoices` and `sal.EwayBills` as the design lists them, enums stored by name, in migration `AddEInvoicing` with the TK-71 block for both. The e-way bill's cancel reason has its own enum, `EwayBillCancelReason`, because the two portals number the same reasons differently. Unique: one e-invoice per `(OrgId, SourceType, SourceId)`, and the IRN and the e-way bill number where set.
  - `UqcCode` is a column on invoice, credit note and challan lines. `LineUqc.StampAsync` fills it at posting from Inventory's new `POST internal/items/uqc` (`IUqcLookup`), and the sales register copies it, so `SalesRegister.UqcCode` is no longer null. A line with no unit gets `OTH`. When Inventory can't be reached, the line stays empty rather than guessed, and an e-invoice for it is refused for the missing unit. Challans are stamped in TK-93.
  - Master: `mst.Organizations.EInvoiceFrom` and `EwayBillEnabled` (migration `OrganizationEInvoicing`), refused without a GSTIN (`EInvoiceNeedsGstin`). They are carried on `internal/orgs/{id}/context` with the branch's phone and email, and reach `BranchSettings` and `OrgIdentity`. The six-hour cache means a change reaches Sales within six hours. Edited in Settings › Organization › Statutory.
  - Master: `POST internal/contacts/addresses` (`IContactAddressBook`) gives the buyer's legal name, GSTIN, billing address fields, state code and registration type. A document keeps its address only as printable text, and the IRP needs the PIN and state separately.
  - `Sales.Api/Services/EInvoicing/`:
    - `Inv01Document` is the schema's own names, serialised with no naming policy and nulls left out.
    - `Inv01Mapper` is pure and sends the document's figures rather than recomputing them. A discount below the lines lands in `ValDtls.Discount`, and UTGST goes in the SGST column. An export is `URP`, state 96 and PIN 999999.
    - `EInvoiceApplicability` names the supply type: B2B, SEZWP or SEZWOP, EXPWP or EXPWOP, or none for B2C.
    - `EInvoiceValidator` runs the design's local checks, with a ₹1 tolerance on totals.
    - `EInvoiceDocumentBuilder` gathers the inputs and reports whether the document applies. A problem reaching Master comes back as a transient problem.
  - `Shared.Kernel.Tax.Gstin` checks the GSTIN's shape and its base-36 check character.
  - `IEInvoiceGateway`:
    - `SandboxEInvoiceGateway` issues the notified IRN hash, answers `2150` on a duplicate, supports get-by-document, cancels within 24 hours only, and gives e-way bills one day per 200 km.
    - `UnconfiguredEInvoiceGateway` refuses every call.
    - `EInvoicing:Gateway` chooses between them. Unset, it is the sandbox in Development and the refusing gateway everywhere else, and the sandbox is refused in Production. Credential names come from `EInvoiceCredentials` (`einvoice-{GSTIN}-username`, and so on).
  - **Not built here:**
    - a screen to enter the IRP credentials, which is needed with the real provider (D-24);
    - storing the document's place-of-supply state. Invoices store `PlaceOfSupplyStateId = 0` and keep only `IsInterState`, so the mapper derives the place of supply: the seller's state intra-state, otherwise the buyer's GSTIN state, then their address state.
  - Specs: `EInvoiceMapperTests` (the recorded INV-01 sample), `EInvoiceValidatorTests`, `SandboxEInvoiceGatewayTests`, `EInvoiceDocumentBuilderTests`, `LineUqcTests`, `Shared.Kernel.Tests.GstinTests`, `Master.Api.Tests.BranchEInvoicingTests` and `InternalContactAddressesTests`. RLS for both tables is covered by the suite's `RlsAudit`.
  - Checks: the backend builds with `-warnaserror`. `has-pending-model-changes` is clean for Sales and both Master contexts. Frontend typecheck and lint pass, and the web build is clean.

### TK-92 · E-invoice: IRN on post, cancel on void, retry, QR on print
- [x] completed (Claude Opus 5.5) — 2026-09-25 · tests written, not run
- **Issue:** [#80](https://github.com/jothi-prabaharan/Bill-Book/issues/80)
- **Lanes:** L-SAL, L-PRT, L-SAL-UI · **Depends on:** TK-91 · **Decision:** —
- **Where:** `InvoiceService.PostAsync` / `VoidAsync`, `CreditNoteService`, `InvoicePrintService`, `Shared.Kernel.Printing.PlaceholderCatalog`.
- **Sub-tasks:**
  - [x] Insert the `Pending` row inside the posting transaction; register once after the commit; answer 200 with the e-invoice state.
  - [x] A hosted retry worker in Sales with backoff; permanent IRP errors stay `Failed` and are written to `sal.ErrorLogs` with `FollowUpStatus = Open`.
  - [x] Duplicate-IRN recovery through "get IRN by document details" (design, decision 6).
  - [x] Void cancels the IRN inside 24 hours and is refused after, naming the credit note as the fix.
  - [x] Print: `Irn`, `AckNo`, `AckDate`, `QrImage` tags; `IRN PENDING` stamp until registered.
  - [x] The invoice list gains an e-invoice status column and a "needs attention" filter.
  - [x] Test: post registers once; a lost answer followed by a duplicate stores the existing IRN; void at 23 h cancels and at 25 h is refused; a pending invoice prints stamped.
- **Done when:** posting a B2B invoice on an e-invoicing branch prints it with its IRN and QR, and voiding it the same day cancels the IRN.
- **As built:**
  - **After commit.** `Shared.Kernel.Persistence.IAfterCommit` (`AfterCommitQueue`, registered by `AddBillBookReliability`): `TransactionFilter` runs the queued work after it commits and before the response is written, and drops it on rollback. `EInvoicePosting.OnPostedAsync` writes the Pending row at the end of `InvoiceService.PostAsync` and `CreditNoteService.PostAsync`, and queues one `EInvoiceRegistrar.RegisterAsync`. That call fills in the `EInvoiceStateView` the posting returns, so the 200 carries the IRP's answer. Work queued where no request transaction runs never runs from the queue; the retry worker covers it.
  - **Which documents get a row.** POS sales never do. The branch must e-invoice from the document's date, and the buyer must have a GSTIN on the document or, looked up in Master, be SEZ or overseas. When the lookup fails the row is written provisionally, and the registrar deletes it if the builder then says the document doesn't apply.
  - **`EInvoiceRegistrar`:**
    - A guarded `ExecuteUpdate` claim holds `NextAttemptAt` for two minutes, and its row count is the answer.
    - It builds and validates the document. A validation refusal is `Failed` with the problems as the message.
    - Duplicate IRN `2150` is recovered through get-by-document.
    - A transient problem is `Pending` with backoff: 1, 5 and 15 minutes, then 1, 3, 6 and 12 hours.
    - A permanent refusal is `Failed`.
    - Every `Failed` goes to `sal.ErrorLogs` through `IWorkerErrorAuditor` (`AddBillBookWorkerErrorAudit<SalesDbContext>`) with `FollowUpStatus = Open`, job reference `EInvoice:{id} {Source}:{id}`.
  - **`EInvoiceRetryWorker`** is a hosted service in Sales. Every 5 minutes it walks `ITenantEnumerator`'s branches, each in its own scope, taking up to 50 due Pending rows per branch. It leaves Failed rows alone.
  - **Void.** `BeforeVoidAsync` runs right after the lifecycle check, before the ledger is withdrawn, so a refusal changes nothing (decision 5).
    - Registered and within 24 hours: cancelled at the IRP with the void's reason as the remark and `VoidInvoiceRequest.CancelReason`, `Other` when not given.
    - Past 24 hours, or refused by the IRP: `InvoiceOutcome.EInvoiceRefused` / `CreditNoteOutcome.EInvoiceRefused`, answered with a 409 that names the credit note.
    - Pending or Failed: marked Cancelled.
    - If the IRP cancels and the ledger withdrawal then fails, the IRN stays cancelled on a posted invoice. That is the design's order, noted rather than solved.
  - **Endpoints:**
    - `GET …/invoices|credit-notes/{id}/e-invoice` (`sales.view`).
    - `POST …/{id}/e-invoice/retry` (`sales.einvoice`, via `[PermissionAction("einvoice")]`). `sales.einvoice` is `ExtraPermissions` id 10003, granted to Owner, Administrator and Sales with fixed ids `900,000,000 + 100,000 × role + id`. Extras are now left out of the sequential grants, so nothing was renumbered. Migration `SalesEInvoicePermission` is inserts only, with menu rows 442 and 443.
    - The invoice list takes `eInvoiceAttention` (Failed or Pending) and returns `EInvoiceStatus`.
  - **Print:**
    - `PlaceholderType.QrCode` and the `EInvoice.Irn`, `AckNo`, `AckDate` and `QrImage` tags, for INV and CRN.
    - Printing draws the QR itself from the signed text with **QRCoder 1.8.0** (MIT, managed PNG, new in `Directory.Packages.props`). The template sanitiser still refuses a template's own `data:` URIs.
    - `EInvoiceStrip` adds the IRN block to the header of a registered invoice whose template places no `EInvoice.` tag.
    - `InvoicePrintService` stamps `IRN PENDING` on a posted invoice whose e-invoice is Pending or Failed.
  - **UI:** the invoice screen's E-invoice panel shows the status, IRN and acknowledgement, the message, **Retry now**, and the 24-hour note. The invoice list gets an **E-invoice** column and an **E-invoice needs attention** checkbox. The credit note screen has no panel yet; its endpoints exist.
  - **Specs:**
    - `EInvoiceRegistrationTests`: post registers once; a lost answer followed by a duplicate stores the existing IRN; void at 23 h cancels and at 25 h is refused; a pending invoice prints stamped; validation fails and is audited; transient backoff; claim; manual retry.
    - `Printing.Api.Tests.EInvoicePrintTests`, `Shared.Kernel.Tests.AfterCommitQueueTests`, `Master.Api.Tests.SalesEInvoicePermissionTests`, `sales-core/e-invoice.spec.ts`.
  - Checks: the backend builds with `-warnaserror`. `has-pending-model-changes` is clean for Sales and both Master contexts. Frontend typecheck and lint pass, and the web build is clean.

### TK-93 · E-way bill: by IRN and standalone for challans
- [x] completed (Claude Opus 5.5) — 2026-09-25 · tests written, not run
- **Issue:** [#81](https://github.com/jothi-prabaharan/Bill-Book/issues/81)
- **Lanes:** L-SAL, L-SAL-UI · **Depends on:** TK-92 · **Decision:** —
- **Where:** `DeliveryChallanService`, `sal.DeliveryChallans.EwayBillNo`/`EwayBillDate`, the design's Flow steps 6 and 7.
- **Sub-tasks:**
  - [x] Ask for the e-way bill with the IRN when transport details are present and the value passes the branch limit.
  - [x] `POST api/sales/{invoices|delivery-challans}/{id}/eway-bill`, Part B update, cancel within 24 hours (`sales.einvoice`).
  - [x] The challan's typed number becomes a `Manual` e-way bill row; the form shows the table's state.
  - [x] Test: a challan under the limit asks for nothing; one over it generates; a cancel after 24 hours is refused.
- **Done when:** a delivery challan over the limit gets an e-way bill number from the sandbox and prints it.
- **As built:**
  - **With the IRN.** The invoice gains optional transport columns (`TransportMode`, `VehicleNo`, `TransporterId`, `TransporterName`, `TransportDistanceKm`; migration `InvoiceTransport`), because flow step 6 needs the invoice to carry them. `EInvoiceDocumentBuilder` adds `EwbDtls` when the branch has `EwayBillEnabled`, a vehicle or transporter is given, and `TotalAmount` is over `EInvoiceRules.EwayBillThreshold` (₹50,000, a constant rather than a branch setting). When the IRP returns the bill, the registrar writes a `ByIrn` `Generated` row.
  - **`EwayBillService`** (`Sales.Api/Services/EInvoicing/`) generates for a posted invoice or challan.
    - It is refused when the branch has e-way bills off, when the document is not posted, at or under the limit ("asks for nothing"), and while another bill is live.
    - Part B is checked by `EwayBillMapper.ValidateTransport`: a vehicle or a transporter, the vehicle's shape, and the distance.
    - It writes a Pending row and sends it after the commit (`IAfterCommit`), by IRN when the invoice has a registered IRN, otherwise standalone. The standalone request comes from the document's lines, stamping challan lines' UQC first. Sub-supply types: sale 1, job work 4, branch transfer 5, others 8.
    - Once generated, the challan's `EwayBillNo` and `EwayBillDate` take the number and date, and its archived PDF is written again (Replace) with the bill in its reference line.
  - **Part B update and cancel** run inline. Cancel works within 24 hours for a generated bill, and at any time for a Manual one, which the portal never had. Cancelling clears the challan's typed columns.
  - **Manual bills.** `EwayBillService.SyncManualAsync` runs from `DeliveryChallanService.SaveAsync`: a typed number becomes a `Manual` `Generated` row, a changed number corrects it, and clearing it removes it. A bill generated here is never touched.
  - **Endpoints**, on `api/sales/invoices` and `api/sales/delivery-challans`: `GET {id}/eway-bill` (view), and `POST {id}/eway-bill`, `…/part-b` and `…/cancel` (`sales.einvoice`). A refusal is 409, and a Part B that fails the checks is 422.
  - **Print.** The `EInvoice.EwbNo`, `EwbDate` and `EwbValidUntil` tags exist. The strip shows the e-way bill beside the IRN, or alone when there is no IRN. The invoice payload carries the live bill.
  - **UI.** `bb-eway-bill-panel`, one component on both the invoice and the challan screens: state, **Generate**, **Change vehicle** and **Cancel**. The invoice form gains vehicle, transporter id and name, and distance.
  - **Not built:** e-way bills for returns (credit notes), and a per-branch threshold.
  - **Specs:**
    - `EwayBillTests`: a challan under the limit asks for nothing; one over it generates from the sandbox and its PDF is written again; the branch switch; one live bill at a time; cancel at 23 h and at 25 h; Part B; manual rows; an invoice with an IRN goes by IRN; the print payload.
    - `EInvoiceRegistrationTests`: the bill with the IRN over the limit, and none under it.
    - `sales-core/eway-bill.spec.ts`.
  - Checks: the backend builds with `-warnaserror`. `has-pending-model-changes` is clean for Sales and Master. Frontend typecheck and lint pass, and the web build is clean.

### TK-94 · Portal: revocable access and one-hour sessions
- [x] completed (Claude Opus 5.5) — 2026-09-26 · tests written, not run
- **Issue:** [#83](https://github.com/jothi-prabaharan/Bill-Book/issues/83)
- **Lanes:** L-CON, L-MST, L-PTL, L-KERNEL · **Depends on:** TK-32 · **Decision:** —
- **Where:** `ContactService.GeneratePortalLinkAsync`, `JwtTokenService.CreatePortalToken`, `ContactsController` (`portal-link`), `apps/portal`; design "Client portal" → Access.
- **Tables:** `con.PortalGrants`
- **Sub-tasks:**
  - [x] `con.PortalGrants` with the RLS block; the link carries a random code whose SHA-256 is stored.
  - [x] `POST api/portal/session` exchanges the code for a one-hour portal JWT with `portal_grant`; rate-limited; exempted in Master's guard test with its reason.
  - [x] Revoke portal access on the contact screen; the 30-day token path is removed.
  - [x] `apps/portal` gains `/access/:code`, keeps the code, and re-exchanges it before the hour runs out.
  - [x] Test: a revoked or expired grant is refused; a code is never stored in clear; a portal token carries no permission claims.
  - Standard delivery sub-tasks (section 5).
- **Done when:** revoking a contact's access stops a fresh session from being issued, and existing sessions expire within an hour.
- **As built (2026-09-26):**
  - **Master:**
    - `con.PortalGrants` (migration `PortalGrants`, with the RLS block) stores `CodeHash` (SHA-256, unique), `App`, `ExpiresAt`, `RevokedAt`/`RevokedBy` and `LastUsedAt`.
    - `PortalAccessService` creates, reads the state of, revokes and exchanges grants. The code is `pg_{customer}_{org}_{secret}`, and the session controller sets the tenant from the code before opening the database. This departs from the design's "past the query filter" wording: no special RLS policy is needed, and the shard is known up front.
    - The lifetime comes from `mst.Configurations` `portal.linkDays`, default 90 (migration `PortalLinkDaysSetting`).
  - **Routes:**
    - `POST api/portal/session` is anonymous, `[NoTransaction]` (the service opens its own scope after the tenant is set), and rate-limited to 10 a minute per address. Every failure is the same 401.
    - `POST api/contacts/{id}/portal-link` now returns `{ code, expiresAt, url }` with `url = {Portal:BaseUrl}/access/{code}`.
    - New: `GET api/contacts/{id}/portal-access` and `POST api/contacts/{id}/portal-access/revoke`.
    - Gateway route `master-portal-session`.
  - **Token and guard:**
    - `CreatePortalToken` issues one hour and carries `portal_grant`. The session never outlives its grant.
    - `[RequirePortalAccess]` requires `portal_grant`, so old 30-day links are refused by every service.
    - `ContactService.GeneratePortalLinkAsync` and its `ITokenService` dependency are gone.
  - **Frontend:**
    - `apps/portal`: `/access/:code` exchanges the code. `PortalSession` keeps the code for the tab and renews five minutes before expiry. `portalSessionGuard` protects every page. `/portal?token=` and a withdrawn link go to `/expired`.
    - Contacts page: shows the link once, the access line (live links, last opened) and **Revoke portal access**.
  - **Tests:**
    - `Master.Api.Tests.PortalAccessTests`: code format; never stored in clear; exchange claims; revoke all; expiry; a session capped at the grant; a deactivated contact; tampered secret or branch; another branch's contact.
    - `PortalTokenTests`: one hour, with the grant claim.
    - `EndpointGuardTests` exemption with its reason.
    - `portal-session.spec.ts`: renewal timing.
  - **Owner step:**
    - Run the Master suite from dropped databases, and the portal specs.
    - Existing portal links stop working at deploy; send contacts new ones.
    - Set `Portal:BaseUrl` so the contact screen shows a full link.
### TK-95 · Portal: dashboard figures and invoices with PDF
- [x] completed (Claude Opus 5.5) — 2026-09-26 · tests written, not run
- **Issue:** [#84](https://github.com/jothi-prabaharan/Bill-Book/issues/84)
- **Lanes:** L-RPT, L-SAL, L-PTL · **Depends on:** TK-94, TK-22 · **Decision:** —
- **Where:** `PortalStatementsController`; `SalesDocumentArchive.OpenAsync` (TK-22); `apps/portal/src/app/portal-dashboard`.
- **Sub-tasks:**
  - [x] `GET api/portal/summary` (Reporting): outstanding net of advances, overdue, trade value this year and all time.
  - [x] `GET api/portal/invoices`, `/{id}`, `/{id}/pdf` (Sales), posted and voided only, filtered by the token's contact.
  - [x] Fix the statement: `DocumentNo` instead of `{code}-{id}`; both sides for a contact who is customer and vendor.
  - [x] Dashboard and invoice pages in `apps/portal`, at 360px.
  - [x] Test: another contact's invoice id is 404; a draft never appears; the summary matches the ledger for a seeded contact.
- **Done when:** a contact signs in by link and downloads their own invoice PDF, and cannot reach another contact's.
- **As built (2026-09-26):**
  - **Reporting:** `PortalAccountService` sits over an `IPortalAccountData` seam, so the arithmetic is tested over lists. `GET api/portal/summary`:
    - Outstanding is the contact's receivable sub-accounts (Asset type) net of advances.
    - Overdue is each past-due posted invoice's own control-leg balance, capped at the outstanding.
    - Trade value is posted invoices' `TotalAmountBase` less posted credit notes', for this financial year (branch start month, via `HttpFinancialYearProvider`) and in all.
    - The last five documents.
  - **Statement:** `api/portal/statements` now returns `{ receivable, payable? }`, each side with its own running balance (payable as credit less debit). Lines carry `DocumentNo`, with a `{code}-{id}` fallback for old rows.
  - **Read models:** `DocumentNo` on the ledger, `TotalAmountBase` on invoices, a new `CreditNoteRead`, and a snapshot-only migration `PortalReadModels`.
  - **Found and fixed:** all eight sales and purchase document read models read `Status` as an integer where the column stores the name, so any report reading one failed on the first row. They now convert.
  - **Sales:** `PortalInvoiceService` and `api/portal/invoices`, `/{id}` and `/{id}/pdf`. Only invoices with `PostedAt` set, posted or voided, and only the token's contact; anything else is 404. Status is Open, PartPaid, Paid, Overdue or Void, from `internal/ledger/settlements`, asked per type code (INV and POS). The PDF is the TK-22 archive copy.
  - **Master:** the portal session token now carries `customer_code`, because the archive is foldered by it.
  - **Gateway:** routes `sales-portal-invoices` and `reporting-portal-summary`.
  - **Frontend:** `apps/portal` has a dashboard (figures and recent documents), an invoice list, an invoice page with **Download PDF**, and a two-sided statement. All are one column, and tables become labelled cards under 600px. The old statement's download (a staff route) is gone.
  - **Tests:**
    - `Reporting.Api.Tests.PortalAccountServiceTests`: outstanding net of advances; other contacts ignored; overdue; overdue capped; trade value by year; recent documents; financial-year start; statement opening and running balance; document number fallback; payable side; customer-only contact.
    - `Sales.Api.Tests.PortalInvoiceTests`: status rules; the list excludes drafts and other contacts; another contact's or a draft invoice is 404 as invoice and as PDF; own invoice lines and PDF.
    - `PortalAccessTests`: the `customer_code` claim.
    - `retail/portal.models.spec.ts`.
  - **Owner step:** run the Reporting, Sales and Master suites and the portal specs. "The summary matches the ledger for a seeded contact" is covered over lists, not against a database, because Reporting's fixture has no `acc` or `sal` tables.
### TK-96 · Portal: accept or reject a quote
- [x] completed (Claude Opus 5.5) — 2026-09-26 · tests written, not run
- **Issue:** [#85](https://github.com/jothi-prabaharan/Bill-Book/issues/85)
- **Lanes:** L-SAL, L-SAL-UI, L-PTL · **Depends on:** TK-94 · **Decision:** —
- **Where:** `sal.Quotes`, `QuoteService`; design "Client portal" → Quotes.
- **Sub-tasks:**
  - [x] `CustomerResponse`, `RespondedAt`, `RespondedByName`, `ResponseNote` on `sal.Quotes` (migration).
  - [x] `GET api/portal/quotes`, `POST …/{id}/accept`, `…/{id}/reject`: posted, unexpired, answered once.
  - [x] The staff quote list shows the answer; the portal page lists quotes with the two actions.
  - [x] Test: an expired or already-answered quote is refused; another contact's quote is 404.
- **Done when:** a contact accepts a posted quote from the portal and staff see it accepted.
- **As built (2026-09-26):**
  - **`sal.Quotes`** (migration `QuoteCustomerResponse`) gains `CustomerResponse` (`QuoteResponse` enum, stored as its name, existing rows `None`), `RespondedAt`, `RespondedByName` and `ResponseNote`, with `chk_quotes_response_stamp`: an answer carries its time.
  - **`PortalQuoteService`:**
    - Lists the contact's posted quotes, with `CanRespond` meaning unanswered, still valid and not yet an order.
    - Answering is a guarded `ExecuteUpdate` on unanswered and valid, so two racing answers record one.
    - The outcomes are NotFound (another contact's quote or a draft), Lapsed, AlreadyAnswered, and Converted (a quote already made into a sales order, which the design did not name; refused so an answer never lands on a decided quote).
  - **Routes:** `api/portal/quotes` and `/{id}/accept`, `/{id}/reject` with `{ name, note }`, plus gateway route `sales-portal-quotes`.
  - **Staff:** `SalesTransactionListItem.CustomerResponse`, shown as a tag beside the quote's status on the sales list. The quote view carries the answer fields.
  - **Portal:** the **Quotes** page, with accept or decline, a name and a note, one column.
  - **Not built:** the design's "staff notification" on an answer. There is no in-app notification channel for staff yet; the answer shows on the list.
  - **Tests:** `Sales.Api.Tests.PortalQuoteTests` covers accept once, lapsed refused, another contact's quote and a draft not found, and converted refused. The portal spec covers `quoteState`.
  - **Owner step:** run the Sales suite and the portal specs.
### TK-97 · Portal: support tickets
- [x] completed (Claude Opus 5.5) — 2026-09-26 · tests written, not run
- **Issue:** [#86](https://github.com/jothi-prabaharan/Bill-Book/issues/86)
- **Lanes:** L-CUS, L-CUS-UI, L-PTL · **Depends on:** TK-94 · **Decision:** —
- **Where:** `TicketsController`, `cus.TicketMessages`; design "Client portal" → Tickets.
- **Sub-tasks:**
  - [x] `IsInternal` on `cus.TicketMessages`; the staff reply box can mark a note internal.
  - [x] `GET/POST api/portal/tickets`, `POST api/portal/tickets/{id}/messages`, never returning internal messages.
  - [x] Portal ticket list, detail and new-ticket pages.
  - [x] Test: an internal message never reaches the portal; another contact's ticket is 404; a portal ticket gets its SLA from `cus.SlaPolicies`.
- **Done when:** a contact raises a ticket in the portal, staff reply, and the contact sees the reply but not an internal note.
- **As built (2026-09-26):**
  - **`cus.TicketMessages.IsInternal`** (migration `TicketMessageInternal`), with `chk_ticketmessages_internal_by_staff`: only a `User` message can be internal.
  - **`PortalTicketService` and `api/portal/tickets`** (list, `/{id}`, raise, `/{id}/messages`):
    - Only the token's contact; another contact's ticket is 404.
    - Internal notes are filtered out.
    - A portal ticket is Medium priority with the branch's SLA.
    - Replying to a Resolved ticket reopens it; a Closed one is 409.
    - Gateway route `customer-portal-tickets`.
  - **Found and fixed on the staff side:**
    - The thread screen called `GET api/tickets/{id}/messages`, which did not exist. It now does, returning the whole thread with `isInternal` and the author type as its name.
    - `POST api/tickets/{id}/messages` took the author from the request body, so the screen's replies were stored as `Contact` (enum 0). A staff message is now always `User` and the signed-in user.
  - **Staff UI:** an **Internal note** checkbox on the reply box, and internal notes marked in the thread.
  - **Portal:** the **Support** list (with New ticket) and a ticket page with replies.
  - **Tests:** `Customer.Api.Tests.PortalTicketTests` covers internal notes never reaching the portal, the database refusing a contact's internal note, another contact's ticket, the SLA from policy, reopening a resolved ticket and a closed ticket refusing. The portal spec covers the status label.
  - **Owner step:** run the Customer suite and the portal specs.
### TK-98 · Portal: pay online
- [x] completed (Claude Opus 5.5) — 2026-09-26 · tests written, not run · real gateway waits on D-25
- **Issue:** [#87](https://github.com/jothi-prabaharan/Bill-Book/issues/87)
- **Lanes:** L-ACC, L-ACC-UI, L-PTL · **Depends on:** TK-95 · **Decision:** D-25
- **Where:** `ReceiveMoneyService`; design "Client portal" → Online payment.
- **Tables:** `acc.OnlinePayments`
- **Sub-tasks:**
  - [x] `acc.OnlinePayments` with RLS; the branch's settlement bank account setting.
  - [x] `IPaymentGateway` for the gateway D-25 names: create order, verify callback signature.
  - [x] `POST api/portal/payments`; `POST api/payments/{gateway}/callback` (anonymous, signature-verified) creates the `RCM` with its allocations once per `GatewayPaymentId`.
  - [x] Portal pay screen: choose invoices or an amount, go to checkout, show the result from the server, not the redirect.
  - [x] Test: a replayed callback creates one receipt; a bad signature creates none; the receipt settles the chosen invoices.
- **Done when:** a sandbox payment for an invoice leaves one receipt allocated to it, however many callbacks arrive.
- **As built (2026-09-26):**
  - **`acc.OnlinePayments`** (migration `OnlinePayments`, with the RLS block) holds:
    - `Reference` (`op_{customer}_{org}_{id}`, unique).
    - The contact, amount and currency.
    - `Allocations` (jsonb), and the bank account.
    - The gateway, `GatewayOrderId` and `GatewayPaymentId` (unique per gateway: the idempotency backstop).
    - Status (Created, Paid, Failed, Refunded), `ReceiveMoneyId`, `PaidAt` and `Note`, with check constraints: paid carries its time, and a receipt only follows a payment.
  - **Settlement account:** `acc.BankAccounts.IsOnlinePaymentAccount`, one per branch through a filtered unique index. It is set with `PUT api/bank-accounts/{id}/online-payments` and shown as a column on the bank accounts page. It is locked against deactivation like the default.
  - **`IPaymentGateway`** has sandbox and unconfigured versions, registered like the IRP gateway: `Payments:Gateway`, sandbox in Development and none elsewhere, and the sandbox is refused in Production. Sandbox callbacks are HMAC-SHA256 under `Payments:Sandbox:Secret`, compared in fixed time.
  - **`OnlinePaymentService.StartAsync`:**
    - Only the contact's own invoices (another contact's reads as not found), in base currency, and no more than is owed on the invoice's control leg.
    - Any excess is an advance.
    - It opens the gateway order.
  - **`CompleteAsync`:**
    - Only a verified callback, and the order and amount must match.
    - Inside one transaction: a guarded claim `Created → Paid`, then a Receive Money created, posted and allocated (source 3, with source 9 for the excess), then `ReceiveMoneyId` set.
    - If that is refused, the same again with the whole amount as an advance.
    - If that is refused too, Paid with a note and no receipt.
    - A replay finds nothing to claim.
  - **Routes:**
    - `api/portal/payments`: start, read, and a `/{id}/sandbox-checkout` that signs and sends the sandbox callback through the same path.
    - `api/payments/{gateway}/callback`: anonymous, `[NoTransaction]`, tenant from the reference, exempted in the guard test with its reason.
    - Gateway routes `accounting-portal-payments` and `accounting-payment-callbacks`.
  - **Portal:** **Pay online** (choose invoices and amounts plus extra), the sandbox checkout page, and a result page that polls the server.
  - **Tests:**
    - `Accounting.Api.Tests.OnlinePaymentTests`: a bad signature is not read; the reference; another contact's invoice or overpaying refused; not set up or no gateway; a replayed callback makes one receipt with lines on INV (source 3) plus the advance; a failed payment makes no receipt; an amount mismatch; a refused receipt leaves Paid with a note; another contact's payment.
    - The portal spec covers `paymentTotal`.
  - **Owner step:** run the Accounting suite and the portal specs. Set an online payments account per branch. A real gateway is added when D-25 is answered: implement `IPaymentGateway` for it and carry the reference in its order notes.
### TK-99 · Approvals: the shared engine in Master, with user and role approvers
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Lanes:** L-KERNEL, L-CON, L-MST · **Depends on:** TK-33 · **Decision:** D-26 (answered 2026-09-24: Master, `apr`)
- **Where:** design "Workflow approvals for RetailErp documents"; the HRMS design's Approvals section; `Shared.Kernel/Documents/DocumentLifecycle.cs`.
- **Tables:** `apr.ApprovalWorkflows`, `apr.ApprovalWorkflowLevels`, `apr.ApprovalDelegates`
- **Sub-tasks:**
  - [x] `Shared.Kernel.Approvals`: `ApprovalStepBase` (with `ApproverUserId` and `ApproverEmployeeId`), the step state machine and its refusal messages.
  - [x] `apr` configuration tables on Master's tenant context, with the RLS block.
  - [x] `POST internal/approval-chains/resolve`: `RoleHolder` and `NamedUser`; the skip rules; the snapshot.
  - [x] Test: skip rules (requester, repeat approver, optional); a required unresolvable level refuses with its name; amount-conditional levels; a changed workflow leaves resolved snapshots alone.
  - Standard delivery sub-tasks (section 5).
- **Done when:** a workflow "Accountant, then Owner above ₹1,00,000" resolves to one step for ₹50,000 and two for ₹2,00,000.
- **As built (2026-09-24):**
  - `Shared.Kernel.Approvals`: the enums, `ApprovalStepBase`, the pure `ApprovalChain` (start, approve, reject, send back, cancel, who may act) and the contracts for Master and Employee. Tests: `Shared.Kernel.Tests.ApprovalChainTests`.
  - Master: `apr.ApprovalWorkflows`, `apr.ApprovalWorkflowLevels`, `apr.ApprovalDelegates` on `ContactsDbContext`, migration `ApprovalWorkflows` with the RLS block. `ApprovalChainResolver` picks the most specific workflow in force, keeps the levels the amount calls for, asks Employee for employee approvers in one call and applies the skip rules. `internal/approval-chains/resolve` and `internal/approval-chains/delegate-check`; `api/approval-workflows` (settings). HRMS branches are seeded with a manager-approved Leave and LeaveEncashment workflow. Tests: `Master.Api.Tests.ApprovalChainResolverTests`, including this card's *Done when*.
  - Employee: `internal/approval-chains/resolve-employees` (reporting chain by depth, relationship, department head, named employee), with `hrm.RelationshipTypes` (seeded Lead and Project Lead) and `hrm.EmployeeRelationships`. Tests: `Employee.Api.Tests.EmployeeApproverTests`.
  - "A changed workflow leaves resolved snapshots alone" is a property of the owning service's stored steps, so it is tested with the first one, leave (TK-49).
  - Owner step: none beyond running the tests. The workflow and delegate screens are TK-103.

### TK-100 · Approvals: purchase orders, bills and debit notes
- [x] completed (Claude Opus 5.5) — 2026-09-26 · tests written, not run
- **Issue:** [#88](https://github.com/jothi-prabaharan/Bill-Book/issues/88)
- **Lanes:** L-PUR, L-PUR-UI · **Depends on:** TK-99 · **Decision:** —
- **Sub-tasks:**
  - [x] `pur.ApprovalSteps`; summary columns on the purchase documents.
  - [x] Submit, act, edit-sends-back, per the design's Flow; the ordinary approve action when no workflow matches.
  - [x] `GET api/approvals/mine` for Purchase; the chain shown on each document.
  - [x] Test: a PO over the threshold waits for both levels; editing it mid-chain returns it to Draft; a user who is not the approver is refused.
- **Done when:** a purchase order above the limit reaches `ReadyToPost` only after both levels approve.
- **As built (2026-09-26):**
  - **Shared.Kernel:**
    - `DocumentHeaderBase` gains `ApprovalStatus?` (stored as its name), `CurrentStepLabel`, `CurrentApproverUserId` and `CurrentApproverRoleId`. Every sales and purchase header carries them, so the Sales migration `DocumentApprovalSummary` ships here for TK-102 to use.
    - `IApprovalChainClient` / `HttpApprovalChainClient` call Master's `internal/approval-chains` (resolve, delegate-check).
    - `DocumentApproval` builds a round's steps, keeps the header summary, and gives the refusal while a document is under approval.
  - **Purchase:**
    - `pur.ApprovalSteps` (`PurchaseApprovalStep` plus `Round`; migration `PurchaseApprovals`, with the RLS block).
    - `PurchaseApprovalService` handles submit (NoWorkflow, Unresolvable or a new round), act (approver, role holder or delegate), the chain, the inbox, the gate and return-to-draft.
    - **The gate:** the ordinary approve, and confirming or posting a *draft*, are refused while a chain is open or rejected, or when a workflow applies. Master unreachable is a refusal.
    - **Editing** returns the document to Draft, cancels the round's open steps, and says so in the update response.
    - The three services take the approval service as an optional constructor parameter, so existing test harnesses are unchanged.
  - **Routes:** `api/purchase/{purchase-orders|bills|debit-notes}/{id}/submit` (`purchase.edit`), `…/approval` GET/POST (`purchase.view`), and `api/purchase/approvals/mine`. The inbox path is per service, not one `api/approvals/mine`, because the gateway routes by prefix and one path cannot reach three services; TK-103 asks each.
  - **UI:** a shared `bb-approval-panel` (ui-components) on the purchase order, bill and debit note forms.
  - **Tests:**
    - `Purchase.Api.Tests.PurchaseApprovalTests`:
      - Chain behaviour: above the limit, both levels before ReadyToPost; below it, one level; a non-approver refused; reject needs a comment.
      - Editing mid-chain returns the document to Draft and cancels open steps, and a new round starts on resubmit.
      - The gate: approve and confirm refused under a workflow; the ordinary approve with no workflow; Master unreachable refuses.
      - The inbox by user and role.
    - `approval-panel.model.spec.ts`.
  - **Not built:** notifying the approver (design flow step 1, TK-19) and escalation. Delegates can act, but their inbox does not list their principal's items.
  - **Owner step:** run the Purchase and Sales suites and the ui-components spec. Configure a workflow through `api/approval-workflows` until TK-103 gives it a screen.
### TK-101 · Approvals: spend money and manual journals
- [x] completed (Claude Opus 5.5) — 2026-09-26 · tests written, not run
- **Issue:** [#89](https://github.com/jothi-prabaharan/Bill-Book/issues/89)
- **Lanes:** L-ACC, L-ACC-UI · **Depends on:** TK-99 · **Decision:** —
- **Sub-tasks:**
  - [x] `acc.ApprovalSteps`; summary columns on `SpendMoney` and `Journal`.
  - [x] The same flow; `GET api/approvals/mine` for Accounting.
  - [x] Test: a journal is approved by two levels; the second approver cannot be the first.
- **Done when:** a manual journal configured for two levels posts only after both.
- **As built:**
  - **One flow for every service.** The document flow moved out of Purchase into `Shared.Kernel.Approvals.DocumentApprovalService<TStep>`: submit, act, chain, gate, return to draft and inbox. Purchase and Accounting each supply only how to find their documents and steps. `DocumentApprovalActionRequest` is the shared request body, and the amount hook is asynchronous because a journal's amount is the sum of its saved lines.
  - **`acc.ApprovalSteps`** (`AccountingApprovalStep`, with `Round`) has RLS. `Journal` and `SpendMoney` carry the four summary columns (migration `AccountingApprovals`).
  - **Neither document has ReadyToPost**, so approval changes only the summary: `Post` is gated (`SaveJournalOutcome.AwaitingApproval = 14`, `MoneyDocumentOutcome.AwaitingApproval = 16`, both 409). Only the public `JournalService.PostAsync` is gated; `PostSystemAsync` (the fixed asset register's journals) is not. An edit mid-chain returns the draft and the PUT answers 200 with a message.
  - **Routes:**
    - `api/journals/{id}/submit|approval` under `accounting`, and `api/spend-money/{id}/submit|approval` under `banking`.
    - Each has an inbox at `…/approvals/mine`, filtered to its own kind so a banking-only user never sees journals. These sit under the gateway's existing prefixes; TK-103 merges them.
  - **Nobody approves two levels of one round** (`ApprovalRefusal.ApprovedEarlierLevel`). This is enforced for every document service, Purchase included. HRMS requests are untouched and keep relying on Master's skip rules.
  - **UI:** `bb-approval-panel` sits under the journal and spend-money draft forms.
  - **Tests:** `AccountingApprovalTests` and `ApprovalChainTests`.

### TK-102 · Approvals: credit notes and the two overrides
- [ ] open
- **Lanes:** L-SAL, L-SAL-UI, L-INV · **Depends on:** TK-99 · **Decision:** —
- **Sub-tasks:**
  - [ ] `sal.ApprovalSteps` and `inv.ApprovalSteps`; summary columns on sales documents and stock adjustments.
  - [ ] `SalesDiscountOverride` and `CreditLimitOverride`: a save that the discount limit or the credit check would refuse offers "request approval" instead, and an approved override lets that one document through.
  - [ ] Test: a sale past the credit limit is refused without an approved override and accepted with one; the override does not carry to another document.
- **Done when:** a credit-limit breach can be approved by the Owner and the invoice then posts.

### TK-103 · Approvals: the inbox and Settings › Approval workflows
- [ ] open
- **Lanes:** L-UI, L-WEB, L-DEPS · **Depends on:** TK-100 · **Decision:** —
- **Sub-tasks:**
  - [ ] `libs/settings/approval-workflows`: workflows per kind, levels with drag reorder.
  - [ ] The Approvals inbox, merging every service's `GET api/approvals/mine`, with inline actions, at 360px.
  - [ ] Menu rows and routes; docs page and release note.
- **Done when:** an approver sees a waiting purchase order in the inbox and approves it there.

### TK-104 · Projects: masters and the `ProjectId` ledger dimension
- [ ] open
- **Lanes:** L-ACC, L-ACC-UI, L-MST · **Depends on:** TK-34 · **Decision:** —
- **Where:** design "Project accounting"; `acc.JournalLedger`, `acc.JournalDetail`, `LedgerPostingService`, `PostLedgerRequest`.
- **Tables:** `acc.Projects`, `acc.ProjectTasks`, `acc.ProjectMembers`, `acc.ProjectMilestones`
- **Sub-tasks:**
  - [ ] The four tables with RLS; the `PRJ` numbering series; the `projects` permission module (Master seed).
  - [ ] `ProjectId` on ledger, journal lines, spend- and receive-money lines; `PostLedgerRequest` legs carry it; the posting API refuses another branch's or a completed project.
  - [ ] `internal/projects/exists` for the other services; project list and form pages.
  - [ ] Test: a leg with a completed project is refused; the manual journal carries a project to the ledger; RlsAudit.
  - Standard delivery sub-tasks (section 5).
- **Done when:** a manual journal line tagged with a project appears on that project's ledger rows.

### TK-105 · Projects: sales and purchase lines carry the project to the ledger
- [ ] open
- **Lanes:** L-KERNEL, L-SAL, L-PUR, L-SAL-UI, L-PUR-UI · **Depends on:** TK-104 · **Decision:** —
- **Where:** `DocumentLineBase`; every `sal`/`pur` poster that builds `PostLedgerRequest` legs.
- **Sub-tasks:**
  - [ ] `ProjectId` on `DocumentLineBase` (migrations in `sal` and `pur`), validated through Accounting.
  - [ ] Line legs carry the line's project; header legs carry it only when every line agrees (design, decision 4).
  - [ ] A project picker in the shared line grid.
  - [ ] Test: an invoice with two projects posts revenue to each and an untagged receivable; one with a single project tags the receivable too.
- **Done when:** an invoice's revenue lands on the project its lines name.

### TK-106 · Projects: timesheets and billing time, expenses and milestones
- [ ] open
- **Lanes:** L-ACC, L-ACC-UI, L-SAL, L-SAL-UI, L-PUR · **Depends on:** TK-105 · **Decision:** —
- **Tables:** `acc.TimeEntries`; `IsBillable`, `MarkupPercent`, `BilledInvoiceId` on bill and spend-money lines
- **Sub-tasks:**
  - [ ] Weekly timesheet and timer; a user logs only their own time without `projects.edit`; 24 hours a day at most.
  - [ ] "Add project items" on the invoice form; `internal/projects/billing/claim` at post and `…/release` on void, guarded by row count.
  - [ ] Test: two invoices claiming the same hours — exactly one posts; a void releases the hours; markup applies to re-billed expenses.
- **Done when:** unbilled hours billed on one invoice cannot be billed on another, and come back when it is voided.

### TK-107 · Projects: reports
- [ ] open
- **Lanes:** L-RPT · **Depends on:** TK-106 · **Decision:** —
- **Sub-tasks:**
  - [ ] Project profitability, budget against actual, unbilled work, time by user, as report sources on `bb-report-grid`.
  - [ ] Test: profitability for a seeded project equals its ledger rows' net by type.
- **Done when:** the four reports run for a branch with a billed project.

### TK-108 · Budgets: tables, entry grid, import and approval
- [ ] open
- **Lanes:** L-ACC, L-ACC-UI · **Depends on:** TK-35 · **Decision:** —
- **Where:** design "Budgeting"; `mst.Organizations.FinancialYearStartMonth` via the org context; `acc.Accounts`.
- **Tables:** `acc.Budgets`, `acc.BudgetLines`
- **Sub-tasks:**
  - [ ] Both tables with RLS; a filtered unique index for one approved default per branch and year.
  - [ ] Create blank, from last year's actuals (± % per account group) or from another budget; spread a year total evenly or by last year's shape.
  - [ ] CSV/XLSX import and export in the account-code × month layout; unknown codes listed and refused.
  - [ ] Approve locks; revise copies with a reason and supersedes on approval.
  - [ ] The budget grid page (accounts × 12 months), usable at 360px as one month at a time.
  - [ ] Test: an approved budget refuses edits; a revision supersedes; last year's actuals seed the right months for an April-start branch.
  - Standard delivery sub-tasks (section 5).
- **Done when:** an accountant imports a year's budget from a spreadsheet, approves it, and cannot edit it afterwards.

### TK-109 · Budgets: budget against actual, and over-budget warnings
- [ ] open
- **Lanes:** L-RPT, L-ACC, L-ACC-UI · **Depends on:** TK-108 · **Decision:** —
- **Where:** `Reporting.Api/Services/Sources`, the P&L source's sign rules.
- **Sub-tasks:**
  - [ ] Report sources: budget against actual (month, quarter, YTD), by project, monthly trend; favourable or adverse by account type.
  - [ ] A warning on bill, spend-money and journal saves that pass the approved budget (month or YTD, a branch setting).
  - [ ] Test: actuals equal the P&L's for the same period; an expense over budget warns and still saves.
- **Done when:** the budget-against-actual report's actual column matches the P&L for the same period.

### TK-110 · Custom fields: definitions and the shared validator
- [ ] open
- **Lanes:** L-CON, L-KERNEL, L-UI, L-DEPS · **Depends on:** TK-36 · **Decision:** —
- **Where:** design "Custom fields and custom reports" → Custom fields.
- **Tables:** `cfd.CustomFieldDefinitions`
- **Sub-tasks:**
  - [ ] The definitions table on Master's tenant context, with RLS; `api/custom-fields` and `internal/custom-fields`.
  - [ ] `Shared.Kernel.CustomFields`: the cached `ICustomFieldDefinitions` client and the pure `CustomFieldValidator`.
  - [ ] Settings › Custom fields (`libs/settings/custom-fields`) and the shared `bb-custom-fields` form component.
  - [ ] Test: the validator per data type; an unknown key refused; a required field enforced on new saves only; a key cannot be changed; 50-per-kind limit.
  - Standard delivery sub-tasks (section 5).
- **Done when:** a branch defines a dropdown field for contacts and the validator refuses a value not in its options.

### TK-111 · Custom fields on contacts and items
- [ ] open
- **Lanes:** L-CON, L-INV, L-MASTER-UI, L-INVENTORY-UI · **Depends on:** TK-110 · **Decision:** —
- **Sub-tasks:**
  - [ ] `CustomFields jsonb` (mapped as `JsonDocument`) on `con.Contacts` and `inv.Items`, with a GIN index; validated on save.
  - [ ] Rendered on the forms, `ShowInList` columns and `IsSearchable` search on the lists.
  - [ ] Test: a search by a custom value finds the contact; a deactivated field keeps its stored value.
- **Done when:** a contact saved with a custom field can be found by searching that value.

### TK-112 · Custom fields on sales and purchase documents, carried forward and printed
- [ ] open
- **Lanes:** L-SAL, L-PUR, L-KERNEL, L-SAL-UI, L-PUR-UI · **Depends on:** TK-110 · **Decision:** —
- **Sub-tasks:**
  - [ ] `CustomFields jsonb` on the header of every `sal` and `pur` document (via `DocumentHeaderBase`); validated on save.
  - [ ] Carry-forward on every conversion (quote → order → challan → invoice; PO → receipt → bill) for keys marked `CarryForward`.
  - [ ] `custom.` placeholders in `PlaceholderCatalog`; values in the print payload.
  - [ ] Test: a quote's carried field arrives on the invoice made from it; an invoice prints `{{custom.site_ref}}`.
- **Done when:** a custom field typed on a quote prints on the invoice converted from it.

### TK-113 · Custom reports: datasets and the report builder
- [ ] open
- **Lanes:** L-RPT, L-REPORTING-UI · **Depends on:** TK-111, TK-112 · **Decision:** —
- **Tables:** `rpt.CustomReports`
- **Sub-tasks:**
  - [ ] Six dataset sources (`IsDataset`), with custom-field columns built from the branch's definitions at run time.
  - [ ] `rpt.CustomReports` with RLS; create from a dataset, save private or shared (`reports.edit`), list under **Custom** by the dataset's permission.
  - [ ] The builder page: pick a dataset, choose columns, filters, grouping and pivot on `bb-report-grid`, save.
  - [ ] Test: a user without `purchase.view` does not see a purchase-lines custom report; a deactivated field's column is dropped with a notice.
- **Done when:** a user builds "sales by salesperson and site reference" from the sales-lines dataset, saves it, and reopens it from the catalog.

### TK-114 · Scheduled report email
- [ ] open
- **Lanes:** L-RPT, L-NTF · **Depends on:** TK-113, TK-19 · **Decision:** —
- **Tables:** `rpt.ReportSchedules`
- **Sub-tasks:**
  - [ ] Schedules (daily, weekly, monthly) with recipients among the branch's users; a hosted service claims due rows with a guarded update.
  - [ ] Each run re-checks the owner's permission and sends the Excel export through Notification.
  - [ ] Test: a due schedule runs once under concurrency; a schedule whose owner lost the permission sends nothing and records why.
- **Done when:** a weekly schedule emails its report once per week.

### TK-115 · Compliance: the purchase register, complete GSTR-3B and the GSTR-1 export
- [ ] open
- **Lanes:** L-PUR, L-RPT, L-REPORTING-UI · **Depends on:** TK-37 · **Decision:** —
- **Where:** design "Compliance bundle" → part 1; `sal.SalesRegister` and its writers; `GstController` (GSTR-2 501, GSTR-3B outward only).
- **Tables:** `pur.PurchaseRegister`
- **Sub-tasks:**
  - [ ] `pur.PurchaseRegister` with RLS, written by the bill and debit note posters in the posting transaction, with ITC eligibility.
  - [ ] GSTR-3B completed with eligible ITC, reverse charge and ineligible ITC.
  - [ ] GSTR-1 offline-tool JSON export with pre-download validation.
  - [ ] Test: a posted bill writes its register rows; 3B's ITC equals the register's eligible tax; the export's HSN summary equals its sections.
  - Standard delivery sub-tasks (section 5).
- **Done when:** GSTR-3B for a month with sales and purchases shows both output tax and eligible ITC.

### TK-116 · Compliance: GSTR-2B reconciliation
- [ ] open
- **Lanes:** L-PUR, L-PURCHASE-UI · **Depends on:** TK-115 · **Decision:** —
- **Tables:** `pur.Gstr2bImports`, `pur.Gstr2bLines`
- **Sub-tasks:**
  - [ ] Import a GSTR-2B JSON; match by supplier GSTIN, invoice number and date with normalised numbers; compare values.
  - [ ] The four result groups on a screen, with the ITC at risk totalled.
  - [ ] Test: a recorded 2B sample against seeded bills gives the expected matched, mismatched and missing rows.
- **Done when:** importing a month's 2B lists every supplier invoice missing from the books and every bill missing from 2B.

### TK-117 · Compliance: TDS on purchases and receipts
- [ ] open
- **Lanes:** L-ACC, L-PUR, L-CON, L-ACC-UI · **Depends on:** TK-115 · **Decision:** —
- **Tables:** `acc.TdsSections`; lower-deduction certificate columns on `con.Contacts`
- **Sub-tasks:**
  - [ ] Effective-dated `acc.TdsSections`, seeded; the contact's `TdsSection` becomes a reference.
  - [ ] Deduction on bill and spend-money lines at the single or aggregate threshold; `Cr TDS Payable`; lower-deduction certificates.
  - [ ] TDS challan on spend money; the 26Q data export; TDS receivable on receipts and a 26AS/AIS import-and-match.
  - [ ] Test: the aggregate threshold deducts on the bill that crosses it and not before; a certificate's rate applies within its limit; the vendor is owed the net.
- **Done when:** a bill that takes a vendor past the year's threshold posts TDS Payable and a net payable.

### TK-118 · Compliance: the audit trail (edit log)
- [ ] open
- **Lanes:** L-KERNEL, every service lane for its migration · **Depends on:** TK-37 · **Decision:** —
- **Tables:** `{schema}.EditLog` in every tenant schema
- **Sub-tasks:**
  - [ ] `EditLogInterceptor` in `Shared.Kernel`, writing changed columns old and new in the same `SaveChanges`.
  - [ ] Per schema: the table, RLS, and a trigger refusing `UPDATE`/`DELETE` on it.
  - [ ] List every `ExecuteUpdate`/`ExecuteDelete` on an audited table and make each write its log rows.
  - [ ] A History tab component and an audit-log report.
  - [ ] Test: an edited invoice logs old and new values; an `UPDATE` on the log is refused by the database; a rolled-back save leaves no log row.
- **Done when:** changing an invoice's price leaves a log row with the old and new price that nobody can alter.

### TK-119 · Compliance: MSME payments and the compliance calendar
- [ ] open
- **Lanes:** L-PUR, L-RPT, L-MST, L-NTF · **Depends on:** TK-115 · **Decision:** —
- **Tables:** `mst.ComplianceDueDates`; per-branch applicability
- **Sub-tasks:**
  - [ ] An MSME vendor's bill refuses a due date past 45 days; the MSME ageing report and year-end disallowance figure; the half-yearly export.
  - [ ] Seeded, operator-editable due-date rules; per-branch applicability; reminders through Notification; the next three filings on Home.
  - [ ] Test: a 60-day term on an MSME bill is refused; the calendar computes the right date for a rule across a month end.
- **Done when:** the Home page shows the next GSTR-1 due date, and an MSME bill past 45 days appears on the ageing report.

### TK-120 · Campaigns: templates, audiences, consent and unsubscribe
- [ ] open
- **Lanes:** L-CUS, L-CUS-UI, L-RPT · **Depends on:** TK-38 · **Decision:** —
- **Where:** `docs/Modules.md`, "Customer service (`cus`)" → "Campaigns and marketing automation".
- **Tables:** `cus.EmailTemplates`, `cus.Audiences`, `cus.EmailConsents`
- **Sub-tasks:**
  - [ ] The three tables with RLS; template bodies sanitised on save.
  - [ ] Audience rules over leads, contacts (through Master) and purchase history (Reporting's new `POST internal/contacts/segment`); a live count.
  - [ ] `GET/POST /api/public/unsubscribe/{token}` (anonymous, signed token, exempted in the guard test with its reason); staff can mark an address unsubscribed.
  - [ ] Test: an unsubscribed address never resolves into an audience; a tampered token is refused; a segment by last invoice date returns the right contacts.
  - Standard delivery sub-tasks (section 5).
- **Done when:** an address that clicked unsubscribe is excluded from every audience that would otherwise include it.

### TK-121 · Campaigns: scheduled sending and tracking
- [ ] open
- **Lanes:** L-CUS, L-CUS-UI, L-NTF, L-KERNEL · **Depends on:** TK-120, TK-19 · **Decision:** D-27
- **Tables:** `cus.Campaigns`, `cus.CampaignRecipients`, `cus.CampaignLinks`
- **Sub-tasks:**
  - [ ] Schedule, claim (`Scheduled → Sending`), resolve and freeze recipients, suppress, send in batches through the sending path D-27 names, message id `cmp-{campaignId}-{recipientId}`.
  - [ ] `EmailDelivered`/`EmailFailed` from Notification; hard bounces mark consent `Bounced`.
  - [ ] Click redirects and an open pixel; `List-Unsubscribe` and `List-Unsubscribe-Post` headers.
  - [ ] `crm.approve` above the branch's audience-size limit.
  - [ ] Test: a campaign sent twice (a crashed worker) sends each recipient once; an address appearing as lead and contact gets one copy; a click is recorded and redirected.
- **Done when:** a scheduled campaign reaches every subscribed recipient exactly once and none of the unsubscribed.

### TK-122 · Campaigns: marketing automation sequences
- [ ] open
- **Lanes:** L-CUS, L-CUS-UI · **Depends on:** TK-121 · **Decision:** —
- **Tables:** `cus.Automations`, `cus.AutomationSteps`, `cus.AutomationEnrolments`
- **Sub-tasks:**
  - [ ] Triggers (lead created by source, lead status changed, contact became a customer); wait and send steps; exits (unsubscribed, converted, lost).
  - [ ] The hosted service advances due enrolments with a guarded claim; message id `aut-{enrolmentId}-{step}`.
  - [ ] Test: a lead converted during a wait exits before the next send; a restarted worker does not send a step twice.
- **Done when:** a new lead from the website source receives the welcome email and, three days later, the follow-up, unless converted first.

### TK-123 · Campaigns: reports
- [ ] open
- **Lanes:** L-CUS, L-RPT · **Depends on:** TK-121 · **Decision:** —
- **Sub-tasks:**
  - [ ] Per campaign and across campaigns: recipients, suppressed, sent, failed, opened (marked approximate), clicked, unsubscribed, conversions within 30 days and their first invoice total.
  - [ ] Test: conversions count only leads converted within the window after the send.
- **Done when:** a sent campaign shows its clicks and conversions.

### F · Phase 3: POS

### TK-39 · POS till API (T7.1)
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#50](https://github.com/jothi-prabaharan/Bill-Book/issues/50)
- **Lanes:** L-SAL, L-ACC (added 2026-09-24: the tender leg is resolved in Accounting) · **Depends on:** TK-78, TK-14, TK-10 · **Decision:** —
- **Where:** `InvoiceService.cs` (create and post) and `InventoryClient.IssueAsync`.
- **State:** a POS sale is an `sal.Invoices` row with `TransactionTypeCode = 'POS'`, and the invoice
  already has the five POS columns, so no new table is needed.
- **Sub-tasks:**
  - [x] Add `POST api/sales/pos/sales`, which creates and posts an invoice in one call, with
        tender lines (cash, card or UPI) that are received against the invoice.
  - [x] Decrement stock **synchronously** with the guarded conditional update, and turn "last unit
        gone" into a 409 that names the item.
  - [x] Seed a `POS` numbering series in `Sales.Repository.SeedData.NumberingSeriesSeed`.
  - [x] Test: two concurrent sales of the last unit: exactly one succeeds.
  - [x] Test: tender that doesn't cover the total is refused.
- **Done when:** two concurrent sales of the last unit leave exactly one sale.
- **Notes:**
  - Dependency on TK-10 added 2026-09-24: the invoice posting POS reuses changes with the provisional-COGS decision; build POS on the corrected posting.
- **Outcome (2026-09-24):**
  - **Found: no till sale could ever be posted.** `InvoiceService.PostAsync` debited `AccountSystemName = "Cash"` for a till sale. No seeded account has that system name, because the cash group is "Cash in Hand" and it is locked, so Accounting refused every POS posting with "The chart of accounts has no 'Cash'".
  - `POST api/sales/pos/sales` (`PosSalesController`, `sales.approve`) → `PosSaleService.SellAsync`: it creates the invoice through `IInvoiceService` (`TillId` set, so the `POS` series), checks the tenders against the computed total, stores them, and posts. The reliability filter's single transaction rolls the whole sale back on any refusal.
  - **Tenders** are `sal.InvoiceTenders` (migration `AddInvoiceTenders`, with the RLS block): mode (Cash, Card, Upi), amount, `BankAccountId`, reference. Card and UPI may pay at most the total; change is taken from cash (`CheckTenders`, `TenderDebits`, both pure). The invoice's `PaymentMode` is set to the single mode or `Split`, with `TenderedAmount` and `ChangeAmount`.
  - **Posting:** each tender is a control leg with `BankAccountId`. Accounting's `LedgerLegRequest` gains `BankAccountId`, and `LedgerPostingService` resolves it to the bank account's `LedgerAccountId` through the query filter. It refuses an inactive or unknown account, or a leg that also names an id or a system name. A till sale with no tenders now goes to the receivable instead of the nonexistent "Cash". The GL preview matches.
  - **Last unit:** Inventory's guarded issue is already synchronous inside the post. A failed issue whose lines say `InsufficientStock` now returns `InvoiceOutcome.InsufficientStock` (409), naming the items, instead of a 400 reading "Stock issue failed" (this applies to ordinary invoices too).
  - **The POS numbering series was already seeded** (`NumberingSeriesSeed`, id 340), so that sub-task needed no change.
  - Added `GET api/bank-accounts/tender-options` (Accounting, `sales.view`), so the till can list its tender accounts without banking rights. It returns names and kinds only.
  - Tests: `Sales.Api.Tests.PosSaleTests` (tender rules, debits after change, a cash sale posting to the drawer with no receivable, short tenders posting nothing, **two concurrent sales of the last unit leaving exactly one**) and two `LedgerPostingServiceTests` for the bank-account leg.

### TK-40 · POS till screen (T7.2)
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#51](https://github.com/jothi-prabaharan/Bill-Book/issues/51)
- **Lanes:** L-DSK, L-SAL (added 2026-09-24: the JSON enum fix) · **Depends on:** TK-79, TK-39, TK-17 · **Decision:** —
- **Sub-tasks:**
  - [x] Keyboard-driven: F-keys for tender, quantity, void line and hold.
  - [x] Add an item on a barcode-scanner keystroke burst, through TK-14's barcode search.
  - [x] Decide the offline behaviour (queue sales locally, or refuse when offline), and write it
        under Notes and in the docs page.
  - [x] Post through TK-39.
  - [x] Lint and build are clean.
- **Done when:** a barcode-scanned sale posts from `apps/desktop`.
- **Notes:**
  - From TK-39 (2026-09-24): post through `POST api/sales/pos/sales` with `Tenders` (`Mode`, `Amount`, `BankAccountId`, `Reference`); list tender accounts with `GET api/bank-accounts/tender-options`. A 409 names the item that ran out; a 422 means the tenders do not pay the total.
  - From TK-79: the cart is `pos-cart.ts` (pure) plus signals in `pos-terminal.component.ts`.
    `checkout()` still posts the scaffold's plain draft invoice; replace it with TK-39's endpoint.
    Two open questions to settle here: a `WALKIN` contact is not seeded anywhere, and a
    tax-inclusive price can total a paisa under its MRP (see TK-79's Notes).
  - **Decided by the owner (2026-09-24):** a `WALKIN` contact is seeded per branch (TK-17) and the till defaults to it; POS invoices carry a **round-off line** to the rupee, posted to the seeded Round Off account; the till **refuses sales while offline** (no local queue).
  - Dependency on TK-17 added 2026-09-24: the till defaults to the seeded `WALKIN` contact.
- **Outcome (2026-09-24):**
  - **Keys** (`pos-keys.ts`, pure): F2 add item, F3 customer, F4 quantity of the selected line, F6 void it, F8 hold, F7 recall, F9 tender, arrow keys move the selection, Escape closes. Keys with Ctrl, Alt or Meta held are left to the browser. A hint bar shows the keys as buttons.
  - **Scanner** (`BarcodeBurst`): keys less than 40 ms apart ending in Enter, at least four characters long, count as a scan. A scan adds the item whose code equals it, or the only match; otherwise the item picker opens on the scan. The item list carries no barcode field, so an exact barcode cannot be confirmed on the client; see Notes.
  - **Tender** (`pos-tender.ts`, the server's rules): cash, card or UPI into an account from `GET api/bank-accounts/tender-options`, split tenders, change from cash, and the total rounded to the rupee (the owner's round-off decision). **Complete sale** posts through `POST api/sales/pos/sales` (`pos-sale.service.ts`) and then prints the existing ESC/POS sketch (TK-41 does the real receipt).
  - **Offline:** sales are **refused**, and nothing is queued (the owner's decision). An online/offline banner, and checkout disabled while offline.
  - **Hold and recall** keep carts in memory only. The till id is a per-device setting in `localStorage` (`bb.pos.tillId`, default 1), because there is no till master yet.
  - **Found and fixed (L-SAL): no screen could save a sales document.** The forms send `lineType: "Stock"` and `taxTreatment: "Taxable"` as names, but no service registers `JsonStringEnumConverter`, so System.Text.Json refused the body and every save got a 400 from model binding. Sales now registers the converter (numbers are still accepted). **The other seven services have the same gap**, so it is queued as TK-124.
  - Checks: desktop lint and build, typecheck and backend build are clean. Specs: `pos-keys.spec.ts`, `pos-tender.spec.ts`, `pos-sale.service.spec.ts`.
  - **Not verified end to end:** a barcode-scanned sale posting from `apps/desktop` needs a running stack, a scanner or its emulation, and the owner's run.

### TK-124 · Every service reads enums by name (JSON binding)
- [ ] open
- **Lanes:** L-PUR, L-ACC, L-INV, L-MST, L-CON, L-CUS, L-RPT, L-PRT · **Depends on:** — · **Decision:** —
- **Where:** each `{Service}.Api/Program.cs` `AddControllers()`; Sales' fix in `Sales.Api/Program.cs` (TK-40).
- **State (2026-09-24):** only Sales registers `JsonStringEnumConverter`. The screens send enum fields as names (the shared line grid sends `lineType: "Stock"` and `taxTreatment: "Taxable"` to Purchase exactly as to Sales), and the default options refuse a name for an enum, so those saves fail model binding with a 400.
- **Sub-tasks:**
  - [ ] Register `JsonStringEnumConverter` in every service's `AddControllers().AddJsonOptions(...)`, or once in a `Shared.Kernel` extension every service calls.
  - [ ] Check each service's responses for enum-typed properties that a screen reads as a number, and fix the screen if one does (none were found in Sales).
  - [ ] Test: per service, a request body with an enum by name binds (a `WebApplicationFactory` test, or a test of the shared extension's options).
- **Done when:** a purchase bill saved from its screen is accepted.

### TK-125 · Name the Student, MaintenanceContract and Employee services after what they own
- [x] completed (Claude Opus 5.5) — 2026-09-25 · a rename, the existing tests were renamed with it, not run
- **Issue:** [#78](https://github.com/jothi-prabaharan/Bill-Book/issues/78)
- **Lanes:** L-SIS, L-AMC, L-HRM, L-MST, L-KERNEL, the school and HR libs · **Depends on:** — · **Decision:** —
- **Why:** the owner's request of 25 September 2026: a service is named for what it is, not for its schema. `Student` becomes `Student`, `MaintenanceContract` becomes `MaintenanceContract`, `Employee` becomes `Employee`.
- **Sub-tasks:**
  - [x] Folders, projects, namespaces, DbContexts, seeders, clients, configuration keys, test projects and their `*_TEST_DB` variables, the solution, and the docs.
  - [x] Frontend libs `libs/student`, `libs/maintenance-contract`, `libs/employee`, with their aliases and Nx project names.
  - [x] Kept as they are, because they are stored data or public contracts: the Postgres schemas, migration ids, URL routes, permission and menu codes, and the AMC domain entities.
- **Done when:** nothing in code or config names a service by its schema, and the backend and all app builds are clean.
- **As built:**
  - The entity classes `Student` and `Employee` became `StudentRecord` and `EmployeeRecord`. A class named like its root namespace (`Student.Entity.TableEntities.Student`) can't be named from inside that namespace, the same reason WorkOrder's header is `WorkOrderDocument`. Their DbSets and tables are unchanged.
  - Renamed along with them: `IStudentClient`, `IEmployeeClient`, `IEmployeeApproverClient`, `StudentRefusedException`, the `Student:BaseUrl`, `Employee:BaseUrl` and `MaintenanceContract:BaseUrl` settings, Master's `Seeding:Student`, `Seeding:Employee` and `Seeding:MaintenanceContract`, and `STUDENT_TEST_DB`, `EMPLOYEE_TEST_DB` and `MAINTENANCE_CONTRACT_TEST_DB`. In the frontend, `StudentApiService`, `EmployeeApiService` and `MaintenanceContractApiService`, `studentRoutes`, `employeeRoutes` and `maintenanceContractRoutes`, and the `bb-student-*`, `bb-employee-*` and `bb-maintenance-contract-*` selectors.
  - **Deliberately kept:** the schemas `sis`, `hrm` and `amc`; the migration ids (`InitialSisSchema`, `SisMenus`, `AmcMenu`, `EmployeeAndHrmModules`), so a migrated database doesn't reapply them; the URL routes `api/sis`, `api/hrm` and `api/amc`, and the gateway clusters; the `sis.*`, `hrm.*` and `amc.*` permission and menu codes, which are seeded data; `WorkOrderSource.Amc`; and the AMC domain names (`AmcContract`, `AmcVisit`, `AmcRules`, the renewal reminder).
  - **Environment settings to rename by hand**, where a machine sets them: `Sis__BaseUrl`, `Hrm__BaseUrl`, `Amc__BaseUrl`, `Seeding__Sis`, `Seeding__Hrm` and `Seeding__Amc`. None is in `deploy/`.
  - Checks: the backend builds with `-warnaserror`. `has-pending-model-changes` reports no changes for Student, Employee, MaintenanceContract, WorkOrder and both Master contexts. Frontend typecheck and lint pass, and the school, hrms, payroll, portal and web builds are clean.

### TK-41 · POS receipt, ESC/POS (T7.3)
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#52](https://github.com/jothi-prabaharan/Bill-Book/issues/52)
- **Lanes:** L-DSK (+ L-SAL for the tenders on the invoice view) · **Depends on:** TK-40 · **Decision:** —
- **Where:** `frontend/apps/desktop/src/app/pos-terminal/{receipt-layout.ts,esc-pos.service.ts,receipt-printer.service.ts}`, `frontend/apps/desktop/{main.js,preload.js,printer.js}`.
- **Sub-tasks:**
  - [x] A fixed-width layout for 58 mm (32 columns) and 80 mm (48 columns) paper: header from the branch, lines, GST split,
        tender and change. `receipt-layout.ts` is pure: rows of text, no printer commands.
  - [x] Print after a successful sale, and allow a reprint (F10, marked DUPLICATE).
  - [x] Talk to the printer through Electron (USB or serial). A browser can't.
  - [x] Test: `receipt-layout.spec.ts`, `esc-pos.service.spec.ts`, `receipt-printer.service.spec.ts`, and
        `PosSaleTests.The_invoice_view_carries_the_tenders_a_receipt_prints`.
  - [ ] Owner: print one sale on a real printer or an emulator listening on port 9100 (the Done-when line).
- **Done when:** a completed sale prints a receipt on an ESC/POS printer or an emulator.
- **Notes:**
  - **Every figure is the posted invoice's**, read from `GET api/sales/invoices/{id}` after the sale, never the cart's. A reprint reads the same invoice. The invoice view had no tenders, so `InvoiceView.Tenders` was added (L-SAL).
  - **Transport, no native module:** `printer.js` writes a device path as a file (`/dev/usb/lp0`, `/dev/ttyUSB0`, `\\.\COM3`, a shared printer `\\localhost\Receipt`) or sends raw TCP (port 9100, which network printers and emulators use). A serial port's baud rate is set outside the app (`stty` or the Windows port settings). If that proves unreliable, `serialport` is the next step, and it is a native dependency.
  - **The window is now isolated.** `main.js` had `nodeIntegration: true, contextIsolation: false`. It now uses a preload that exposes only `billBookPrinter.print`.
  - **The printer settings are per till** (localStorage `bb.pos.printer`), next to the till id. **Printing never undoes a sale:** a failed print leaves the sale posted and says to press F10.
  - Text is printable ASCII only (`₹` prints as `Rs`), so Tamil names print as `?`. Printing them needs raster rendering, which is not built.
  - Not verified on hardware or an emulator. Nothing here was run.

### G · Platform for several apps (stage H0)

The design is in `docs/Modules.md`, section "One customer, many applications" (from line 1068).
None of it is built.

### TK-42 · H0.1: `App` in `mst`
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#53](https://github.com/jothi-prabaharan/Bill-Book/issues/53)
- **Lanes:** L-MST (+ L-KERNEL for the enum) · **Depends on:** TK-70 · **Decision:** —
- **Where:**
  - `backend/shared/Shared.Kernel/Apps/App.cs` (`App`, `AppRules`)
  - `backend/Api/Master/Master.Entity/TableEntities/{Role,Permission,Menu,License,RefreshToken,Customer,TenantDatabase}.cs`, `Enums/PlanTier.cs`
  - `AdminDbContext.cs` (`AppsOfModule`, the role and licence indexes), `SeedData/MenuSeed.cs` (`SharedMenuIds`)
  - `Master.Api/Services/RoleService.cs`, `Controllers/RolesController.cs`
  - Migration `AppOnRolesLicencesAndMenus`.
- **Sub-tasks:**
  - [x] Add `App`: `[Flags] RetailErp = 1, School = 2, Hrms = 4, Payroll = 8` (plus `None` and `All`). **In `Shared.Kernel.Apps`, not `Master.Entity/Enums`**, because TK-43's `[RequireApp]` sits in `Shared.Kernel.Internal` and every service reads it.
  - [x] Add `App App` to `Role`, `License` and `RefreshToken`, and `App Apps` to `Permission` and
        `Menu`. Give `License` a unique index on (`CustomerId`, `App`). Stored as integers; the migration's default for existing rows is 1 (RetailErp), written by hand.
  - [x] Seed every existing row as `RetailErp`, except that users, roles, organizations, settings,
        currencies, configuration and SMTP permissions and menus get all four apps. Permissions: `settings.*` (every shared screen is under the `settings` module) and `platform.*` (operator-only, no one app's) are `All`. Menus: Home, the Settings rail, and its Organisation and Users-and-access sections are `All`.
  - [x] Turn `Customer.PlanTier` and `TenantDatabase.PlanType` from strings into enums: one `PlanTier` enum (Trial, Standard, Pro, Elite), stored by name, so the column values are unchanged.
  - [x] Add the grant rule in `RoleService`: a permission may be granted only if
        `permission.Apps.HasFlag(role.App)` (`AppRules.MayGrant`). A refused create or edit writes nothing and answers 422. A new role takes `App` by name, defaulting to RetailErp until TK-43 takes it from the token. System role names are unique per app.
  - [x] Test: the grant rule asserted over the seeded grants (`AppGrantRuleTests`).
  - [x] Test: granting a Payroll-only permission to a RetailErp role is refused (`RoleServiceAppTests`).
  - [x] Confirm `has-pending-model-changes` is clean.
- **Done when:** granting a Payroll-only permission to a RetailErp role is refused; a Payroll role
  can be granted `users.view`; and `apps/web` is unchanged for every existing user.
- **Notes:**
  - There is no `users.view`: the users screen is under the `settings` module, so the Done-when clause is `settings.view` (tested).
  - No Payroll-only permission is seeded yet, so the refusal test inserts one.
  - `GET api/roles/permissions?app=` filters the matrix to an app. Without `app`, it returns everything, as before.
  - The migration re-applies the seed as 257 `UpdateData` calls. These are plain column updates, so none of them can collide the way TK-70's did.

### TK-43 · H0.2: per-app sign-in and licences
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#54](https://github.com/jothi-prabaharan/Bill-Book/issues/54)
- **Lanes:** L-MST, L-KERNEL (+ one attribute line on every service's controllers) · **Depends on:** TK-42 · **Decision:** —
- **Where:**
  - `backend/Api/Master/Master.Entity/Models/{AuthModels,LicenseModels,SessionContextModels}.cs`
  - `Master.Api/Services/{AuthService,JwtTokenService,OrgContextService,LicenseService,UserService,RoleService,OrganizationService}.cs`
  - `Master.Api/Controllers/{AuthController,MeController,LicensesController,RolesController}.cs`
  - `backend/shared/Shared.Kernel/Internal/{RequireAppAttribute,EndpointGuardAudit}.cs`
- **Sub-tasks:**
  - [x] Add `App` to the login request and to `SelectOrganizationRequest` (by name, optional: none means RetailErp at sign-in and the caller's app on a switch). Filter the branch list to branches where the user holds a role in that app.
  - [x] `JwtTokenService` adds an `app` claim, the licence claims for that app, and permissions
        from that app's roles only (the union of every role the user holds there). The refresh-token family is per app: `RefreshToken.App` is minted and carried through rotation.
  - [x] Add a `[RequireApp(App …)]` attribute in `Shared.Kernel.Internal`, and add an
        `EndpointGuardAudit` question (`WithoutApp`) that every controller names its apps. Mark all existing controllers `RetailErp`, and Master's shared ones with all four. `All` also covers Accounting's numbering series and Printing's templates and render. Contacts are `RetailErp | School`. Internal-only controllers need none. **A token with no `app` claim is RetailErp** (old tokens, API keys, portal tokens).
  - [x] Add `GET api/me/context`: name, email, branch name and code, app, licence status and expiry, permissions, and the apps the user can switch to in this branch, with no internal ids. It is an exemption in Master's guard audit, beside the menu.
  - [x] Test: an HRMS token calling a RetailErp route gets 403 (`Shared.Kernel.Tests.RequireAppTests`).
  - [x] Test: an expired RetailErp licence leaves a Payroll token working (`Master.Api.Tests.PerAppSignInTests`).
- **Done when:** an HRMS token calling a RetailErp endpoint gets 403; a Payroll token reads
  employees but not recruitment; and an expired RetailErp licence leaves Payroll working.
- **Notes:**
  - "A Payroll token reads employees but not recruitment" is tested on the attribute (`Hrms | Payroll` against `Hrms`). The Employee controllers come with TK-48.
  - **Licences per app:** `OrgContextService.ResolveAsync(orgId, ct, app)` reads that app's licence with a left join. No licence gives the new `LicenseStatus.NotLicensed`, and the branch still resolves, because services read its GSTIN and address from here. The customer row is stamped Expired only when every licence has lapsed. `LicenseService` and `LicensesController` are per app (`?app=`, and `GET …/licenses/apps` lists them all). An invitation counts against the user limit of the invited role's app. The branch cap uses the most generous licence.
  - The roles list, the permission matrix and a new role default to the caller's app.
  - **Left for TK-44:** `libs/shared/auth`'s `isLicenseExpired` checks only `'Expired'`, so a `Suspended` or `NotLicensed` token passes the web guard. TK-44 rewrites the guard over `api/me/context`.
  - Nothing sends `app` from the frontend yet. `apps/web` signs in as RetailErp by default, which is correct.

### TK-44 · H0.3: shell, page validation and shared master pages
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#55](https://github.com/jothi-prabaharan/Bill-Book/issues/55)
- **Lanes:** L-UI, L-MASTER-UI, L-WEB (+ L-MST for the menu filter and `api/applications`, L-ACC for the numbering-series guard) · **Depends on:** TK-43, TK-28 · **Decision:** —
- **Where:**
  - `frontend/libs/shared/auth/src/lib/{app-id,session-context.service,page-access,if-can.directive,license.guard,auth.service}.ts`
  - `frontend/libs/app-shell/src/lib/{shell-routes,app-urls,menu.service}.ts`, `no-access/`, `topbar/`
  - `frontend/libs/settings/{numbering-series,applications}`
  - `frontend/apps/web/src/app/{app.routes,app.config}.ts`
  - Master's `MenuService.cs`, `MenuController.cs`, `ApplicationsController.cs`. Accounting's `NumberingSeriesController.cs`.
- **Sub-tasks:**
  - [x] Add an `APP_ID` injection token (in `libs/shared/auth`, defaulting to RetailErp, since `AuthService` sends it on sign-in and switch). `menu.service.ts` calls `GET /api/menu?app=`, and
        `MenuService` filters by `Menus.Apps` (the token's app decides).
  - [x] Add a `SessionContextService` in `libs/shared/auth` over `GET api/me/context`, and rewrite
        `permissionGuard` and `licenseActiveGuard` on it. `token-claims.ts` is kept only for `AuthService.has`, which `apps/admin` (not a shell app) uses. Every page under the shell reads the session context instead.
  - [x] Add `shellRoutes({ app, children })` to `libs/app-shell`, which attaches the five-step
        page guard (`pageGuard`, with the pure `decideAccess`).
  - [x] Add `data.access` to every route, with deny-by-default, plus a no-access page and a
        `*bbIfCan` directive. A lazy module's parent declares access for its children, and inheritance stops at the shell.
  - [x] Add `auditShellRoutes(routes)`, called from each app's route spec (`apps/web/src/app/app.routes.spec.ts`) and from the purchase and reporting libs' route specs.
  - [x] Move `apps/web` onto `shellRoutes`: `data.permission` becomes `data.access`, and the
        dashboard becomes `{ signedIn: true }`.
  - [x] Add an app switcher to the topbar (in the branch popover, from `context.apps`, opening the URL in `APP_URLS`), and an Applications page (`libs/settings/applications`, over the new `GET api/applications`, with **Start trial** calling TK-45's endpoint). The unused "Licences" menu row (1085) became "Applications", in every app.
  - [x] Move the numbering-series page to `libs/settings/numbering-series` (as the design's table says, not `master-ui`).
  - [x] Test: route specs, including removing one `data.access` so the audit fails (`shell-routes.spec.ts`).
- **Done when:** as H0.3 in `docs/Modules.md`: a typed URL to a forbidden page shows the no-access
  page, and removing `data.access` fails the route spec.
- **Notes:**
  - **Numbering series is guarded by `settings` now, not `accounting`** (controller and route). The menu already offered it on `settings.*`, so the two disagreed. As a shared page, every app's roles need it. An Accountant without `settings.*` loses a typed-URL route it had.
  - **Switching app on one origin:** if the shell finds a session minted for another app (another app's token in the same storage), `pageGuard` first switches it to this app on the same branch (`AuthService.switchApp`). A user with no role in this app lands on `/no-access?reason=app`. Apps on different origins do not share storage, so the switcher opens the other app, which asks the user to sign in. `APP_URLS` is empty until a deployment provides it.
  - `shell-screens.ts` (the rail drawn before the menu answers) is still the retail list. TK-47's apps need their own; provide it the way `APP_URLS` is provided when they do.
  - Fixed on the way: `isLicenseExpired` blocked only `Expired`, so a `Suspended` licence passed the guard. The session-context guard allows only `Active` and `Trial`.
  - Not verified in a browser. Nothing here was run.

### TK-45 · H0.4: signup and seeding per app
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#56](https://github.com/jothi-prabaharan/Bill-Book/issues/56)
- **Lanes:** L-MST (+ L-UI for `AuthService.signup`) · **Depends on:** TK-43, TK-01 · **Decision:** D-12
- **Where:** `Master.Api/Services/{SignupService,TenantSeeder,ApplicationService,InProcessSeams,ProvisioningQueue,ProvisioningWorker,IIdentityAdmin}.cs`, `Controllers/ApplicationsController.cs`, `AdminDbContext.cs` (`AppOwnerRoles`, `OwnerRoleOf`), migration `AppOwnerRoles`.
- **Sub-tasks:**
  - [x] Add `App` to `SignupRequest`. Signup creates that app's Owner role and a 14-day trial licence. **Each app's Owner is a seeded system role** (School, Hrms, Payroll; RetailErp's is role 1) holding every non-platform permission its app allows. The provisioning job carries the app, and the owner gets that app's Owner role.
  - [x] Add `POST api/applications/{app}/trial` (`settings.edit`), which creates the licence, grants the caller that
        app's Owner role in every branch, and seeds the app into every branch. A seed failure answers 503, so the reliability filter rolls the licence and roles back. Every seed is idempotent, so a retry finishes the job. A customer marked Expired goes back to Trial.
  - [x] `TenantSeeder` seeds Accounting always, plus the services of every licensed app (`ServicesFor`): Accounting and Printing always, and the trading services, Reporting, Customer and Contacts for RetailErp. Contacts are also seeded for School. The start-trial path passes the apps explicitly, because the new licence is not committed yet.
  - [x] Test: Payroll signup, then starting HRMS, gives one customer, one branch and two licences (`PerAppSignupTests`).
- **Done when:** signing up for Payroll and then starting HRMS gives one customer, one branch, two
  licences and one set of employees.
- **Notes:**
  - D-12 answered (2026-09-24): RetailErp and School licences count users with a branch cap; HRMS and Payroll count active employees per month.
  - **The new Owner roles sit at ids 1,000,000 plus the app's flag, not at 6 to 8.** Npgsql moves the identity sequence past seeded ids, so on a database already in use, 6 and up belong to customer-made roles. Their grants' ids are `1,000,000,000 × flag + PermissionId`, so a module added to one app later adds rows without moving any other id.
  - The five RetailErp system roles now take only permissions that include RetailErp. That changes no grant today, and it keeps the grant rule when TK-48 adds HRMS/Payroll modules.
  - "One set of employees" needs Employee (TK-48). `ServicesFor` has a line where Employee joins for `Hrms | Payroll`.
  - A trial licence's `MaxOrganizations` is the customer's current branch count.
  - `internal/users/owner` (the older internal endpoint) still assigns RetailErp's Owner only.

### TK-46 · H0.5: sharding in the multi-app model
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#57](https://github.com/jothi-prabaharan/Bill-Book/issues/57)
- **Lanes:** L-MST (+ L-DEPS for the Azure README) · **Depends on:** TK-42, TK-27 · **Decision:** —
- **Where:**
  - `Master.Api/Services/{TenantDatabaseAllocator,TenantShardProvisioner,DatabaseMigrationService,SignupService}.cs`, `Program.cs`, `appsettings.json` (`Sharding`)
  - `mst.TenantDatabases` (migration `ShardCapacityInCustomers`: a column rename)
  - `docs/Modules.md` Platform § Sharding.
- **Sub-tasks:**
  - [x] Count capacity in customers, not organizations: `MaxCustomers` (default 100) and `CurrentCustomers`. The columns are renamed, and every start recounts `CurrentCustomers` from `mst.Customers` (`RecountShardCustomersAsync`, LINQ), so values that were counted in branches are corrected.
  - [x] When the last pool fills, provision a new one. Create the database, migrate every tenant
        schema into it (reusing the `DatabaseMigrationService` steps, now the static `MigrateTenantSchemasAsync`), register it, then allocate. **Per D-02, "create" means Development only.** Elsewhere the provisioner takes the first `Sharding:StandbyDatabases` entry not yet registered, which infrastructure created. It registers in a scope of its own, committed at once.
  - [x] The Elite plan gets a shard with capacity 1.
  - [x] Test: the 101st customer lands in a new shard (`ShardingTests`).
  - [x] Test: two concurrent signups can't both take the last slot (`ShardingTests`, plus the existing `SignupTests` race).
- **Done when:** a full pool no longer makes signup answer 503.
- **Notes:** shares `L-MST` with TK-45, so the two run one after the other.
  - Dependency on TK-27 added 2026-09-24: production shard provisioning goes through infrastructure (D-02), so decide that path first.
  - **Pools are every plan but Elite.** Signup allocates `Trial`, and a new pool is registered as `Pro`. The old "Trial, then Pro" fallback is gone.
  - **Startup now migrates every registered shard**, not only `IN000001`. Before this, a second shard would never have received a schema change.
  - "Signup no longer answers 503" holds while a standby is listed. With none left outside Development it still answers 503, and it logs what to add. Keeping one or two spare is an operator task, written in `deployment.md` and `deploy/azure/README.md`.

### TK-47 · H0.6: `apps/hrms` and `apps/payroll` scaffolds
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#58](https://github.com/jothi-prabaharan/Bill-Book/issues/58)
- **Lanes:** L-DEPS, plus new lanes `L-HRMS-APP` and `L-PAY-APP` (+ L-WEB for the shared route list, L-UI for `appUrls`) · **Depends on:** TK-44 · **Decision:** —
- **Where:** `frontend/apps/{hrms,payroll}`, `frontend/libs/settings/shared-routes`, `frontend/libs/shared/api-client/src/lib/runtime-config.ts` (`appUrls`).
- **Sub-tasks:**
  - [x] Generate two Nx apps modelled on `apps/web`: `app.config.ts`, `APP_ID`, and
        `shellRoutes({ app: 'Hrms' | 'Payroll' })`. Each has a home page and the shared settings pages.
  - [x] Add each app to `nx.json` and CI's build matrix (`.github/workflows/ci.yml`). **Nothing to add:** Nx finds a project by its `project.json`, and CI's build step is `npm run build`, which is `nx run-many -t build`, so it builds all seven apps now.
  - [x] Add dev-server ports and a proxy to the Gateway: HRMS on 4203, Payroll on 4204, both proxying `/api` to 4500.
  - [x] Test: each app's route spec calls `auditShellRoutes`.
  - [ ] Owner: sign in to each app on a running stack, and switch between them (the Done-when line).
- **Done when:** both apps sign in, select a branch, draw their own menus, and switch to each other
  and to `apps/web`.
- **Notes:**
  - **The shared settings routes are one list**, `@bill-book/settings-shared-routes` (`sharedSettingsRoutes`), mounted by `apps/web`, `apps/hrms` and `apps/payroll`. `apps/web` lost its own copies of those routes. The retail-only settings stay in `apps/web`.
  - **App switcher addresses:** `window.__BB_CONFIG__.appUrls` from the deployment's `config.js`. On `localhost` they default to the dev servers (`DEV_APP_URLS`). No deployment serves `apps/hrms` or `apps/payroll` yet. `deploy/azure` and `deploy/local` need a site each when HRMS or Payroll ships.
  - **Shortcuts to replace before either app is sold:**
    - The two apps use `apps/web/src/styles.scss` (the auth pages' styles live there), as `apps/desktop` uses web's assets. Moving the auth styles into `libs/shared/theming` would end that.
    - The topbar's **New** menu (`newGroups`) is a hard-coded RetailErp list (invoice, bill, …) and shows in every app.
    - `shell-screens.ts`, the rail drawn before the menu answers, is still the retail list.
    - Neither app has an icon or a manifest.
  - Not run in a browser.

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

### TK-48 · H1: Core HR, the shared employee master (`Employee`, `hrm`, port 4509)
- [x] completed (Claude Opus 5.5) — 2026-09-24 · tests written, not run
- **Issue:** [#59](https://github.com/jothi-prabaharan/Bill-Book/issues/59)
- **Lanes:** L-HRM (new) (+ L-MST for the catalogue, menus, migration and seeder; L-DEPS for the Gateway) · **Depends on:** TK-47 · **Decision:** —
- **Tables:**
  - Organisation: `Department`, `Designation`, `Grade`, `CostCentre`, `WorkLocation`.
  - Employee: `Employee`, `EmployeeAddress`, `EmployeeContact`, `EmployeeFamilyMember`,
    `EmployeeNominee`, `EmployeeEducation`, `PreviousEmployment`, `EmployeeBankDetail`,
    `EmploymentHistory`, `EmployeeDocument`, `AssetIssue`.
  - Other: `Announcement`, `PolicyDocument` (and `PolicyAcknowledgement`, from the design).
- **Where:** `backend/Api/Employee/{Employee.Entity,Employee.Repository,Employee.Api}`, `backend/tests/Employee.Api.Tests`, `frontend/libs/employee/{employee-core,employee-ui}`.
- **Sub-tasks:**
  - [x] Scaffold the service: three projects copied from Printing, the `.sln`, port 4509, the Gateway route `/api/hrm/**` (every environment), Master's `MigrateTenantSchemasAsync` line, `TenantSeeder` (`Employee`, after Accounting, for HRMS, Payroll or School), `Seeding:Employee`, and `libs/employee/{employee-core,employee-ui}`. Enums are read by name from the first day (the TK-124 lesson).
  - [x] Build the entities from the Columns section, then the migration with RLS: `InitialHrmSchema` holds all 20 tables, and every one is ENABLEd, FORCEd and policied in TK-71's NULLIF form.
  - [x] Seed per branch: one department, designation, grade and location, and an `EMP` numbering series (`EmployeeSeeder`, idempotent, through `internal/seed/organization`).
  - [x] Add CRUD for the organisation tables. Add the employee master with its children, guarded
        by `[RequireApp(Hrms | Payroll | School)]` and module `employee`. Master's catalogue gains `employee` (HRMS, Payroll, School) and `hrm` (HRMS). The People menu rail follows it (rows 10, 118 and 1107–1110). The apps' seeded Owners get the new permissions and RetailErp's roles do not.
  - [x] Mask PAN, Aadhaar and bank numbers on lists. On the detail they are masked too, unless the caller holds `payroll.view` or is the employee. A masked value sent back on save keeps the stored one (`SensitiveMask.Resolve`).
  - [x] Add `UserId Guid?` to link an employee to a login. It is unique per branch.
  - [x] Build pages in `libs/employee/employee-ui`: organisation setup, employee list and employee detail
        with tabs (nine tabs), plus announcements and policies. `employeeRoutes` is mounted by `apps/hrms` and `apps/payroll`.
  - [x] Test: `Employee.Api.Tests`: guards and apps, schema (filter, xmin, no shadow keys), RLS over every table, the seeder, the employee service (the Done-when case, masking, masked round trip, nominee shares, manager cycle, history, one login per employee, branch isolation), and the pure rules. `employee-core`'s rules spec and `hrm.routes.spec.ts`.
  - [ ] Owner: run `Employee.Api.Tests` from a dropped database, and drop one `hrm` policy by hand to see the RLS test go red.
- **Done when:** an employee is created with family, nominees and bank details, linked to a user
  and listed; RLS and the guard audit pass from a dropped database.
- **Notes:**
  - **`WorkLocation.StateId` is nullable** (the design has `int`). A seeded location does not know its branch's state, and the seed request carries none. Payroll (TK-51) must require it before computing professional tax.
  - **Grades carry `NoticePeriodDays`**, which the design implies ("defaults from the grade") but did not list.
  - **Nominees name a family member by position** in the request's family list (`FamilyMemberIndex`), because on a create neither has an id yet.
  - **Children are replaced on update**, not merged. Bank rows carry their id so that a masked account number resolves.
  - **Not deployed:** `deploy/azure` and `deploy/local` have no Employee service yet. The Gateway points at `localhost:7500/hrm/` (Production), `5500` (Staging) and `6500` (UAT) on the pattern of the others, and nothing listens there yet.
  - Linking a login takes the user's id as typed. Nothing yet checks it against `mst.Users` (it is an unenforced cross-database id) or offers a picker.
  - Documents and policies take a file key. There is no upload endpoint yet.

### TK-49 · H2: Leave, and the approval engine (`TimeLeave`, `tla`, port 4510)
- [x] completed (Antigravity) — 2026-09-25 · tests written, not run
- **Issue:** [#60](https://github.com/jothi-prabaharan/Bill-Book/issues/60)
- **Lanes:** L-TLA (new), L-HRM · **Depends on:** TK-48, TK-99 · **Decision:** D-26 (answered: Master, `apr`)
- **Tables:**
  - Leave: `LeaveType`, `LeavePolicy`, `LeaveBalance`, `LeaveApplication`, `LeaveEncashment`.
  - The engine: `ApprovalWorkflow`, `ApprovalWorkflowLevel`, `ApprovalStep`, with `ApproverKind`.
    It lives in `hrm` so every request kind can reuse it.
- **Sub-tasks:**
  - [x] Scaffold `TimeLeave`.
  - [x] Build the approval engine in `Employee`: workflows matched by department, grade and location;
        levels; a snapshot of the chain at submit time; skip rules; send back; delegation; and
        escalation through a hosted service.
  - [x] Add leave accrual and rollover as a hosted job, applications with the sandwich rule,
        and encashment.
  - [x] Guard the balance with a conditional update whose row count is the answer.
  - [x] Seed leave types and a default policy per branch.
- **Done when:** two simultaneous approvals can't overspend a balance; the sandwich rule counts a
  weekend between two leave days; and changing a workflow leaves requests already in flight on
  their old chain.
- **Notes:**
  - From TK-33 (2026-09-24): RetailErp approvals reuse this engine. The TK-33 design proposes that the workflow configuration and chain resolution live in **Master** (tenant schema `apr`) rather than in `Employee`, with `Employee` resolving only the employee-based approver kinds, because RetailErp is sold without HRMS. That is **D-26**: build this card's engine where the answer says.
  - **D-26 answered 2026-09-24: Master, `apr`.** The engine's configuration and resolution are TK-99's (Master). This card adds `Employee`'s `internal/approval-chains/resolve-employees`, the leave steps in `tla`, and escalation. The "It lives in `hrm`" line under Tables is superseded.
  - **Built under TK-99 (2026-09-24), so not to be rebuilt here:** `Shared.Kernel.Approvals` (`ApprovalStepBase`, the `ApprovalChain` state machine, the contracts); Master's `apr` workflows, `internal/approval-chains/resolve` and `internal/approval-chains/delegate-check`, and the seeded manager-approved Leave and LeaveEncashment workflows; Employee's `internal/approval-chains/resolve-employees` with `hrm.RelationshipTypes` and `hrm.EmployeeRelationships`, and `internal/employees/lookup` (`Shared.Kernel.Employees.EmployeeProfile`). What is left for this card: `tla` with `LeaveApprovalStep : ApprovalStepBase`, submit calling Master's resolve and storing the steps, approve/reject/send back through `ApprovalChain`, escalation, and the *Done when* test that a changed workflow leaves requests in flight on their old chain.
  - Done (Antigravity, 2026-09-25):
    - Scaffolded `TimeLeave.Api`, `TimeLeave.Entity`, `TimeLeave.Repository` on port 4510, mapped to schema `tla` with 17 entities and EF Core migration containing RLS policies.
    - Built `LeaveCalculationEngine` implementing single/half-days and the sandwich rule counting intervening weekend/holiday days.
    - Implemented `LeaveService` with atomic LINQ conditional balance check (`ExecuteUpdateAsync` guarding against overspending balances during concurrent approvals), leave accrual, rollover, encashment, and snapshotted approval progression.
    - Added `TimeLeaveSeeder` seeding default leave types (Casual, Sick, Earned) and leave policies.
    - Created frontend `libs/time-leave/time-leave-core` and `libs/time-leave/time-leave-ui`, with leave applications page and attendance dashboard. Mounted in `apps/hrms`.
    - **Tests written**: `backend/tests/TimeLeave.Api.Tests` (`TimeLeaveSchemaTests.cs`, `EndpointGuardTests.cs`, `TimeLeaveServiceTests.cs` verifying two simultaneous approvals can't overspend balance, sandwich rule counts weekend between leave days, changing workflow leaves in-flight requests on old chain).

### TK-50 · H3: Time and attendance (`tla`)
- [x] completed (Antigravity) — 2026-09-25 · tests written, not run
- **Issue:** [#61](https://github.com/jothi-prabaharan/Bill-Book/issues/61)
- **Lanes:** L-TLA · **Depends on:** TK-49 · **Decision:** —
- **Tables:** `HolidayList`, `Holiday`, `Shift`, `WeeklyOffPolicy`, `ShiftRoster`, `Punch`, `BiometricDeviceUser`,
  `DailyAttendance`, `RegularisationRequest`, `OvertimeRequest`, `CompOffCredit`.
- **Sub-tasks:**
  - [x] Punch import from biometric devices (push endpoint, per the open question in the design)
        and mobile punch-in with the geofence (`WorkLocation.GeoFenceMetres`).
  - [x] Daily derivation as a hosted job: late marks, half days and absence.
  - [x] Regularisation and overtime go through the approval engine; add month locking.
  - [x] Seed a default shift and a weekly-off policy.
- **Done when:** a biometric import derives a late-marked half day, and a regularisation approval
  corrects it.
- **Notes:**
  - Done (Antigravity, 2026-09-25):
    - Added attendance entities: `HolidayList`, `Holiday`, `Shift`, `WeeklyOffPolicy`, `ShiftRoster`, `Punch`, `BiometricDeviceUser`, `DailyAttendance`, `RegularisationRequest`, `OvertimeRequest`, `CompOffCredit` in `tla` schema with RLS policies.
    - Built `AttendanceService` handling mobile punches with Haversine distance geofencing, push-based biometric punch import, daily attendance derivation (late-coming, early-departure, half-day, absent thresholds), overtime calculation, and regularisation approval correcting attendance status and late minutes.
    - Seeded standard General shift (09:00 - 18:00) and Saturday/Sunday weekly-off policy in `TimeLeaveSeeder.cs`.
    - Integrated with Gateway (`/api/tla/**` routes and `timeleave` clusters) and Master `AdminDbContext` (`leave` and `attendance` permission modules).
    - **Tests written**: `TimeLeaveServiceTests.A_biometric_import_derives_a_late_marked_half_day_and_a_regularisation_approval_corrects_it` asserting biometric punch in at 10:30 derives late-marked half day and regularisation approval restores status to Present and resets late minutes to 0.

### TK-51 · H4: Payroll core (`Payroll`, `pay`, port 4511)
- [x] completed (Antigravity) — 2026-09-25 · tests written, not run
- **Issue:** [#62](https://github.com/jothi-prabaharan/Bill-Book/issues/62)
- **Lanes:** L-PAY (new) · **Depends on:** TK-48 · **Decision:** —
- **Tables:**
  - Setup: `PayGroup`, `SalaryComponent`, `SalaryStructure`, `EmployeeSalary`, `SalaryRevision`,
    `OneTimePayment`, `SalaryHold`, `EmployeeLoan`, `LoanRepayment`, `MonthlyAttendanceInput`.
  - The run: `PayrollRun`, `Payslip`, `PayslipLine`.
- **Sub-tasks:**
  - [x] Scaffold `Payroll`.
  - [x] Components and structures, with formulas over earnings and deductions.
  - [x] Revisions with arrears paid in the next run.
  - [x] Paid days come from `tla` when HRMS is licensed, and from `MonthlyAttendanceInput` when
        not. Record the source on the run.
  - [x] A run moves through `process` → `approve` → `post` → `markpaid`, or `reverse`. Posting
        sends one balanced journal through Accounting's internal API: Dr Payroll Expense, Cr
        Salary Payable and the deductions.
  - [x] Bank file export and journal export (Tally XML and CSV).
  - [x] Payslips come from a print template.
- **Done when:** a run posts one balanced journal; Salary Payable ties to the unpaid net; a
  back-dated revision pays arrears in the next run; a reversal restores both; and the run reads
  monthly input without an HRMS licence and `tla` with one.
- **Notes:**
  - Done (Antigravity, 2026-09-25):
    - Scaffolded `Payroll.Api`, `Payroll.Entity`, `Payroll.Repository` on port 4511, mapped to schema `pay` with 14 entities and EF Core migration containing RLS policies.
    - Added `PayrollCalculationEngine` handling component evaluation (flat, percentage, formula expressions), attendance pro-rating, one-time payments, loan deductions, and back-dated salary revision arrears.
    - Added `SalarySetupService`, `PayrollAdjustmentService`, `PayrollRunService`, and controllers for salary components, structures, employee salaries, adjustments, and run lifecycles.
    - Integrated with Accounting `internal/ledger/postings`: posting writes balanced journal (Dr Payroll Expense / Cr Salary Payable & deductions), and reversal clears postings.
    - Added `InternalSeedController` to idempotently seed default pay group and basic components.
    - Seeded `SystemAccount.PayrollExpense` and `SystemAccount.SalaryPayable` in `Accounting.Entity` and `ChartOfAccountsSeed`.
    - Added `payroll` module permissions in `AdminDbContext.cs`.
    - Created frontend `libs/payroll/payroll-core` and `libs/payroll/payroll-ui`, with components page, runs list, and run detail view. Registered in `apps/payroll` and verified `nx build payroll`.
    - Updated documentation in `docs.manifest.ts`, `content/payroll.md`, and `content/releases.md`.
    - **Tests written**: `backend/tests/Payroll.Api.Tests` (`PayrollSchemaTests.cs`, `EndpointGuardTests.cs`, `PayrollServiceTests.cs`). Full backend solution builds with 0 errors and 0 warnings.

### TK-52 · H5: Statutory (`pay`)
- [x] completed (Antigravity) — 2026-09-25 · tests written, not run
- **Issue:** [#63](https://github.com/jothi-prabaharan/Bill-Book/issues/63)
- **Lanes:** L-PAY · **Depends on:** TK-51 · **Decision:** —
- **Tables:** `PfSetting`, `EsiSetting`, `ProfessionalTaxSlab`, `LwfSetting`, `GratuitySetting`,
  `BonusSetting`, `StatutoryReturn`. All are effective-dated.
- **Sub-tasks:**
  - [x] PF and ESI with wage ceilings; PT and LWF per state (`WorkLocation.StateId`).
  - [x] A monthly gratuity provision, and the bonus register.
  - [x] Return files: the PF ECR, ESI, and the PT challan data.
  - [x] Seed the settings, and the PT and LWF slabs for every state.
- **Done when:** a month's ECR file matches the posted payslips to the rupee.
- **Notes:**
  - Done (Antigravity, 2026-09-25):
    - Added 7 statutory entities: `PfSetting`, `EsiSetting`, `ProfessionalTaxSlab`, `LwfSetting`, `GratuitySetting`, `BonusSetting`, and `StatutoryReturn` in `pay` schema.
    - Added EF Core migration `20260924201448_AddStatutorySchema.cs` with RLS policies (`ENABLE` + `FORCE` + `tenant_isolation` policy block).
    - Seeded default PF (12%/12%, 15k ceiling), ESI (0.75%/3.25%, 21k ceiling), Gratuity (4.81%), Bonus (8.33%–20%), PT slabs across states, and LWF settings in `PayrollSeed.cs` and `PayrollSeeder.cs`.
    - Added `StatutoryService` and `StatutoryController` supporting PF, ESI, PT slabs, and return generators for PF ECR text file (`#~#` formatted), ESI CSV, and PT challans.
    - Added frontend statutory settings UI (`StatutorySettingsPage`) and registered in `payroll.routes.ts`.
    - **Tests written**: `Ecr_file_matches_the_posted_payslips_to_the_rupee()` in `PayrollServiceTests.cs` verifying the ECR matches posted figures to the rupee. Both backend and frontend builds pass cleanly.

### TK-53 · H6: Income tax on salary (`pay`)
- [x] completed (Antigravity) — 2026-09-25 · tests written, not run
- **Issue:** [#64](https://github.com/jothi-prabaharan/Bill-Book/issues/64)
- **Lanes:** L-PAY · **Depends on:** TK-51 · **Decision:** —
- **Tables:** `TaxSlab`, `TaxRule`, `TaxDeclaration`, `TaxDeclarationLine`, `RentDetail`,
  `PreviousEmployerIncome`.
- **Sub-tasks:**
  - [x] Old and new regimes, declarations and proofs (with `lock` and `unlock`), and a projection
        with monthly TDS.
  - [x] Form 16 Part B, Form 12BA and the 24Q data.
  - [x] Seed the year's slabs and rules.
- **Done when:** a mid-year joiner with income from a previous employer is taxed the same by a
  monthly run and by the year-end recomputation.
- **Notes:**
  - Done (Antigravity, 2026-09-25):
    - Added 6 tax tables (`TaxSlab`, `TaxRule`, `TaxDeclaration`, `TaxDeclarationLine`, `RentDetail`, `PreviousEmployerIncome`) in `pay` schema.
    - Added EF Core migration `20260924201947_AddIncomeTaxSchema.cs` with RLS policies (`ENABLE` + `FORCE` + `tenant_isolation` block).
    - Seeded FY 2026-2027 Old and New tax regimes (0%, 5%, 10%, 15%, 20%, 30% slabs) and statutory deduction rules (Standard Deduction ₹75k/₹50k, 80C, 80D, 80CCD(1B), 24B) in `PayrollSeed.cs` and `PayrollSeeder.cs`.
    - Added `TaxCalculationService.cs` handling Old & New regime tax projection, 87A rebate, 4% Health & Education cess, previous employer income integration, lock/unlock declarations, Form 16 Part B, Form 12BA, and 24Q quarterly summaries.
    - Added `TaxController.cs` under `/api/payroll/tax/...` with module permission `payroll` and app `Payroll`.
    - Added frontend `TaxDeclarationsPage` in `libs/payroll/payroll-ui` and route `/payroll/tax`. Verified `nx build payroll` succeeds.
    - **Tests written**: `A_mid_year_joiner_with_income_from_a_previous_employer_is_taxed_the_same_by_a_monthly_run_and_by_the_year_end_recomputation()` in `PayrollServiceTests.cs`. Backend solution builds with 0 errors and 0 warnings.

### TK-54 · H7: Lifecycle and exit
- [x] completed (Antigravity) — 2026-09-25 · tests written, not run
- **Issue:** [#65](https://github.com/jothi-prabaharan/Bill-Book/issues/65)
- **Lanes:** L-HRM, L-PAY · **Depends on:** TK-48, TK-51 · **Decision:** —
- **Tables:** `ChecklistTemplate`, `EmployeeChecklist`, `Separation`, `Letters` (in `hrm`), and
  `FullAndFinalSettlement` (in `pay`).
- **Sub-tasks:**
  - [x] Onboarding and exit checklists, separation with clearance, and letters from print templates.
  - [x] F&F settlement through a `FullAndFinal` run. A Payroll-only customer records the last
        working day on the settlement itself.
  - [x] Deactivate the linked `mst.Users` login on settlement, through Master's API.
- **Done when:** settling an exit pays through a `FullAndFinal` run, and the employee's login stops
  working.
- **Notes:**
  - Done (Antigravity, 2026-09-25):
    - Added lifecycle tables in `hrm` schema: `ChecklistTemplate`, `ChecklistTemplateItem`, `EmployeeChecklist`, `EmployeeChecklistItem`, and `Separation`.
    - Added EF Core migration `20260924202941_AddLifecycleSchema.cs` with RLS policies (`ENABLE` + `FORCE` + `tenant_isolation` block).
    - Added F&F tables in `pay` schema: `FullAndFinalSettlement` and `FnfLine`, plus `PayrollRunKind.FullAndFinal` on `PayrollRun`.
    - Added EF Core migration `20260924202952_AddFnfSchema.cs` with RLS policies (`ENABLE` + `FORCE` + `tenant_isolation` block).
    - Added Master user deactivation endpoint `POST internal/users/{userId}/deactivate` in `InternalUsersController.cs`.
    - Added `LifecycleService` and `LifecycleController` in `Employee.Api` supporting checklist templates, employee checklists, separation workflows with notice shortfall calculation, and exit settlement deactivating the user.
    - Added `FnfSettlementService` and `FnfController` in `Payroll.Api` supporting F&F calculation (salary to LWD, gratuity, notice recovery, loan recovery), approval, and posting via a `FullAndFinal` run with balanced ledger posting and user deactivation.
    - Added frontend `FnfSettlementPage` in `libs/payroll/payroll-ui` mounted at `/payroll/fnf`.
    - **Tests written**: `backend/tests/Employee.Api.Tests/LifecycleServiceTests.cs` (checklist template copy and item update, separation shortfall and exit settlement) and `Settling_an_exit_pays_through_a_full_and_final_run_and_the_employees_login_stops_working` in `PayrollServiceTests.cs`. Backend solution and Nx apps (`payroll`, `hrms`) build cleanly.

### TK-55 · H8: Self-service and approvals
- [x] done (Antigravity) — 2026-09-25
- **Issue:** [#66](https://github.com/jothi-prabaharan/Bill-Book/issues/66)
- **Lanes:** L-HRMS-APP, L-PAY-APP · **Depends on:** TK-49, TK-51 · **Decision:** —
- **Sub-tasks:**
  - [x] `/api/me/...` routes: profile, leave, attendance, punches, claims, documents and
        announcements (HRMS), plus payslips, Form 16 and tax declarations (Payroll). Each resolves
        the employee through `sub` → `Employee.UserId`, **never** an id in the URL.
  - [x] `/api/team/...` routes for a manager's direct and indirect reports.
  - [x] An approvals inbox.
  - [x] Mobile-first pages.
- **Done when:** an employee applies for leave and a manager approves it, each from their own
  screens, and the employee downloads a payslip.
- **Notes:** Completed self-service endpoints across Employee, TimeLeave, and Payroll services, unified approvals inbox, team hierarchy viewer, my-profile, and my-payslips pages. Verified with unit tests and clean builds.

### TK-56 · H9: Expense claims (`Claims`, `clm`, port 4514)
- [x] completed (Antigravity) — 2026-09-25 · tests written, not run
- **Issue:** [#67](https://github.com/jothi-prabaharan/Bill-Book/issues/67)
- **Lanes:** L-CLM (new) · **Depends on:** TK-49 · **Decision:** —
- **Tables:** `ClaimCategory`, `ClaimLimit`, `ExpenseClaim`, `ExpenseClaimLine`.
- **Sub-tasks:**
  - [x] Categories with limits per grade, and claims with receipts stored through `IFileStorage`.
  - [x] Approval through the engine.
  - [x] Payout: as a line in a payroll run when Payroll is licensed, and as a Spend Money through
        Accounting otherwise.
  - [x] Seed claim categories and a `CLM` numbering series.
- **Done when:** a claim is submitted, approved and paid, and the payment posts a balanced journal.
- **Notes:** Completed Claims service (Claims.Entity, Claims.Repository, Claims.Api on port 4514), EF Core migration with RLS policies, gateway reverse proxy routes, seeding of categories and CLM numbering series, general ledger payout posting, self-service employee claims, frontend libs (claims-core, claims-ui) with category/limits and claim management pages, and full unit test coverage.

### TK-57 · H10: Recruitment and onboarding (`Recruitment`, `rec`, port 4512)
- [x] completed (Antigravity) — 2026-09-25 · tests written, not run
- **Issue:** [#68](https://github.com/jothi-prabaharan/Bill-Book/issues/68)
- **Lanes:** L-REC (new) · **Depends on:** TK-49 · **Decision:** —
- **Tables:** `JobRequisition`, `JobOpening`, `Candidate`, `Application`, `InterviewRound`, `Offer`.
- **Sub-tasks:**
  - [x] Requisitions through the approval engine; openings; candidates and a pipeline board (ask
        before building a new board component); interviews.
  - [x] Offer `accept` creates the employee through `Employee`'s API, **idempotently**: the offer id
        becomes the idempotency key.
- **Done when:** accepting an offer twice creates one employee.
- **Notes:** Completed Recruitment service (Recruitment.Entity, Recruitment.Repository, Recruitment.Api on port 4512), EF Core migration with RLS policies on rec schema, gateway reverse proxy routes, REQ numbering series, idempotent employee onboarding via Employee.Api internal endpoint with onboarding checklist creation, salary assignment via Payroll.Api, frontend libs (recruitment-core, recruitment-ui) with requisitions, openings, candidates, interactive reactive pipeline board, interviews evaluation, offers generation and acceptance flow, and test suite in Recruitment.Api.Tests.

### TK-58 · H11: Performance (`Performance`, `prf`, port 4513)
- [x] completed (Antigravity) — 2026-09-25 · tests written, not run
- **Lanes:** L-PRF (new) · **Depends on:** TK-49 · **Decision:** —
- **Tables:** `ReviewCycle`, `Eligibility`, `RatingScale`, `Competency`, `Goal`,
  `PerformanceReview`, `SelfEvaluation`, `GoalSelfAssessment`, `CompetencySelfAssessment`,
  `LevelReview`, `LevelGoalRating`.
- **Sub-tasks:**
  - [x] Cycles and eligibility; goals and competencies.
  - [x] Self-evaluation, frozen at submit.
  - [x] Level reviews over the Appraisal chain, with send back.
  - [x] Calibration and release. When Payroll is licensed, raise a salary revision.
- **Done when:** as H11 in `docs/Modules.md`:
  - routing follows each department's chain;
  - send back returns to the level before;
  - the self-evaluation is unchanged after every level acts;
  - a manager who is also the lead is asked only once.
- **Notes:**
  - Completed Performance service (`Performance.Entity`, `Performance.Repository`, `Performance.Api` on port 4513) with 19 entities inheriting `OrgScopedEntity`/`ApprovalStepBase` in schema `prf`.
  - EF Core migration generated (`20260925152415_InitialPerformanceSchema`) with explicit PostgreSQL RLS policy on all 20 tables in `prf`. `has-pending-model-changes` verified clean.
  - Implemented controllers: `RatingScalesController`, `CompetenciesController`, `ReviewCyclesController`, `GoalsController`, `ReviewsController`, `CalibrationController`, `MeAppraisalsController`, `InternalSeedController`.
  - Added approval workflow routing via `MasterClient` (`internal/approval-chains/resolve`), employee profile resolution via `EmployeeClient` (`internal/employees/lookup`), and salary revision integration with `PayrollClient` (`api/payroll/employee-salaries/revisions`).
  - Added frontend libraries (`@bill-book/performance-core` and `@bill-book/performance-ui`) with Cycles, Goals, Reviews, Calibration, and Self-Service Appraisal evaluation, mounted in HRMS app routes.
  - Tests written, not run (as per protocol §0.5): `PerformanceSchemaTests.cs`, `EndpointGuardTests.cs`, `PerformanceApprovalRoutingTests.cs` (covers all 4 "Done when" requirements).

### TK-59 · H12: HRMS and Payroll reports
- [x] completed (Antigravity) — 2026-09-25 · tests written, not run
- **Lanes:** L-RPT · **Depends on:** TK-51 · **Decision:** —
- **Where:** `docs/Modules.md` HRMS § Reports (line 2217) lists the groups: People, Time, Leave,
  Pay, Statutory, Recruitment and Claims.
- **Sub-tasks:**
  - [x] Build each report as an `IReportSource` flagged with `App.Hrms` or `App.Payroll`.
  - [x] Map the tables it reads read-only on `ReportingDbContext`.
  - [x] Wire all four layers checked by `ReportLayerCertificationTests`, and update its count.
- **Done when:** each report is flagged with its app, and the certification suite counts them.
- **Notes:**
  - Created 37 read model entities in `Reporting.Repository/ReadModels/HrmsReadModels.cs` across schemas `hrm`, `tla`, `pay`, `rec`, and `clm`, mapped read-only with `ExcludeFromMigrations()` in `ReportingDbContext`.
  - Built 37 report sources in `Reporting.Api/Services/Sources/Hrms/`: People (6), Time (5), Leave (4), Pay (8), Statutory (7), Recruitment (4), Claims (3).
  - Seeded all 37 report definitions and column metadata in `ReportCatalogSeeder`.
  - Registered all 37 sources in `Reporting.Api/Program.cs` and added to `ReportSourceTests.Sources`.
  - Updated `ReportLayerCertificationTests` total count to 90 (53 existing + 37 new HRMS/Payroll reports).
  - Extended frontend contracts in `@bill-book/reporting-core` with new report modules and optional `App` flag.

### I · School (S0–S9)

Design: `docs/Modules.md` "School" (from line 2403). The sections to read are Service map (2454),
Columns (2472), Endpoints (2793) and Stages (2884). Every card also carries section 5's standard
delivery sub-tasks, and a new service needs the scaffold steps listed under H.

### TK-60 · S0: School prerequisites
- [x] completed (Claude Opus 5.5) — 2026-09-25 · tests written, not run
- **Issue:** [#2](https://github.com/jothi-prabaharan/Bill-Book/issues/2)
- **Lanes:** L-CON, L-MST, L-DEPS, `L-SCH-APP` (new) · **Depends on:** TK-47, TK-48 · **Decision:** —
- **Sub-tasks:**
  - [x] Add `IsGuardian` to `con.Contact`, with a migration and a role filter on `/api/contacts`.
  - [x] Seed School's permissions, menus and roles (Principal, Office Admin, Accountant, Teacher,
        Maintenance, Viewer) with `App = School`.
  - [x] School signup and a trial licence, reusing TK-45.
  - [x] An empty `apps/school` on `shellRoutes`, mounting the shared master pages and the employee master.
  - [x] Add numbering series `ADM`, `APL`, `FDM`, `FRC` and `WRK`.
- **Done when:** `apps/school` shows only School menus, and a guardian contact can be created and
  filtered.
- **Notes:**
  - **As built (2026-09-25):**
    - `con.Contacts.IsGuardian` (migration `GuardianContacts`), in the role check constraint, a filtered index, `?role=guardian` on `/api/contacts` and quick create. Guardians take the `CUSTOMER` series. The checkbox and **Guardians** filter show only in the School app.
    - Modules `sis`, `admission`, `fee`, `facility`, `workorder`, `preventive`, `amc` (School). **`attendance` is shared with HRMS** (`App.Hrms | App.School`): TK-50 had already seeded it for staff attendance, and since a role and a token each belong to one app, one module row serves both safely. `attendance.unlock` and `workorder.close` are extra permissions outside the grid (ids from 10,001, School only). **`contacts` is now RetailErp and School**, with its rail, section and screen, because guardians and AMC vendors are contacts; `apps/school` mounts the same contacts page.
    - Roles Principal, Office Admin, Accountant, Teacher, Maintenance and Viewer (ids 1,000,101–1,000,106) with the grants in `AdminDbContext.SchoolRoles`.
    - School menus: Students, Fees and Maintenance rails with fourteen items, **seeded inactive**; each stage switches its own rows on.
    - Migration `SchoolRolesAndMenus`. It also carries the `payroll`, `leave` and `attendance` permission rows that TK-51 and TK-50 added to the model without a migration, which would have stopped Master starting on a new database (`PendingModelChangesWarning`, the TK-70 failure).
    - Signup and a trial for School needed nothing new: TK-45 already accepts `School`.
    - **Numbering series**: each is seeded by the service that allocates from it, as Purchase seeds `POR`: `ADM` by Student (TK-61), `APL` by Admission (TK-62), `FDM` and `FRC` by Fee (TK-64), `WRK` by WorkOrder (TK-66).
    - `apps/school` (port 4205), with the shared settings, the employee master and contacts.
    - Tests: `Master.Api.Tests.SchoolSeedTests`, `GuardianContactTests`; `apps/school` routes spec; `AppGrantRuleTests` updated.
    - Owner step: none beyond the tests. `apps/school` is in neither `deploy/azure` nor `deploy/local`, like `apps/hrms`.

### TK-61 · S1: Student (`sis`, port 4515)
- [x] completed (Claude Opus 5.5) — 2026-09-25 · tests written, not run
- **Issue:** [#3](https://github.com/jothi-prabaharan/Bill-Book/issues/3)
- **Lanes:** L-SIS (new) · **Depends on:** TK-60 · **Decision:** —
- **Tables:** `AcademicYear`, `SchoolClass`, `Section`, `Subject`, `Student`, `StudentGuardian`,
  `Enrolment`, `Exam`.
- **Sub-tasks:**
  - [x] Scaffold the service; seed `SchoolClass` LKG–XII per branch.
  - [x] CRUD for years, classes, sections and subjects.
  - [x] Students with guardians, where each guardian is a `con` contact validated through Master's API.
  - [x] Enrolment.
- **Done when:** a student is admitted directly, enrolled in a section and listed; RLS and the
  guard audit pass from a dropped database.
- **Notes:**
  - **As built (2026-09-25):**
    - `backend/Api/Student` (schema `sis`, port 4515, gateway `/api/sis/**`): `AcademicYear`, `SchoolClass`, `Section`, `Subject`, `Student`, `StudentGuardian`, `Enrolment`, `Exam`, `ExamSubject`, `ExamMark`. Migration `InitialSisSchema` with RLS on all eleven tables (ErrorLogs included). Master migrates `sis` into every shard and seeds it for School branches (`HttpTenantSeeder.SchoolServices`).
    - Seed per branch: classes LKG–XII and the `ADM` series (TK-60's numbering sub-task, for Student).
    - Guardians are checked through Master's new `internal/contacts/lookup` (`Shared.Kernel.Contacts.IContactDirectory`, reused by Fee and MaintenanceContract): an active contact of the branch with `IsGuardian`, one or two per student, exactly one primary (a filtered unique index too).
    - `Student.SourceApplicationId` is unique per branch and `StudentService.CreateAsync(…, sourceApplicationId)` returns the existing student, ready for TK-62's idempotent admit.
    - Exams: Planned → MarksOpen → Published (→ MarksOpen for a correction) → Locked; marks only while MarksOpen and only for the class's students in the exam's year. Moving an exam takes `sis.approve`; marks take `sis.edit`.
    - `libs/student/{student-core, student-ui}`: students list, student record (guardians, enrolment), academic setup, exams and marks; mounted in `apps/school`. Menu rows 11, 119, 1111–1113 switched on (migration `SisMenus`).
    - Tests: `Student.Api.Tests` (schema and RLS audit, guard audit, `StudentServiceTests` including the *Done when*, `StudentRuleTests`, `ExamRuleTests`, `ExamServiceTests`); `sis-rules.spec.ts`; `sis.routes.spec.ts`. `Master.Api.Tests.SeedingPerAppTests` gains the School case and is corrected for Payroll, which TK-51 added to the seeder without updating it.
    - Also: Master's `Seeding` config gains `TimeLeave` and `Payroll`, which TK-49/TK-51 left out, so every HRMS or Payroll branch was reported as failing to seed.
    - **`claims` moved to the end of `PermissionModules`** (migration `ClaimsPermissions`). TK-56 inserted it before the School modules, which would have renumbered every School permission TK-60 had migrated: the row-by-row `UpdateData` against the unique `Code` index that stopped Master starting in TK-70. Appended, the School ids stand and `claims` gets the migration it lacked.
    - Owner step: run `Student.Api.Tests` with `STUDENT_TEST_DB` from a dropped database. `Student` is in neither `deploy/azure` nor `deploy/local`, like the other new services.

### TK-62 · S2: Admission (`adm`, port 4516)
- [x] completed (Claude Opus 5.5) — 2026-09-25 · tests written, not run
- **Issue:** [#4](https://github.com/jothi-prabaharan/Bill-Book/issues/4)
- **Lanes:** L-ADMN (new) · **Depends on:** TK-61 · **Decision:** —
- **Tables:** `Enquiry`, `Application`, `ApplicationDocument`.
- **Sub-tasks:**
  - [x] Enquiry → application → `admit`.
  - [x] Admit creates the student through Student's API and the guardian through Master's API, both
        idempotently.
- **Done when:** admitting twice creates one student.
- **Notes:**
  - **As built (2026-09-25):**
    - `backend/Api/Admission` (schema `adm`, port 4516, gateway `/api/admission/**`): `Enquiry`, `Application`, `ApplicationDocument`; migration `InitialAdmissionSchema` with RLS on all four tables; seeds the `APL` series (yearly). Seeded for School branches after Student.
    - **`Application` gains guardian columns** (name, mobile, email, relationship, `GuardianContactId`) plus `ChildGender` and `AdmissionNo`, which the design lacks: admit has to make a guardian, and an application need not come from an enquiry.
    - Stages move forward to Offered (skipping allowed), to Rejected/Withdrawn from any open stage, and to Admitted only through admit. DocumentsVerified needs every recorded document verified; Assessed needs a score.
    - **Admit** is idempotent end to end: the guardian through Master's new `internal/contacts/guardians/ensure` (matched on mobile number, created with `IsGuardian` and its sub-ledger otherwise), the student through Student's new `internal/sis/students/admit` (keyed on `SourceApplicationId`, TK-61). An admitted application returns its student again. Student's `internal/sis/academic-check` validates year, class and section ids for both services. Contracts in `Shared.Kernel.School` and `Shared.Kernel.Contacts`.
    - `libs/admission/{admission-core, admission-ui}`: enquiries, applications list, application record with stage moves and Admit; mounted in `apps/school`; menus 1114–1115 switched on (migration `AdmissionMenus`).
    - Tests: `Admission.Api.Tests` (`AdmitTests`, including *Done when* "admitting twice creates one student", a retry after a failure halfway, siblings sharing a guardian; `ApplicationStageTests`; schema, RLS and guard audits); `admission-rules.spec.ts`; `admission.routes.spec.ts`.
    - Owner step: run `Admission.Api.Tests` with `ADMISSION_TEST_DB` from a dropped database, and admit one application end to end with Master and Student running.

### TK-63 · S3: Student attendance (`att`, port 4517)
- [x] completed (Claude Opus 5.5) — 2026-09-25 · tests written, not run
- **Issue:** [#5](https://github.com/jothi-prabaharan/Bill-Book/issues/5)
- **Lanes:** L-ATT (new) · **Depends on:** TK-61 · **Decision:** —
- **Tables:** `StudentAttendance`, `AttendanceLock`.
- **Sub-tasks:**
  - [x] A daily register per section, taking the roll from Student.
  - [x] `lock` and `unlock`, where unlocking needs `attendance.unlock`.
  - [x] The register component: ask before building it if `ui-components` lacks one.
- **Done when:** a locked day refuses an edit from a teacher and accepts one from `attendance.unlock`.
- **Notes:**
  - **Backend built (2026-09-25):** `backend/Api/Attendance` (schema `att`, port 4517, gateway `/api/student-attendance/**`, kept apart from HRMS's `/api/tla/attendance`): `StudentAttendance` (one mark per enrolment per day) and `AttendanceLock`; migration `InitialAttendanceSchema` with RLS on all three tables. The roll comes from Student's new `internal/sis/sections/roll`, read at the moment of use; a day must be inside the section's open school year and not in the future. Saving a locked day, or locking it, needs `attendance.unlock` (423 otherwise); unlocking demands it on the route. Tests: `Attendance.Api.Tests.RegisterTests` (the *Done when*), `RegisterRuleTests`, schema, RLS and guard audits.
  - **The register component, asked and answered 2026-09-25: a new shared component.** `bb-attendance-register` in `libs/shared/ui-components` takes entries and the statuses (with a key, glyph and tone each), cycles a tile on a tap, sets a status from its key and moves on, and moves focus with the arrow keys; its rules are pure (`attendance-register.model.ts`) and tested (`attendance-register.model.spec.ts`). It is generic, so HRMS can reuse it. Documented in `inputs.md`.
  - `libs/student-attendance/{student-attendance-core, student-attendance-ui}`: the register page (section, day, All present, Save, Lock, Unlock) on the component; mounted in `apps/school`; menu 1116 switched on (migration `StudentAttendanceMenu`). `register-rules.spec.ts`, `student-attendance.routes.spec.ts`.
  - Owner step: run `Attendance.Api.Tests` with `ATTENDANCE_TEST_DB` from a dropped database, and take one register at 360px.

### TK-64 · S4: Fee (`fee`, port 4518)
- [x] completed (Claude Opus 5.5) — 2026-09-25 · tests written, not run
- **Issue:** [#6](https://github.com/jothi-prabaharan/Bill-Book/issues/6)
- **Lanes:** L-FEE (new) · **Depends on:** TK-61 · **Decision:** —
- **Tables:** `FeeHead`, `FeeStructure`, `FeeConcession`, `FeeDemand`, `FeeReceipt`.
- **Sub-tasks:**
  - [x] Seed fee heads.
  - [x] Structures per class, concessions, and demand generation per enrolment (idempotent).
  - [x] Receipts and allocation.
  - [x] Post the demand (Dr the guardian's AR sub-account, Cr fee income) and the receipt through
        Accounting's internal API.
- **Done when:** a demand and its receipt post balanced journals, and the guardian's AR
  sub-account ties to the open demands.
- **Notes:**
  - **As built (2026-09-25):**
    - `backend/Api/Fee` (schema `fee`, port 4518, gateway `/api/fee/**`): `FeeHead`, `FeeStructure`, `FeeStructureLine`, `FeeConcession`, `FeeDemand`, `FeeDemandLine`, `FeeReceipt`, `FeeReceiptAllocation`; migration `InitialFeeSchema` with RLS on all nine tables. Seeds five heads and the `FDM` and `FRC` series (yearly, gapless: a demand is numbered when posted, so a draft takes no number).
    - Differences from the design: `FeeHead.IncomeAccountId` is **nullable**, meaning "Fee Income" (or "Refundable Deposits" for a refundable head), so a new branch's heads post without anyone choosing an account; `FeeStructure.FirstMonth` says which month frequencies count from; `FeeDemand` carries `FeeStructureId`, `PeriodKey` (the idempotency key with the enrolment) and `PaidAmount`; there is no `JournalId`, because the ledger is keyed on the document (`FDM`/`FRC` + id).
    - **Accounting (outside this card's lane, needed for posting):** `SystemAccount.FeeIncome` (4300), `DiscountGiven` (4250, contra Income) and `RefundableDeposits` (2400) in every branch's chart; `internal/accounts/lookup` and `internal/accounts/bank-accounts` (`Shared.Kernel.Ledgers.IAccountDirectory`). Master: transaction types `FDM` and `FRC` (migration `FeeTransactionTypes`).
    - Student: `internal/sis/enrolments` (by year and class, by id, or by guardian for TK-69), with each enrolment's primary guardian.
    - Postings go through `internal/ledger/postings`, which replaces by document, so posting again after a rollback lands once. Demand: Dr Accounts Receivable (guardian, trade) net / Cr each head's income in full / Dr Discount Given per concession. Receipt: Dr the bank / Cr Accounts Receivable (trade) for what it settles / Cr the overpayment advance for the rest. A demand's `PaidAmount` moves only by a guarded `ExecuteUpdate`. Voids withdraw the document's rows.
    - Not done: GST on a taxable head (SAC stored only), applying an advance to a later demand, and checking a concession's `StudentId` through Student.
    - `libs/fee/{fee-core, fee-ui}`: setup, demands and receipts; mounted in `apps/school`; menus 12, 120, 1117–1119 on (migration `FeeMenus`).
    - Tests: `Fee.Api.Tests.FeeServiceTests` (the *Done when*: balanced postings and the receivable tying to open demands; the advance; idempotent generation; concessions; voids; a refused posting under the request transaction) and `FeeRuleTests`; `Accounting.Api.Tests.FeeAccountsSeedTests`; `fee-rules.spec.ts`, `fee.routes.spec.ts`.
    - Owner step: run `Fee.Api.Tests` with `FEE_TEST_DB` from a dropped database, and post one demand and one receipt with Accounting running, then check the guardian's ledger.

### TK-65 · S5: Facility (`fac`, port 4519)
- [x] completed (Claude Opus 5.5) — 2026-09-25 · tests written, not run
- **Issue:** [#7](https://github.com/jothi-prabaharan/Bill-Book/issues/7)
- **Lanes:** L-FAC (new) · **Depends on:** TK-60 · **Decision:** —
- **Tables:** `Building`, `Space`, `FacilityAsset`.
- **Sub-tasks:**
  - [x] CRUD, with the hierarchy: building → space → asset.
- **Done when:** buildings, spaces and assets can be created, listed and deactivated.
- **Notes:**
  - **As built (2026-09-25):** `backend/Api/Facility` (schema `fac`, port 4519, gateway `/api/facility/**`): `Building`, `Space`, `FacilityAsset`, migration `InitialFacilitySchema` with RLS on all four tables. Codes and tags unique per branch; a space needs an active building and a floor below its top; a building deactivates only after its spaces; a disposed asset stays disposed. `internal/facility/lookup` (`Shared.Kernel.School.IFacilityClient`) for TK-66–TK-68. Nothing to seed; the seed endpoint answers the fan-out. `libs/facility/{facility-core, facility-ui}`: buildings and spaces, and assets; menus 13, 121, 1120–1121 on (migration `FacilityMenus`). Tests: `Facility.Api.Tests.FacilityServiceTests` (the *Done when*), `FacilityRuleTests`, schema, RLS and guard audits; `facility.models.spec.ts`, `facility.routes.spec.ts`. Owner step: run `Facility.Api.Tests` with `FACILITY_TEST_DB` from a dropped database.

### TK-66 · S6: WorkOrder (`wrk`, port 4520)
- [x] completed (Claude Opus 5.5) — 2026-09-25 · tests written, not run
- **Issue:** [#8](https://github.com/jothi-prabaharan/Bill-Book/issues/8)
- **Lanes:** L-WRKO (new) · **Depends on:** TK-65 · **Decision:** —
- **Tables:** `WorkOrder`, `WorkOrderTask`, `WorkOrderPart`.
- **Sub-tasks:**
  - [x] The lifecycle, through `assign`, `complete` and `close`. An Assigned work order can't be edited.
  - [x] The assignee is an employee, validated through `Employee`.
  - [x] Parts are issued through Inventory's issue API.
- **Done when:** editing an Assigned work order is refused, and issuing a part moves stock.
- **Notes:**
  - **As built (2026-09-25):** `backend/Api/WorkOrder` (schema `wrk`, port 4520, gateway `/api/work-orders/**`): `WorkOrders` (entity `WorkOrderDocument`, because `WorkOrder` is the namespace), `WorkOrderTasks`, `WorkOrderParts`; migration `InitialWorkOrderSchema` with RLS on all four tables. Checks: an asset or a space, a completion date once Completed or Closed, labour cost not negative.
    - Lifecycle in `WorkOrderLifecycle`: Open → Assigned (re-assignable) → InProgress ⇄ OnHold → Completed → Closed; Open or Assigned → Cancelled with a reason, and not once a part is issued. Only Open is editable. Close is its own route with `[PermissionAction("close")]`, so it takes `workorder.close` (seeded by TK-60 for the Principal).
    - Where and who: the asset and space through Facility's `internal/facility/lookup`; the assignee through Employee's `internal/employees/lookup` (new `Shared.Kernel.Employees.IEmployeeDirectory`), refusing an exited employee.
    - Parts: the part row is saved first and its id is the `SourceLineId` of Inventory's `internal/stock/issue` under `SourceType = WRK`, so a retried issue moves stock once and posts Dr COGS / Cr Inventory under the new `WRK` transaction type. A refusal answers 409 and the request's transaction takes the part row back. New `Shared.Kernel.Stock.IStockClient`, and Inventory's `internal/items/search` and `internal/items/warehouses` for the pickers.
    - `internal/work-orders/raise` for TK-67 and TK-68: idempotent on `SourceKey` (a filtered unique index).
    - Seed: the `WRK` series per School branch (`HttpTenantSeeder.SchoolServices`).
    - `libs/work-order/{work-order-core, work-order-ui}`: one page, list and record (checklist, actions, parts); menu 1122 on and `WRK` added (migration `WorkOrderTypeAndMenu`).
    - Tests: `WorkOrder.Api.Tests.WorkOrderServiceTests` (the *Done when*, with Facility, Employee and Inventory faked), `WorkOrderLifecycleTests`, schema, RLS and guard audits; `work-order.models.spec.ts`, `work-order.routes.spec.ts`; `apps/school` routes spec.
    - Raised **D-28**: a School-only customer has no Inventory seeding or item screens, so it has nothing to issue as a part.
    - Owner step: run `WorkOrder.Api.Tests` with `WORKORDER_TEST_DB` from a dropped database.

### TK-67 · S7: Preventive (`ppm`, port 4521)
- [x] completed (Claude Opus 5.5) — 2026-09-25 · tests written, not run
- **Issue:** [#9](https://github.com/jothi-prabaharan/Bill-Book/issues/9)
- **Lanes:** L-PPM (new) · **Depends on:** TK-66 · **Decision:** —
- **Tables:** `PreventivePlan`, `PreventiveOccurrence`.
- **Sub-tasks:**
  - [x] Plans with a recurrence.
  - [x] A hosted service generates occurrences and raises work orders, with (plan, due date) as
        the idempotency key.
- **Done when:** running generation twice raises one work order per occurrence.
- **Notes:**
  - **As built (2026-09-25):** `backend/Api/Preventive` (schema `ppm`, port 4521, gateway `/api/preventive/**`): `PreventivePlans`, `PreventiveOccurrences`, migration `InitialPreventiveSchema` with RLS on all three tables. Checks: an asset or a space, end not before start, interval ≥ 1 and lead days ≥ 0.
    - `Recurrence` (pure): month-based frequencies count from the start date, so the 31st stays the 31st after a short month.
    - Generation (`PreventiveService.GenerateAsync`): each due date is claimed by advancing the plan's `NextDueDate` with a guarded `ExecuteUpdate` (row count is the answer) and the occurrence is inserted in the same transaction, unique on plan and due date. Work orders are then raised for Scheduled occurrences through WorkOrder's `internal/work-orders/raise` (new `Shared.Kernel.School.IWorkOrderClient`) under source key `PPM:{plan}:{yyyy-MM-dd}`, so a run that dies between the two raises nothing twice. At most 60 occurrences per plan per run.
    - `PreventiveGenerator` walks every branch hourly (`ITenantEnumerator`, as the payment reminders do); `POST api/preventive/generate` runs the caller's branch now. Occurrences can be skipped (Scheduled) or marked done (Raised), by a guarded update.
    - Changing a plan's start, frequency or interval restarts `NextDueDate` after the last generated date.
    - `libs/preventive/{preventive-core, preventive-ui}`: plans and occurrences on one page; menu 1123 on (migration `PreventiveMenu`).
    - Tests: `Preventive.Api.Tests.PreventiveServiceTests` (the *Done when*, a failed raise retried once, a raise already made not repeated, lead days and end date, skip, schedule change), `RecurrenceTests`, schema, RLS and guard audits; `preventive.models.spec.ts`, `preventive.routes.spec.ts`.
    - Owner step: run `Preventive.Api.Tests` with `PREVENTIVE_TEST_DB` from a dropped database.

### TK-68 · S8: AMC (`amc`, port 4522)
- [x] completed (Claude Opus 5.5) — 2026-09-25 · tests written, not run
- **Issue:** [#10](https://github.com/jothi-prabaharan/Bill-Book/issues/10)
- **Lanes:** L-AMC (new) · **Depends on:** TK-65 · **Decision:** —
- **Tables:** `AmcContract`, `AmcCoveredAsset`, `AmcVisit`.
- **Sub-tasks:**
  - [x] Contracts, where the vendor is a `con` contact validated through Master.
  - [x] Covered assets.
  - [x] Visits, which may raise a work order.
  - [x] Renewal reminders through Notification.
- **Done when:** a contract's covered assets and visits are recorded, and a renewal reminder fires
  before expiry.
- **Notes:**
  - **As built (2026-09-25):** `backend/Api/MaintenanceContract` (schema `amc`, port 4522, gateway `/api/amc/**`): `AmcContracts`, `AmcCoveredAssets`, `AmcVisits`, migration `InitialAmcSchema` with RLS on all four tables. Checks: end after start, value not negative, a terminated contract carries its reason; contract number unique per vendor.
    - Added to the design: `AmcContract.ReminderEmail` (who the reminder is written to, since the design named nobody), `TerminationReason`, `Remarks`, and `AmcVisit.FacilityAssetId` (a work order needs an asset, and it must be one the contract covers).
    - Lifecycle: Draft → Active (needs at least one asset, none under another Active contract whose term overlaps) → Terminated with a reason; Expired shown once the end date passes and stored by the renewal read. An Active contract's terms are fixed; its assets, reminder and remarks may change.
    - Vendor through Master's `internal/contacts/lookup` (active, `IsVendor`); assets through Facility. A visit is saved first and raises its work order through WorkOrder under `AMC:{visitId}`.
    - Renewals: `internal/amc/renewals-due` returns Active contracts inside their reminder window. `Notification.Worker` gains `AmcRenewalReminderRun` and a daily `AmcRenewalReminderWorker` over every branch; each reminder goes through `EmailRequestHandler` under a message id fixed by branch, contract and end date, so a contract is reminded once per term (new `Shared.Kernel.School.IAmcRenewals`; `MaintenanceContract:BaseUrl` in the worker's settings).
    - `libs/maintenance-contract/{maintenance-contract-core, maintenance-contract-ui}`: contracts list, edit sheet with covered assets, record view with activate, terminate and visits; menu 1124 on (migration `AmcMenu`).
    - Tests: `MaintenanceContract.Api.Tests.MaintenanceContractServiceTests` (the *Done when*'s recording half, one active contract per asset, vendor and asset checks, fixed terms, visit rules, renewal window and expiry, termination), `AmcRulesTests`, schema, RLS and guard audits; `Notification.Worker.Tests.AmcRenewalReminderRunTests` (the reminder fires once before expiry); `amc.models.spec.ts`, `amc.routes.spec.ts`.
    - Owner step: run `MaintenanceContract.Api.Tests` with `MAINTENANCE_CONTRACT_TEST_DB` and `Notification.Worker.Tests` from dropped databases.

### TK-69 · S9: Parent portal
- [x] completed (Claude Opus 5.5) — 2026-09-25 · tests written, not run
- **Issue:** [#11](https://github.com/jothi-prabaharan/Bill-Book/issues/11)
- **Lanes:** L-PTL · **Depends on:** TK-63, TK-64, TK-32 · **Decision:** —
- **Sub-tasks:**
  - [x] Add routes in `apps/portal` for demands, receipts, attendance and published marks.
  - [x] Controllers take `[RequirePortalAccess]`, and the guardian's link comes from
        `JwtTokenService.CreatePortalToken` against their `ContactId`.
- **Done when:** a guardian sees their child's demands, receipts, attendance and published marks.
- **Notes:**
  - Dependency on TK-32 added 2026-09-24: the parent portal adds to `apps/portal`, whose next screens TK-32 designs first.
  - **As built (2026-09-25):**
    - **The portal token names its app.** `CreatePortalToken` takes the app, and `POST api/contacts/{id}/portal-link` passes the caller's (`RequireAppAttribute.AppOf`), so a link made in the School app carries `app = School` and a RetailErp link keeps its old shape (no claim, read as RetailErp). Without it every guardian token read as RetailErp and School's `[RequireApp]` refused it.
    - **Three portal controllers, each in the service that owns the data**, all `[Authorize][RequirePortalAccess][RequireApp(App.School)]`, the contact taken from the token and never from the route:
      - Student `api/portal/school/children` (children with their latest class and section) and `…/{studentId}/marks` (Published or Locked exams only) — `PortalService`. A child is the guardian's only through a `StudentGuardian` row with `HasPortalAccess`; anything else is not found.
      - Fee `api/portal/school/fees/demands` and `…/receipts` — `PortalFeeService`: posted documents addressed to the contact (so the primary guardian), with balances, head names and the demands each receipt settled.
      - Attendance `api/portal/school/attendance/{studentId}?month=` — `PortalAttendanceService` asks Student for the guardian's enrolments (`EnrolmentQueryRequest.PortalAccessOnly`, new) and shows only those days, with counts.
      - Gateway routes for the three paths.
    - `apps/portal`: `/portal?token=` (the link's own path, which nothing handled before) keeps the token in `PortalSession` and opens `/school` for a School token, `/dashboard` otherwise; `portalTokenInterceptor` sends it on `/api/portal/` calls. `/school` (children, fees, payments) and `/school/children/:studentId` (attendance by month, published marks). The RetailErp statement pages benefit too: they were only reachable with a staff login.
    - Contacts screen: **Portal link** on an open contact, showing the link or, with no `Portal:BaseUrl`, the token.
    - Still the 30-day, unrevocable token: TK-94 replaces it with revocable grants and one-hour sessions, and these routes need no change for it.
    - Tests: `Student.Api.Tests.PortalServiceTests` (the *Done when* for children and marks, access flag, strangers), `Fee.Api.Tests.PortalFeeTests` (posted only, own only, balances, settlements), `Attendance.Api.Tests.PortalAttendanceTests` (own child's month and counts, another child not found, Student down), `Master.Api.Tests.PortalTokenTests` (app claim, no permissions); `portal-session.spec.ts`, `school-portal.models.spec.ts`.
    - Owner step: run `Student.Api.Tests`, `Fee.Api.Tests`, `Attendance.Api.Tests` and `Master.Api.Tests` from dropped databases.

### Z · Done — waiting on the owner's test run

Finished cards, in ID order. A card marked `tests written, not run` needs the owner to run its
tests (section 0.5); the owner then adds `· tests passed (owner) — date`, or moves the card back
into the queue with the failure under its Notes.

### TK-70 · Master fails to start on a fresh database
- [x] completed (Claude Opus 5.5) — 2026-09-23 · tests written, not run
- **Issue:** [#12](https://github.com/jothi-prabaharan/Bill-Book/issues/12)
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
- **Issue:** [#13](https://github.com/jothi-prabaharan/Bill-Book/issues/13)
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
- **Issue:** [#69](https://github.com/jothi-prabaharan/Bill-Book/issues/69)
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
- **Issue:** [#70](https://github.com/jothi-prabaharan/Bill-Book/issues/70)
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
- **Issue:** [#71](https://github.com/jothi-prabaharan/Bill-Book/issues/71)
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
- **Issue:** [#72](https://github.com/jothi-prabaharan/Bill-Book/issues/72)
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
- **Issue:** [#73](https://github.com/jothi-prabaharan/Bill-Book/issues/73)
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
- **Issue:** [#74](https://github.com/jothi-prabaharan/Bill-Book/issues/74)
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
- **Issue:** [#75](https://github.com/jothi-prabaharan/Bill-Book/issues/75)
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
- **Issue:** [#76](https://github.com/jothi-prabaharan/Bill-Book/issues/76)
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
- **Issue:** [#77](https://github.com/jothi-prabaharan/Bill-Book/issues/77)
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

**D-01 to D-21 were answered by 25 September 2026.** A new question gets the next number (D-30).

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
| D-21 | How does an invoice move a challan's goods out of Goods Delivered Not Invoiced into cost of sales? **(a) Full link:** `sal.InvoiceChallanAllocations` records which challan lines each invoice line billed (oldest first for order-billed goods), so a void reverses exactly; `inv.StockMovementBillings` lets the worker re-post each invoice's Dr COGS / Cr GDNI at the settled cost and after any restatement, so GDNI stays at zero. **(b) Cost at invoice time:** only the `sal` table; the invoice clears GDNI at whatever cost Inventory holds when it posts, and a later restatement leaves a small GDNI balance. | TK-90 | **(b), cost at invoice time** (owner, 2026-09-25): the invoice clears GDNI at whatever cost Inventory holds when it posts. No allocation-link tables — a void reverses the invoice's own entry, and a later WAC restatement of the challan's cost can leave a small residual GDNI balance rather than being reconciled back to zero. TK-90 |
| D-22 | Archived PDFs: how to reach PDF/A-2b, and render from the print template? PDFsharp 6.1.1 (D-11's pin) has no PDF/A API and cannot lay out HTML. Options: **(a)** move to a later PDFsharp with PDF/A support and keep the fixed layout; **(b)** hand-build PDF/A (XMP metadata, sRGB output intent, embedded fonts) on 6.1.1; **(c)** add an HTML-to-PDF engine to Printing so the archive is the template's own output | TK-22 | *Open.* Raised 2026-09-24 by TK-22 |
| D-23 | Does a **General** branch get the metal purities? The `Vertical` enum and master.md 5.14 say yes (General is the everything branch); TK-30's card asks that a General branch get none. | TK-30 | *Open.* Raised 2026-09-24 by TK-30, which kept the recorded answer (General gets everything) |
| D-24 | E-invoicing and e-way bill: reach the IRP through a GST Suvidha Provider (which one), or NIC's direct API? The design (TK-31) is written against an interface either can fill. | TK-91 | *Open.* Raised 2026-09-24 by TK-31 |
| D-25 | Client portal online payments: which gateway — Paytm (named in the roadmap), Razorpay, PayU, Cashfree or another? The design (TK-32) records a receipt only on the gateway's verified callback, whichever it is. | TK-98 | *Open for the real provider.* Raised 2026-09-24 by TK-32. **Owner, 2026-09-26: build TK-98 now against a sandbox `IPaymentGateway`**, the way e-invoicing waits on D-24; the real provider is added when this is answered |
| D-29 | Which discount limit raises a `SalesDiscountOverride` (TK-102)? The design says "the branch's limit", but no branch has one; the only limit is `Contact.MaxDiscountPercent`, which Sales never enforced. | TK-102 | **The contact's, else the branch's** (owner, 2026-09-26): a branch setting for the maximum line discount %, overridden by the contact's `MaxDiscountPercent` when that is set. Sales enforces both, and a save past the limit offers "request approval" |
| D-26 | Approvals: move the approval engine's configuration and chain resolution from `Employee` (as TK-49 plans) to Master, with `Employee` answering only the employee-based approver kinds? RetailErp is sold without HRMS and has no employees, so a Employee-only engine cannot serve it (TK-33). | TK-99, TK-49 | **Master, `apr`** (owner, 2026-09-24). The state machine and step shape are in `Shared.Kernel.Approvals`; workflow configuration and chain resolution are Master's, in tenant schema `apr`; `Employee` answers only the employee-based approver kinds. TK-99 is unblocked, and TK-49 builds on it |
| D-27 | CRM campaigns: send bulk email through a transactional email provider (Amazon SES, SendGrid, Postmark or another — which, and on whose account), or through each branch's own SMTP with a low daily cap? A branch mailbox would be rate-limited and risks blacklisting (TK-38). | TK-121 | *Open.* Raised 2026-09-24 by TK-38 |
| D-28 | School work-order parts: a School-only customer has no Inventory. Its branches are not seeded for Inventory and `apps/school` has no item or store screens, so the part picker is empty. Options: **(a)** seed Inventory for School branches and mount the item and warehouse screens in `apps/school` under Maintenance; **(b)** let a School work order record a part as free text with a cost, with no stock kept; **(c)** issue parts only for customers who also hold RetailErp. | work-order parts for School-only customers | *Open.* Raised 2026-09-25 by TK-66, which issues through Inventory as its card says |

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
