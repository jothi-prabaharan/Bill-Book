# AGENTS.md

Instructions for any AI coding agent working in this repository — Antigravity, Copilot, Cursor, or anything else. **Claude Code reads [`CLAUDE.md`](./CLAUDE.md) instead; this file is the same rules for everyone else.**

Read this before writing a line. The rules here are decisions already taken, not suggestions to weigh.

**RetailErp** is a multi-tenant retail ERP and accounting SaaS for Indian SMBs. .NET 10 + EF Core + PostgreSQL on the backend, Angular 20 + Nx on the frontend.

---

## The five rules that cause damage when broken

The full list is in [`docs/standards/ai-agent-structure-rules.md`](./docs/standards/ai-agent-structure-rules.md), which is written for you by name. These five are the ones where a mistake is silent — it compiles, it passes tests, and it is wrong in production.

**1. LINQ only. Never write raw SQL.** The only exceptions, because no LINQ equivalent exists: `CREATE DATABASE`, RLS policies, triggers, `set_config`. Every query, insert, update and delete is LINQ.

**2. Every per-customer table carries `OrgId`, a global query filter, and an RLS policy.** All three. `OrgId` is the branch boundary and it is load-bearing for security — a missing filter serves one customer's ledger to another, and nothing turns red when it happens. Inherit `Shared.Kernel.Tenancy.OrgScopedEntity` and the filter comes from `TenantDbContext` automatically; the RLS policy you write by hand in the migration.

**3. Org context must reach the query, and must never outlive the request.** Pooled connections are reused across requests, so a value left behind leaks to whoever borrows that connection next. **Do not "fix" `RlsConnectionInterceptor` back to `set_config(..., true)`** — that was tried, and because `ConnectionOpenedAsync` runs outside any transaction the transaction-local value was discarded before the next statement, so org context was never set at all. It now sets the value session-level and overwrites it unconditionally on every connection open, clearing it when there is no org, which is what closes the leak. `REPORTS.md` §9.9 has the measurements.

**4. Entities are plain property bags.** No constructors, no methods, no validation logic, no computed properties. Just `public X Y { get; set; }` with Data Annotations — and **every Data Annotation carries an `ErrorMessage`**.

**5. Never reference another service's `DbContext`.** Use its API or an event. The reporting service has a **recorded exception** to this, described in `docs/architecture/REPORTS.md` §2 — that exception is specific to reporting and does not generalize.

Also, without exception: **PascalCase table and column names** matching the C# property names; **PostgreSQL only**, never add SQL Server compatibility; **enums, not magic strings**; **never set audit fields manually** — `AuditSaveChangesInterceptor` does it.

---

## Branch

**Everything commits to `main`. Reporting included. There is no other branch.**

```
git pull origin main      # before you start, every time
git push -u origin main
```

Reporting used to be an exception and is not any more. `Report` merged into `main` on 17 August 2026 and the exception ended with it, by the repository owner's instruction of the same day. **Do not push to `Report`, and do not branch off it** — it is left in place pointing at the merge and carries nothing `main` lacks. Anything you commit there is work `main` will not have.

Because two agents now share one branch, **pull before you start and pull again before you push**. A rebase onto `origin/main` is the normal way to land: your commits go on top of whatever arrived while you were working, and nothing needs merging afterwards.

```
git pull --rebase origin main
```

**Do not open a pull request unless you are asked for one.** There is no second branch for one to merge from.

---

## What you own

**Every outstanding reporting task is yours**, by the repository owner's instruction of 18 August 2026 — stage **R7** in `docs/architecture/REPORTS.md` §9.2, and section 10 of `docs/architecture/REPORTS-ANTIGRAVITY-BRIEF.md`. Claude Code's queue is empty; nothing is waiting on a review gate and there is nobody to hand a task back to.

That includes **G3**, the row-level-security gate, which was previously Claude Code's own verification. A gate is signed off by querying as a second organization and getting zero rows back — **never by reading the diff**. G3 has already been marked cleared once by inspection and found broken by measurement, which is why this sentence exists.

---

## Packages

**Do not add a package. Not one, backend or frontend.**

`backend/Directory.Packages.props` and `frontend/package.json` are closed lists. If a task appears to require something not already pinned, **the task is wrong — stop and say so** rather than adding a dependency.

What you already have and should use: `@angular/cdk` for drag-and-drop (nine pages use it — copy the idiom from `numbering-series.page.ts`), `DocumentFormat.OpenXml` for reading and writing `.xlsx`, EF Core and Npgsql, YARP, xunit.

What you do **not** have and must not reach for: any grid, chart, PDF or spreadsheet library. Syncfusion appears in one comment and one mock class; it is not installed.

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

`dotnet build` must be clean — `TreatWarningsAsErrors` is on, so a warning is a failure. Database-backed tests skip with a reason when no PostgreSQL answers.

**A commit that does not build blocks whoever is working in parallel with you.**

Commit messages follow [`docs/standards/commit-rules.md`](./docs/standards/commit-rules.md): `feat(reporting): add the generic query builder`. Imperative mood, no capital, no full stop.

**Documentation ships in the same commit as the feature.** A user-visible change updates its page under `frontend/apps/docs/content/`, its status in `docs.manifest.ts`, and adds a bullet under **Unreleased** in `release-notes.md`. Not a sweep before release — by then the detail is gone.

---

## Where the specifications are

| Area | File |
|---|---|
| **Reports — the grid, the engine, 45 reports, and the task list** | [`docs/architecture/REPORTS.md`](./docs/architecture/REPORTS.md) — start at §9 |
| Structural rules for agents | [`docs/standards/ai-agent-structure-rules.md`](./docs/standards/ai-agent-structure-rules.md) |
| Coding standards | [`docs/standards/coding-standards.md`](./docs/standards/coding-standards.md) |
| Project layout | [`docs/standards/project-structure.md`](./docs/standards/project-structure.md) |
| Overall specification | [`docs/architecture/Specification.md`](./docs/architecture/Specification.md) |
| Per-module task checklists | [`docs/modules/`](./docs/modules/) |

---

## Tenancy, in one screen

Getting this wrong is the most expensive mistake available here, so it is worth the sixty seconds.

**Two levels, not three.** A **Customer** is the head office — the account, the billing relationship — and owns **one physical database**. An **Organization** is a branch: one place the business trades from, one complete set of books, with its own code, GSTIN, currency and numbering.

- Customer ↔ Customer is separated by **different databases**.
- Organization ↔ Organization is separated by **`OrgId` + EF query filter + Postgres RLS**.

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

Where a task says "copy `Inventory.Api/Program.cs`" or "copy `AccountMovementSource.cs`", that is an instruction rather than a hint. Consistency is what keeps forty-five reports maintainable by one person.
