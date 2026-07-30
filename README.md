# RetailErp

A multi-tenant retail ERP and accounting SaaS for Indian SMBs. Zoho Books is the functional benchmark.

Billing, inventory, double-entry accounting, GST compliance, CRM and a support helpdesk, delivered as web, client-portal, admin and desktop apps over a set of .NET services on PostgreSQL.

> **Status:** early. See [Current status](#current-status) before trying to build anything — the application code is not yet in this repository.

---

## Architecture

### Services

Twelve services, each owning exactly one PostgreSQL schema:

| Service | Schema | Responsibility |
|---|---|---|
| Master | `mst` | Countries, states, GST state codes |
| Platform | `plt` | Customers, organizations, tenant directory, provisioning |
| Identity | `idn` | Users, roles, permissions, JWT auth, password reset |
| Contacts | `con` | Customers and vendors |
| Crm | `crm` | Leads, opportunities |
| Inventory | `inv` | Items, stock, weighted average costing |
| Sales | `sal` | Invoices, POS |
| Purchase | `pur` | Bills, vendor documents |
| Accounting | `acc` | Chart of accounts, journal entries, fixed assets, tax master |
| Banking | `bnk` | Accounts, transactions, reconciliation |
| Support | `sup` | Helpdesk tickets, SLA, chat |
| Reporting | `rpt` | Sales, purchase, accounting, inventory, GSTR-1/3B |

Three background workers — **Notification** (`ntf`), **CostingEngine**, **RateSync** (`rat`) — plus a **YARP** gateway.

Services communicate by API call or event. A service never reaches into another service's `DbContext`.

### Tenancy

Two nested boundaries, enforced by different mechanisms:

- **Customer** — the account and billing entity. Owns **one physical database**.
- **Organization** — a set of books, with its own GSTIN, currency and branches. A Customer owns **many**, all sharing that Customer's database.

| Boundary | Enforced by |
|---|---|
| Customer ↔ Customer | Separate physical databases |
| Organization ↔ Organization | `OrgId` + EF Core global query filter + Postgres RLS |

`mst`, `plt`, `idn` and `rat` live in a shared **master database**. Every other schema is replicated per customer database.

Request flow: resolve `CustomerId` from the JWT → select the database via the `plt` tenant directory (cached) → set `app.current_org_id` **transaction-locally** with `set_config(..., true)`. Never connection-level, because pooled connections are reused across requests.

`OrgId` is load-bearing for security. A per-customer table without a query filter leaks data between organizations.

### Accounting model

Everything posts through a **Journal Entry** — invoices, bills, payments, depreciation, opening balances. Nothing else writes GL rows: Sales, Purchase and Banking publish events, and Accounting consumes them.

- Chart of accounts is four tables: `AccountTypes` (5 fixed) → `AccountSubTypes` → `Accounts` → `SubAccounts`
- Journal entry lines are debit **xor** credit, never negative
- Lifecycle is Draft → Posted → Reversed. A posted entry is never edited, only reversed with an offsetting entry.
- Inventory (Asset) and Cost of Goods Sold (Expense) are deliberately separate — gross profit depends on it

### Stack

.NET services · EF Core code-first (LINQ only, no raw SQL) · PostgreSQL with RLS and JSONB · Azure Service Bus, Key Vault, Blob Storage and SignalR Service · Angular/Nx frontend with Ionic-compatible core libraries · Syncfusion for PDF, ESC/POS for receipt printing.

---

## Repository layout

```
backend/
├── Bill-Book.sln
├── Api/
│   └── {Module}/                     one folder per service (×12)
│       ├── {Module}.Entity/          TableEntities/ · Models/ · Enums/
│       ├── {Module}.Repository/      DbContext, repositories, seed data
│       └── {Module}.Api/             controllers, services, DI
├── shared/
│   └── Shared.Kernel/                AuditableEntity, interceptors
├── worker/
│   ├── Notification.Worker/
│   ├── CostingEngine.Worker/
│   └── RateSync.Worker/
└── Gateway/                          YARP
frontend/
├── apps/
│   ├── web/                          main application
│   ├── portal/                       client portal (Phase 2)
│   ├── admin/                        platform operator screens
│   └── desktop/                      Electron; only host for ESC/POS printing
└── libs/
    ├── {module}/                     one folder per module, mirroring Api/
    │   ├── {module}-core/            view-models + models, no templates
    │   └── {module}-ui/              pages
    └── shared/                       auth, api-client, ui-components,
                                      currency-format, theming
```

Each of the twelve services gets its own folder under `backend/Api/`, holding exactly the three `{Module}.*` projects. Dependency direction is one-way: `Api` → `Repository` → `Entity` → `Shared.Kernel`.

`-core` libraries must stay Ionic-compatible — no `window`/`document`, no Syncfusion, no Electron or Node APIs. Every page must work at ~360px wide.

---

## Getting started

> These steps describe the intended workflow. They have **not** been executed successfully yet — the code they refer to is not committed, and the existing sources have never been compiled. Treat this section as the target, and expect to correct it during the first real build.

### Prerequisites

- .NET SDK 10 (EF Core 10 is the pinned data-access version)
- PostgreSQL 16 or newer
- Node.js 20+ and the Nx CLI
- `dotnet-ef` tools: `dotnet tool install --global dotnet-ef`
- For full functionality: Azure Key Vault, Service Bus, Blob Storage and SignalR Service, or local equivalents

### Database

Create the master database with UTF-8 encoding — multi-language support (Tamil, Chinese) depends on it:

```bash
createdb -E UTF8 retailerp_master
```

Per-customer databases are created at runtime by Platform during provisioning, not by hand.

The account the application uses needs `CREATEDB`. Who holds that privilege in production is still an open decision.

### Migrations

Each service owns and migrates its own schema:

```bash
cd backend
dotnet ef database update --project Api/Master/Master.Repository     --startup-project Api/Master/Master.Api
dotnet ef database update --project Api/Platform/Platform.Repository --startup-project Api/Platform/Platform.Api
dotnet ef database update --project Api/Identity/Identity.Repository --startup-project Api/Identity/Identity.Api
```

### Run

```bash
cd backend && dotnet run --project Gateway          # then each Api/{Module}/{Module}.Api
cd frontend && npx nx serve web
```

---

## Current status

**Authored:** Master (countries and 37 Indian states with GST codes), Platform (customers, organizations, tenant directory, trial signup, database provisioning), Identity (users, roles, permissions, JWT auth, password reset) — roughly 45 C# files across 10 projects.

**Not in this repository.** At present the repo contains only `CLAUDE.md`, `README.md` and `.gitignore`. The services above exist outside version control and still need to be committed.

**Never compiled.** The code was written without a .NET SDK available. Expect failures on the first `dotnet build`, most likely around EF Core 10 package versions and namespace collisions (`Identity` and `Platform` are close to framework namespaces).

### Blocking issues

1. `AuthController.ResolveCustomerIdAsync` returns null — needs a Platform call. **Login cannot complete until this is implemented.**
2. `ISecretStore`, `IEventPublisher` and `IEmailSender` are interfaces with no implementations — DI startup fails.
3. Login does not check whether the customer's database finished provisioning.
4. `CustomerCode` generation reads the maximum then increments — needs retry-on-conflict for concurrent signups.

### Not yet built

Contacts, Crm, Inventory, Sales, Purchase, Accounting, Banking and Reporting services. The entire frontend. The gateway. All three background workers.

---

## Roadmap

**Phase 1** — Contacts, Inventory, Sales, Purchase, Accounting core (chart of accounts, journal entries, fixed assets, opening balances), Tax Master, COGS and weighted average costing, Banking core, CRM, Support helpdesk, reports including GSTR-1/3B, multi-currency, RBAC, organization settings, Platform provisioning.

**Phase 2** — Recurring invoices, payment reminders, retainer invoices, client portal, Paytm, bank feeds and reconciliation, multi-location price lists, API clients.

**Phase 3** — Project accounting, budgeting, workflow approvals, custom fields and reports, e-invoicing and e-way bill, FIFO/FEFO/LIFO batch allocation, compliance bundle.

---

## Contributing

Read **[CLAUDE.md](./CLAUDE.md)** before writing any code. It holds the binding conventions — LINQ-only data access, entity shape, tenancy rules, the accounting model, and the recipes for adding a table or an endpoint. The rules there are decisions already taken, not suggestions.

It also lists what is still undecided. If your work touches one of those items, ask rather than assume.

Then read **[SPEC.md](./SPEC.md)** for the concrete what-to-build: column-level table definitions, seed data, page specifications and the build order. Each item carries a status — ✅ built, 🔨 designed but not built, 📋 scoped only.
