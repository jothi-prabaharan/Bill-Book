# CLAUDE.md

Instructions for Claude working on **RetailErp** — a multi-tenant retail ERP and accounting SaaS for Indian SMBs. Zoho Books is the functional benchmark.

Read this before writing any code. The rules here are decisions already made — follow them rather than re-deciding.

---

## Hard rules

These are non-negotiable. Violating them means the code gets rejected.

1. **LINQ only. Never write raw SQL.** The only exceptions, because no LINQ equivalent exists: `CREATE DATABASE`, RLS policies, triggers, `set_config`. Everything else — every query, insert, update, delete — is LINQ.
2. **Entities are plain property bags.** No constructors. No methods. No validation logic. No computed properties. Just `public X Y { get; set; }` with Data Annotations.
3. **Every Data Annotation needs `ErrorMessage`.**
4. **PascalCase table and column names**, matching C# property names exactly. Postgres needs quoted identifiers for this — that's expected.
5. **PostgreSQL only.** Never add SQL Server compatibility, never avoid a Postgres feature for portability. RLS, `xmin`, and JSONB are all in use deliberately.
6. **All table entities inherit `Shared.Kernel.Entities.AuditableEntity`.** Never set audit fields manually — `AuditSaveChangesInterceptor` does it.
7. **Enums, not magic strings**, for any fixed set of values.
8. **Never cross a service boundary by referencing another service's `DbContext`.** Use its API or an event.
9. **Ask before expanding scope.** If a request is ambiguous, present a short plan and wait rather than building the larger interpretation.

---

## When asked to add a table

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

Three projects per service, no more:

```
{Module}.Entity/       TableEntities/ · Models/ · Enums/
{Module}.Repository/   DbContext, repositories, seed data
{Module}.Api/          controllers, services, DI
```

Dependency direction: `Api` → `Repository` → `Entity` → `Shared.Kernel`. Never backwards.

**Services** (11): Master, Platform, Identity, Contacts, Crm, Inventory, Sales, Purchase, Accounting, Banking, Support, Reporting
**Background workers** (3): Notification, CostingEngine, RateSync
**Gateway**: YARP

**Frontend** (Nx): `apps/{web, portal, admin, desktop}` · `libs/{module}-core` (view-models + models, no templates) + `libs/{module}-ui` (pages) · `libs/shared/{auth, api-client, ui-components, currency-format, theming}`

`-core` libs must stay Ionic-compatible: Signals and DI are fine, but no `window`/`document`, no Syncfusion, no Electron/Node APIs.

Every page must work at ~360px — grids become card lists, forms stack, modals become full-screen sheets.

---

## Tenancy — the thing most likely to be got wrong

**Customer** = the account/billing entity. Owns **one physical database**.
**Organization** = a set of books (own GSTIN, currency, branches). A Customer owns **many**, all sharing that Customer's database.

| Boundary | Enforced by |
|---|---|
| Customer ↔ Customer | Separate physical databases |
| Organization ↔ Organization | `OrgId` + EF Core query filter + Postgres RLS |

**`OrgId` is load-bearing for security.** A missing query filter leaks data between organizations. Never omit it on a per-customer table.

Per request: resolve `CustomerId` from JWT → pick the database via the `plt` tenant directory (cached) → set `app.current_org_id` transaction-locally via `set_config(..., true)`. **Never connection-level** — pooled connections are reused across requests and would leak org context.

### Schemas

Master database: `mst` (countries/states), `plt` (customers/orgs/tenant directory), `idn` (users/roles/tokens), `rat` (currency + metal rates)

Per-customer database: `con` `crm` `inv` `sal` `pur` `acc` `bnk` `sup` `rpt` `ntf`

### Provisioning
- **New Customer**: create row → generate `CustomerCode` → `CREATE DATABASE` → store connection in Key Vault → publish `CustomerProvisioned` → each service migrates its own schema → mark Active. Block login until ready.
- **New Organization** under an existing Customer: insert row, seed its Chart of Accounts + Tax Master. No new database.

### Cross-database FKs are impossible in Postgres
- `CreatedBy`/`ModifiedBy` (Users are in master) → plain `Guid`, no FK. Resolve names from Identity in C#, **batched** — watch for N+1 on list screens.
- Contacts referencing `mst` Countries/States → unenforced ids, validate in C#

---

## Accounting — get this right or reports lie

### Chart of accounts is four tables
- `AccountTypes` — 5 fixed rows (Asset, Liability, Equity, Income, Expense), each with `NormalBalance` and `ReportSection`
- `AccountSubTypes` — FK to type, `IsContra` flag
- `Accounts` — the CoA, `OrgId`-scoped, seeded at org creation
- `SubAccounts` — per-contact / per-item detail under a parent control account

`AccountTypeId` is denormalized onto `Accounts` and `SubAccounts`. **Always derive it from the subtype or parent on write. Never accept it from a caller** — if the two columns disagree, reports contradict each other depending on which one they group by.

### SubAccount rules
- Each Contact → 2 SubAccounts (Accounts Receivable, Accounts Payable)
- Each Item → 3 SubAccounts (Inventory, Cost of Goods Sold, Sales Revenue)
- `JournalEntryLine.SubAccountId` is nullable — bank, GST, and equity lines have no contact or item dimension

### Both Inventory and Cost of Goods Sold exist. Don't merge them.
Inventory (Asset) = stock still held. COGS (Expense) = cost of stock sold.
- Purchase: `Dr Inventory / Cr Accounts Payable`
- Sale: `Dr Accounts Receivable / Cr Sales Revenue` **and** `Dr COGS / Cr Inventory`

Gross profit exists only because Revenue (Income) and COGS (Expense) are separate types.

### Subtypes
- **Asset**: Cash, Bank, Accounts Receivable, Inventory, Prepaid Expense, Advance to Vendor, Other Current Asset, Fixed Asset, Accumulated Depreciation *(contra)*, Input GST
- **Liability**: Accounts Payable, Credit Card, Advance from Customer, Output GST, TDS Payable, Other Current Liability, Long-term Liability
- **Equity**: Capital, Drawings, Retained Earnings, Opening Balance Equity
- **Income**: Operating Revenue, Sales Returns *(contra)*, Discount Given *(contra)*, Other Income
- **Expense**: Cost of Goods Sold, Purchase Returns *(contra)*, Operating Expense, Payroll Expense, Rent, Depreciation, Other Expense

### Journal Entry is the only posting mechanism
Everything — invoices, bills, payments, depreciation, opening balances — produces a JE.

- Lines are debit **xor** credit. Never both. Never negative.
- Lifecycle: Draft → Posted → Reversed. **Never edit a posted entry.** Reverse it with an offsetting entry.
- Balance is checked three times: domain guard on Post, `SaveChangesInterceptor`, and a Postgres **deferred** constraint trigger (deferred so multi-line inserts don't trip on intermediate state; only enforced when Posted, so Drafts may be unbalanced)
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

**Stock is one shared pool across all branches.** `BranchId` is a reporting dimension only — never partition inventory or WAC by it.

**Point-of-sale stock decrement must be synchronous** — a concurrency-safe conditional update against Inventory. Not event-driven, or two branches oversell the last unit. Everything downstream (costing, accounting, notifications) is async.

Weighted average cost per SKU, company-wide:
- Receipt: `newWac = (oldQty × oldWac + recvQty × recvCost) / (oldQty + recvQty)`
- Sale: `COGS = qtySold × currentWac` (WAC unchanged; only quantity moves)

**Service Bus is at-least-once. Every consumer needs idempotency** (dedup on event id) or costs double-count on redelivery.

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

JWT claims: `sub`, `customer_id`, `org_id`, `display_name`, `permission[]`.

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

**Implemented**: Master (countries + 37 Indian states with GST codes), Platform (customers, orgs, tenant directory, trial signup + DB provisioning), Identity (users, roles, permissions, JWT auth, password reset). ~45 C# files, 10 projects.

**Never compiled** — was authored without a .NET SDK available. Expect fixes on first `dotnet build`, most likely EF Core 10 package versions and namespace collisions (`Identity` and `Platform` are close to framework namespaces).

### Blocking gaps
- **`AuthController.ResolveCustomerIdAsync` returns null** — needs a Platform call. **Login cannot complete until this is implemented.**
- `ISecretStore` (Key Vault), `IEventPublisher` (Service Bus), `IEmailSender` (Notification worker) — interfaces only. DI startup fails without implementations.
- Login doesn't check whether the customer's database finished provisioning
- `CustomerCode` generation is read-max-then-increment — needs retry-on-conflict under concurrent signups

### Not yet built
Contacts, Crm, Inventory, Sales, Purchase, Accounting, Banking, Support, Reporting services. All frontend. Gateway. All three background workers.

---

## Roadmap

**Phase 1** — Contacts, Inventory, Sales, Purchase, Accounting core (CoA, JE, Fixed Assets, Other Income/Expense, opening balances), Tax Master, COGS + weighted average costing, Banking core, **CRM**, **Support helpdesk (SLA/ticketing/chat)**, **Reports (Sales, Purchase, Accounting, Inventory, Support SLA, GSTR-1/3B)**, multi-currency, RBAC, org settings, Platform provisioning
**Phase 2** — Recurring invoices, payment reminders, retainer invoices, Client Portal, Paytm, bank feeds/reconciliation, multi-location price lists, API clients
**Phase 3** — Project accounting, budgeting, workflow approvals, custom fields/reports, e-invoicing + e-way bill, FIFO/FEFO/LIFO batch allocation, compliance bundle

---

## Undecided — ask, don't assume

- Who holds `CREATEDB` in production; sync vs async provisioning UX
- RBI rate ingestion: scrape / paid wrapper / manual
- Trial expiry behaviour: read-only or blocked
- Empty-string vs null normalization for optional phone fields
- Whether `settings` splits into per-sub-screen libs
- CRM: campaign/marketing automation in v1?
- API client scope granularity: per-module or per-action
- Fixed assets: straight-line only, or both books and tax depreciation?
- Costing: blended WAC confirmed, or batch-level for expiry-sensitive stock?
