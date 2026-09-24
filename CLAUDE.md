# CLAUDE.md

Instructions for Claude working on **RetailErp** — a multi-tenant retail ERP and accounting SaaS for Indian SMBs. Bill Books is the functional benchmark.

Read this before writing any code. The rules here are decisions already made — follow them rather than re-deciding.

---

## Hard rules

These are non-negotiable. Violating them means the code gets rejected.

1. **LINQ only. Never write raw SQL.** The only exceptions, because no LINQ equivalent exists: `CREATE DATABASE`, RLS policies, triggers, `set_config`. Everything else — every query, insert, update, delete — is LINQ.
2. **Entities are plain property bags.** No constructors. No methods. No validation logic. No computed properties. Just `public X Y { get; set; }` with Data Annotations.
3. **Every Data Annotation needs `ErrorMessage`.**
4. **PascalCase table and column names**, matching C# property names exactly. Postgres needs quoted identifiers for this — that's expected.
5. **PostgreSQL only.** Never add SQL Server compatibility, never avoid a Postgres feature for portability. RLS, `xmin`, and JSONB are all in use deliberately.
6. **All table entities inherit `Shared.Kernel.Entities.AuditableEntity`.** Never set audit fields manually — `AuditSaveChangesInterceptor` does it. All four audit columns are **nullable**; `CreatedBy IS NULL` marks system/seed master data (written by no user).
7. **Enums, not magic strings**, for any fixed set of values.
8. **Never cross a service boundary by referencing another service's `DbContext`.** Use its API or an event. Master maps two contexts, and that is a database boundary rather than a service one — `mst` and `con` are different Postgres databases, so there is still no foreign key between them and ids across them are still validated in C#.
9. **Ask before expanding scope.** If a request is ambiguous, present a short plan and wait rather than building the larger interpretation.
10. **Ship documentation with the feature, in the same commit.** A user-visible change updates its page under `frontend/apps/docs/content/`, its status in `docs.manifest.ts`, and adds a bullet under **Unreleased** in `release-notes.md`. Not a sweep before release — by then the detail is gone and someone is reverse-engineering a month of git log.
11. **Every feature task commits to `main`.** Not a feature branch, not a session branch, not a branch per stage — `main`, as each piece of the task is finished. See below.
12. **Never create a new branch. Not one.** Not for a feature, not for a session, not because a harness or tool assigns one by default. If something outside your control puts you on a branch anyway, do the work there only because you have no choice, then merge it into `main` and delete it before you stop — a branch that outlives the session that made it is the failure this rule exists to prevent.
13. **Every write endpoint runs in a transaction, and no controller opens one.** `AddBillBookReliability<TContext>()` registers the filter; inside a service use `await _db.Database.BeginScopeAsync(ct)`, never `BeginTransactionAsync` — the raw call throws the moment anything above it has started a transaction. Commit is earned by the returned status, so a `Conflict()` after a partial write rolls back. Full rules in [`docs/Architecture.md`](./docs/Architecture.md).
14. **No `catch` block writes its own message for a caller.** Every failure goes through `SqlErrorCatalog` → status, `ApiErrorCode`, curated sentence. **Development returns the exact database error; every other environment returns the sentence and an `errorReference`**, with the detail in the service's own `{schema}.ErrorLogs`. A curated message may never name a table, column, constraint or figure. Workers audit to the same table with `FollowUpStatus = Open` — that set is the task list.

---

## Pending work — `docs/TASKS.md`

**All pending work is one queue in [`docs/TASKS.md`](./docs/TASKS.md).** Before starting any
task:
- read its section 0;
- take the first card that is claimable. That means it is open, its dependencies are done, no
  decision blocks it, and none of its lanes is held by another card;
- **card numbers are the order of work**: TK-01 first, then TK-02, and so on (renumbered
  24 September 2026; section 4 of `docs/TASKS.md` maps the old numbers);
- **claim it with a claim commit on `main` before doing any work**;
- mark it `working (AI name)`, and later `completed (AI name)`.

**AI agents write unit tests and never run them.** An AI writes the tests for every change. It does
not run `dotnet test`, `npm run test`, `npm run check` or any other test runner, because the
repository owner runs the tests. It still runs the build, lint and typecheck. A finished card is
marked `completed (AI name) · tests written, not run`. The rule is in section 0.5 of
`docs/TASKS.md`, and it overrides every instruction below to run tests before claiming something
works.

Lanes are what let several agents work at once without editing the same files.

---

## Git — how work reaches main

> **Note. Never create a new branch. Commit every change directly to the default branch, `main`.**

There is one branch, and it is `main`. Every commit goes there as the work is done — no feature branch, no session branch, nothing to merge afterwards. A branch invented mid-task splits the work across two places and leaves whichever one nobody merges behind, which is how a change that was written and reviewed goes missing from the product.

The same applies to a follow-up: commit it to `main` alongside the work it follows, rather than opening a branch beside it.

### There is no exception any more. Reporting merged.

Reporting was the one standing exception: for a fortnight it committed to `Report`, because it was built by two agents in parallel and it seemed easier to merge one branch than to interleave two agents on `main`.

**`Report` merged into `main` on 17 August 2026 and the exception ended with it**, by the repository owner's instruction of the same day. `main` is again the only branch, reporting included.

The exception did not pay for itself, which is worth recording so it is not reinvented. Both agents ended up committing the same non-reporting work to both branches independently, so fifteen commits on `main` had content-identical twins on `Report` under different ids, and the merge had to reconcile a history that had said the same thing twice. One conflict resolved cleanly and wrongly — git reverted a fix to `Bill-Book.sln` because the same lines had been touched on both sides — and nothing would have caught it but a diff of the merged tree against the branch.

**`Report` is left in place pointing at the merge. Do not push to it and do not branch from it.** It carries nothing `main` lacks.

**Two agents on one branch is the arrangement now**, so pull before starting and pull again before pushing; `git pull --rebase origin main` puts your commits on top of whatever landed while you were working.

### This covers every feature task, without exception

A task with a stage number — **T2.3**, **T4.4**, **T5.3** — is not a reason to open a branch. Neither is a task that spans a schema, an API, a page and a ledger posting, and neither is one that will take several sessions. **Each of those commits to `main` as it is finished**, in whatever pieces it naturally divides into:

- schema and migration, then the service, then the controller, then the page — each its own commit on `main` when it is written and building, rather than one branch holding all four until the end
- a fix to work already on `main` goes on `main` beside it
- documentation goes in the **same** commit as the code it describes (hard rule 10), which is only possible when both land on the same branch

**Commit at the point the work stands up, not at the point the task is finished.** A stage that builds and whose tests pass is worth committing even if the stage after it has not been started — the next session picks up from `main` and needs no instructions about where the work is. Work parked on a branch is work the next session cannot find.

**If a session is started on a branch by its harness**, do the work there if the harness requires it, but merge it into `main` before the session ends and say plainly that you did. A branch that outlives its session is the failure this rule exists to prevent.

**Never open a pull request unless it is asked for.** The branch model has nothing for a PR to do — there is no second branch to merge from.

The one thing this rule does not override: **push only where you have been told to push.** Committing to `main` locally is always right; pushing to any remote branch other than the one you were given needs saying so first.

---

## When asked for status

Always report in **this order**, each with a completion status:

1. **Master**
2. **Accounting**
3. **Contacts**
4. **Inventory**
5. **Transactions**
6. **Reports**
7. **Settings**

Present each area as a table with these columns:

| Master | Schema | Rows | API | Page |

State what is built, what is designed but uncoded, and what is not designed — never blur those three together. "Designed" is not "done".

---

## When asked to add a table

Column-level schemas and page specs live under [`docs/`](./docs/) — check there before designing anything new. One file per module: [`docs/Modules.md`](./docs/Modules.md), [`docs/Modules.md`](./docs/Modules.md), [`docs/Modules.md`](./docs/Modules.md), [`docs/Modules.md`](./docs/Modules.md), [`docs/Modules.md`](./docs/Modules.md), [`docs/Modules.md`](./docs/Modules.md), [`docs/Modules.md`](./docs/Modules.md), each with its columns, decisions and tasks in one place. [`docs/Modules.md`](./docs/Modules.md) is the odd one out: printing has no service yet, so that file is the design for giving it one rather than a record of a module that exists — what is built is in `Master.md` stage 7.

**`Transactions_And_Specs.md`, `SALES.md` and `PURCHASE.md` do not exist and have not for some time** — they are named throughout older notes, and every reference to them means the `docs/` file above. `docs/Modules.md` is itself two documents concatenated: the first half is current, the second half from "designed, not coded" onward is stale and contradicts it.

Produce, in this order:
1. Entity class in `{Module}.Entity/TableEntities/{Name}.cs`
2. Enums (if any) in `{Module}.Entity/Enums/`
3. `DbSet` + Fluent config in `{Module}.Repository/{Module}DbContext.cs`
4. Seed data if it's reference data

Do **not** write CREATE TABLE SQL. This is EF Core code-first — migrations generate the schema.

Every per-customer table needs `OrgId` plus a global query filter. Master-database tables (the `mst` schema) do not.

---

## When asked to add an endpoint

1. Request/response models in `{Module}.Entity/Models/` (Data Annotations with error messages)
2. Controller action in `{Module}.Api/Controllers/`
3. Validate the caller's `OrgId` matches the target resource's — always
4. Return `Forbid()` when the token's org does not match an org id the route names. A row id that is not in the caller's branch is `NotFound()`: row-level security hides other branches' rows from the service itself, so there is nothing to tell "another branch's" from "no such row", and a 403 would confirm the id exists in someone else's books. Never step past the query filter to find out (decided 23 September 2026, TK-71)

---

## Project layout

Two top-level halves. Inside `backend/`, four groups — `Api/`, `shared/`, `worker/`, `Gateway/`.

```
backend/
├── Bill-Book.sln
├── Api/
│   └── {Module}/                one folder per service (×8)
│       ├── {Module}.Entity/
│       ├── {Module}.Repository/
│       └── {Module}.Api/
├── shared/
│   └── Shared.Kernel/
├── worker/
│   ├── Notification.Worker/
│   ├── CostingEngine.Worker/
│   └── RateSync.Worker/
└── Gateway/
frontend/
├── apps/                    web · portal · admin · desktop · docs
└── libs/
    ├── {module}/                one folder per module, mirroring Api/
    │   ├── {module}-core/
    │   └── {module}-ui/
    └── shared/
```

Three projects per service, no more — all three under `backend/Api/{Module}/`:

```
{Module}.Entity/       TableEntities/ · Models/ · Enums/
{Module}.Repository/   DbContext, repositories, seed data
{Module}.Api/          controllers, services, DI
```

Dependency direction: `Api` → `Repository` → `Entity` → `Shared.Kernel`. Never backwards.

**Services** (8): Master, Inventory, Accounting, Sales, Purchase, Customer, Reporting, Printing
**Background workers** (3): Notification, CostingEngine, RateSync
**Gateway**: YARP

There were twelve. Three merges took them to seven, and the reason each time was that two services were two halves of one job:

| Now | Was | Because |
|---|---|---|
| **Master** (`mst`, `con`) | Master + Platform + Identity + Contacts | Signing in reads a user, their branches and the customer's licence — one query now, two service hops before |
| **Accounting** (`acc`) | Accounting + Banking | A money document exists to move a balance in the ledger; the two could not share a transaction while they were separate |
| **Customer** (`cus`) | Crm + Support | A lead becomes a customer and a customer raises a ticket — one subject, one lifecycle. Both were empty scaffolds |

**Then one split, the other way: Printing (`prt`) left Master** (cut over 24 September 2026, TK-81). It is the only work that shares a transaction with nothing, the only one growing a native dependency (an HTML parser today, a PDF engine next) and the only one that is CPU-bound and bursty, which is the opposite of the reason each merge was made. It owns one table and reads nobody else's. The argument is written out in the Printing section of [`docs/Modules.md`](./docs/Modules.md).

**Master is the only service with two DbContexts, and that is the tenancy model rather than an accident.** `AdminDbContext` is the shared master database; `ContactsDbContext` is the customer's own. See Tenancy below.

**Frontend** (Nx, Angular v20): `apps/{web, portal, admin, desktop, docs}` · `libs/{module}/{module}-core` (view-models + models, no templates) + `libs/{module}/{module}-ui` (pages) · `libs/shared/{auth, api-client, ui-components, currency-format, theming}`

### Angular Component Structure
- **Standalone Only**: Use `standalone: true`. No `NgModules` are allowed.
- **Dependency Injection**: Use the `inject()` function (e.g., `private readonly http = inject(HttpClient);`) instead of constructor injection.
- **State & Reactivity**: Use `signal()` and `computed()` for component state over RxJS `BehaviorSubject` where possible.
- **Data Fetching**: Use `async/await` with Promises for straightforward REST calls instead of heavily piping RxJS streams (e.g., `await this.req(...)`).
- **File Naming**: Suffix component files accurately according to their role (`.page.ts`, `.dialog.ts`, `.list.ts`, `.component.ts`).
- **Separation of Concerns**: Use separate `templateUrl` and `styleUrl` instead of inline templates for anything beyond trivial wrappers.

`-core` libs must stay Ionic-compatible: Signals and DI are fine, but no `window`/`document`, no Syncfusion, no Electron/Node APIs.

Every page must work at ~360px — grids become card lists, forms stack, modals become full-screen sheets.

---

## Tenancy — the thing most likely to be got wrong

**Two levels, not three.**

**Customer** = the **head office**. The account, the billing relationship, the licence. Shares **one physical database** with every other Customer.
**Organization** = a **branch**. One place the business trades from, and one complete set of books — own code, GSTIN, address, currency. A Customer owns **many**, all sharing that Customer's rows in that database.

| Boundary | Enforced by |
|---|---|
| Customer ↔ Customer (head office ↔ head office) | `CustomerId` + EF Core query filter + Postgres RLS |
| Organization ↔ Organization (branch ↔ branch) | `CustomerId` + `OrgId` + EF Core query filter + Postgres RLS |

Both rows are enforced the same way now — the difference is only which column, or pair of columns, the filter checks. Before, a Customer's own database was the isolation; now every table also carries `CustomerId`, checked alongside `OrgId` in the same query filter, the same RLS policy and the same `set_config` call. This is defence in depth, not a replacement: `OrgId` was already globally unique on its own, so `CustomerId` doubles the check rather than being the only thing standing between two customers' rows. *(Decided 25 August 2026 — see the note at the end of this section.)*

**There is no separate Branches table, and no `BranchId` column anywhere.** `OrgId` *is* the branch. A second column naming a branch on the same row says the same thing twice, and only `OrgId` is ever enforced — a `Branches` table existed briefly and was removed for exactly that reason. If you find yourself wanting `BranchId`, you want `OrgId`.

**A branch is a hard data boundary, not a reporting tag.** Each branch has its own items, contacts, stock, chart of accounts and numbering series. Nothing crosses between them. Consolidated reporting across branches is a **read across organizations**, done deliberately and above the query filter — never by relaxing it.

**`CustomerId` and `OrgId` are both load-bearing for security.** A missing query filter leaks data between branches, or between customers entirely. Never omit either on a per-customer table.

Per request: resolve `CustomerId` and `OrgId` from JWT → set `app.current_customer_id` and `app.current_org_id` transaction-locally (`RlsConnectionInterceptor` does it with a `SET LOCAL` in front of every command, so a read with no explicit transaction is covered by its own). **Never connection-level** — pooled connections are reused across requests and would leak tenant context. There is no connection to pick any more: every service opens the one tenant database at startup, the same way it has always opened the one master database.

### Schemas

Master database: `mst` (countries and states, users and roles), `rat` (currency + metal rates)

Tenant database — every customer, together: `con` `inv` `sal` `pur` `acc` `cus` `rpt` `prt` `ntf`

Platform and Identity schemas were folded into `mst`, and `bnk` into `acc`; `crm` and `sup` became `cus`. Nothing about tenancy changed with them — `mst` is still the shared database and every per-customer schema still carries `CustomerId` and `OrgId` with a query filter and an RLS policy.

**`con` did not move into `mst` and must not.** A contact belongs to one branch's books and lives in the tenant database alongside every other customer's; the tables in `mst` are a different kind of shared — global reference and account data, not scoped by `CustomerId` at all. They are still in different Postgres databases, which is why Master holds two DbContexts rather than one; what changed is that `ContactsDbContext`'s database is no longer one per customer.

### Provisioning
- **New Customer**: create row → generate `CustomerCode` → create the owner user → seed every service's master data into the tenant database, scoped to the new `CustomerId` and `OrgId` → mark Active. All synchronous, in the request — there is no database left to create and wait on, so nothing has to happen in the background any more. Block login until every service confirms its seed; a customer a service could not reach stays at `Provisioning` (or, for a public signup with no way back in to retry, the terminal `Failed`) rather than being handed a login into an empty chart of accounts. `apps/admin` lists every customer's status and can retry either one — seeding is idempotent, so a retry only adds what a service is still missing.
- **New Organization (a new branch)** under an existing Customer: insert row with its own `OrgCode`, seed its full master data — Chart of Accounts, Tax Master, numbering series, units, payment terms. No new database, same as before. A branch starts empty and is seeded exactly like the first one, because it is a complete set of books.

### Cross-database FKs are impossible in Postgres
- `CreatedBy`/`ModifiedBy` (Users are in the master database) → plain nullable `Guid`, no FK. Resolve names from `mst.Users` in C#, **batched** — watch for N+1 on list screens.
- Contacts referencing `mst` Countries/States → unenforced ids, validate in C#. **Master holding both contexts does not make this a foreign key** — they are still two databases, and one service mapping both is not one database.

### One database per customer, decided, reversed, and then partly reinstated

**Read this before the section below, which describes an architecture the code no longer has.**

The 25 August reversal to a single shared tenant database was itself superseded by the sharded-tenancy work (commit `7cccf1f`, "implement sharded tenant databases and dynamic connection resolution"). What is on `main` as of 4 September 2026:

- **`mst.TenantDatabases` is a shard registry** — a row per physical database, with a `PlanType` and a `MaxOrganizations` / `CurrentOrganizations` capacity.
- **`mst.Customers.DatabaseName` names the shard a customer's books live in.** It is not vestigial; see the signup caveat above for how nearly it was deleted, and why nothing would have caught that.
- **`ITenantDatabaseResolver` maps a request's `CustomerId` to a connection string**, cached ten minutes, reading that column in raw SQL.
- `DatabaseMigrationService` provisions the first shard (`IN000001`) on startup and migrates all eight schemas into it.

So it is **many customers per database, several databases**, rather than either one-per-customer or one-for-everyone. Isolation inside a shard is unchanged and is still the thing doing the work: `CustomerId` + `OrgId`, an EF query filter, an RLS policy that is enabled **and FORCEd**, and a transaction-local `set_config`. The shard boundary is a capacity and blast-radius measure on top of that, not the isolation mechanism — two customers on one shard are as isolated as two branches of one customer, which is the property the tests assert.

**The section below is kept because its reasoning about why the reversal happened is still sound and still worth not reinventing.** Its claim about the current shape is not.

### One database per customer, decided and reversed

**One physical database per Customer was the model through 24 August 2026.** On 25 August 2026, by the repository owner's instruction, it was reversed: every Customer now shares one tenant database, isolated from every other Customer the same way a branch is already isolated from another branch in the same Customer — `CustomerId`, a query filter, an RLS policy, a `set_config` call — rather than by a database boundary. `CustomerId` was added to every table that carries `OrgId`. Greenfield: there was no released deployment and no real customer data to migrate, so nothing needed a backfill.

The trigger was **new-customer provisioning becoming an operator-driven admin screen** (`apps/admin`) rather than a background worker running `CREATE DATABASE` per signup — a shape that does not suit a thousand customers each waiting on their own database, template clone and Key Vault entry. `ITenantConnectionResolver`, the per-request tenant directory, `CustomerDatabase`, `ProvisioningWorker` and the `CustomerProvisioned` event are all gone; a service now opens the one tenant database at startup, the same way it has always opened the one master database, and signup was meant to seed a customer synchronously instead of queuing work for later. **It still queues**: `SignupService` enqueues a `ProvisioningJob` onto an in-memory channel that `ProvisioningWorker` drains, and both files — which this paragraph says are gone — are live. The end-to-end behaviour is right, it is simply still asynchronous.

**What this did not change**: the branch-level model. `OrgId` was already globally unique, so nothing about Organization ↔ Organization isolation moved — `CustomerId` is an added layer under it, not a replacement for it. What moved is Customer ↔ Customer, from a physical wall to the same kind of filter-and-policy boundary the branch level already trusted.

**Left over from the previous model, worth knowing about rather than reinventing**: nothing currently grants `platform.*` to any account — it is seeded into the permission catalogue and `apps/admin` checks for it, but no role's seed includes it, deliberately, because `Role` rows are shared system rows rather than per-customer copies, and granting `platform.*` to a tenant role (Owner, say) would grant platform access to that role's holders across every customer, not just one. **Decided 24 September 2026 (D-01): a flag on the user, never a role.** `mst.Users.IsPlatformOperator`, set only by bootstrap configuration (`Bootstrap:OperatorEmails`, grant-only, applied at every Master start) or by another operator (`PUT api/admin/platform-operators/{userId}`, `platform.edit`), puts `platform.*` in the token — built 24 September 2026 (TK-13). Any `platform.*` row on a role is ignored when the token is minted.

---

## Accounting — get this right or reports lie

### Chart of accounts is three tables
- `mst.AccountTypes` — 5 fixed rows (Asset, Liability, Equity, Income, Expense), each with `NormalBalance` and `ReportSection`. **Master database** — global reference data, not duplicated per customer
- `acc.Accounts` — the CoA, `OrgId`-scoped, seeded at org creation. `AccountTypeId` is an unenforced cross-database reference; `IsContra` marks accounts whose normal balance is opposite their type
- `acc.SubAccounts` — per-contact / per-item / per-tax detail under a parent control account

**There is no `AccountSubTypes` table** — removed by decision. `ParentAccountId` on `Accounts` supplies any display grouping it used to give, and `IsContra` moved onto `Accounts`.

`AccountTypeId` is denormalized onto `SubAccounts`. **Always derive it from the parent account on write, never accept it from a caller** — if the two disagree, reports contradict each other depending on which one they group by. On `Accounts` it is chosen directly, and becomes immutable once the account has been used.

### SubAccount rules
- Each Contact → **6** SubAccounts: the trade balance, a prepayment advance and an overpayment advance, beneath **each** of Accounts Receivable and Accounts Payable. `SubAccountPurpose` completes the key, the way `TaxComponent` does for CGST/SGST/IGST — without it all three under a parent collide. There are **no separate advance control accounts**: grouping is by the direction the balance runs, so every sub-account's type matches its parent. The consequence to carry: neither control total is a Schedule III line on its own, because advances must be reported apart from trade receivables and payables, and the purpose column is what splits them back out
- Each Item → 3 SubAccounts (Inventory, Cost of Goods Sold, Sales Revenue)
- `JournalDetail.SubAccountId` and `JournalLedger.SubAccountId` are nullable — bank and equity lines have no sub-dimension. Contact, item **and GST** legs do carry one (GST → a per-rate GST subaccount)

### Both Inventory and Cost of Goods Sold exist. Don't merge them.
Inventory (Asset) = stock still held. COGS (Expense) = cost of stock sold.
- Purchase: `Dr Inventory / Cr Accounts Payable`
- Sale: `Dr Accounts Receivable / Cr Sales Revenue` **and** `Dr COGS / Cr Inventory`

Gross profit exists only because Revenue (Income) and COGS (Expense) are separate types.

### Contra accounts
With subtypes gone, `IsContra` on `acc.Accounts` is what tells a report to subtract. Set it on accounts whose normal balance runs opposite their type — **Accumulated Depreciation** (Asset), **Sales Returns** and **Discount Given** (Income), **Purchase Returns** (Expense). Miss it and the report overstates silently.

Useful account names to seed or expect under each type (guidance, not a table):
- **Asset**: Cash, Bank, Accounts Receivable, Inventory, Prepaid Expense, Advance to Vendor, Fixed Asset, Accumulated Depreciation *(contra)*, Input GST
- **Liability**: Accounts Payable, Credit Card, Advance from Customer, Output GST, TDS Payable, Long-term Liability
- **Equity**: Capital, Drawings, Retained Earnings, Opening Balance Equity
- **Income**: Operating Revenue, Sales Returns *(contra)*, Discount Given *(contra)*, Other Income
- **Expense**: Cost of Goods Sold, Purchase Returns *(contra)*, Operating Expense, Payroll Expense, Rent, Depreciation

### Journal Entry is the only posting mechanism
Everything — invoices, bills, payments, depreciation, opening balances — produces a JE.

- Lines are debit **xor** credit. Never both. Never negative.
- Lifecycle: Draft → Posted → Reversed. **Never edit a posted entry.** Reverse it with an offsetting entry.
- Balance is checked three times: domain guard on Post, `SaveChangesInterceptor`, and a Postgres **deferred** constraint trigger (deferred so multi-line inserts don't trip on intermediate state; only enforced when Posted, so Drafts may be unbalanced). **As built the interceptor was never written; the domain guard and the triggers exist.** The triggers — two on the journal, because posting a draft changes the header and never touches the lines, plus the one on `acc.JournalLedger` and the allocation pairs on the money documents — were lost in the squash of `2c5ed6f` and restored by TK-07 (`RestoreLedgerTriggers`). The ledger trigger sums the branch once per transaction, not once per row, and checks deletes as well as inserts
- Sales and Purchase **publish events**. Accounting consumes them and writes the JE. Never let another service write GL rows. The money documents are Accounting's own now, so they post through `LedgerPostingService` directly — in the same transaction, which is what the merge was for.

### Fixed Assets
The **category** owns the GL mapping (Fixed Asset / Accumulated Depreciation / Depreciation Expense), not the individual asset. Per-asset mapping doesn't scale.

### Opening balance / migration screen
Highest-risk screen in the system:
- Accounting orchestrates; calls Inventory (opening qty + unit cost → seeds WAC) and Master for contacts (opening AR/AP **per contact**, never a lump sum — aging breaks otherwise)
- Opening Balance Equity must net to zero — that's the validation
- Block finalize until AR, AP, and Inventory subledgers tie to their control accounts
- Migrated fixed assets skip historical depreciation
- Read-only after go-live

---

## Inventory & costing

**Stock is one shared pool per branch — that is, per `OrgId`.** One quantity and one running cost per item within a branch, shared across every warehouse in it. `WarehouseId` is a location dimension only: never partition inventory, cost layers or WAC by it. Branches do not share stock, because a branch is a separate set of books; the query filter already sees to that, so no code needs to think about it.

**Point-of-sale stock decrement must be synchronous** — a concurrency-safe conditional update against Inventory. Not event-driven, or two tills oversell the last unit. Everything downstream (costing, accounting, notifications) is async.

**The costing method is per item**, chosen on the item master and frozen the moment stock first moves — earlier postings were made under it, so changing it later would restate history silently. WeightedAverage / Fifo / Lifo / Fefo / SpecificIdentification / None.

Weighted average, which is the default and the only one needing a formula:
- Receipt: `newWac = (oldQty × oldWac + recvQty × recvCost) / (oldQty + recvQty)`
- Sale: `COGS = qtySold × currentWac` (WAC unchanged; only quantity moves)

**That formula is only the request path's provisional figure, and the ledger never posts it.** The request path applies it in the order movements *arrive*, because a till needs an answer now — and arrival order is wrong the moment anything is backdated. So a weighted-average movement stays `Pending`, and `CostingEngine.Worker` recalculates the item over its whole history with `WeightedAverageCalculator` (pure) and `WeightedAverageRecosting` (the write-back), once per batch:
- **Order**: by movement date; on one date every stock-in before every stock-out; then entry order
- **Negative stock**: a stock-out that would take the quantity below zero has the next later stock-in moved ahead of it, so a sale keyed before its purchase is valued at the stock that covered it rather than at nothing
- **Averaging**: at each stock-in the average becomes running value ÷ running quantity. A stock-in keeps its own stored cost — a sales return included — and only stock-outs are revalued
- **Lock date**: the branch's strictest Accounting period lock (`GET internal/period-locks/branch`). A stock-out on or before it is never changed but still counts toward the running value. If Accounting cannot be reached, weighted-average items wait for the next tick rather than be recalculated with no lock
- **Rounding**: average and unit cost to 12 decimals, the posted line value to 2, with a cumulative cent correction so the item's total cost of sales is exact
- **Write-back**: only stock-outs whose line value changed go back in the ledger queue. `ItemStock`'s average is updated; its quantity on hand is **not** — that belongs to the guarded decrement, and a disagreement is logged instead

Everything else runs on **cost layers**. A receipt opens an `inv.CostLayer` at what it cost; an issue records `inv.CostLayerConsumption` rows naming which layers it drew from and how much from each, so the cost of a sale walks back to the purchases behind it. Layers are consumed by the same guarded conditional update stock uses, each capped by its own remaining quantity.

**Costing is asynchronous; quantity is not.** A movement's quantity is applied inside the request — see the POS rule above — and its cost is settled just after by `CostingEngine.Worker`. A movement carries a `CostingStatus`, and until it reaches `Costed` the screen says so rather than showing zero.

**There is no message broker.** `inv.StockMovements` *is* the queue: ordering comes from `ORDER BY ItemId, MovementDate, StockMovementId`, and exactly-once from a guarded `Pending → InProgress` status claim whose row count is the answer. If a broker is added later it should **wake** that loop, not replace it — the ordering guarantee is the point, and a broker does not give it. When one does arrive: **Service Bus is at-least-once, so every consumer needs idempotency** (dedup on event id) or costs double-count on redelivery.

**A backdated receipt restates the issues after it.** Under FIFO that stock should have gone out first, so the affected movements are requeued and recosted, and each restatement is written to `inv.RecostingAdjustments` with the before and after. Quantities never change; the old figures are kept rather than overwritten.

---

## Multi-currency

Every transaction row stores `CurrencyCode`, `ExchangeRate`, and a computed base-currency amount.

**`ExchangeRate` is a snapshot at transaction date. Never look it up live** — historical documents would silently reprice.

GL postings are always in base currency; original currency and rate stay on the source document. Realized FX gain/loss at settlement; unrealized from a period-end revaluation job.

---

## Indian GST

- Item master: HSN/SAC + rate slab. Contact master: GSTIN (first 2 digits = state code).
- **One shared tax-determination component** used by Sales and Purchase — `Shared.Kernel.Tax`: `GstCalculator` is pure and takes rates already resolved for the document's date, `PlaceOfSupply` decides intra against inter, `ITaxRateProvider` reads them from Accounting cached per branch **and date**. Same state → CGST+SGST. Different → IGST. Never duplicate this logic per service; the TypeScript copy in `line-math.ts` exists because the browser needs it and is held to the same answers by `shared-fixtures/tax-fixture.json`.
- Tax Master is **effective-dated** (rates get revised), with CGST/SGST/IGST split and the **3% gold/silver bullion rate** (outside the standard 0/5/12/18/28 slabs)
- Validate `StateCode` matches the GSTIN's first two digits, or CGST/SGST vs IGST goes silently wrong
- Tax Master is a **Settings screen** but the data is owned by the **Accounting service**

---

## Auth

Two-step login, because one account spans multiple organizations:
1. `POST /api/auth/login` — credentials → pre-auth token (5 min, no org context) + accessible orgs
2. `POST /api/auth/select-organization` — → access token (15 min) + refresh token (7 days)

JWT claims: `sub`, `customer_id`, `customer_code`, `org_id`, `display_name`, `license_status`, `license_expiry` (when set), `permission[]`. `customer_code` is for places a person reads — the first folder of every stored file — and is never an isolation key; `customer_id` is. The licence claims are what let a page and its API both refuse an expired customer without either asking Master per request.

- BCrypt work factor 12; all tokens stored **hashed**. **Refresh-token rotation is built** (4 September 2026): `POST /api/auth/refresh` hashes the presented token, finds it, revokes it and issues a new one in the same family; `POST /api/auth/logout` revokes the family. `RefreshToken` carries `FamilyId` and `OrgId` for exactly this — the family so a replayed token can end the whole chain rather than one link, the org so a refresh mints the same branch's access token. Presenting a spent token is read as theft and revokes every live token in its family, recorded in `mst.LoginHistory` without the token in it. Concurrency is a guarded `ExecuteUpdate` whose row count is the answer, so two simultaneous refreshes cannot both rotate. Every refusal is the same 401 with the same message
- Lockout: 5 failed attempts → 15 min
- **Forgot password always returns 200**, even for unknown emails — otherwise it leaks which addresses exist
- Password reset revokes all refresh tokens
- New users get an **invitation link**, never a temporary password
- 5 system roles (Owner, Administrator, Accountant, Sales, Viewer) + customer-defined (`Role.CustomerId` null = system)
- Permissions are `{module}.{action}`; `platform.*` is operator-only
- System roles read-only; roles in use can't be deleted
- Trial: 14 days, public self-service signup

---

## Other conventions

**Phone numbers**: local stored **without** prefix, foreign **with** leading `+`. The `+` is the discriminator — SMS/OTP prepends the org's `Country.PhoneCode` when absent. Landline needs an STD code (2+ digits) then number (3+ digits). Mobile has no regex (lengths vary too much by country).

**Multi-language** (Tamil, Chinese) works natively — Postgres `varchar` is UTF-8 and counts characters not bytes. Create databases with UTF8 encoding.

**Printing — two separate paths, don't conflate:**
- Standard documents → **PDFsharp** (6.1.1, MIT), server-side, PDF/A for archiving. Syncfusion was the earlier plan and is dropped (D-11, 24 September 2026)
- POS receipts → ESC/POS commands (not PDF), fixed-width text, only from `apps/desktop` (browsers can't reach USB/serial printers)
- Archive every generated document to blob storage, linked by `SourceType` + `SourceId`

**What a standard document looks like is a template, not code** (6 September 2026).
`prt.PrintTemplates` holds one designable layout per document type per branch — five bands in a
fixed order, merge fields resolved at print time — and Printing's `PrintRenderer` turns one plus
a document's data into paginated HTML. Settings › Print templates is the editor (TK-82).

**Printing is the eighth service, and it has carried traffic since 24 September 2026** (TK-80,
TK-81). A document's own service builds a `PrintPayload` from its own tables — keyed by
`PlaceholderCatalog`'s tags — and posts it to Printing's `api/print/render` **under the user's own
token**, so Printing resolves that branch's template through its own query filter and no internal
endpoint has to be told which branch it is serving. Sales never reads a template and Printing
never reads an invoice. Only the invoice prints this way so far (`GET api/sales/invoices/{id}/print`);
the other eleven printable types have no print route yet.

- **The contract stays in `Shared.Kernel.Printing`**: `PrintPayload`, `PrintFormatContext`,
  `DocumentTypeCatalog`, `PlaceholderCatalog` and `MergeTags`' text-level half — what both sides
  must agree on. Everything else — the stored shape, the renderer, the sanitiser, and the
  `AngleSharp`/`HtmlSanitizer` references — is Printing's own.
- **A draft prints stamped PROFORMA and a voided document VOID**, by the renderer, whatever the
  template holds — the caller passes `Watermark`.
- Printing seeds each branch through `internal/seed/organization` like every other service.
  `con.PrintTemplates` was dropped rather than copied (D-13: nothing deployed); an old
  `PrintTemplateId` that no longer resolves falls back to the branch default, then to the
  standard layout.
- **PDFsharp is the PDF library** (D-11, 24 September 2026); PDF/A output and the archive for every
  document are TK-22.

**SignalR needs Azure SignalR Service as a backplane** — with multiple replicas a message otherwise lands on the wrong pod.

**Rate sync**: RBI has **no official public API**, so exchange rates come from **manual entry plus a daily scrape** of RBI's reference-rate page (D-03). Metal rates come from **manual entry and IBJA's paid API** (D-14). Both land in `rat` (TK-24), with **dated history**, not just today's rate.

---

## Current state

**705 C# files across 36 projects**, counted on 4 September 2026 excluding `obj/`, `bin/` and generated migrations. Compiled, tested and migrated — see the caveats below. (The long-standing "~429 across 31" was stale by roughly two thirds; a count is cheap and worth redoing rather than carrying forward.)

### Built and wired end to end

Schema, API and page all exist for these. Pending work is queued in [`docs/TASKS.md`](./docs/TASKS.md), and every module's design is in [`docs/Modules.md`](./docs/Modules.md) — one file holding all of them, not one file per module. This is the shape of the thing, not the to-do list.

| Service | Schema | Tables | What works |
|---|---|---|---|
| **Master** | `mst` | AccountType, Country, State, Currency, HsnSacCode, LedgerType, LedgerSource, TransactionType | 37 Indian states with GST codes; HSN/SAC with a CBIC CSV importer |
| **Master** | `mst` | Customer, Organization, License, OrgCurrency, Configuration, SmtpSettings | Trial signup → seed → Active into the shared tenant database (still via a queue and a background worker, not in the request — see Tenancy); branch (organization) CRUD; per-org currencies, config and SMTP; platform admin (`apps/admin`) — customer list with status, admin-initiated creation, retry for one stuck mid-seed, read-only branch view per customer |
| **Master** | `mst` | User, Role, Permission, RolePermission, UserOrganizationRole, RefreshToken, PasswordResetToken, OtpVerification, LoginHistory | Two-step login, org switching, invitations, OTP password reset, permission matrix |
| **Master** | `con` | Contact, ContactAddress, ContactPerson, ContactPersonRole, ContactBankDetail, ContactLicence, ContactAttachment | One master with roles; GSTIN vs place-of-supply check; licence expiry report; file attachments |
| **Printing** | `prt` | PrintTemplate | The print template master — one designable layout per document type per branch, seeded at branch creation — with its editor, the renderer, and `api/print/render`; sales invoices print through it |
| **Inventory** | `inv` | UomType, UnitOfMeasure, ItemCategory, MetalPurity, Warehouse, Item, ItemBarcode, ItemPharmaDetails, ItemJewelleryDetails, ItemStock, StockMovement, CostLayer, CostLayerConsumption, ItemBatch, ItemSerial, RecostingAdjustment | Item master with pharma/jewellery profiles; guarded stock decrement; WAC + FIFO/LIFO/FEFO/specific layers; batches, serials, backdated recosting |
| **Accounting** | `acc` | Account, SubAccount, TaxMaster, PaymentTerm, JournalLedger, Journal, JournalDetail, PeriodLock, OpeningBalance, OpeningBalanceLine | Chart of accounts, sub-accounts, effective-dated GST rates, payment terms, numbering series screen; the general ledger with a deferred balance trigger, and the internal posting API every other service writes through; the manual journal (draft → post → line-paired reversal), the account ledger, the trial balance, period locks and the opening balance; document-to-document allocation — the **Settle documents** workspace plus an **Allocate** dialog on invoices, bills, credit notes and money-in/out advances |
| **Accounting** | `acc` | Bank, BankAccount, SpendMoney, SpendMoneyDetail, ReceiveMoney, ReceiveMoneyDetail, TransferMoney, BankStatement, BankStatementLine, StatementImportProfile | Each bank account provisions its own ledger account; spend, receive and transfer money with allocation, settlement and FX; CSV and XLSX statement import with matching |

The four **Master** rows are one service and one API host, but **two databases**: the first three are `mst` in the shared master database, the fourth is `con` in the one shared tenant database, alongside every other customer's own `con` rows.

`NumberingSeries` lives in `Shared.Kernel` and is mapped by three services — Accounting owns the migration; Master (on its contacts context), Inventory and Sales map the same shape with `ExcludeFromMigrations`. **A settled exception to the no-shared-tables rule, not a loose end.**

The reason is the allocation. `NumberGenerator` takes a number with a guarded `ExecuteUpdate` on `NextNumber`, and that statement joins the caller's transaction — so an item insert that fails gives its code back, and a document series stays gapless. Both properties need the table in the caller's `DbContext`. Ask another service for a number over HTTP and the transaction ends at the wire: the number is spent whether or not the insert succeeds.

A table per service would also break the screen. Settings › Numbering series is one list of every series, and splitting the table means three services to query and no single place to enforce one default per code.

If this is ever revisited, the thing to preserve is the transaction, not the table.

The Accounting/Banking merge is what that argument predicted: Banking mapped this table for exactly this reason, and merging the two made its mapping simply Accounting's own.

**Gateway**: YARP with request logging, purging and per-environment route config. **CostingEngine.Worker**: built — claims movements from `inv.StockMovements` with a guarded status update, costs them, then drains a second queue on the same table that posts them to the ledger.

**Frontend**: `apps/web` and `apps/docs` build. 28 pages across `master-ui` (contacts, contact person roles, HSN/SAC), **one lib per settings screen under `libs/settings/`** (`users`, `roles`, `organizations`, `organization-settings`, `currencies`, `configuration`, `smtp`, `api-clients`, `print-templates`, each imported as `@bill-book/settings-<screen>`; D-05, TK-28), `accounting-ui` (the ledger screens and the banking ones) and `inventory-ui`, plus shared auth. The libs mirror the services: `libs/{master, inventory, accounting, sales, purchase, customer, reporting}`. Of `libs/shared`, **auth**, **api-client**, **ui-components**, **currency-format** and **theming** all have real source — ui-components carries ~18 components including the hand-built report grid the whole Reports section runs on, currency-format a full lakh/crore amount-in-words, theming the `--color-*` / `--space-*` design tokens every page reads. `libs/app-shell` is real too and is the authenticated root of `apps/web`. Of the seven `-core` libs, **reporting-core** and **customer-core** have source; the other five are still empty scaffolds with a path alias each.

**Lint and tests**: ESLint across the workspace (`npm run lint`), Vitest for services, guards and interceptors (`npm run test`), `npm run check` for all four (lint, typecheck, 66 tests, both builds). Component tests need the Angular Vite plugin and are not set up. The backend has three test projects: `Shared.Kernel.Tests`, `Inventory.Api.Tests` and `Accounting.Api.Tests` — the last absorbed `Banking.Api.Tests` with the merge, so there is now one `ACCOUNTING_TEST_DB` rather than two environment variables.

`Accounting.Api.Tests` needs **a real PostgreSQL** and skips itself with a reason when no server answers. The ledger's guarantees are half in the database — deferred triggers, `ExecuteDelete`, the guarded numbering update, and the allocation triggers on the money documents — so an in-memory provider would prove nothing about them.

### Still not built

- ~~**Customer, Purchase, Reporting**~~ — **all three are built; this line was stale and is kept only so nobody trusts a copy of it.** Purchase has 12 tables, 6 controllers / 33 actions and 45 tests across PO → GRN → Bill → Debit Note. Reporting has **48 report sources wired end to end** — 41 of the 46 in `reports.json` plus 7 beyond it — an OpenXML Excel writer and a CSV writer. **Five of the 46 are not built**: four fixed-asset reports, which now have a register to read (see Fixed assets in the Roadmap notes) but no read model in `ReportingDbContext` and no source, and *Business Performance*, which `reports.json` defines with no columns at all and so is not a specification. The seven sales/purchase settlement reports this line once listed as unbuilt were built in September. See `docs/Modules.md` §8.2. Customer has Lead/Ticket/TicketMessage, 2 controllers / 12 actions and 5 UI components, wired into `apps/web`; its seed data (stage C4) is the per-branch `cus.SlaPolicies`, seeded through Customer's `internal/seed/organization` (TK-18)
- **Sales past the invoice.** The sixteen `sal` tables, the three document base classes and all five services and controllers are written. The **quote** (T2.1), the **sales order** (T2.2) and the **invoice** (T3.1) each have their list, their form, their conversion from the document upstream, their docs and their tests; the invoice posts the double entry and issues the stock, and `LedgerClient` now sends `DebitAmount`/`CreditAmount` as Accounting requires. **Delivery challan and credit note have a controller and a scaffold page but no verified path** — until 21 August no sales document could be saved at all (see below), so "written" has never been "works" for those two. **T2.1, T2.2/T2.4 and T3.1/T3.2 were closed on 22 August 2026 by the repository owner's decision**, with **partial fulfilment deferred to T3.6**: nothing advances `DeliveredQuantity` or `InvoicedQuantity` yet, so `FulfilmentStatus.PartlyDelivered` is unreachable and an order can be neither shipped nor billed in part. The three documents are complete as commitment and supply documents; the fulfilment half arrives with the challan. **The item and customer pickers are numeric id fields on every sales form**, awaiting the item lookup endpoint. **POS** is an `sal.Invoices` row and shares the invoice's posting; its till screen is Phase 3 and `apps/desktop` holds only a scaffold
- **Notification.Worker sends payment reminders and can deliver email; RateSync.Worker is empty.** `PaymentReminderWorker` walks the branches Master lists (`Shared.Kernel.Tenancy.ITenantEnumerator`, shared with the costing engine) once a day, each in its own scope with its tenant set. It reminds a posted invoice past a profile's trigger only when Accounting's `internal/ledger/settlements` says something is still owed, writes to the address from Master's `internal/contacts/emails`, and sends through TK-19's `EmailRequestHandler` under a message id fixed by branch, invoice, profile and day, so a rerun sends nothing (TK-20). RateSync.Worker is a `Program.cs` and nothing else. **Email has two paths (TK-19).** By default Master sends in process (`QueuedEmailSender` → `EmailDispatchWorker` → `SmtpEmailSender`). With `ServiceBus:Namespace` **and** `Notification:EmailWorker` set, Master publishes `EmailRequested` and Notification.Worker consumes it: it resolves the mailbox from Master's `internal/smtp/resolved` (the decrypted password crosses the internal network for that one send), sends through `Shared.Kernel.Email.SmtpMailer`, and claims each message id in `ntf.ProcessedMessages` so a redelivery sends nothing. The worker is deployed nowhere yet, so no environment has the flag on
- **`apps/portal`, `apps/desktop`** — both have real routed pages and **both build**: `nx run-many -t build` compiles all five apps. Portal has a dashboard and a statement list against real endpoints; desktop has the POS terminal sketch, which is real code but still a sketch (no cart, a hardcoded walk-in customer). The claim that `apps/desktop/project.json` declares `"targets": {}` and has "never compiled" is false — it has three real targets and the file was introduced that way. **`apps/admin` is no longer one of them** — it builds, lints and serves, with a customer list, an inline create form and a retry action, and a read-only branch view per customer. A platform operator signs in to it: `mst.Users.IsPlatformOperator`, granted by `Bootstrap:OperatorEmails` or by another operator, puts `platform.*` in the token (TK-13)
- ~~**Purchase's numbering series never reach a new branch.**~~ Fixed by TK-01 (24 September 2026): `TenantSeeder` fans out to Accounting, Inventory, Sales, Purchase, Reporting and Printing, and Master's startup bootstrap seeds Purchase's `POR`/`GRN`/`BIL`/`DBN` under its own check. A branch created before the fix gets them from the retry in `apps/admin`, which is idempotent

### Standing caveats

- **There is CI now, and there was none before 4 September 2026.** Everything this file claimed about itself was verified on whichever machine last ran it, which is exactly how a package pin that made the backend unbuildable went unnoticed and a suite that had never run once was described as passing. `.github/workflows/ci.yml` gates on the backend building with `-warnaserror`, the backend tests passing against a real PostgreSQL 16 service container **with zero skips** (a skipped database-backed suite is a green gate that checked nothing), and the frontend's lint, typecheck, tests and all five app builds.
- **Row-level security is enabled, FORCEd and on one policy in every tenant schema (restored 23–24 September 2026, TK-71–TK-74, TK-02–TK-05; reviewed in TK-08).** Every table carrying `CustomerId` and `OrgId` in `acc`, `con`, `cus`, `inv`, `pur`, `sal`, `rpt` and `prt` has `{table}_tenant_isolation`, `FOR ALL`, with `"CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid` and no `WITH CHECK`, so `USING` doubles as the write check. `NULLIF` makes "no tenant" see nothing rather than throw on `''::uuid`. The exemptions are named, never implied: `rpt.ReportMasters` and `rpt.ReportColumns` (the imported `reports.json` specification, no tenant column) each schema's `__EFMigrationsHistory`, and `ntf.ProcessedMessages` (message ids the notification worker has sent, no tenant column, and the platform's own mail has no customer — TK-19). `acc`'s `EnableRowLevelSecurity` migration is the template; `con.ApiClients` carries a customer-level read beside it, because a key is matched before its branch is known.

  **How the tenant reaches the policy** is `RlsConnectionInterceptor`: every command is prefixed with `SET LOCAL app.current_customer_id = '…'; SET LOCAL app.current_org_id = '…';`, taken from `TenantContext` at the moment the command runs. Npgsql sends prefix and command as one batch, which Postgres runs as one transaction — the explicit one when there is one, an implicit one of its own when not — so the setting is transaction-local and gone at its end, on every path: GET reads, workers, seeders, the error log. It replaced a session-level `set_config(…, false)` on connection open (TK-08, owner's decision of 24 September). The branch as it stands *now* is what matters, and that is not a nicety: the reliability filter opens the request's transaction before an internal route has copied the branch out of its request, so a setting taken at connection open or at `BEGIN` held no branch, and **every internal write — ledger postings, stock issues, seeding — would have been refused the moment RLS bound.** Nothing saw it, for the reason below.

  **Every developer and CI connection is `postgres`, a superuser, and a superuser bypasses RLS even when it is FORCEd** — so no ordinary test sees a policy work or fail. Three kinds of test cover it: `tests/Shared/RlsAudit.cs`, linked into every suite, asks the catalog whether each table is enabled, FORCEd and policed; each suite's `*RowLevelSecurityTests` switches to a `NOSUPERUSER NOBYPASSRLS` role and asks what the policy lets through; `Accounting.Api.Tests.TenantInterceptorTests` does the same through the interceptor itself. **Deployments connect as the Azure Flexible Server admin login** (`deploy/azure/main.bicep`, `connectionBase`), which is neither a superuser nor `BYPASSRLS` but does own the tables — which is exactly why FORCE is what makes the policy bind. A dedicated login with only DML rights would be tighter still; that is the owner's call, raised in TK-08.

  **How this was lost, because it will be lost the same way again.** The 14 September squash regenerated every migration from the model, which knows nothing of hand-written SQL, so every RLS block and every trigger went with it — and it read as enforced for ten days because the developer databases predated the squash and still carried the old policies. Before that, each schema's test read only `pg_tables.rowsecurity`, which says RLS is switched on and not that a policy exists or that FORCE is set. **A green RLS suite means nothing until the database under it was built from zero and one policy has been dropped by hand to watch it go red.** Any future squash must carry the `migrationBuilder.Sql` blocks forward by hand; `grep -rl "ENABLE ROW LEVEL SECURITY" backend/Api` should name one migration per tenant schema before and after.

- **Public signup was broken on `main` until 4 September 2026, and is fixed — but not the way it first looked.** `Customer.DatabaseName` was `[Required]` over a NOT NULL column that `SignupService` never set, so every `POST /api/customers/signup` died on a not-null violation and **there was no way to create a customer through the product at all**.
  The obvious reading was that the column is vestigial — the last piece of the one-database-per-customer model this file records as reversed on 25 August — and dropping it is exactly what was tried. **That reading is wrong, and the mistake is worth keeping.** `TenantDatabaseResolver` reads `mst."Customers"."DatabaseName"` to choose the connection for every signed-in request, and it reads it in **raw SQL inside a string**: dropping the property compiles clean, migrates clean, and passes all 845 tests, because the only thing that touches it is text no compiler parses and no test exercises. The break would have been at run time, on the first request of every user.
  The column was never dead. **Nothing had ever assigned it**, which is a different thing: the sharded-tenancy work built the shard registry (`mst.TenantDatabases`, with a plan type and a capacity) and the resolver, and left out the step in between. `ITenantDatabaseAllocator` is that step — it picks the fullest shard that still has room and claims the capacity with a guarded `ExecuteUpdate` whose row count is the answer, so two signups cannot both take the last slot. With every shard full, signup answers 503 rather than inventing a database name no migration has run against.
  **The first test that ever signed a customer up found a second bug behind it.** `NextCustomerCodeAsync` took the greatest `CustomerCode` and called `long.Parse` on it, so a single non-numeric code would throw `FormatException` and make *every subsequent signup* fail, permanently, naming neither the row nor the reason. It now skips codes that are not numbers and orders on the parsed value rather than on text, where "9" sorts above "10". Both bugs were invisible for the same reason: `Master.Api.Tests.SignupTests` did not exist, and every other fixture builds its rows directly, filling in whatever the entity happens to require.
- **A committed password hash was seeded as an Owner on every startup, and is gone.** `DatabaseMigrationService` unconditionally created a customer named after the repository owner's company, a user with their personal email address, and a BCrypt hash written into the source file, assigned `RoleId = 1`. It ran on any deployment starting with an empty admin database, production included. What replaces it runs only while `mst.Users` is empty, takes the address from `Bootstrap:OwnerEmail` rather than from source, and creates the account **with no password** — the only way in is the ordinary reset flow, which proves control of the mailbox. Nothing is created when the setting is absent. It grants the tenant Owner role and deliberately not `platform.*`, which is still undecided (see Undecided).
- **Display formats come from the server now, and a screen must not invent them.** `GET /api/formats` (Master, `[Authorize]` only, like `MenuController` — see the guards note above for why no module permission fits) composes the branch's date pattern from `mst.Configuration`'s `format.date` with the symbol, position, decimals and grouping mask off the base `mst.Currency`. Only the date pattern was ever missing; everything else was already modelled, and **copying any of it into a new config key would give two places to change one answer**. `FormatSettingsService` in `libs/shared/currency-format` fetches it once under the shell and exposes `formatDate` / `formatMoney` / `formatNumber`; the grouping is read off the currency's mask, so Indian `##,##,##0.00` and Western `###,###,##0.00` need no code to tell them apart.
- **The ledger carries `DocumentNo` now, and three reports stopped inventing one.** `acc.JournalLedger.DocumentNo` is set by the poster at post time across the nine `sal`/`pur` posting sites, because Accounting can neither read `sal.Invoices` (rule 8) nor ask over HTTP without inverting the dependency — Sales and Purchase call Accounting, not the reverse. Money documents are the exception and are looked up directly in `GetOpenDocumentsAsync`: they take their number in the transaction *after* the one that posts them, and they are Accounting's own tables. The `$"{code}-{id}"` stand-in survives for rows written before the column, so `INV-42` in a UI means an old row, not a bug.
- **A shared component can quietly outlive a cross-cutting change, and only a screenshot will say so.** `bb-allocation-grid` predated the format work and went on drawing Western thousands and `MMM d, y` dates — so the allocation modal showed **three date formats and two money formats at once**, its own summary strip disagreeing with the grid's directly beneath it. Every test passed throughout: the formatting functions had their own suite, the modal had its own, and neither could see the other rendered on one screen. The grid now takes `formats` and a `showSummary` flag (defaulting to on, so other callers are untouched). **The general lesson is that "all tests green" says nothing about two correct components sitting badly together**, and this workspace's Vitest cannot compile a `templateUrl` component at all — so for anything visual, build it, serve `dist/apps/web/browser`, and drive it with Playwright against `/opt/pw-browsers/chromium-1194/chrome-linux/chrome` (the installed playwright expects a newer build than the image pins, so pass `executablePath`).
- **`bb-date-input` shows the browser's locale in its placeholder, not the branch's.** It is a native `<input type="date">`, whose placeholder and display come from the browser and cannot be overridden by any attribute — so a branch on `dd/MM/yyyy` still sees `mm/dd/yyyy` in the field. The value it stores is ISO, so nothing downstream is wrong. Fixing the display needs a custom date component, which is a bigger decision than it looks; it affects every date field in the product, not one screen.

- **T0.4 — the lifecycle — is written but not verified, and one of its five *Done when* clauses belongs to T3.1.** `Shared.Kernel.Documents.DocumentLifecycle` holds the whole table: what each status permits, with one set of refusal messages so every document answers alike. `PermissionAction` on a route **replaces** the action `RequireModulePermission` would derive from the HTTP method — `[PermissionAction("void")]` needs `{module}.void` and not `{module}.edit`, which is the separation `sales.void` was seeded for and never got. Three things settled: `ReadyToPost` is editable, a void always needs a reason, and a document row is never deleted.
- **T0.2 — tax determination — is written but not verified.** `Shared.Kernel.Tax` holds the pure calculator, the place-of-supply resolver and the cached rate client; Accounting serves `GET internal/tax/rates?on={date}`. The three sub-decisions are settled and written down in `Transactions_And_Specs.md`: inclusive **and** exclusive pricing per line, discount reduces the taxable value when the branch says so, tax rounds per component then sums. `shared-fixtures/tax-fixture.json` is read by both `Shared.Kernel.Tests` and `tax-fixture.spec.ts`, so a divergence between the C# and TypeScript implementations is a failing test rather than a wrong GST return — but neither suite has been run.
- **`SalesDbContext` shipped without `base.OnModelCreating`, and it is worth knowing why nobody noticed.** For the whole life of the `sal` schema, `TenantDbContext` never ran against it: **no OrgId query filter on any of the sixteen tables**, no OrgId index, and `Version` mapped to a real column instead of Postgres `xmin`, so there was no optimistic concurrency and a dead column on every table. RLS still refused every cross-branch read, so nothing leaked — but the filter is the first line of defence and it was absent everywhere, and `IgnoreQueryFilters` would have walked past the only guard left. `sal.SalesRegister` was worse: it had been left out of the RLS loop too, so it had **neither** guard, and it is the table GSTR-1 is filed from.
  It stayed invisible because **nothing queried the tables while the schema was being written** — a schema nobody queries is a schema nobody has checked, and "written" is not "verified" no matter how carefully it was written. Fixed, with `Sales.Api.Tests.SalesQueryFilterTests` asserting the filter, the `xmin` mapping and RLS coverage over the whole model rather than over one table a test happens to touch.
  **The lesson that generalises past this bug**: those tests were useless when first written, because `PostgresFixture` caught *every* exception and reported it as "no PostgreSQL", so removing the fix turned the run green with five skips. Every suite in `tests/` still does this. A model that disagrees with its migrations is a failure; only a socket that will not open is a skip — `Sales.Api.Tests` now distinguishes them and the others should be brought into line.

- **The same blind spot hid a worse bug, and it was found on 21 August by writing the first test that ever saved a sales document.** Every header-to-line relationship in `SalesDbContext` was configured `HasOne<Quote>().WithMany()` — a valid relationship on the real `QuoteId` column that has nothing to do with `Quote.Lines`. EF therefore mapped that collection a second time by convention and gave it a shadow key of its own: `QuoteId1`, `SalesOrderId1`, and eight more. Adding a line through the navigation — which is what every service does — filled the shadow column and left the real `NOT NULL` one at zero, so **`SaveChanges` failed with a foreign key violation on every create, in all five document types**. No sales document could be saved at all, for the whole life of the schema.
  Bound in `BindDocumentLineNavigations`, with `Sales.Api.Tests.SalesSchemaTests` asserting over the whole model that no foreign key carries a shadow property and that all ten collections name their column. The six conversion links beside them stay navigation-less on purpose.
  **What to take from it beyond the fix**: "the schema compiles, the snapshot matches, the migration applies" was true throughout and proved nothing, because the failure lives in the relationship EF *inferred* rather than in the one that was written. Only a round trip through the service finds that. Purchase's suite has written through the service from the start; `sal` had none of that until now.
  **The other schemas were checked and are clean** — `information_schema.columns WHERE column_name LIKE '%Id1'` over freshly migrated databases returns nothing for `pur` (12 tables — an earlier note here said 35, which was never right), `acc` (23), `inv` (41) or `sal` (39 after the fix). Worth re-running whenever a schema gains a header/line pair; it is a thirty-second answer.

- **Compiled, tested and migrated as of 12 August 2026.** `dotnet build` is clean with zero warnings under `TreatWarningsAsErrors`, every EF snapshot matches its model, and all **13** migrations apply to PostgreSQL 16 (counted 4 September; the "14" here was never recounted after the squash, and the test figure this line used to carry was four months out of date — the live numbers are in the bullet below, which is the one to trust). There are 13 rather than 33 because the three merged services squashed their chains to one migration each — the product has no released deployment to upgrade, so a clean history was worth more than a preserved one. If a session reports the SDK as unavailable: the egress policy denies `dot.net` and `builds.dotnet.microsoft.com`, but `apt-get update && apt-get install -y dotnet-sdk-10.0` works and is what the session-start hook now tries first.
- **Run `npm run check` in `frontend/` and `dotnet build && dotnet test` in `backend/` before claiming anything works.** As of 4 September 2026 the frontend chain is fully green — lint across 23 projects, typecheck with zero errors, **533 tests** (plus 3 skipped) and **all five app builds** under `strictTemplates`. The backend builds clean and runs **1,042 tests, none skipped** — **1,022 pass and 20 fail**, given a PostgreSQL and clean test databases (18 September 2026, with `Printing.Api.Tests` added). Four were `Reporting.Api.Tests.ReportLayerCertificationTests`, which counted report sources against an expected 41 that the last seven reports moved past. The expectation is 48 now, matching the 48 registered sources, so they are likely green; nobody has run them since (TK-09). **The other sixteen are the RLS assertions in all seven schemas**, and they are red because the policies are genuinely absent rather than because anything recent broke them — see the FORCE bullet above, which is where that is set out. **`ADMIN_TEST_DB` and `CONTACTS_TEST_DB` are both needed for Master** — the suite has two fixtures now, and a missing database is a wholesale skip rather than a failure. This figure has been 845 and then 969 within a week; count it rather than copy it. **The claim that both figures were re-run from dropped databases on 4 September cannot be right, and is the reason the RLS gap went unseen for a fortnight** — from dropped databases the RLS assertions fail, as they did on 18 September when that was actually done. Do it literally: `DROP DATABASE` each one before the run, and treat a figure quoted without that as measured against whatever was lying around. Each suite points at its own — `SALES_TEST_DB`, `PURCHASE_TEST_DB`, `ACCOUNTING_TEST_DB`, `INVENTORY_TEST_DB`, `BANKING_TEST_DB`, `REPORTING_TEST_DB` — and **a stale one is the usual cause of a suite skipping wholesale**, because the fixture migrates into it and a half-migrated database throws. Drop and recreate rather than reusing. In a fresh container there is no server running: `service postgresql start`, then `ALTER USER postgres PASSWORD '123'` to match the default connection strings.
- **`Directory.Packages.props` had `Npgsql` pinned to 9.0.2 while `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.0 requires ≥ 10.0.0**, so `dotnet restore` failed with NU1109 across every project referencing both — the backend did not build at all, and `Purchase.Api.Tests` had therefore never run once. The two versions must move together.
- **All three infrastructure interfaces have a real implementation now** (23 September 2026), each chosen by which setting is present, in one `Add…` registration apiece in `Shared.Kernel`. **`IEventPublisher`** → `ServiceBusEventPublisher` when `ServiceBus:Namespace` is set (managed identity, one topic per event type, `MessageId` for dedupe — consumers must still dedupe, delivery is at-least-once), `LoggingEventPublisher` otherwise, which logs and drops. **Nothing consumes an event yet**, so publishing is the half that exists. **`IFileStorage`** → `AzureBlobFileStorage` by `Storage:ConnectionString` (development, Azurite) or by `Storage:AccountUrl` and managed identity (deployment — the account refuses shared keys, and download links are signed with a user delegation key), `SftpFileStorage` by `Storage:Sftp:Host` (a self-hosted file server: one SSH connection per operation, exclusive create for create-only saves, optional host-key pin — plain FTP is deliberately not supported, it cannot refuse an overwrite atomically), `LocalDiskFileStorage` otherwise. **Every key is `{customerCode}/{orgId}/{app}/{module}/{area}/…`, composed only by `StorageKey`** (23 September 2026, owner's decision), which validates each segment and maps each `StorageApp`/`StorageModule` to a fixed folder name so a rename in code cannot move files. **Saves are create-only by default** — the refusal is the write's own condition (`If-None-Match: *`, `FileMode.CreateNew`), never a separate existence check — and throw `StorageKeyExistsException`; only the Sales invoice archive passes `FileWriteMode.Replace`, because it is written before its commit and a retry must not be blocked by its own leftover. Rows store the full key, so files written under the older `{orgId}/…` layout stay readable. Deployment is Azure: `deploy/azure` (Bicep) and `.github/workflows/deploy-azure.yml`. **It also runs on the owner's own PCs** (24 September 2026): `deploy/local` is Docker Compose over the same images plus a Cloudflare Tunnel for the public address, so no static IP is needed. Compose profiles split it one part per PC — `db`, `services`, `worker`, `gateway`, `web`, `tunnel` — with `DB_HOST`/`SERVICES_HOST`/`GATEWAY_HOST` naming the others (empty means this PC); each service listens on its own port (7501–7508) inside and outside the container so one URL works from either side, and `setup.ps1` writes one least-privilege `.env` per PC. Files can go to an SFTP server on a further PC. Its services run as environment **`SelfHosted`** — not Production, which refuses to start without Key Vault, and not Development, which returns raw database errors — so secrets come from `.env` through configuration. The web container forwards `/api` to the gateway on each app's own origin, so the shipped empty `config.js` is correct there and no CORS setting is involved. **Public signup answers 503 on every fresh install, Azure included**: `DatabaseMigrationService` registers `IN000001` as `Elite` with capacity 1 of 1, and signup allocates `Trial` then `Pro`, so there is no shard to put a trial in until H0.5. `deploy/local` gets its one business from `Bootstrap:*` instead (with `Bootstrap:OwnerPassword`, and a ten-year Elite licence) and seeds that branch with `seed/first-branch.sh`, which posts to each service's `internal/seed/organization` — migration runs before any service is up, so the bootstrap cannot seed its own branch. Two gateway gaps found by running it, both pre-existing and both on Azure too: **`gwy.RequestLogs` has no migration** (the 14 September squash deleted the gateway's and nothing replaced it, so request logging fails on every fresh database), and **the gateway has no route for `/api/formats`**, so every page falls back to default formats. A Google Cloud path was built and removed the same week, by the repository owner's decision to deploy where they have experience; do not reintroduce it without asking.
- **Master could not start on a fresh database until 23 September 2026, and it was not a deployment problem.** The `mst.Menus` / `mst.MenuPermissions` seed (`HasData`) was renumbered in `a3d066c` after the admin snapshot had been generated, so `MigrateAsync` threw `PendingModelChangesWarning` and every host refused to start. **Adding a migration did not fix it**: EF emitted 379 `UpdateData` calls applied row by row, and one collided on the unique `IX_MenuPermissions_MenuId_PermissionCode` midway. **Re-squashing the admin migration did** (TK-70): the chain is one migration again, `InitialAdminDbContextSchema`, with the same 26 tables and 38 indexes as before, and Master migrates, seeds and creates `IN000001` from empty databases. A developer database migrated under the old id holds stale menu rows and fails loudly on the new one — drop it. `Master.Api.Tests.MigrationModelTests` now asserts both Master contexts against their snapshots without opening a connection, so it cannot skip; `dotnet ef migrations has-pending-model-changes` is the same check by hand. Run either after touching any `HasData`. **`ISecretStore` is done too** (4 September 2026): `Shared.Kernel.Secrets` holds `KeyVaultSecretStore` and `ConfigurationSecretStore`, chosen the same way — Key Vault when `KeyVault:Uri` is set, configuration otherwise, and **a startup failure if the environment is Production and neither**. The Azure packages were pinned in `Directory.Packages.props` the whole time; only the implementation was missing. The five per-service copies of `ConfigurationSecretStore` and Master's in-memory dictionary — which accepted every write and lost it on restart, so it reported success and stored nothing — are gone.
- **Every endpoint is behind a credential and a permission** — true as of 4 September 2026, and it has now been false twice. On 2 September `ApiClientsController`, `CreditNotesController` and `PriceListsController` carried no permission attribute and `InternalApiKeysController` was anonymous with no shared-key guard; those were closed, and the claim was written down as settled. It was not. Two more turned up on 4 September in Reporting — `GstController` served GSTR-1/2/3B to any signed-in user with no module permission, and `InternalCreditCheckController` had neither guard, so Reporting's `FallbackPolicy` demanded a user token from Sales' `CreditCheckClient`, which sends only the internal key. **The lesson is about how the first fix was checked**: it was verified by reading the four controllers it changed, and a missing attribute is invisible in a file you are not reading. `Reporting.Api.Tests.EndpointGuardTests` now asserts over the whole assembly instead — every controller carries one of the three guards, and every module named is one the catalogue seeds. **All seven services now carry the test** (4 September 2026), over one shared `Shared.Kernel.Internal.EndpointGuardAudit` rather than seven copies of the logic. It asks four things of an assembly: which endpoints carry none of the four legitimate guards, which demanded modules and permissions the catalogue does not seed, and which routes take a `:guid` tenant id off the URL without comparing it to the token. **There are four guards, not three** — `[RequirePermission]` names one permission on one action, for routes whose authority is not their controller's module (`platform.view` on an operator screen, `settings.edit` on a per-customer mailbox), and the earlier count of three missed it. Exemptions are arguments somebody had to write and defend, not attributes nobody added: sign-in and signup run before a token exists; the menu and the branch's display formats belong to no module; the operator screens are cross-customer by definition. `Customer.Api.Tests.PermissionModuleTests` covers the module half for Customer, which is how Leads and Tickets came to be locked to every role including Owner.
- **There are three guards, not two, and the third was invisible until it was made an attribute.** Staff routes take `[RequireModulePermission]`, service routes take `[InternalOnly]`, and client-portal routes take `[RequirePortalAccess]` — a contact reading their own statement holds no staff permission and never should. That check lived inside `PortalStatementsController`'s action, which made it correct and undiscoverable: a controller guarded in its method body looks, to a reader of attributes or a test reflecting over them, exactly like one guarded nowhere. Declaring it is what let the assertion be made over a whole assembly rather than over a list with exceptions on it. Services default-deny; the exceptions are sign-in, signup and the country/state lists the signup form needs.

---

## Roadmap

**Phase 1** — Contacts, Inventory, Sales, Purchase, Accounting core (CoA, JE, Other Income/Expense, opening balances), Tax Master, COGS + weighted average costing, banking core, **CRM**, **Support helpdesk (SLA/ticketing/chat)**, multi-currency, RBAC, org settings, tenant provisioning
**Phase 2** — **Reports (Sales, Purchase, Accounting, Inventory, Support SLA, GSTR-1/3B)**, **Fixed assets (register, acquisition, depreciation, disposal)**, recurring invoices, payment reminders, retainer invoices, Client Portal, Paytm, bank feeds/reconciliation, multi-location price lists, API clients, **document print & PDF/A archive (T3.4)**, **report Excel/CSV export**, **POS ESC/POS receipt printing (T7.3)**
**Phase 3** — **POS (till API, screen)**, Project accounting, budgeting, workflow approvals, custom fields/reports, e-invoicing + e-way bill, compliance bundle
**Designed, no phase, nothing built — four apps, one customer** (owner's decisions, 23 September 2026). RetailErp, **HRMS**, **Payroll** and **School** are each sold on their own, and one customer may buy any or all of them: one set of branches and users across every app, **one licence per app**, per-app sign-in (an `app` claim, that app's licence claims and only that app's permissions), an app switcher, and a public signup with a 14-day trial per app. `App` is a flags enum: a role belongs to one app, a permission or menu to one or more, so shared screens keep one code each. **Master pages — users, roles, branches, organization settings, currencies, configuration, SMTP, API clients, numbering series, print templates — are shared by every app**: one page in a shared lib, mounted by each app, never copied; a new master page is shared by all apps unless a narrower set is written down with its reason. **Pages are validated by the shell** (`shellRoutes` in `libs/app-shell`): every page declares `data.access` — a permission, or signed-in only — and one that declares nothing is refused, with an audit that fails the build. That platform work is stage H0 and is built first. Then **HRMS** (`apps/hrms`) and **Payroll** (`apps/payroll`) over six services, one per schema: `hrm` `tla` `pay` `rec` `prf` `clm`. **Payroll is sellable without HRMS**: both share one employee master, and Payroll takes paid days from HRMS's attendance when HRMS is licensed and from its own monthly input when not. Then **School management & maintenance** (`apps/school`; `sis` `adm` `att` `fee` `fac` `wrk` `ppm` `amc`). Designs are the `One customer, many applications`, `HRMS & Payroll` and `School` sections at the end of [`docs/Modules.md`](./docs/Modules.md). The service count above stays **8** until one of them carries traffic.
  
*Reports moved Phase 1 → Phase 2 on 24 August 2026, by decision.*

*POS moved Phase 1 → Phase 3 on 15 August 2026, by decision. Its stage — T7 in [`docs/modules/Sales.md`](./docs/modules/Sales.md) — stays where it is, boxes kept rather than deleted.*

*It was the most expensive thing left in Phase 1 and the least shared: the till screen is the bulk of the stage, it is keyboard- and barcode-driven, it has to tolerate being offline, and it lives in `apps/desktop` — which is still a scaffold with no source. The receipt is **ESC/POS commands rather than PDF** and can only be printed from the desktop app, because a browser cannot reach a USB or serial printer, so none of the printing work already done applies to it.*

*What it does **not** block is the reason it can wait: **a POS sale is an `sal.Invoices` row** with `TransactionTypeCode = 'POS'`, and T7.1 reuses T3.1's posting rather than adding one. The tables, the numbering series and the GST determination are all in place already. Nothing else in the product is waiting on POS — the counter sale it replaces is an invoice raised directly, which is the common case in a shop anyway.*

*FIFO/FEFO/LIFO batch allocation was Phase 3 and landed early, with cost layers — it is built. Do not defer work that depends on it.*

*Fixed assets moved Phase 1 → Phase 2 on 4 August 2026, by decision, and **the register has since been built**. `acc.FixedAssetCategories`, `acc.FixedAssets`, `acc.DepreciationSchedules` (a `Books` and a `Tax` schedule per asset, `StraightLine` or `WrittenDownValue`) and `acc.AssetTransactions` exist; `FixedAssetsController` lists, registers, capitalises and disposes, and `DepreciationService` runs depreciation from the `Books` schedule. The two schema questions that once blocked it have answers in code: acquisition and disposal ride on existing transaction codes (`docs/Modules.md`, the transaction-type table), and both books and tax schedules are kept — confirmed by the owner on 24 September 2026 (D-08).*

*The postings are built (24 September 2026, TK-11 and TK-12), all through `FixedAssetService` and one system journal each (`JournalService.PostSystemAsync`, which may post to control accounts because the register is their subledger):
- **a posted bill's capital line** becomes a register row through Accounting's `internal/fixed-assets/capitalise-bill`, keyed on the bill line so a retry registers nothing twice, and the cost is reclassified from the shared Fixed Asset account to the category's (D-19);
- **an asset entered with no bill** is a migrated one and debits its category's account against Opening Balance Equity (D-19);
- **a disposal** writes back accumulated depreciation, removes cost, takes proceeds from a chosen bank account or from a posted sales invoice's pre-GST credits, and puts the difference to `Gain/Loss on Asset Disposal` (D-20).

Written-down-value depreciation charged nothing until TK-11. The two pages under `accounting-ui/src/lib/fixed-assets/` are still routed from nowhere. The old T10 stage file, `TRANSACTIONS-ACCOUNTING-BANKING.md`, no longer exists.*

*Document print & archive (T3.4) moved Phase 1 → Phase 2 on 24 August 2026, by decision. The print half already works — `/sales/invoices/{id}/print` renders a full tax-invoice layout, watermarks drafts and voided documents, and splits GST per component and per rate. **The archive half is no longer blocked on Syncfusion.** Posting an invoice renders a PDF with PDFsharp 6.1.1 (`Sales.Api/Services/Pdf/PdfSharpInvoiceRenderer.cs`, MIT, already pinned) and saves it to storage under `StorageKey.DocumentKey(scope, "invoices", "{id}.pdf")`. What stays undone: the file is **plain PDF, not PDF/A**; it has a fixed layout of its own rather than the print template; it covers invoices only; and no endpoint returns it. PDFsharp replaces Syncfusion for good (D-11, 24 September 2026), and the rest is TK-22 in [`docs/TASKS.md`](./docs/TASKS.md). See T3.4 in [`docs/Modules.md`](./docs/Modules.md).*

*Report Excel/CSV export moved Phase 1 → Phase 2 on 24 August 2026, by decision, as a roadmap label only — the Excel half is built and shipped — `ExcelReportWriter` lives in `Reporting.Api` today, proven by `ExcelReportWriterTests`. **CSV export is now built** (4 September 2026): `ExportFormat` carries `Csv`, `CsvReportWriter` produces RFC 4180 output as UTF-8 with a BOM — Excel on Windows reads a BOM-less file in the machine's ANSI code page and renders every Tamil or Chinese name as mojibake — and money goes out invariant with no grouping, so an Indian-format branch does not export `12,34,567.89` into a comma-separated file. Both writers take the same `ReportResultView` with paging off, so the two formats cannot disagree with each other or with the screen.*

*T7.3 — POS ESC/POS receipt printing — moved Phase 3 → Phase 2 on 24 August 2026, by decision. Phase 3's POS entry now names only the till API and screen (T7.1, T7.2); T7.3 is called out separately because the receipt printer is genuinely separable — it talks ESC/POS, not PDF, and only from `apps/desktop`, unreachable from a browser regardless of when it is built. The sketch that exists today (`pos-terminal.component.*`, `esc-pos.service.ts`) builds: `apps/desktop/project.json` has real `build`, `serve-web` and `serve` targets (an earlier note here said `"targets": {}`, which was never true). Building is not the same as working — the terminal has no cart and a hardcoded walk-in customer — and T7.1/T7.2 still have to exist first for there to be a sale to print a receipt for. See T7 in [`docs/Modules.md`](./docs/Modules.md).*

---

## Undecided — ask, don't assume

**Nothing is undecided as of 24 September 2026.** The owner answered every open question that day. The full record, with the card that carries each answer, is section 3 of [`docs/TASKS.md`](./docs/TASKS.md). In short:

- **Platform operators** get `platform.*` from `mst.Users.IsPlatformOperator`, never a role (D-01, TK-13)
- **`CREATEDB`**: infrastructure creates the databases in production; the app auto-creates them in Development only (D-02, TK-27)
- **Exchange rates**: manual entry plus a daily RBI scrape (D-03). **Metal rates**: manual entry plus IBJA's paid API (D-14)
- **Blank optional phones** are stored as NULL (D-04, TK-21)
- **Settings** splits into one lib per sub-screen (D-05, TK-28)
- **CRM campaigns and marketing automation** are in v1 (D-06; TK-38 designs them)
- **API clients** get per-action `{module}.{action}` permissions through their role (D-07, TK-29)
- **Fixed assets**: books **and** tax schedules (D-08); no new transaction codes (D-09); capitalising reclassifies to the category account (D-19); a disposal pays into a chosen bank account or is invoiced to the buyer (D-20)
- **A branch declares its trade**, and seeds and menus follow it (D-10, TK-30)
- **PDF library**: PDFsharp (D-11)
- **Pricing**: per user with a branch cap for RetailErp and School; per active employee for HRMS and Payroll (D-12)
- **Tenant setting**: transaction-local, as the Tenancy section says (TK-08)
- **Approvals**: one engine. Its state machine is in `Shared.Kernel.Approvals`, workflow configuration and chain resolution are in Master (tenant schema `apr`), and `Hrm` resolves only the employee approver kinds (D-26, TK-99, TK-49)

A new question goes into `docs/TASKS.md` section 3 as the next D-number, and is asked rather than assumed.
