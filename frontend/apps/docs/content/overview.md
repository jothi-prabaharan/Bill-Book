# Bill-Book

A multi-tenant retail ERP and accounting product for Indian SMBs. Bill Books is the functional benchmark.

This is the **help and reference site**. It is a static app — markdown files, no API and no database — so it can be hosted anywhere and read offline.

## What this documents

Pages describe what is **actually built**. A page tagged `partial` or `planned` in the sidebar means the feature is not finished; the page says what exists and what does not, rather than describing an aspiration as if it worked.

## Where to start

- **[Architecture](#/architecture)** — the twelve services, schemas and how they talk
- **[Tenancy model](#/tenancy)** — customers, organizations and the database-per-customer rule
- **[Build status](#/status)** — an honest list of what is done
- **[Running locally](#/running-locally)** — get it up on your machine

## The two rules that shape everything

1. **A Customer owns one physical database; an Organization is a set of books inside it.** Every per-customer table carries `OrgId`, and a missing query filter leaks data between organizations.
2. **Everything posts through a journal.** Invoices, bills, payments, depreciation and opening balances all become ledger rows — nothing writes to the general ledger directly.



# Architecture

Two halves: a .NET backend and an Nx/Angular frontend.

## Backend

Seven services, plus three workers and a YARP gateway.

| Service | Schema | Owns |
|---|---|---|
| Master | `mst` | Countries, states, currencies, transaction/ledger/account-type masters; customers, organizations, licences, the tenant directory, SMTP and config; users, roles, permissions, tokens, OTP |
| Master | `con` | Contacts — customers and vendors |
| Inventory | `inv` | Items, stock, weighted-average and cost-layer costing |
| Accounting | `acc` | Chart of accounts, journals, the ledger, tax, period locks, opening balances; banks, money documents, reconciliation |
| Sales | `sal` | Invoices, POS |
| Purchase | `pur` | Bills |
| Customer | `cus` | Leads and opportunities; helpdesk, SLA |
| Reporting | `rpt` | Reports, GSTR-1/3B |

There were twelve, one schema each. Three merges took them to seven, each time because two services were two halves of one job:

- **Master** absorbed Platform, Identity and Contacts. Signing in reads a user, the branches they can reach and the customer's licence — three tables that were in three schemas behind two service hops, and are now one query.
- **Accounting** absorbed Banking. A money document exists to move a balance in the ledger, and the two could not share a transaction while they were separate: a payment could save and its posting fail, with nothing to roll back.
- **Customer** is CRM and Support. A lead becomes a customer and a customer raises a ticket — one subject, one lifecycle. Both were empty scaffolds.

Master is the exception to one-schema-per-service, and deliberately. `mst` lives in the shared master database; `con` lives in each customer's own, because a contact belongs to one branch's books. One API host, two DbContexts, two databases — which is the tenancy model rather than an accident.

Each service is exactly three projects:

```
{Module}.Entity/       table entities, models, enums
{Module}.Repository/   DbContext, repositories, seed data
{Module}.Api/          controllers, services, DI
```

Dependency direction is one-way — `Api → Repository → Entity → Shared.Kernel` — and it is enforced by the project references, so a backwards reference fails the build.

## Service boundaries

A service never touches another service's `DbContext`. Cross-service reads go over HTTP through a named seam:

| Seam | Direction | Purpose |
|---|---|---|
| `ITenantDirectory` | any service → Master | which database a customer's books live in |
| `IFinancialYearProvider` | any service → Master | the branch's financial year start, for document numbers |
| `IBaseCurrencyProvider` | any service → Master | the branch's base currency, for ledger rows |
| `IAccountingSubAccounts` | Master → Accounting | a contact's six sub-accounts |
| `IInventoryOpeningStock` | Accounting → Inventory | opening quantity and unit cost, which seed the weighted average |
| `ITenantSeeder` | Master → each service | master data for a newly created organization |

Writes that cross a boundary go via events instead: Sales and Purchase publish, Accounting consumes and writes the ledger.

Several seams that used to be on this list are gone, because both ends are now one service. `IPlatformDirectory` (Identity → Platform) and its DTO were deleted outright; `IMasterCurrencies`, `IIdentityAdmin` and the tenant directory kept their interfaces but read the database directly. Their internal endpoints stay, because the services that did not merge still call them.

## Frontend

| App | Audience |
|---|---|
| `web` | The customer's staff — the full ERP |
| `portal` | Their customers and vendors — own invoices only *(Phase 2)* |
| `admin` | Platform operators — tenants, provisioning |
| `desktop` | Shop floor — POS with ESC/POS receipt printing |
| `docs` | This site |

Libraries mirror the backend: `libs/{module}/{module}-core` holds view-models with no templates, `{module}-ui` holds pages. `-core` stays Ionic-compatible — no `window`, no Syncfusion, no Electron APIs — so the same view-models drive mobile.

## Stack

.NET with EF Core (LINQ only, no raw SQL except `CREATE DATABASE`, RLS policies, triggers and `set_config`) · PostgreSQL with row-level security · Angular with signals · Azure Service Bus, Key Vault and Blob Storage.



# Build status

An honest inventory.

The backend builds clean with zero warnings under `TreatWarningsAsErrors`, and 214 tests pass with none skipped — against a real PostgreSQL 16, because the ledger's guarantees are half in the database. All 14 migrations apply. The frontend's `npm run check` runs lint, typecheck, 66 tests and both app builds.

The full solution builds, including the three services that are still empty shells: each carries a `Program.cs` that starts and reports what it is, so there is nothing to exclude. `Bill-Book.Debug.slnf` still exists for a faster inner loop.

## Built — schema, API and screens

| Area | What exists |
|---|---|
| `Shared.Kernel` | `AuditableEntity`, audit interceptor, `ICurrentUser`, tenancy, numbering, GST calculation, secret/event/email/storage interfaces |
| Master · reference | Countries, states, currencies, 4 reference masters and HSN/SAC with a CBIC CSV importer |
| Master · tenancy | Customers, organizations, licences, the tenant directory, SMTP, config, org currencies; public signup, background provisioning, status polling |
| Master · auth | Users, roles, 120 permissions, tokens, OTP; two-step login, org switching, invitations, password reset |
| Master · contacts | Contacts with roles, addresses, bank details, licences and attachments; the GSTIN versus place-of-supply check |
| Inventory | Item master with pharma and jewellery profiles, guarded stock decrement, weighted average and FIFO/LIFO/FEFO/specific cost layers, batches, serials, backdated recosting |
| Accounting · ledger | Chart of accounts, sub-accounts, effective-dated GST rates, payment terms, numbering series; the general ledger with a deferred balance trigger, the manual journal, the account ledger, the trial balance, period locks and opening balances |
| Accounting · banking | Banks, bank accounts each provisioning their own ledger account, spend/receive/transfer money with allocation, settlement and FX, CSV and XLSX statement import with matching |
| Purchase | Purchase Orders, Goods Receipts, Bills, and Debit Notes with full UI screens and API integration |
| Sales | Quotes, Sales Orders, Delivery Challans, Invoices, and Credit Notes with full UI forms, lists, and API integration |
| Workers | CostingEngine — claims movements from `inv.StockMovements`, costs them, then posts them to the ledger |
| Tooling | 31-project solution, 25 Nx projects, VS Code one-press debug, YARP gateway, a Postman collection generated from the controllers |

## Not built

- **Customer, Reporting** — project folders and `.csproj` exist, nothing else. Customer is where CRM and the support helpdesk will both be built
- **Notification and RateSync workers** — a `.csproj` and an empty `Consumers/` folder. Mail currently sends from Master, queued in process
- **`apps/portal`, `apps/admin`** — scaffolded, zero source files
- **`apps/desktop`** — POS terminal module and ESC/POS thermal printing service built; full CRUD, inventory sync, and offline database support pending

## Known gaps

- `ISecretStore` and `IEventPublisher` have development stand-ins only. The secret store keeps what it was given in memory and reads through to configuration for anything else; the event publisher logs and delivers nothing, so nothing that reads an event works yet. Key Vault and Service Bus are still to write
- `JournalDetails` is the only per-customer table without `OrgId` (it scopes via its parent journal)
- No SMS provider, so mobile OTP cannot deliver
- Component tests need the Angular Vite plugin and are not set up



# Form validation

**Status: built.**

All major create/edit forms now validate mandatory fields **before** sending the API request.

## What changed

- Required fields are now checked in the submit handler, not only by disabled buttons.
- Nested rows in composite forms (contacts, items, money documents) are validated row-by-row before save/post.
- The user now gets an immediate, field-specific message instead of a round-trip failure from the server.

## Coverage

Validation guards were added for these UI areas:

- **Accounting**: chart of accounts, numbering series, payment terms, tax master
- **Banking**: banks, bank accounts, spend/receive money, transfer money
- **Master**: contacts, contact person roles, roles, users
- **Inventory**: categories, items, metal purities, warehouses
- **Settings**: organizations, organization settings, currencies, SMTP settings

The API remains authoritative, but the client now blocks obvious mandatory-field misses earlier.



