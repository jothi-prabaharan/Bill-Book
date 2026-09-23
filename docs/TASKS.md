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
| `- [x] completed (AI name) — YYYY-MM-DD` | Done and checked against its **Done when** line. Compiling is not enough |
| `- [!] blocked — D-xx` or `- [!] blocked — reason` | Can't start yet. Waiting on an owner decision (section 3), or on a problem written in the card's Notes |

- **AI name** is the model that did the work, as `get_session` reports it (e.g. `Claude Opus 5.5`,
  `Claude Sonnet 5`). A human uses their own name.
- **Tick a card in the same commit as the work that finishes it.** Tick sub-tasks as you finish
  them.
- **Never delete a card.** If a card is wrong or not needed, strike it through (`~~…~~`), give
  one line of reason and move it to section 4.
- **Card IDs never change.** To change priority, move the card and keep its `TK-nn`. A new card
  takes the next unused number, even if it goes in the middle of the queue.

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
normal for agent 1 to be on TK-01 while agent 2 is on TK-05.

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
- **Done**: tick it in the same commit as the last piece of work, then push.
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
- **Touches:** paths
- **Sub-tasks:**
  - [ ] …
- **Done when:** the test that proves it
- **Notes:** handovers and findings
```

Cards that build a feature (the HRMS, Payroll and School stages) also carry the **standard
delivery** sub-tasks in section 5.

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

### A · Blockers: builds, startup and security

### TK-01 · Master fails to start on a fresh database
- [ ] open
- **Lanes:** L-MST · **Depends on:** — · **Decision:** —
- **Touches:** `backend/Api/Master/Master.Repository/Migrations` (admin), the `mst.Menus` / `mst.MenuPermissions` `HasData`
- **The problem:** `AdminDbContext` has drifted from its migrations, so `MigrateAsync` throws
  `PendingModelChangesWarning`. No host starts on an empty database. Adding one more migration
  doesn't help: EF emits 379 `UpdateData` calls, and one of them collides on
  `IX_MenuPermissions_MenuId_PermissionCode`.
- **Sub-tasks:**
  - [ ] Check whether the menu seed is still mid-change on `main` before squashing.
  - [ ] Re-squash the admin migration chain.
  - [ ] `dotnet ef migrations has-pending-model-changes` reports clean.
  - [ ] Drop `ADMIN_TEST_DB` and `CONTACTS_TEST_DB`, then run `dotnet build && dotnet test` for Master.
  - [ ] Update the "Master cannot start" caveat in `CLAUDE.md` in the same commit. Taking `L-DOC`
        for that one commit is allowed.
- **Done when:** Master starts against dropped and recreated databases, and its suite is green.
- **Notes:**

### TK-02 · RLS template: restore it in `acc`
- [ ] open
- **Lanes:** L-ACC · **Depends on:** — · **Decision:** —
- **Touches:** a new `acc` migration, `Accounting.Api.Tests`
- **The problem:** `ENABLE`, `FORCE` and `CREATE POLICY` appear in no migration except `prt`'s.
  The squash on 14 September dropped them. Right now the EF query filter is the only guard.
- **Sub-tasks:**
  - [ ] Recover the policy text from any developer database that hasn't been dropped:
        `SELECT tablename, policyname, qual FROM pg_policies WHERE schemaname = 'acc'`.
  - [ ] Write one migration covering every `acc` table, shaped like `prt`'s migration.
  - [ ] Write the template down in this card's Notes: the migration shape, how the table list is
        derived, and how exemptions are named. TK-03 to TK-08 copy it.
  - [ ] Drop `ACCOUNTING_TEST_DB` and run the suite. Then drop one policy by hand and watch
        `RlsAudit` fail.
- **Done when:** `acc`'s RLS assertions pass against a dropped database, and fail when a policy is
  removed.
- **Notes:**

### TK-03 · RLS for `con`
- [ ] open
- **Lanes:** L-CON · **Depends on:** TK-01, TK-02 · **Decision:** —
- **Sub-tasks:** [ ] migration per TK-02's template · [ ] suite green from dropped `CONTACTS_TEST_DB`
- **Done when:** `con`'s RLS assertions pass against a dropped database. If any `con` table has
  no `CustomerId` or `OrgId`, stop and write it in Notes before going on.
- **Notes:**

### TK-04 · RLS for `cus`
- [ ] open
- **Lanes:** L-CUS · **Depends on:** TK-02 · **Decision:** —
- **Sub-tasks:** [ ] migration per TK-02's template · [ ] suite green from a dropped database
- **Done when:** as TK-03, for `cus`.
- **Notes:**

### TK-05 · RLS for `inv`
- [ ] open
- **Lanes:** L-INV · **Depends on:** TK-02 · **Decision:** —
- **Sub-tasks:** [ ] migration per TK-02's template · [ ] suite green from dropped `INVENTORY_TEST_DB`
- **Done when:** as TK-03, for `inv`.
- **Notes:**

### TK-06 · RLS for `pur`
- [ ] open
- **Lanes:** L-PUR · **Depends on:** TK-02 · **Decision:** —
- **Sub-tasks:** [ ] migration per TK-02's template · [ ] suite green from dropped `PURCHASE_TEST_DB`
- **Done when:** as TK-03, for `pur`.
- **Notes:**

### TK-07 · RLS for `sal`
- [ ] open
- **Lanes:** L-SAL · **Depends on:** TK-02 · **Decision:** —
- **Sub-tasks:** [ ] migration per TK-02's template, including `sal.SalesRegister`, which has been
  missed before · [ ] suite green from dropped `SALES_TEST_DB`
- **Done when:** as TK-03, for `sal`.
- **Notes:**

### TK-08 · RLS for `rpt`
- [ ] open
- **Lanes:** L-RPT · **Depends on:** TK-02 · **Decision:** —
- **Sub-tasks:** [ ] migration per TK-02's template, leaving out `rpt.ReportMasters` and
  `rpt.ReportColumns` (they have no tenant column, and the exemption is documented) · [ ] suite
  green from dropped `REPORTING_TEST_DB`
- **Done when:** as TK-03, for `rpt`.
- **Notes:**

### TK-09 · Review of the RLS work
- [ ] open
- **Lanes:** L-DOC · **Depends on:** TK-02 … TK-08 · **Decision:** —
- **Sub-tasks:**
  - [ ] Read every RLS migration from TK-03 to TK-08 against TK-02's template.
  - [ ] Drop all seven test databases and run the whole backend suite: 0 RLS failures.
  - [ ] In one schema, drop one policy by hand and confirm the suite goes red.
  - [ ] Rewrite the FORCE bullet in `CLAUDE.md`'s standing caveats to describe what is true now.
- **Done when:** the policy you dropped by hand turns the suite red, and restoring it turns the
  suite green again.
- **Notes:**

### TK-10 · `ReportLayerCertificationTests`: 4 failures, already red on `main`
- [ ] open
- **Lanes:** L-RPT · **Depends on:** — · **Decision:** —
- **Sub-tasks:**
  - [ ] Compare the test's expected count and list with `docs/Modules.md` §8.2. That section records
        41 of the 46 reports implemented and 7 beyond them.
  - [ ] Find out which side is stale before changing either. Don't just raise the expected number.
  - [ ] Suite green from a dropped `REPORTING_TEST_DB`.
- **Done when:** `Reporting.Api.Tests` has 0 failures.
- **Notes:** Shares `L-RPT` with TK-08, so these two run one after the other.

### TK-11 · Correct the stale facts in `CLAUDE.md`
- [ ] open
- **Lanes:** L-DOC · **Depends on:** — · **Decision:** —
- **Sub-tasks:**
  - [ ] Reporting: "twelve of the 46 not built" should be five (4 fixed-asset reports and
        *Business Performance*). The 7 settlement reports are built (Modules.md §8.2).
  - [ ] Workers: "Notification.Worker … nothing else" is wrong, since it has a
        `PaymentReminderWorker`.
  - [ ] Numbering series: "Sales and Purchase seed theirs when those services land" is done.
        `SeedData/NumberingSeriesSeed.cs` exists in both.
  - [ ] "There is no `Modules.md`" is wrong: `docs/Modules.md` exists and is the per-module file.
- **Done when:** each of these statements in `CLAUDE.md` matches the code.
- **Notes:**

### B · Phase 1: finish what's in flight

### TK-12 · Sales delivery challan: prove it saves
- [ ] open
- **Lanes:** L-SAL · **Depends on:** — · **Decision:** —
- **Sub-tasks:**
  - [ ] Write a create → save → reload test through the service, the way `SalesSchemaTests` does.
  - [ ] If the test finds a shadow-FK column (`…Id1`), bind the navigation in
        `BindDocumentLineNavigations`.
  - [ ] Confirm the form posts, and that list and detail show the saved challan.
- **Done when:** a challan round-trips through the service against a real PostgreSQL.
- **Notes:**

### TK-13 · Sales credit note: prove it saves
- [ ] open
- **Lanes:** L-SAL · **Depends on:** — · **Decision:** —
- **Sub-tasks:** same three as TK-12, for credit notes. If the credit note's posting
  or stock return turns out to be wrong, write it in Notes and raise a new card rather than widening this one.
- **Done when:** a credit note round-trips through the service against a real PostgreSQL.
- **Notes:**

### TK-14 · Partial fulfilment (T3.6)
- [ ] open
- **Lanes:** L-SAL (plus L-INV if the reservation API changes) · **Depends on:** TK-12 · **Decision:** —
- **Sub-tasks:**
  - [ ] Advance `DeliveredQuantity` when a challan posts.
  - [ ] Advance `InvoicedQuantity` when an invoice posts.
  - [ ] Make `FulfilmentStatus.PartlyDelivered` reachable, and test it.
  - [ ] Fix `ReleaseReservation`. It is keyed off `SalesOrderId.HasValue`, which has no control on
        the form, so a challan raised from the screen never releases its reservation
        (Modules.md ~line 451).
- **Done when:** an order is delivered and billed in two parts, its status goes Open →
  PartlyDelivered → Delivered, and its reservation is released.
- **Notes:**

### TK-15 · Item lookup endpoint
- [ ] open
- **Lanes:** L-INV · **Depends on:** — · **Decision:** —
- **Sub-tasks:** [ ] search by name, SKU or barcode, paged and scoped to the branch · [ ] guard
  attribute, so the `EndpointGuardAudit` test passes · [ ] tests
- **Done when:** a search returns only the caller's branch's items.
- **Notes:**

### TK-16 · Contact lookup endpoint
- [ ] open
- **Lanes:** L-CON · **Depends on:** TK-01 · **Decision:** —
- **Sub-tasks:** [ ] search by name, code, GSTIN or phone, filtered by role (customer or vendor) ·
  [ ] guard · [ ] tests
- **Done when:** as TK-15, for contacts.
- **Notes:**

### TK-17 · Picker component, used on the sales forms
- [ ] open
- **Lanes:** L-UI, L-SAL-UI · **Depends on:** TK-15, TK-16 · **Decision:** —
- **Sub-tasks:** [ ] a shared typeahead picker in `libs/shared/ui-components` that works at 360px ·
  [ ] replace the numeric id fields on the quote, order, invoice, challan and credit-note forms ·
  [ ] docs page and release-notes bullet
- **Done when:** every sales form picks its item and customer by name.
- **Notes:**

### TK-18 · Picker on the purchase forms
- [ ] open
- **Lanes:** L-PUR-UI · **Depends on:** TK-17 · **Decision:** —
- **Sub-tasks:** [ ] replace the numeric id fields on the PO, GRN, bill and debit-note forms · [ ] docs
- **Done when:** every purchase form picks its item and vendor by name.
- **Notes:**

### TK-19 · Customer module seed data (stage C4)
- [ ] open
- **Lanes:** L-CUS · **Depends on:** — · **Decision:** —
- **Sub-tasks:** [ ] list the reference data Leads and Tickets need (sources, priorities, statuses)
  and check it against the enums already in the code · [ ] seed it idempotently when a branch is
  created · [ ] test that seeding twice adds nothing
- **Done when:** a new branch's lead and ticket forms have their dropdowns filled.
- **Notes:** `CLAUDE.md` names stage C4, but no C4 section was found in `docs/Modules.md`. Check the
  code before assuming what it covers.

### TK-20 · Notification.Worker takes over email from Master
- [ ] open
- **Lanes:** L-NTF, L-MST · **Depends on:** TK-01 · **Decision:** —
- **Sub-tasks:**
  - [ ] Design the consumer on `IEventPublisher`. Delivery is at least once, so dedupe on `MessageId`.
  - [ ] Move sending from Master's `SmtpEmailSender` and in-process `EmailQueue` to the worker.
  - [ ] Keep `PaymentReminderWorker` working.
  - [ ] Write the design in `docs/Modules.md`, and ask the owner before going beyond email.
- **Done when:** an invitation email is sent by the worker, and a redelivered message sends once.
- **Notes:**

### TK-21 · `apps/desktop`: a real cart
- [ ] open
- **Lanes:** L-DSK · **Depends on:** TK-17 · **Decision:** —
- **Sub-tasks:** [ ] cart state with signals · [ ] replace the hardcoded walk-in customer with the
  picker · [ ] `nx build desktop` green
- **Done when:** the terminal sketch builds a cart of real items for a real customer. Posting it
  is TK-33.
- **Notes:**

### C · Phase 2

### TK-22 · Printing: how an internal call carries its branch
- [ ] open
- **Lanes:** L-DOC · **Depends on:** — · **Decision:** D-13
- **Sub-tasks:** [ ] write up the options in `docs/Modules.md` stage P: a service token that
  carries `OrgId`, or `[InternalOnly]` plus an explicit org, with RLS `set_config` in each case ·
  [ ] recommend one and put it to the owner as D-13
- **Done when:** D-13 is answered.
- **Notes:** `TenantMiddleware` sets the tenant context only for a user-authenticated request.

### TK-23 · Printing.Api controllers
- [ ] open
- **Lanes:** L-PRT · **Depends on:** TK-22 · **Decision:** D-13
- **Sub-tasks:** [ ] template CRUD and render endpoints, with guards · [ ] internal render endpoint
  using D-13's mechanism · [ ] tests from a dropped database
- **Done when:** Printing answers requests instead of returning 401 to everything.
- **Notes:**

### TK-24 · Switch serving from `con.PrintTemplates` to `prt.PrintTemplates`
- [ ] open
- **Lanes:** L-PRT, L-CON · **Depends on:** TK-23 · **Decision:** —
- **Sub-tasks:** [ ] copy the data · [ ] switch callers · [ ] drop `con.PrintTemplates` ·
  [ ] change the service count in `CLAUDE.md` from 7 to 8
- **Done when:** a sales invoice prints through Printing, and `con.PrintTemplates` no longer exists.
- **Notes:**

### TK-25 · Print-template editor screen
- [ ] open
- **Lanes:** L-MASTER-UI · **Depends on:** TK-23 · **Decision:** —
- **Sub-tasks:** [ ] list and edit the five bands and their merge fields · [ ] preview through the
  render endpoint · [ ] works at 360px · [ ] docs
- **Done when:** a user changes a template and sees the change in the preview.
- **Notes:**

### TK-26 · Document PDF/A archive (T3.4)
- [!] blocked — D-11
- **Lanes:** L-SAL, L-KERNEL · **Depends on:** — · **Decision:** D-11
- **Sub-tasks:** [ ] PDF/A from the existing print layout · [ ] archive through `IFileStorage` and
  `StorageKey` (the invoice archive is already `FileWriteMode.Replace`) · [ ] link by
  `SourceType` + `SourceId`
- **Done when:** posting an invoice leaves a PDF/A file in storage, and you can reach it from the invoice.
- **Notes:**

### TK-27 · Date input that follows the branch's format
- [ ] open
- **Lanes:** L-UI · **Depends on:** — · **Decision:** —
- **Sub-tasks:** [ ] design a replacement for the native `<input type="date">` in `bb-date-input`
  that shows `FormatSettingsService.formatDate` and stores ISO · [ ] propose it to the owner
  before swapping it in, since it changes every date field · [ ] check it with Playwright
- **Done when:** a branch set to `dd/MM/yyyy` sees that format in every date field.
- **Notes:**

### TK-28 · RateSync.Worker: metals (IBJA)
- [!] blocked — D-14
- **Lanes:** L-RATE · **Depends on:** — · **Decision:** D-14
- **Sub-tasks:** [ ] client for IBJA's paid API · [ ] store dated history · [ ] schedule and retry
- **Done when:** the day's metal rates appear in `rat` with their date.
- **Notes:**

### TK-29 · RateSync.Worker: currency (RBI)
- [!] blocked — D-03
- **Lanes:** L-RATE · **Depends on:** — · **Decision:** D-03
- **Sub-tasks:** follow D-03's answer · [ ] store dated history
- **Done when:** the day's exchange rates appear in `rat` with their date.
- **Notes:**

### TK-30 · Fixed-asset register
- [!] blocked — D-08, D-09
- **Lanes:** L-ACC · **Depends on:** — · **Decision:** D-08, D-09
- **Sub-tasks:** [ ] register schema, with the GL mapping held on the category · [ ] capitalise
  from a bill · [ ] depreciation run (`DEP`) · [ ] disposal · [ ] migrate fixed assets through the
  opening balance
- **Done when:** an asset bought on a bill depreciates for a month, is disposed of, and each step
  posts a balanced JE.
- **Notes:**

### TK-31 · The four fixed-asset reports
- [ ] open
- **Lanes:** L-RPT · **Depends on:** TK-30 · **Decision:** —
- **Sub-tasks:** [ ] Depreciation Schedule · [ ] Disposal Schedule · [ ] Fixed Asset Reconciliation ·
  [ ] Fixed Assets Schedule. Wire each through the four layers that `ReportLayerCertificationTests`
  checks.
- **Done when:** the certification suite counts all four.
- **Notes:**

### TK-32 · *Business Performance* report
- [!] blocked — D-15
- **Lanes:** L-RPT · **Depends on:** — · **Decision:** D-15
- **Sub-tasks:** build whatever D-15 specifies
- **Done when:** the certification suite counts it.
- **Notes:**

### D · Phase 3: POS

### TK-33 · POS till API (T7.1)
- [ ] open
- **Lanes:** L-SAL · **Depends on:** TK-14 · **Decision:** —
- **Sub-tasks:** [ ] a POS sale is an `sal.Invoices` row with `TransactionTypeCode = 'POS'`, using
  T3.1's posting · [ ] the stock decrement happens synchronously in the request · [ ] tests
- **Done when:** two concurrent sales of the last unit leave exactly one sale succeeding.
- **Notes:**

### TK-34 · POS till screen (T7.2)
- [ ] open
- **Lanes:** L-DSK · **Depends on:** TK-21, TK-33 · **Decision:** —
- **Sub-tasks:** [ ] keyboard- and barcode-driven · [ ] decide how it behaves offline and record it ·
  [ ] posts through TK-33
- **Done when:** a barcode-scanned sale posts from `apps/desktop`.
- **Notes:**

### TK-35 · POS receipt, ESC/POS (T7.3)
- [ ] open
- **Lanes:** L-DSK · **Depends on:** TK-34 · **Decision:** —
- **Sub-tasks:** [ ] build on `esc-pos.service.ts` · [ ] fixed-width layout · [ ] print from the
  till after a sale
- **Done when:** a completed sale prints a receipt on an ESC/POS printer, or on an emulator.
- **Notes:**

### E · Platform for several apps (stage H0)

The multi-app design is in `docs/Modules.md` under "One customer, many applications". None of it
is built. HRMS, Payroll and School can't start until TK-36 to TK-41 are done.

### TK-36 · H0.1: `App` in `mst`
- [ ] open
- **Lanes:** L-MST · **Depends on:** TK-01 · **Decision:** —
- **Sub-tasks:** [ ] `[Flags] App` enum · [ ] `App` on roles, licences and refresh tokens; `Apps`
  on permissions and menus · [ ] the grant rule, checked in C# and asserted over every seed ·
  [ ] turn `PlanTier` and `PlanType` into enums
- **Done when:** granting a Payroll-only permission to a RetailErp role is refused; a Payroll role
  can be granted `users.view`; and `apps/web` is unchanged for every existing user.
- **Notes:**

### TK-37 · H0.2: per-app sign-in and licences
- [ ] open
- **Lanes:** L-MST, L-KERNEL · **Depends on:** TK-36 · **Decision:** —
- **Sub-tasks:** [ ] `App` on `SelectOrganizationRequest` · [ ] an `app` claim, with licence and
  permission claims taken from that app · [ ] `[RequireApp]`, plus an `EndpointGuardAudit`
  question that asks for it
- **Done when:** an HRMS token calling a RetailErp endpoint gets 403; a Payroll token reads
  employees but not recruitment; and an expired RetailErp licence leaves Payroll working.
- **Notes:**

### TK-38 · H0.3: shell and shared master pages
- [ ] open
- **Lanes:** L-UI, L-MASTER-UI, L-WEB · **Depends on:** TK-37 · **Decision:** —
- **Sub-tasks:** [ ] `APP_ID`, `GET /api/menu?app=` · [ ] app switcher · [ ] `data.access`
  required on every route in `shellRoutes`, with an audit spec
- **Done when:** as H0.3 in `docs/Modules.md`.
- **Notes:**

### TK-39 · H0.4: signup and seeding per app
- [ ] open
- **Lanes:** L-MST · **Depends on:** TK-37 · **Decision:** D-12
- **Done when:** signing up for Payroll and then starting HRMS gives one customer, one branch, two
  licences and one set of employees.
- **Notes:**

### TK-40 · H0.5: sharding in the multi-app model
- [ ] open
- **Lanes:** L-MST · **Depends on:** TK-36 · **Decision:** —
- **Notes:** Shares `L-MST` with TK-39, so these two run one after the other.

### TK-41 · H0.6: `apps/hrms` and `apps/payroll` scaffolds
- [ ] open
- **Lanes:** L-DEPS (new apps), plus new lanes `L-HRMS-APP` and `L-PAY-APP` · **Depends on:** TK-38 · **Decision:** —
- **Done when:** both apps sign in, select a branch and draw the shared shell.
- **Notes:**

### F · HRMS and Payroll (H1–H12)

Each card also carries the standard delivery sub-tasks in section 5. Full *Done when* lines are in
`docs/Modules.md`. **Payroll without HRMS** needs TK-42, 45, 46, 47, the settlement half of 48,
and 49. **The first sellable HRMS** needs TK-42 to 44, 48 and 49.

### TK-42 · H1: Core HR (the shared employee master)
- [ ] open
- **Lanes:** L-HRM (new) · **Depends on:** TK-41
- **Done when:** an employee is created with family, nominees and bank details, linked to a user
  and listed; RLS and the guard audit pass from a dropped database.

### TK-43 · H2: Leave
- [ ] open
- **Lanes:** L-HRM · **Depends on:** TK-42
- **Done when:** two simultaneous approvals can't overspend a balance; the sandwich rule is
  applied correctly; and changing a workflow leaves requests already in flight on their old chain.

### TK-44 · H3: Time and attendance
- [ ] open
- **Lanes:** L-TLA (new) · **Depends on:** TK-42
- **Done when:** a biometric import derives a late-marked half day, and a regularisation approval
  corrects it.

### TK-45 · H4: Payroll core
- [ ] open
- **Lanes:** L-PAY (new) · **Depends on:** TK-42
- **Done when:** a run posts one balanced JE; Salary Payable ties to the unpaid net; a back-dated
  revision pays arrears in the next run; a reversal restores both; and the run reads monthly input
  without an HRMS licence and `tla` with one.

### TK-46 · H5: Statutory
- [ ] open
- **Lanes:** L-PAY · **Depends on:** TK-45
- **Done when:** a month's ECR file matches the posted payslips to the rupee.

### TK-47 · H6: Income tax
- [ ] open
- **Lanes:** L-PAY · **Depends on:** TK-45
- **Done when:** a mid-year joiner with income from a previous employer is taxed the same by a
  monthly run and by the year-end recomputation.

### TK-48 · H7: Lifecycle and exit
- [ ] open
- **Lanes:** L-HRM, L-PAY · **Depends on:** TK-42, TK-45
- **Done when:** settling an exit pays through a `FullAndFinal` run, and the employee's login stops
  working.

### TK-49 · H8: Self-service and approvals
- [ ] open
- **Lanes:** L-HRMS-APP, L-PAY-APP · **Depends on:** TK-43, TK-45
- **Done when:** an employee applies for leave and a manager approves it, both from their own
  screens; the employee downloads a payslip.

### TK-50 · H9: Expense claims
- [ ] open
- **Lanes:** L-CLM (new) · **Depends on:** TK-42
- **Done when:** a claim is submitted, approved and paid, and the payment posts a balanced JE.
  If the posting isn't already covered by an existing posting pattern, raise it as a card of its own.

### TK-51 · H10: Recruitment and onboarding
- [ ] open
- **Lanes:** L-REC (new) · **Depends on:** TK-42
- **Done when:** accepting an offer twice creates one employee.

### TK-52 · H11: Performance
- [ ] open
- **Lanes:** L-PRF (new) · **Depends on:** TK-42
- **Done when:** as H11 in `docs/Modules.md`: routing by level, send-back, self-evaluation left
  unchanged, and a manager who is also the lead asked only once.

### TK-53 · H12: HRMS and Payroll reports
- [ ] open
- **Lanes:** L-RPT · **Depends on:** TK-45
- **Done when:** each report is flagged with its app, and the certification suite counts them.

### G · School (S0–S9)

Needs TK-36 to TK-41 and TK-42. Each card also carries the standard delivery sub-tasks in section 5.

### TK-54 · S0: School prerequisites
- [ ] open · **Lanes:** L-DEPS, L-SCH-APP (new) · **Depends on:** TK-41, TK-42
- **Done when:** `apps/school` shows only School menus, and a guardian contact can be created and filtered.

### TK-55 · S1: Sis
- [ ] open · **Lanes:** L-SIS (new) · **Depends on:** TK-54
- **Done when:** a student is admitted directly, enrolled in a section and listed; RLS and the
  guard audit pass from a dropped database.

### TK-56 · S2: Admission
- [ ] open · **Lanes:** L-ADMN (new) · **Depends on:** TK-55
- **Done when:** admitting twice creates one student.

### TK-57 · S3: Attendance
- [ ] open · **Lanes:** L-ATT (new) · **Depends on:** TK-55
- **Done when:** a locked day refuses an edit from a teacher and accepts one from `attendance.unlock`.

### TK-58 · S4: Fee
- [ ] open · **Lanes:** L-FEE (new) · **Depends on:** TK-55
- **Done when:** a demand and its receipt post balanced JEs, and the guardian's AR sub-account ties
  to the open demands.

### TK-59 · S5: Facility
- [ ] open · **Lanes:** L-FAC (new) · **Depends on:** TK-54
- **Done when:** buildings, spaces and assets can be created and listed.

### TK-60 · S6: WorkOrder
- [ ] open · **Lanes:** L-WRK (new) · **Depends on:** TK-59
- **Done when:** editing an Assigned work order is refused, and issuing a part moves stock.

### TK-61 · S7: Preventive
- [ ] open · **Lanes:** L-PPM (new) · **Depends on:** TK-60
- **Done when:** running generation twice raises one work order per occurrence.

### TK-62 · S8: AMC
- [ ] open · **Lanes:** L-AMC (new) · **Depends on:** TK-59
- **Done when:** contracts, covered assets and visits are recorded, and renewal reminders fire.

### TK-63 · S9: Parent portal
- [ ] open · **Lanes:** L-PTL · **Depends on:** TK-57, TK-58
- **Done when:** a guardian sees their child's demands, receipts, attendance and published marks.

### H · Phase 3: not designed yet

The owner has to approve each of these before the design is written. Each is a design card
that ends in a design section under `docs/` and a new batch of cards in this queue.

### TK-64 · Design: `apps/portal`, the next screens
- [!] blocked — D-16 · **Lanes:** L-DOC

### TK-65 · Design: project accounting
- [!] blocked — D-17 · **Lanes:** L-DOC

### TK-66 · Design: budgeting
- [!] blocked — D-17 · **Lanes:** L-DOC

### TK-67 · Design: workflow approvals
- [!] blocked — D-17 · **Lanes:** L-DOC

### TK-68 · Design: custom fields and custom reports
- [!] blocked — D-17 · **Lanes:** L-DOC

### TK-69 · Design: e-invoicing and e-way bill
- [!] blocked — D-17 · **Lanes:** L-DOC

### TK-70 · Design: compliance bundle
- [!] blocked — D-17 · **Lanes:** L-DOC

---

## 3. Decisions waiting on the owner

These aren't tasks. An agent never answers one itself. When a decision is made, record the answer
and the date here, then change the blocked cards to `- [ ] open`.

| ID | Question | Blocks | Answer |
|---|---|---|---|
| D-01 | How does a platform operator's account get `platform.*`? | `apps/admin` sign-in, TK-38 | |
| D-02 | Who holds `CREATEDB` in production? Should the database be auto-created at startup, or provisioned by infra? | deployment | |
| D-03 | RBI rate ingestion: scraping, a paid wrapper, or manual entry? | TK-29 | |
| D-04 | Optional phone fields: normalise to an empty string or to null? | — | |
| D-05 | Does `settings` split into a lib per sub-screen? | — | |
| D-06 | CRM: is campaign and marketing automation in v1? | — | |
| D-07 | API client scopes: per module or per action? | — | |
| D-08 | Fixed assets: straight-line only, or book **and** tax depreciation? | TK-30 | |
| D-09 | Fixed assets: do acquisition and disposal get their own transaction codes? | TK-30 | |
| D-10 | Does a branch declare its trade (Pharma, Jewellery or General)? | — | |
| D-11 | PDF library: Syncfusion (licensed, not installed) or PDFsharp 6.1.1 (already pinned)? | TK-26 | |
| D-12 | Pricing per app: per user, per branch, or per employee? | TK-39 | |
| D-13 | Printing: how does an internal call carry its branch? (TK-22 writes the options) | TK-23 | |
| D-14 | Subscribe to IBJA's paid metals API? | TK-28 | |
| D-15 | What is the *Business Performance* report? | TK-32 | |
| D-16 | What should the client portal do next? | TK-64 | |
| D-17 | Go-ahead, and the order, for each Phase 3 design | TK-65 … TK-70 | |

---

## 4. Retired

- ~~Seed the Sales and Purchase numbering series~~. Already done: `SeedData/NumberingSeriesSeed.cs`
  exists in both services and seeds `SO`, `INV`, `DC`, `CN`, `PO`, `GRN`, `BIL`, `DBN` and more.
  Found 23 September 2026.
- ~~Wire `apps/desktop` into the Nx workspace~~. `apps/desktop/project.json` already has real
  targets, and Nx finds the project from that file. The cart work is TK-21.
- The first version of this file (23 September 2026) numbered tasks by section, 1.1 to 4.7. Those
  numbers are gone. Use the `TK-nn` IDs.

---

## 5. Standard delivery sub-tasks (feature cards)

Copy these into any card that builds a feature, and tick them as you go:

- [ ] Entity in `{Module}.Entity/TableEntities`, inheriting `AuditableEntity`, with an
      `ErrorMessage` on every annotation. Enums in `Enums/`.
- [ ] `DbSet` and Fluent config. `CustomerId` + `OrgId` and the query filter; `xmin` concurrency.
- [ ] The migration, with an RLS `ENABLE` + `FORCE` + policy block in it (the TK-02 template).
      Run `dotnet ef migrations has-pending-model-changes`.
- [ ] Seed data that is idempotent when a branch is created.
- [ ] Service and controller. Writes happen inside the reliability filter's transaction
      (`BeginScopeAsync`, never `BeginTransactionAsync`). Errors go through `SqlErrorCatalog`. The
      controller carries a guard attribute. A request for another branch's data gets `Forbid()`.
- [ ] `-core` view-model and `-ui` page: standalone components, `inject()`, signals, working at 360px.
- [ ] Tests against a **dropped** database, plus the `RlsAudit` and `EndpointGuardAudit` tests for
      the new schema and controllers.
- [ ] Update the docs page under `frontend/apps/docs/content/` and `docs.manifest.ts`, and add a
      `release-notes.md` bullet under **Unreleased**, all in the same commit.
- [ ] `npm run check` in `frontend/` and `dotnet build && dotnet test` in `backend/` are both green.
