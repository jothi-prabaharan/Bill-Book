To eliminate the frustrating loops where your AI assistants blindly rely on outdated `.md` tracking files and ignore the real code you've built, you need a prompt engineered to enforce **strict file system and code verification**.

Here is the newly engineered **Project Status Review Prompt**. You can save this to your `AGENTS.md` file, or paste it directly into Claude Code or other local workspace assistants to force them to read the raw files before giving you a status update.

***

### 📋 Bill-Book Codebase-Reality Project Status Review Prompt

```text
### Project Status Review Prompt

**⚠️ CRITICAL INSTRUCTION (MANDATORY FILE-SYSTEM CHECK):** 
Every time you are asked to generate or review the project status, you MUST bypass and ignore all static `.md` tracking files (such as PROJECT.md, TRANSACTIONS.md, or cached checklists) as your primary sources of truth. These manual files are frequently outdated or lagging behind reality. 

You are strictly forbidden from summarizing previous chat history or relying on your training memory. You MUST actively inspect the physical repository, parse the source code files directly, and verify actual implementations before writing a single percentage.

#### 🔍 MANDATORY CODEBASE REALITY DISCOVERY STEPS:
Before outputting any status metrics, you must run search commands or scan the workspace to verify:
1. **Physical File Existence:** Verify that the respective Controller, Entity, and Repository files exist on disk.
   - *Example (Invoices):* Is `SalesInvoicesController.cs` actually present in `backend/Api/Sales/Sales.Api/Controllers/`?
   - *Example (Sales Orders):* Is `SalesOrdersController.cs` actually present?
2. **Method & Logic Inspection:** Do not just check if a file exists. Open and parse the code to verify implementation depth:
   - Check if methods are empty skeletons, stubbed with `throw new NotImplementedException()`, or fully coded with LINQ, tenancy filters, security, and transaction hooks.
   - Check if separate database tables for Details, Tax, StockMovement, and Ledger are registered in the DbContext.
3. **Frontend UI Audit:** Verify if reactive Angular components exist in `frontend/libs/` or `frontend/apps/`.
   - Read the `.component.ts` and `.html` files to confirm forms, fields, validation messages, and styling variables (`var(--...)`) are written rather than just scaffolded.
4. **Git Analysis:** Query the Git tree (`git status`, `git log -n 5`) to detect recent commits, branch status, and uncommitted modifications.

#### 📊 OUTPUT REQUIREMENTS:
Generate the true, code-verified status report using the following structure:

##### 1. Codebase-Reality Project Status Table
Use this exact table format. Every completion percentage must represent a strict mathematical average of (Schema, Backend API, Frontend UI, Validations, and Auth) verified *from the raw source files* you inspected:

| Task Name | % Completion | Blocker (Module/Task) | Schema & Table Status | Backend Status | Frontend Status | Validations Handled? | Auth & Authz Done? |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| [Name of task] | [0-100%] | [List actual blockers found in files] | [Inspected Status] | [Inspected Status] | [Inspected Status] | [Yes/No/Partial] | [Yes/No/Partial] |

##### 2. Direct Source Verification Proof
For each module in the table, list the exact file paths you read to confirm the status, along with a 1-sentence note of the actual class/method/component logic found (e.g., *"Verified endpoint implementation in SalesInvoicesController.cs and reactive forms in sales-invoice-form.component.ts"*).

##### 3. Outdated Tracking Synced (Audit Trail)
List any discrepancies where manual `.md` checklists in the repository listed a task as pending or at a lower percentage, but your raw codebase inspection proved it was finished or in progress.

##### 4. Suggested Next Tasks & AI Agent Routing
Recommend the next development priorities based strictly on actual codebase gaps, routing the work to Claude (full-stack commits directly to main) or other agents (scaffolding/boilerplate for manual human review).

```

***

### 💡 Why this prompt forces the AI to check the raw code:
*   **The Untrusted-Source Guardrail:** By explicitly marking static checklists and `.md` files as *untrusted/outdated* drafts, it shuts down the AI's tendency to take the "easy path" of parsing other markdown files instead of code.
*   **Mandatory verification proofs:** The requirement to print out the exact file paths and a 1-sentence summary of the C# or TypeScript logic forces local coding agents (like Claude Code) to physically run file searches and open those files.
*   **A Git-Reality Hook:** Directing the agent to read current branch commit logs and git statuses forces it to realize when a feature has been recently developed and merged, preventing destructive prompts that might overwrite your 100% finished modules.

***

⚙️ Would you like me to draft a quick instruction showing you how to permanently embed this status review prompt into your `.agents` or `.claude` workspace files so your tools enforce this behavior on launch?

---
---

# AGENTS.md — agent onboarding (restored 24 August 2026)

**The status-review prompt above and this section serve different purposes and both stay.** The prompt above is a request template for a status report. Everything below is what a coding agent — Antigravity, Copilot, Cursor, or anything else that is not Claude Code — needs to read before writing a line. It was the entire file until commit `e20af93` replaced it with the prompt above; it is restored here rather than reverted over it, so neither is lost. **Claude Code reads [`CLAUDE.md`](./CLAUDE.md) instead of this section; this section is the same rules for everyone else.**

Read this before writing a line. The rules here are decisions already taken, not suggestions to weigh.

**RetailErp** is a multi-tenant retail ERP and accounting SaaS for Indian SMBs. .NET 10 + EF Core + PostgreSQL on the backend, Angular 20 + Nx on the frontend.

---

## Senior and junior

**Claude Code is senior. Antigravity is junior.** This was established for Reporting (`docs/Reporting.md` §9.1, `docs/ai-agent-structure-rules.md`) and is now the general model for this repository, by the repository owner's preference stated 24 August 2026 — not a Reporting-specific arrangement.

The split is not a claim about which model is more capable — it is about where a defect surfaces. A missing query filter serves another branch's ledger to a customer and nothing turns red. A page that doesn't render is obvious inside a minute. **Senior owns the pieces whose mistakes are silent: schema, tenancy, RLS, the ledger, anything a passing test could still get wrong. Junior owns the pieces whose mistakes are loud: controllers, pages, seed data, the volume work built on a foundation senior already laid.**

**Senior reviews every junior commit before it merges.** Where a stage has verification gates (a schema whose `OrgId`/RLS/query-filter coverage cannot be confirmed by reading the diff), those gates are signed off by querying as a second organization and getting zero rows back — never by reading the code. `docs/Reporting.md` §9.1's G1–G3 gates are the worked example of what this looks like and why "cleared by inspection" was found broken by measurement once already.

**Stop and ask rather than invent.** A junior that guesses at the tenancy model, a column's meaning, or a lifecycle rule writes code that passes its own tests and is wrong in production. Ambiguity in a brief is a question, not a judgement call.

---

## The five rules that cause damage when broken

The full list is in [`docs/ai-agent-structure-rules.md`](./docs/ai-agent-structure-rules.md), which is written for you by name. These five are the ones where a mistake is silent — it compiles, it passes tests, and it is wrong in production.

**1. LINQ only. Never write raw SQL.** The only exceptions, because no LINQ equivalent exists: `CREATE DATABASE`, RLS policies, triggers, `set_config`. Every query, insert, update and delete is LINQ.

**2. Every per-customer table carries `OrgId`, a global query filter, and an RLS policy.** All three. `OrgId` is the branch boundary and it is load-bearing for security — a missing filter serves one customer's ledger to another, and nothing turns red when it happens. Inherit `Shared.Kernel.Tenancy.OrgScopedEntity` and the filter comes from `TenantDbContext` automatically; the RLS policy you write by hand in the migration.

**3. Org context must reach the query, and must never outlive the request.** Pooled connections are reused across requests, so a value left behind leaks to whoever borrows that connection next. **Do not "fix" `RlsConnectionInterceptor` back to `set_config(..., true)`** — that was tried, and because `ConnectionOpenedAsync` runs outside any transaction the transaction-local value was discarded before the next statement, so org context was never set at all. It now sets the value session-level and overwrites it unconditionally on every connection open, clearing it when there is no org, which is what closes the leak. `Reporting.md` §9.9 has the measurements.

**4. Entities are plain property bags.** No constructors, no methods, no validation logic, no computed properties. Just `public X Y { get; set; }` with Data Annotations — and **every Data Annotation carries an `ErrorMessage`**.

**5. Never reference another service's `DbContext`.** Use its API or an event. The reporting service has a **recorded exception** to this, described in `docs/Reporting.md` §2 — that exception is specific to reporting and does not generalize. (One nuance worth stating explicitly: `cus` and `con` are schemas in the **same** per-customer physical database, unlike `mst`, so a real foreign key between them is possible and preferred over an unenforced id — this is different from the `mst` case, where cross-database ids are always unenforced and validated in C#.)

Also, without exception: **PascalCase table and column names** matching the C# property names; **PostgreSQL only**, never add SQL Server compatibility; **enums, not magic strings**; **never set audit fields manually** — `AuditSaveChangesInterceptor` does it.

---

## Branch

**Everything commits to `main`. There is no other branch. Never create a new branch — not one.** Not for a feature, not for a task, not because a tool or harness assigns one by default. If something outside your control puts you on a branch anyway, treat it as a mistake to correct rather than a place to keep working: do only what you cannot avoid there, then merge it into `main` and delete the branch before you stop. This happened once already — a session was assigned `claude/project-status-review-4us30i` by its harness, worked there for a stretch, and the branch had to be reconciled and merged back afterward. Don't repeat it.

```
git pull origin main      # before you start, every time
git push -u origin main
```

`Report` merged into `main` on 17 August 2026 and the exception ended with it, by the repository owner's instruction of the same day. **Do not push to `Report`, and do not branch off it** — it is left in place pointing at the merge and carries nothing `main` lacks.

**The divergence warning that stood here since 24 August 2026 is cleared, 2 September 2026.** `origin/main` and the `claude/project-status-review-4us30i` branch it described have been reconciled — that branch no longer exists, and `origin/main` is a single, non-diverged line of history (most recently squashed to one EF Core migration per module in commit `befdac6`). If a future session finds `main` diverged from a working branch again, treat it the same way this entry did: name the branch and commit counts here rather than guessing, and clear the note once it is actually resolved rather than leaving it to go stale.

Because more than one agent shares this branch, **pull before you start and pull again before you push**. A rebase onto `origin/main` is the normal way to land: your commits go on top of whatever arrived while you were working, and nothing needs merging afterwards.

```
git pull --rebase origin main
```

**Do not open a pull request unless you are asked for one.** There is no second branch for one to merge from.

---

## What you own

**The active brief is Customer service (CRM/Support), stage C2/C3** — see [`docs/Customer.md`](./docs/Customer.md). C0 (the four open questions) and C1 (schema, tenancy, RLS) are done and audited as of 2 September 2026; C2 (controllers) is written but has two open findings (C1-3, C1-4 in that doc) to pick up before starting C3. **C3 is not untouched either**: `customer-ui` has five real components and `apps/web` lazy-loads `customer/leads` and `customer/tickets` today.

Reporting (`docs/Reporting.md` §9–12) is **built but was not "finished with nothing waiting on review"** — that was true when written and was overtaken the same week. Fifteen tracker and finance sources landed registered in neither the container, the catalog seeder, nor `ReportSourceTests.Sources`, so 239 tests passed over reports no screen could reach. All 41 are wired now and the suite is 344; wiring them surfaced a real fault two of them had been hiding. The lesson generalises: a report needs a **registered source, a seeded row, and a line in the test list**, and only the third makes the first two check each other.

---

## Packages

**Do not add a package. Not one, backend or frontend.**

`backend/Directory.Packages.props` and `frontend/package.json` are closed lists. If a task appears to require something not already pinned, **the task is wrong — stop and say so** rather than adding a dependency.

What you already have and should use: `@angular/cdk` for drag-and-drop (nine pages use it — copy the idiom from `numbering-series.page.ts`), `DocumentFormat.OpenXml` for reading and writing `.xlsx`, EF Core and Npgsql, YARP, xunit.

What you do **not** have and must not reach for: any grid or chart library. (Exception: PDFsharp is permitted for server-side PDF generation). Syncfusion appears in comments and a mock class; it is not installed.

---

## Angular

- **Standalone components only.** No `NgModule`.
- **`inject()`**, not constructor injection.
- **`signal()` and `computed()`** over RxJS `BehaviorSubject`.
- **`async`/`await` over promises** for REST calls, rather than heavily piped streams.
- **Separate `templateUrl` and `styleUrl`.** No inline templates beyond trivial wrappers.
- **File suffixes mean something**: `.page.ts`, `.dialog.ts`, `.list.ts`, `.component.ts`.
- **`-core` libs stay Ionic-compatible**: no `window`, no `document`, no Electron or Node APIs.
- **Every page works at ~360px.** Grids become card lists, forms stack, modals become full-screen sheets.

---

## Before every commit

```
cd frontend && npm run check          # lint, typecheck, tests, both builds
cd backend  && dotnet build && dotnet test
```

`dotnet build` must be clean — `TreatWarningsAsErrors` is on, so a warning is a failure. Database-backed tests skip with a reason when no PostgreSQL answers. **`npm run check` passes as of 2 September 2026** — lint across 23 projects, typecheck, 474 tests, all five app builds. The `allocation-form.component.html` lint failure noted here on 24 August is fixed, along with the Ionic imports that had kept `accounting-ui` from typechecking at all. The backend is green too: **756 tests, all passing, none skipped.**

**A commit that does not build blocks whoever is working in parallel with you.**

Commit messages follow [`docs/commit-rules.md`](./docs/commit-rules.md): `feat(customer): add the lead entity`. Imperative mood, no capital, no full stop.

**Documentation ships in the same commit as the feature.** A user-visible change updates its page under `frontend/apps/docs/content/`, its status in `docs.manifest.ts`, and adds a bullet under **Unreleased** in `release-notes.md`. Not a sweep before release — by then the detail is gone.

---

## Where the specifications are

| Area | File |
|---|---|
| **Customer service (CRM/Support) — the active brief** | [`docs/Customer.md`](./docs/Customer.md) |
| **Reports — the grid, the engine, the report catalog** | [`docs/Reporting.md`](./docs/Reporting.md) — start at §9 |
| Structural rules for agents | [`docs/ai-agent-structure-rules.md`](./docs/ai-agent-structure-rules.md) |
| Coding standards | [`docs/coding-standards.md`](./docs/coding-standards.md) |
| Project layout | [`docs/project-structure.md`](./docs/project-structure.md) |
| Overall specification | [`docs/Specification.md`](./docs/Specification.md) |
| Per-module task checklists | [`docs/`](./docs/) |

---

## Tenancy, in one screen

Getting this wrong is the most expensive mistake available here, so it is worth the sixty seconds.

**Two levels, not three.** A **Customer** is the head office — the account, the billing relationship — and shares **one physical database** with every other Customer. An **Organization** is a branch: one place the business trades from, one complete set of books, with its own code, GSTIN, currency and numbering.

- Customer ↔ Customer is separated by **`CustomerId` + EF query filter + Postgres RLS**.
- Organization ↔ Organization is separated by **`CustomerId` + `OrgId` + EF query filter + Postgres RLS**.

*(Until 24 August 2026 a Customer owned a physical database of its own; reversed 25 August 2026 — see CLAUDE.md's Tenancy section for the full account.)*

**There is no `Branches` table and no `BranchId` column.** `OrgId` *is* the branch. If you find yourself wanting `BranchId`, you want `OrgId`.

**A branch is a hard data boundary, not a reporting tag.** Each has its own items, contacts, stock, chart of accounts and numbering. Nothing crosses. Consolidated reporting across branches is a deliberate read above the query filter — never a relaxed filter.

---

## When to stop and ask

**Ambiguity is a question, not a judgement call.** An agent that guesses at the tenancy model writes code that passes its own tests and leaks data between customers.

Stop and ask when:

- a task seems to need a package that is not pinned;
- a spec does not say what a column means, and you would have to invent it;
- something in the codebase contradicts what you were told here;
- you are about to write raw SQL for anything other than the four permitted cases;
- you are about to add a table without `OrgId`.

Where a task says "copy `Inventory.Api/Program.cs`" or "copy `AccountMovementSource.cs`", that is an instruction rather than a hint. Consistency is what keeps the product maintainable by one person reading many services.
