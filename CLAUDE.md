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
8. **Never cross a service boundary by referencing another service's `DbContext`.** Use its API or an event.
9. **Ask before expanding scope.** If a request is ambiguous, present a short plan and wait rather than building the larger interpretation.
10. **Ship documentation with the feature, in the same commit.** A user-visible change updates its page under `frontend/apps/docs/content/`, its status in `docs.manifest.ts`, and adds a bullet under **Unreleased** in `release-notes.md`. Not a sweep before release — by then the detail is gone and someone is reverse-engineering a month of git log.

---

## Git — how work reaches main

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.**

One branch is named for the session's work; every commit goes there, and it is merged into `main` when the work is done. A branch invented mid-task splits the work across two places and leaves whichever one nobody merges behind — which is how a change that was written and reviewed is missing from the product.

The same applies to a follow-up: reuse the designated branch rather than opening a second one beside it.

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

Column-level schemas and page specs live in [`SPEC.md`](./SPEC.md) — check there before designing anything new. **Sales and purchase documents are the exception**: `sal.*` lives in [`SALES.md`](./SALES.md) and `pur.*` in [`PURCHASE.md`](./PURCHASE.md), each with its columns, decisions and tasks in one place.

Produce, in this order:
1. Entity class in `{Module}.Entity/TableEntities/{Name}.cs`
2. Enums (if any) in `{Module}.Entity/Enums/`
3. `DbSet` + Fluent config in `{Module}.Repository/{Module}DbContext.cs`
4. Seed data if it's reference data

Do **not** write CREATE TABLE SQL. This is EF Core code-first — migrations generate the schema.

Every per-customer table needs `OrgId` plus a global query filter. Master-database tables (`mst`, `plt`) do not.

---

## When asked to add an endpoint

1. Request/response models in `{Module}.Entity/Models/` (Data Annotations with error messages)
2. Controller action in `{Module}.Api/Controllers/`
3. Validate the caller's `OrgId` matches the target resource's — always
4. Return `Forbid()` on cross-org access, not `NotFound()`

---

## Project layout

Two top-level halves. Inside `backend/`, four groups — `Api/`, `shared/`, `worker/`, `Gateway/`.

```
backend/
├── Bill-Book.sln
├── Api/
│   └── {Module}/                one folder per service (×12)
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

**Services** (12): Master, Platform, Identity, Contacts, Crm, Inventory, Sales, Purchase, Accounting, Banking, Support, Reporting
**Background workers** (3): Notification, CostingEngine, RateSync
**Gateway**: YARP

**Frontend** (Nx): `apps/{web, portal, admin, desktop, docs}` · `libs/{module}/{module}-core` (view-models + models, no templates) + `libs/{module}/{module}-ui` (pages) · `libs/shared/{auth, api-client, ui-components, currency-format, theming}`

`-core` libs must stay Ionic-compatible: Signals and DI are fine, but no `window`/`document`, no Syncfusion, no Electron/Node APIs.

Every page must work at ~360px — grids become card lists, forms stack, modals become full-screen sheets.

---

## Tenancy — the thing most likely to be got wrong

**Two levels, not three.**

**Customer** = the **head office**. The account, the billing relationship, the licence. Owns **one physical database**.
**Organization** = a **branch**. One place the business trades from, and one complete set of books — own code, GSTIN, address, currency. A Customer owns **many**, all sharing that Customer's database.

| Boundary | Enforced by |
|---|---|
| Customer ↔ Customer (head office ↔ head office) | Separate physical databases |
| Organization ↔ Organization (branch ↔ branch) | `OrgId` + EF Core query filter + Postgres RLS |

**There is no separate Branches table, and no `BranchId` column anywhere.** `OrgId` *is* the branch. A second column naming a branch on the same row says the same thing twice, and only `OrgId` is ever enforced — a `plt.Branches` table existed briefly and was removed for exactly that reason. If you find yourself wanting `BranchId`, you want `OrgId`.

**A branch is a hard data boundary, not a reporting tag.** Each branch has its own items, contacts, stock, chart of accounts and numbering series. Nothing crosses between them. Consolidated reporting across branches is a **read across organizations**, done deliberately and above the query filter — never by relaxing it.

**`OrgId` is load-bearing for security.** A missing query filter leaks data between branches. Never omit it on a per-customer table.

Per request: resolve `CustomerId` from JWT → pick the database via the `plt` tenant directory (cached) → set `app.current_org_id` transaction-locally via `set_config(..., true)`. **Never connection-level** — pooled connections are reused across requests and would leak org context.

### Schemas

Master database: `mst` (countries/states), `plt` (customers/orgs/tenant directory), `idn` (users/roles/tokens), `rat` (currency + metal rates)

Per-customer database: `con` `crm` `inv` `sal` `pur` `acc` `bnk` `sup` `rpt` `ntf`

### Provisioning
- **New Customer**: create row → generate `CustomerCode` → `CREATE DATABASE` → store connection in Key Vault → publish `CustomerProvisioned` → each service migrates its own schema → mark Active. Block login until ready.
- **New Organization (a new branch)** under an existing Customer: insert row with its own `OrgCode`, seed its full master data — Chart of Accounts, Tax Master, numbering series, units, payment terms. No new database. A branch starts empty and is seeded exactly like the first one, because it is a complete set of books.

### Cross-database FKs are impossible in Postgres
- `CreatedBy`/`ModifiedBy` (Users are in master) → plain nullable `Guid`, no FK. Resolve names from Identity in C#, **batched** — watch for N+1 on list screens.
- Contacts referencing `mst` Countries/States → unenforced ids, validate in C#

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
- Balance is checked three times: domain guard on Post, `SaveChangesInterceptor`, and a Postgres **deferred** constraint trigger (deferred so multi-line inserts don't trip on intermediate state; only enforced when Posted, so Drafts may be unbalanced). **As built the interceptor is the missing one** — the other two are in place for both `acc.Journals` and `acc.JournalLedger`, and there are in fact two triggers on the journal, because posting a draft changes the header and never touches the lines
- Sales/Purchase/Banking **publish events**. Accounting consumes them and writes the JE. Never let another service write GL rows.

### Fixed Assets
The **category** owns the GL mapping (Fixed Asset / Accumulated Depreciation / Depreciation Expense), not the individual asset. Per-asset mapping doesn't scale.

### Opening balance / migration screen
Highest-risk screen in the system:
- Accounting orchestrates; calls Inventory (opening qty + unit cost → seeds WAC) and Contacts (opening AR/AP **per contact**, never a lump sum — aging breaks otherwise)
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
- **One shared tax-determination component** used by Sales and Purchase. Same state → CGST+SGST. Different → IGST. Never duplicate this logic per service.
- Tax Master is **effective-dated** (rates get revised), with CGST/SGST/IGST split and the **3% gold/silver bullion rate** (outside the standard 0/5/12/18/28 slabs)
- Validate `StateCode` matches the GSTIN's first two digits, or CGST/SGST vs IGST goes silently wrong
- Tax Master is a **Settings screen** but the data is owned by the **Accounting service**

---

## Auth

Two-step login, because one account spans multiple organizations:
1. `POST /api/auth/login` — credentials → pre-auth token (5 min, no org context) + accessible orgs
2. `POST /api/auth/select-organization` — → access token (15 min) + refresh token (7 days)

JWT claims: `sub`, `customer_id`, `org_id`, `display_name`, `license_status`, `license_expiry` (when set), `permission[]`. The licence claims are what let a page and its API both refuse an expired customer without either asking Platform per request.

- BCrypt work factor 12; refresh tokens **rotate** on use; all tokens stored **hashed**
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
- Standard documents → Syncfusion .NET PDF library, server-side, PDF/A for archiving
- POS receipts → ESC/POS commands (not PDF), fixed-width text, only from `apps/desktop` (browsers can't reach USB/serial printers)
- Archive every generated document to blob storage, linked by `SourceType` + `SourceId`

**SignalR needs Azure SignalR Service as a backplane** — with multiple replicas a message otherwise lands on the wrong pod.

**Rate sync**: RBI has **no official public API** (scrape, paid wrapper, or manual entry). IBJA has a paid API for metals. Store **dated history**, not just today's rate.

---

## Current state

~381 C# files across 44 projects. Compiled, tested and migrated — see the caveats below.

### Built and wired end to end

Schema, API and page all exist for these. Task tracking lives in [`master.md`](./master.md); this is the shape of the thing, not the to-do list.

| Service | Tables | What works |
|---|---|---|
| **Master** | AccountType, Country, State, Currency, HsnSacCode, LedgerType, LedgerSource, TransactionType | 37 Indian states with GST codes; HSN/SAC with a CBIC CSV importer |
| **Platform** | Customer, Organization, CustomerDatabase, License, OrgCurrency, Configuration, SmtpSettings | Trial signup → `CREATE DATABASE` → seed → Active; branch (organization) CRUD; per-org currencies, config and SMTP |
| **Identity** | User, Role, Permission, RolePermission, UserOrganizationRole, RefreshToken, PasswordResetToken, OtpVerification, LoginHistory | Two-step login, org switching, invitations, OTP password reset, permission matrix |
| **Contacts** | Contact, ContactAddress, ContactPerson, ContactPersonRole, ContactBankDetail, ContactLicence, ContactAttachment | One master with roles; GSTIN vs place-of-supply check; licence expiry report; file attachments |
| **Inventory** | UomType, UnitOfMeasure, ItemCategory, MetalPurity, Warehouse, Item, ItemBarcode, ItemPharmaDetails, ItemJewelleryDetails, ItemStock, StockMovement, CostLayer, CostLayerConsumption, ItemBatch, ItemSerial, RecostingAdjustment | Item master with pharma/jewellery profiles; guarded stock decrement; WAC + FIFO/LIFO/FEFO/specific layers; batches, serials, backdated recosting |
| **Accounting** | Account, SubAccount, TaxMaster, PaymentTerm, JournalLedger, Journal, JournalDetail | Chart of accounts, sub-accounts, effective-dated GST rates, payment terms, numbering series screen; the general ledger with a deferred balance trigger, and the internal posting API every other service writes through; the manual journal (draft → post → line-paired reversal), the account ledger and the trial balance |
| **Banking** | Bank, BankAccount, MoneyTransaction, MoneyTransactionDetail | Each bank account provisions its own ledger account; the money document's schema for spend, receive and transfer — **schema only, no API or screen yet** |

`NumberingSeries` lives in `Shared.Kernel` and is mapped by four services — Accounting owns the migration, Contacts, Inventory and Banking map the same shape with `ExcludeFromMigrations`. **A settled exception to the no-shared-tables rule, not a loose end.**

The reason is the allocation. `NumberGenerator` takes a number with a guarded `ExecuteUpdate` on `NextNumber`, and that statement joins the caller's transaction — so an item insert that fails gives its code back, and a document series stays gapless. Both properties need the table in the caller's `DbContext`. Ask another service for a number over HTTP and the transaction ends at the wire: the number is spent whether or not the insert succeeds.

A table per service would also break the screen. Settings › Numbering series is one list of every series, and splitting the table means four services to query and no single place to enforce one default per code.

If this is ever revisited, the thing to preserve is the transaction, not the table.

**Gateway**: YARP with request logging, purging and per-environment route config. **CostingEngine.Worker**: built — claims movements from `inv.StockMovements` with a guarded status update, costs them, then drains a second queue on the same table that posts them to the ledger.

**Frontend**: `apps/web` and `apps/docs` build. 28 pages across accounting, banking, contacts, identity, inventory, platform and shared auth. Of `libs/shared`, only **auth** and **api-client** have any source — `ui-components`, `currency-format` and `theming` are empty scaffolds, as are all twelve `-core` libs, though `tsconfig.base.json` maps a path alias for every one of them.

**Lint and tests**: ESLint across the workspace (`npm run lint`), Vitest for services, guards and interceptors (`npm run test`), `npm run check` for all three. Component tests need the Angular Vite plugin and are not set up. The backend has four test projects: `Shared.Kernel.Tests`, `Inventory.Api.Tests`, `Accounting.Api.Tests` and `Banking.Api.Tests`. The last of these needs **a real PostgreSQL** — the ledger's guarantees are half in the database (deferred triggers, `ExecuteDelete`, the guarded numbering update), so an in-memory provider would prove nothing about them. The last two need a real PostgreSQL and skip themselves with a reason when no server answers; point `ACCOUNTING_TEST_DB` and `BANKING_TEST_DB` at one to run them.

### Still not built

- **Crm, Sales, Purchase, Support, Reporting** — project folders and `.csproj` exist; no entities, no controllers, no pages
- **Notification.Worker and RateSync.Worker** — `.csproj` and an empty `Consumers/` folder, nothing else. Email currently sends from Platform (`SmtpEmailSender` + an in-process `EmailQueue`), not from a worker
- **`apps/portal`, `apps/admin`, `apps/desktop`** — scaffolded, zero source files
- **Document numbering series beyond `JRN`, `SPM`, `RCM` and `TRM`.** Accounting and Banking seed their own; Sales and Purchase seed theirs when those services land

### Standing caveats

- **Compiled, tested and migrated as of 3 August 2026.** `dotnet build` is clean with zero warnings under `TreatWarningsAsErrors`, `dotnet test` passes 110, every EF snapshot matches its model, and all 33 migrations apply to PostgreSQL 16. If a session reports the SDK as unavailable: the egress policy denies `dot.net` and `builds.dotnet.microsoft.com`, but `apt-get update && apt-get install -y dotnet-sdk-10.0` works and is what the session-start hook now tries first.
- **Run `npm run check` in `frontend/` and `dotnet build && dotnet test` in `backend/` before claiming anything works.** Both are green today; the frontend chain is lint, typecheck, 41 tests and both builds.
- **Two infrastructure interfaces still have development stand-ins only.** `ISecretStore` → `InMemorySecretStore` / `ConfigurationSecretStore`, and `IEventPublisher` → `LoggingEventPublisher`, which logs and delivers nothing — so nothing that reads an event works yet, because nothing publishes one anywhere it can be read. Key Vault and Service Bus still to write. `IFileStorage` is done: `AzureBlobFileStorage` when `Storage:ConnectionString` is set, `LocalDiskFileStorage` otherwise.
- **Every endpoint is behind a credential and a permission** (master.md 5.10, 5.17). Services default-deny; the exceptions are sign-in, signup and the country/state lists the signup form needs. `internal/` routes take a shared key instead of a token.

---

## Roadmap

**Phase 1** — Contacts, Inventory, Sales, Purchase, Accounting core (CoA, JE, Other Income/Expense, opening balances), Tax Master, COGS + weighted average costing, Banking core, **CRM**, **Support helpdesk (SLA/ticketing/chat)**, **Reports (Sales, Purchase, Accounting, Inventory, Support SLA, GSTR-1/3B)**, multi-currency, RBAC, org settings, Platform provisioning
**Phase 2** — **Fixed assets (register, acquisition, depreciation, disposal)**, recurring invoices, payment reminders, retainer invoices, Client Portal, Paytm, bank feeds/reconciliation, multi-location price lists, API clients
**Phase 3** — Project accounting, budgeting, workflow approvals, custom fields/reports, e-invoicing + e-way bill, compliance bundle

*FIFO/FEFO/LIFO batch allocation was Phase 3 and landed early, with cost layers — it is built. Do not defer work that depends on it.*

*Fixed assets moved Phase 1 → Phase 2 on 4 August 2026, by decision. It was blocked twice over anyway: an asset is capitalised from the bill that bought it, and the bill does not exist yet; and both of its schema-shaping decisions are still open — whether acquisition and disposal get transaction codes of their own, and straight-line only versus books **and** tax depreciation. See Stage T10 in `TRANSACTIONS-ACCOUNTING-BANKING.md`, whose boxes are kept rather than deleted.*

*The consequence to carry: **the opening balance cannot migrate a fixed asset** until the register exists. One comes across as a plain account balance, with no cost, life or schedule of its own, and "migrated assets skip historical depreciation" defers with the register.*

---

## Undecided — ask, don't assume

- Who holds `CREATEDB` in production *(the UX half is settled: provisioning is async, behind a progress screen that waits)*
- RBI rate ingestion: scrape / paid wrapper / manual
- Empty-string vs null normalization for optional phone fields
- Whether `settings` splits into per-sub-screen libs
- CRM: campaign/marketing automation in v1?
- API client scope granularity: per-module or per-action
- Fixed assets: straight-line only, or both books and tax depreciation?
- Whether a branch should declare its trade (Pharma / Jewellery / General), so seeding and the settings menu can narrow themselves — today every branch gets everything (master.md 5.14)
