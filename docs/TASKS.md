# TASKS.md — the one pending-work checklist

Every outstanding piece of work in RetailErp, in one file, so a session can pick up the next
unfinished item without re-deriving it from `CLAUDE.md`, `docs/Modules.md`,
`docs/Architecture.md` and `docs/Transactions_And_Specs.md` each time. Compiled from those four
on 23 September 2026 — treat it as a snapshot, not a replacement for them; when a task here and
its source file disagree, the source file is still the fuller account.

This file tracks **what is left**, not what is built. For what already works, see "Built and
wired end to end" in `CLAUDE.md` and the per-module detail in `docs/Modules.md`.

---

## 0. How this file is kept

**Rule — every session, human or AI, marks its own work on the checklist as it happens, not
after:**

- `- [ ]` — not started.
- `- [~] working (AI name)` — picked up and in progress right now. Claim a task by changing its
  box to this **before** starting the work, so two sessions don't pick up the same item. Use the
  name reported by `get_session` (e.g. `Claude Sonnet 5`), not a generic "Claude".
- `- [x] completed (AI name) — YYYY-MM-DD` — done and verified against its **Done when** /
  acceptance line. Not "compiles" — "works", per the same standard `docs/Modules.md` already
  uses.
- If a task turns out wrong, unnecessary, or superseded, strike it (`~~text~~`) and add one line
  saying why, rather than deleting it. A struck task with a reason is worth more than a missing
  one — this repository's own history (see `CLAUDE.md`'s standing caveats) is full of silent
  deletions that turned out to hide a real bug.
- Tick a box in the **same commit** as the work it describes, per `CLAUDE.md` hard rule 10's
  spirit (documentation ships with the feature) and the existing convention in
  `docs/Modules.md`.
- This same marking rule applies to the checklists inside `docs/Modules.md` and
  `docs/Transactions_And_Specs.md` — see the note added to each.

**Order of work**: take the first unclaimed (`- [ ]`) box in the highest section number that has
one. Sections are ordered roughly by urgency (blockers first, then in-flight Phase 1 work, then
Phase 2, Phase 3, then the not-yet-started platform/HRMS/Payroll/School design). Within a
section, earlier items are usually prerequisites for later ones in the same section — check the
"depends on" notes.

**Before claiming a task**: confirm it's still accurate. This codebase's own docs record several
cases (the RLS gap, the Master seed bug, the `sal` shadow-FK bug) where a task believed done was
not, because nobody re-verified from a dropped database. Re-run the relevant suite from a dropped
database before trusting a "built" claim this file or any other doc makes.

---

## 1. Critical — build, startup and security blockers

These block other work or leave real gaps open; take these first regardless of section 2's order.

### 1.1 — Fix Master's fresh-database startup failure
**Status:** - [ ] not started

`AdminDbContext` has drifted from its migrations: the `mst.Menus` / `mst.MenuPermissions` seed
(`HasData`) changed after the admin migration was squashed and after `UpdateAdminModel`, so
`MigrateAsync` throws `PendingModelChangesWarning` and **no host can start on a fresh database**.

- [ ] Re-squash the admin migration chain (adding one more migration does not work — EF emits 379
      `UpdateData` calls applied row by row and collides on `IX_MenuPermissions_MenuId_PermissionCode`
      partway through; this was verified and then reverted once already because the menu seed was
      mid-change — check current state of that seed first).
- [ ] Run `dotnet ef migrations has-pending-model-changes` and confirm it reports clean.
- [ ] Verify `dotnet build && dotnet test` against a **dropped and recreated** `ADMIN_TEST_DB` and
      `CONTACTS_TEST_DB`.
- [ ] Verify a fresh container (`DROP DATABASE` both, `service postgresql start`, migrate) actually
      starts Master end to end.

### 1.2 — Restore Postgres RLS (`ENABLE`, `FORCE`, `CREATE POLICY`) across every tenant schema
**Status:** - [ ] not started

`ENABLE ROW LEVEL SECURITY`, `FORCE`, and `CREATE POLICY` appear nowhere in the current migration
chain except `prt` (Printing). The 14 September squash dropped the hand-written RLS blocks that
used to be in the chains it replaced. The EF query filter is currently the **only** guard between
one branch's or customer's rows and another's — nothing leaks while it holds, but
`IgnoreQueryFilters`, a raw command, or a context that skips `base.OnModelCreating` (as
`SalesDbContext` once did) would meet no second guard. Policy text is recoverable verbatim from
any un-dropped developer database: `SELECT tablename, policyname, qual FROM pg_policies WHERE
schemaname = '<schema>'`. `prt`'s own migration is the shape to copy.

Do this as one migration per service (not per table) so each is reviewable and testable on its
own:

- [ ] `acc` (Accounting) — write the RLS migration, verify against a **dropped** `ACCOUNTING_TEST_DB`.
- [ ] `con` (Contacts, under Master) — write the RLS migration, verify against a dropped `CONTACTS_TEST_DB`.
- [ ] `cus` (Customer) — write the RLS migration, verify against a dropped test DB.
- [ ] `inv` (Inventory) — write the RLS migration, verify against a dropped `INVENTORY_TEST_DB`.
- [ ] `pur` (Purchase) — write the RLS migration, verify against a dropped `PURCHASE_TEST_DB`.
- [ ] `sal` (Sales) — write the RLS migration, verify against a dropped `SALES_TEST_DB`.
- [ ] `rpt` (Reporting) — write the RLS migration; remember `rpt.ReportMasters` and
      `rpt.ReportColumns` are the documented exemption (imported `reports.json` spec, no tenant
      column) and carry no policy.
- [ ] Re-run every service's RLS audit test (`RlsAudit`) from a **dropped** database per service —
      a database carried over from before this work will pass even when the migration is missing,
      which is exactly how the gap went unnoticed for a fortnight. Confirm 0 RLS failures across
      all seven suites.
- [ ] Update the "Standing caveats" section of `CLAUDE.md` once verified — this is exactly the kind
      of claim that must not be copied forward without a fresh, dropped-database run.

### 1.3 — Fix `ReportLayerCertificationTests` (4 failures, already red on `main`)
**Status:** - [ ] not started

Unrelated to 1.2, but part of the same 20-test gap. The suite's expected report-source count
(historically "41") needs reconciling against the current wired count — `docs/Modules.md` §8.2/8.3
now records 41 of 46 `reports.json` entries implemented, 7 more beyond the spec (48 wired total),
and the earlier "12 not implemented" figure is stale — only 5 remain unimplemented, 4 of them
blocked on the fixed-asset register (see 3.3) and one (*Business Performance*) needing a business
decision (see 3.4), not engineering.

- [ ] Read the current assertion in `ReportLayerCertificationTests` and compare it against the
      verified 41/7/48 figures in `docs/Modules.md` §8.2.
- [ ] Update the expected counts/list to match reality, or fix whichever of the two is actually
      wrong — don't just raise the expected number to make the test pass without checking which
      side is stale.
- [ ] Confirm `dotnet test` is green for this suite from a dropped `REPORTING_TEST_DB`.

---

## 2. Phase 1 — finish what's already in flight

### 2.1 — Verify Sales Delivery Challan actually saves end to end
**Status:** - [ ] not started

Has a controller and a scaffold page but, per `CLAUDE.md`, no *verified* save path — the same
class of shadow-foreign-key bug that silently broke every other `sal` document type
(`QuoteId1`-style shadow columns from `HasOne<T>().WithMany()` without a real navigation) was only
found by writing the first test that actually persisted a document.

- [ ] Write a round-trip test (create → save → reload) for `sal.DeliveryChallans` the way
      `SalesQueryFilterTests` / `SalesSchemaTests` did for the other five document types.
- [ ] Fix any shadow-key or FK issue the test surfaces.
- [ ] Wire the form to actually post and confirm the list/detail pages render a saved challan.

### 2.2 — Verify Sales Credit Note actually saves end to end
**Status:** - [ ] not started

Same gap, same fix shape as 2.1, for `sal.CreditNotes`.

- [ ] Round-trip persistence test.
- [ ] Fix any surfaced shadow-key/FK issue.
- [ ] Confirm the form and pages work against a real save.

### 2.3 — Partial fulfilment (T3.6)
**Status:** - [ ] not started
**Depends on:** 2.1 (challan must actually save first)

Nothing currently advances `DeliveredQuantity` or `InvoicedQuantity`, so `FulfilmentStatus.PartlyDelivered`
is unreachable and an order can be neither shipped nor billed in part.

- [ ] Advance `DeliveredQuantity` on a posted delivery challan.
- [ ] Advance `InvoicedQuantity` on a posted invoice.
- [ ] Make `FulfilmentStatus.PartlyDelivered` reachable and covered by a test.
- [ ] Fix the known bug: `salesOrderId` on the challan has no control and `ReleaseReservation` is
      set from `SalesOrderId.HasValue`, so a challan raised from the screen never releases a
      reservation — this is the specific clause T3.6's *Done when* line turns on (see
      `docs/Modules.md` line ~451).

### 2.4 — Item and customer picker (lookup endpoint + UI component)
**Status:** - [ ] not started

Every sales form currently takes item and customer as raw numeric id fields, awaiting a real
lookup endpoint.

- [ ] Build a search/typeahead endpoint for items (by name, SKU, barcode) scoped to `OrgId`.
- [ ] Build the equivalent for contacts/customers.
- [ ] Build a shared `-ui`/`-core` picker component and replace the numeric id fields on every
      sales (and purchase, where the same gap exists) form.

### 2.5 — Customer module seed data (stage C4)
**Status:** - [ ] not started

Lead/Ticket/TicketMessage, controllers and UI are wired; only seed data is not started.

- [ ] Define and seed default lead sources, ticket priorities/statuses and any other reference
      data stage C4 in `docs/Modules.md` calls for.
- [ ] Confirm seeding is idempotent (per the provisioning model, a retry only adds what's missing).

### 2.6 — Document numbering series beyond `JRN`, `OPB`, `SPM`, `RCM`, `TRM`
**Status:** - [ ] not started
**Depends on:** 2.1, 2.2 (documents must be reliably saving first)

Accounting seeds its five; Sales and Purchase still need to seed theirs (quote, sales order,
invoice, challan, credit note; PO, GRN, bill, debit note, etc.) into the shared `NumberingSeries`
table.

- [ ] Seed Sales' numbering series at org creation.
- [ ] Seed Purchase's numbering series at org creation.
- [ ] Confirm `NumberGenerator`'s guarded `ExecuteUpdate` behaves correctly (gapless, transaction-joined)
      for each new series.

### 2.7 — Notification.Worker
**Status:** - [ ] not started

Currently only `.csproj` and an empty `Consumers/` folder. Email still sends synchronously from
Master (`SmtpEmailSender` + an in-process `EmailQueue`).

- [ ] Design the consumer contract against `IEventPublisher` (Service Bus when configured, no-op
      `LoggingEventPublisher` otherwise — nothing currently consumes an event at all).
- [ ] Decide what moves here first (candidate: outbound email, freeing Master of the in-process
      queue) — keep scope small per hard rule 9, ask before widening it.
- [ ] Build the consumer, with idempotency/dedup on `MessageId` per the Service Bus at-least-once
      guarantee noted in `CLAUDE.md`.

### 2.8 — RateSync.Worker
**Status:** - [ ] not started
**Blocked on:** an Undecided item — see section 8 ("RBI rate ingestion: scrape / paid wrapper / manual").

- [ ] Get a decision on RBI ingestion method before building the RBI half.
- [ ] IBJA (metals) has a paid API — the metals half can start independently of the RBI decision.
- [ ] Store **dated history**, not just today's rate, per the existing rule.

### 2.9 — Wire `apps/desktop` into the Nx workspace
**Status:** - [ ] not started

The POS sketch (`pos-terminal.component.*`, `esc-pos.service.ts`) has never compiled as part of
the checked workspace — `project.json` needs real targets confirmed wired, and the app needs to be
in `tsconfig.base.json` and `nx.json` so `npm run check` actually reads it instead of silently
skipping it. (Note: `CLAUDE.md` records that the "declares `{}` targets, never compiled" claim was
itself false as of the last check — verify current state before starting, this may already be
partly done.)

- [ ] Confirm `apps/desktop/project.json` targets and `nx.json`/`tsconfig.base.json` membership are
      correct as of today, not as of whichever date a caveat above was written.
- [ ] Build a real cart (the sketch has none).
- [ ] Replace the hardcoded walk-in customer with the picker from 2.4 once it exists.
- [ ] Full POS till API/screen is Phase 3 (see 4.1) — this task is foundation only.

### 2.10 — Continue `apps/portal`
**Status:** - [ ] not started

Has a real dashboard and statement list against real endpoints. Scope for "what's next" is not
yet written down anywhere — this is a case for asking before picking a direction (hard rule 9)
rather than guessing what a customer-facing portal needs next.

- [ ] Ask what portal screens are wanted next, or check for a `docs/` spec before building further.

---

## 3. Phase 2

### 3.1 — Printing service cutover (Stage P)
**Status:** - [ ] not started

The eighth service scaffold (`backend/Api/Printing/{Entity,Repository,Api}`) builds, `prt.PrintTemplates`
exists with RLS (the one schema that has it — see 1.2), and the service starts on port 4508
answering 401 to everything because it has no controller yet. `con.PrintTemplates` is still the
copy that actually serves requests.

- [ ] Resolve the tenancy question blocking this: an internal call from a document's own service to
      Printing needs to carry branch context, but `TenantMiddleware` only fills tenant context for
      an authenticated (user-originated) request. Decide the mechanism (service-to-service token
      carrying `OrgId`? Internal API key plus explicit org parameter, per the existing `[InternalOnly]`
      guard pattern?) before writing controllers.
- [ ] Build `Printing.Api` controllers (currently none exist).
- [ ] Migrate serving from `con.PrintTemplates` to `prt.PrintTemplates` — read `docs/Modules.md`
      7.6/7.7 for the plan and why an eighth service is defensible here.
- [ ] Build the print-template editor screen — `CLAUDE.md` notes **no editor screen exists today**
      even for the current `con.PrintTemplates` copy.
- [ ] Once cutover is complete and carrying real traffic, update the service count in `CLAUDE.md`
      from 7 to 8 (explicitly deferred until then, by the file's own note).

### 3.2 — Document PDF/A archive (T3.4)
**Status:** - [ ] not started

The print half already works (`/sales/invoices/{id}/print` renders a full tax invoice, watermarks
drafts/voided documents, splits GST per component/rate). What's missing is archiving a PDF/A copy
to blob storage.

- [ ] Resolve the library decision: Syncfusion (licensed, not installed — named only in a
      `Directory.Packages.props` comment) vs. **PDFsharp 6.1.1**, which `CLAUDE.md` notes is
      already pinned and *not* licence-blocked, and which may reopen this choice. Get a decision
      rather than assuming (hard rule 9) — this affects every future document-print feature.
- [ ] Implement PDF/A generation and archive-to-blob-storage using `IFileStorage` (already built —
      `AzureBlobFileStorage` / `LocalDiskFileStorage`, keyed via `StorageKey`).
- [ ] Link the archived file by `SourceType` + `SourceId`, per the existing archiving convention.

### 3.3 — Fixed assets register
**Status:** - [ ] not started
**Blocked on:** two Undecided schema questions — see section 8.

- [ ] Get a decision: do acquisition and disposal get their own transaction codes, or continue
      riding on `BIL`/`OPB`/`JRN`/`INV` as decided for `DEP` (T10.2 already answers this for
      depreciation itself — the same question is still open for acquisition/disposal).
- [ ] Get a decision: straight-line only, or both books and tax depreciation.
- [ ] Build the register schema, capitalisation from a bill, depreciation runs, disposal.
- [ ] Unblocks the four fixed-asset reports (Depreciation Schedule, Disposal Schedule, Fixed Asset
      Reconciliation, Fixed Assets Schedule) — build/enable those once the register exists.
- [ ] Unblocks opening-balance migration of a fixed asset with its own cost/life/schedule (today it
      comes across as a plain account balance and skips historical depreciation).

### 3.4 — *Business Performance* report — needs a specification, not code
**Status:** - [ ] not started

`reports.json` names it under *Financial performance* with no columns, no sub-group, nothing else.

- [ ] Get a decision on what this report actually is before treating it as engineering work.

### 3.5 — POS ESC/POS receipt printing (T7.3)
**Status:** - [ ] not started
**Depends on:** 4.1 (till API/screen must exist — there is no sale to print a receipt for yet)

Talks ESC/POS, not PDF, and only reachable from `apps/desktop` (a browser can't reach a USB/serial
printer). None of the PDF printing work (3.2) applies here.

- [ ] Build once 4.1 exists; do not start before there's a sale to print.

### 3.6 — Locale-aware date input component
**Status:** - [ ] not started

`bb-date-input` is a native `<input type="date">` whose placeholder/display follow the browser's
locale, not the branch's `format.date` config — so a branch on `dd/MM/yyyy` still sees
`mm/dd/yyyy` in the field (the stored value is ISO, so nothing downstream is wrong, only the
display). Fixing it needs a custom component, which affects every date field in the product.

- [ ] Design a custom date input that reads `FormatSettingsService`'s `formatDate` for display
      while keeping the stored value ISO. This is a bigger decision than one screen — confirm scope
      before starting (hard rule 9).

---

## 4. Phase 3 — not yet designed

Nothing in this section has a schema, an API, or column-level design under `docs/` yet. Per hard
rule 9, the first step for each is a short design pass and a check-in before writing code, not
assuming the larger interpretation.

- [ ] **4.1 — POS till API and screen (T7.1, T7.2).** The bulk of the POS stage; keyboard- and
      barcode-driven, must tolerate being offline, lives in `apps/desktop`. Reuses T3.1's invoice
      posting (`TransactionTypeCode = 'POS'`) rather than adding a new posting path — the tables,
      numbering series and GST determination already exist. Depends on 2.9 (desktop app actually
      wired into the workspace) and 2.4 (item/customer picker).
- [ ] **4.2 — Project accounting.**
- [ ] **4.3 — Budgeting.**
- [ ] **4.4 — Workflow approvals.**
- [ ] **4.5 — Custom fields / custom reports.**
- [ ] **4.6 — E-invoicing + e-way bill.**
- [ ] **4.7 — Compliance bundle.**

---

## 5. Platform for multiple apps — Stage H0

**Nothing in this section is built.** This is the prerequisite platform work for HRMS, Payroll and
School (sections 6–7) — none of those can start before H0 lands, since nothing in a second app
works without per-app licences, tokens and menus. Full detail and *Done when* lines are in
`docs/Modules.md` under "One customer, many applications"; summarized here.

- [ ] **H0.1 — `App` flags enum in `mst`.** `App` on roles, licences and refresh tokens; `Apps` (a
      combination) on permissions and menus.
      *Done when*: granting a Payroll-only permission to a RetailErp role is refused; a Payroll
      role can be granted `users.view`; `apps/web` is unchanged for every existing user.
- [ ] **H0.2 — Per-app sign-in and licences.** Per-app login filtering, the `app` claim,
      `[RequireApp(...)]` on controllers.
      *Done when*: an HRMS token calling a RetailErp endpoint gets 403; a Payroll token reads
      employees but not recruitment; an expired RetailErp licence leaves Payroll working.
- [ ] **H0.3 — Shell and shared master pages.** `APP_ID`, `GET /api/menu?app=`, the app switcher,
      `shellRoutes` access validation.
      *Done when*: every app's menu shows only its own screens plus the shared ones flagged in its
      Apps column; no app's source tree contains a copy of a shared page; a typed URL to a page the
      user lacks the permission for shows the no-access page; removing `data.access` from any shell
      route fails that app's route spec.
- [ ] **H0.4 — Signup and seeding per app**, and starting another app's trial.
      *Done when*: signing up for Payroll then starting HRMS gives one customer, one branch, two
      licences and one set of employees, with HRMS's master data seeded into the existing branch.
- [ ] **H0.5 — Sharding** — extend the shard registry/allocator to the multi-app model.
- [ ] **H0.6 — `apps/hrms` and `apps/payroll` scaffolds** — two empty apps that sign in, select a
      branch, and draw the shared shell.

**Also waiting on a decision before H0 can fully close**: how a platform operator's account
acquires `platform.*` (section 8) and pricing per app (section 8).

---

## 6. HRMS & Payroll — H1 through H12

**Depends on:** section 5 (H0) complete. **Payroll-sellable-without-HRMS** needs H0, H1, H4, H5,
H6 and the settlement half of H7, plus H8's self-service. **First sellable HRMS** needs H0–H3, H7
and H8. Full detail in `docs/Modules.md`.

- [ ] **H1 — Core HR** *(shared employee master, both apps)*. Organisation setup, employee master
      with every child table, history.
      *Done when*: an employee is created with family, nominees and bank details, linked to a user
      and listed; RLS and the guard audit pass from a dropped database.
- [ ] **H2 — Leave** *(HRMS)*. Types, policies, accrual and rollover, balances, applications,
      encashment. *Done when*: a weekend between two leave days is excluded correctly; changing a
      workflow leaves in-flight requests on their old chain.
- [ ] **H3 — Time and attendance** *(HRMS)*. Holidays, shifts, rosters, weekly offs, punches, daily
      derivation. *Done when*: a biometric import derives a late-marked half day, and a
      regularisation approval corrects it.
- [ ] **H4 — Payroll core** *(Payroll)*. Components, structures, salaries, revisions with arrears,
      runs. *Done when*: a run posts one balanced JE, Salary Payable ties to the unpaid net, a
      back-dated revision pays arrears in the next run and a reversal restores both; the same run
      with no HRMS licence reads monthly input, and with one reads `tla`.
- [ ] **H5 — Statutory** *(Payroll)*. PF, ESI, PT, LWF, gratuity provision, bonus, return files.
      *Done when*: the ECR file for a month matches the posted payslips to the rupee.
- [ ] **H6 — Income tax** *(Payroll)*. Slabs/rules, declarations and proofs, projection and monthly
      TDS. *Done when*: a mid-year joiner with a previous employer's income is taxed the same under
      a monthly run and a year-end recomputation.
- [ ] **H7 — Lifecycle and exit** *(HRMS: checklists, separation, clearance, letters; Payroll:
      F&F)*. *Done when*: settling an exit pays through a `FullAndFinal` run and the employee's
      login stops working.
- [ ] **H8 — Self-service and approvals** *(both)*. Employee/manager screens, approval inbox;
      payslips, Form 16, tax declarations.
- [ ] **H9 — Expense claims** *(HRMS)*.
- [ ] **H10 — Recruitment and onboarding** *(HRMS)*. *Done when*: accepting an offer twice creates
      one employee.
- [ ] **H11 — Performance** *(HRMS)*. Cycles, goals, competencies, self-evaluation, multi-level
      review. *Done when*: a four-level and a two-level route differ correctly; a level sent back
      returns to the one before it; self-evaluation is unchanged after every level acts; a manager
      who is also the lead is asked once, not twice.
- [ ] **H12 — Reports** *(both — each flagged with the app it serves)*.

---

## 7. School — S0 through S9

**Depends on:** section 5 (H0) and H1 (HRMS Core HR, for staff-as-employees). Full detail in
`docs/Modules.md`.

- [ ] **S0 — School prerequisites.** *Done when*: `apps/school` shows only School menus, and a
      guardian contact can be created and filtered.
- [ ] **S1 — Sis.** Scaffold, schema, seeds, API, pages for years, classes, sections, subjects.
      *Done when*: a student is admitted directly, enrolled in a section and listed; RLS and the
      guard audit pass from a dropped database.
- [ ] **S2 — Admission.** Enquiry → application → admit, creating the student through Sis.
      *Done when*: admitting twice creates one student.
- [ ] **S3 — Attendance.** Daily register per section, and locking. *Done when*: a locked day
      refuses an edit from a teacher and accepts one from `attendance.unlock`.
- [ ] **S4 — Fee.** Heads, structures, concessions, demand generation per enrolment, receipts.
      *Done when*: a demand and its receipt post balanced JEs, and the guardian's AR sub-account
      ties to the open demands.
- [ ] **S5 — Facility.** Buildings, spaces and assets.
- [ ] **S6 — WorkOrder.** Lifecycle, tasks, parts issued from Inventory. *Done when*: editing an
      Assigned work order is refused, and issuing a part moves stock.
- [ ] **S7 — Preventive.** Plans, occurrence generation by the hosted service. *Done when*: running
      generation twice raises one work order per occurrence.
- [ ] **S8 — Amc.** Contracts, covered assets, visits, renewal reminders.
- [ ] **S9 — Parent portal.** Guardian pages for demands, receipts, attendance, published marks.

---

## 8. Decisions needed before certain tasks can start

Not tasks — these need the repository owner or user to decide, per hard rule 9 ("ask before
expanding scope"). Listed here so the tasks blocked on them (cross-referenced above) are traceable.

- [ ] How a platform operator's account acquires `platform.*` (blocks H0 fully closing).
- [ ] Who holds `CREATEDB` in production — infra-provisioned ahead of time vs. Master's own
      idempotent startup check.
- [ ] RBI rate ingestion: scrape / paid wrapper / manual entry (blocks 2.8).
- [ ] Empty-string vs. null normalization for optional phone fields.
- [ ] Whether `settings` splits into per-sub-screen libs.
- [ ] CRM: campaign/marketing automation in v1?
- [ ] API client scope granularity: per-module or per-action.
- [ ] Fixed assets: straight-line only, or both books and tax depreciation (blocks 3.3).
- [ ] Fixed assets: transaction codes for acquisition/disposal, or ride existing codes (blocks 3.3).
- [ ] Whether a branch should declare its trade (Pharma/Jewellery/General) to narrow seeding and
      settings menus.
- [ ] Document archive library: Syncfusion vs. PDFsharp (blocks 3.2).
- [ ] Pricing per app — per user, per branch, or per employee (Platform/HRMS/Payroll/School).

---

## 9. Ongoing hygiene — not a one-time task, applies to every session

- [ ] Before trusting any "built"/"passes" claim in `CLAUDE.md` or `docs/Modules.md`, re-verify it
      against a **dropped and recreated** test database — this repository has repeatedly shipped
      green suites that proved nothing because the database under them predated the change being
      tested.
- [ ] Ship documentation with the feature, same commit (`CLAUDE.md` hard rule 10): update the
      relevant page under `frontend/apps/docs/content/`, its status in `docs.manifest.ts`, and add a
      bullet under **Unreleased** in `release-notes.md`.
- [ ] Keep this file's checkboxes current in the same commit as the work (see section 0).
- [ ] Re-run `information_schema.columns WHERE column_name LIKE '%Id1'` over freshly migrated
      databases whenever a schema gains a new header/line pair — the cheap check that catches the
      `sal` shadow-FK class of bug before it ships.
