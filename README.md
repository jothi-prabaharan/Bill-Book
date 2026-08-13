# RetailErp

## Local development credentials

> **Localhost only.** These values exist so a fresh clone runs without setup. They are committed on purpose and are safe precisely because they reach nothing but your own machine. Staging, UAT and [...]

```text
PostgreSQL          localhost:5432
Username            postgres
Password            123

Development DB      retailerp_master     mst, rat schemas
Testing DB          retailerp_test       drop and recreate freely
Design-time DB      retailerp_design     per-customer schemas, for dotnet ef
Sample customer DB  IN0000000001         stands in for a provisioned tenant
```

Connection string:

```text
Host=localhost;Port=5432;Database=retailerp_master;Username=postgres;Password=123
```

First run:

```powershell
./scripts/setup-dev-db.ps1
```

That creates all four databases, generates the EF Core migrations and applies them, then prints the seeded master row counts. Re-running it is safe. Then press <kbd>F5</kbd> in VS Code and pick **[...]

---

A multi-tenant retail ERP and accounting SaaS for Indian SMBs. Bill Books is the functional benchmark.

Billing, inventory, double-entry accounting, GST compliance, CRM and a support helpdesk, delivered as web, client-portal, admin, desktop and documentation apps over a set of .NET services on Postg[...]

> **Status:** early but real. Master, Inventory and Accounting are built end to end — schema, API and screens. Sales has its schema only; Purchase, Customer and Reporting are scaffolds. The back[...]

---

## Architecture

### Services

Seven services:

| Service | Schema | Responsibility |
|---|---|---|
| Master | `mst`, `con` | Countries and states; customers, organizations, the tenant directory and provisioning; users, roles, permissions, JWT auth; contacts |
| Inventory | `inv` | Items, stock, weighted average costing |
| Accounting | `acc` | Chart of accounts, journal entries, tax master; bank accounts, money documents, reconciliation |
| Sales | `sal` | Invoices, POS |
| Purchase | `pur` | Bills, vendor documents |
| Customer | `cus` | Leads and opportunities; helpdesk tickets, SLA, chat |
| Reporting | `rpt` | Sales, purchase, accounting, inventory, GSTR-1/3B |

Three background workers — **Notification** (`ntf`), **CostingEngine**, **RateSync** (`rat`) — plus a **YARP** gateway.

There were twelve, one schema each. Three merges took them to seven, each time because two services were two halves of one job — signing in needs the user, the branch and the licence together; a[...]

Master is the exception to one-schema-per-service, and deliberately: `mst` is in the shared master database while `con` is in each customer's own, so contacts keep their `OrgId` filter and their R[...]

Services communicate by API call or event. A service never reaches into another service's `DbContext`.

### Tenancy

Two nested boundaries, enforced by different mechanisms:

- **Customer** — the account and billing entity. Owns **one physical database**.
- **Organization** — a set of books, with its own GSTIN, currency and branches. A Customer owns **many**, all sharing that Customer's database.

| Boundary | Enforced by |
|---|---|
| Customer ↔ Customer | Separate physical databases |
| Organization ↔ Organization | `OrgId` + EF Core global query filter + Postgres RLS |

`mst` and `rat` live in a shared **master database**. Every other schema is replicated per customer database.

Request flow: resolve `CustomerId` from the JWT → select the database via the `mst` tenant directory (cached) → set `app.current_org_id` **transaction-locally** with `set_config(..., true)`. N[...]

`OrgId` is load-bearing for security. A per-customer table without a query filter leaks data between organizations.

### Accounting model

Everything posts through a **Journal Entry** — invoices, bills, payments, depreciation, opening balances. Nothing else writes GL rows: Sales and Purchase publish events, and Accounting consumes [...]

- Chart of accounts is three tables: `mst.AccountTypes` (5 fixed) → `acc.Accounts` → `acc.SubAccounts`. Sub-types were removed; `IsContra` lives on the account
- Journal entry lines are debit **xor** credit, never negative
- Lifecycle is Draft → Posted → Reversed. A posted entry is never edited, only reversed with an offsetting entry.
- Inventory (Asset) and Cost of Goods Sold (Expense) are deliberately separate — gross profit depends on it

### Stack

.NET services · EF Core code-first (LINQ only, no raw SQL) · PostgreSQL with RLS and JSONB · Azure Service Bus, Key Vault, Blob Storage and SignalR Service · Angular/Nx frontend with Ionic-com[...]

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
│   ├── desktop/                      Electron; only host for ESC/POS printing
│   └── docs/                         static help site (markdown, no API)
└── libs/
    ├── {module}/                     one folder per module, mirroring Api/
    │   ├── {module}-core/            view-models + models, no templates
    │   └── {module}-ui/              pages
    └── shared/                       auth, api-client, ui-components,
                                      currency-format, theming
```

Each of the seven services gets its own folder under `backend/Api/`, holding exactly the three `{Module}.*` projects. Dependency direction is one-way: `Api` → `Repository` → `Entity` → `Sha[...]

`-core` libraries must stay Ionic-compatible — no `window`/`document`, no Syncfusion, no Electron or Node APIs. Every page must work at ~360px wide.

---

## Getting started

> These steps describe the intended workflow. They have **not** been executed successfully yet — the code they refer to is not committed, and the existing sources have never been compiled. Trea[...]

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

Per-customer databases are created at runtime by Master during provisioning, not by hand.

The account the application uses needs `CREATEDB`. Who holds that privilege in production is still an open decision.

### Migrations

Each service owns and migrates its own schema:

```bash
cd backend
dotnet ef database update --project Api/Master/Master.Repository \
  --startup-project Api/Master/Master.Api --context MasterDbContext
dotnet ef database update --project Api/Master/Master.Repository \
  --startup-project Api/Master/Master.Api --context ContactsDbContext
dotnet ef database update --project Api/Inventory/Inventory.Repository \
  --startup-project Api/Inventory/Inventory.Api
dotnet ef database update --project Api/Accounting/Accounting.Repository \
  --startup-project Api/Accounting/Accounting.Api
```

Master needs `--context` because it maps two: `MasterDbContext` against the shared master database, `ContactsDbContext` against a customer's own.

### Run

```bash
cd backend && dotnet run --project Gateway          # then each Api/{Module}/{Module}.Api
cd frontend && npx nx serve web
```

---

## Current status

**Built end to end — schema, API and screens:**

| Area | State |
|---|---|
| `Shared.Kernel` | `AuditableEntity`, audit interceptor, secret/event/email interfaces |
| Master | Countries, states, currencies + TransactionTypes, LedgerTypes, LedgerSources, AccountTypes — all seeded, read-only API |
| Master | Customers, organizations, licences, SMTP, config, org-currencies; signup + background provisioning |
| Master | Users, roles, permissions (120), tokens, OTP; login, org selection, OTP reset |
| Master | Contacts with roles, addresses, bank details, licences and attachments |
| Inventory | Item master, stock, weighted average and cost-layer costing, batches and serials |
| Accounting | Chart of accounts, journals, the ledger, period locks, opening balances; banks, money documents and statement import |
| Frontend | Teams-style shell, login, signup, OTP wizard, trial-expired page, currency settings, docs app |
| Tooling | 31-project solution, 25 Nx projects, VS Code one-press debug, YARP gateway, generated Postman collection |

**Verified.** `dotnet build` is clean under `TreatWarningsAsErrors`, `dotnet test` passes 214 with none skipped, and all 14 migrations apply to PostgreSQL 16. `npm run check` runs lint, typecheck[...]

**Development stand-ins**, all marked in code: the secret store keeps written secrets in memory and reads through to configuration for anything it was never given, and the event publisher logs ra[...]

### Not built

Sales beyond its schema. Purchase, Customer and Reporting services. The Notification and RateSync workers. The `portal`, `admin` and `desktop` apps are empty scaffolds.

---

## Roadmap

**Phase 1** — Contacts, Inventory, Sales, Purchase, Accounting core (chart of accounts, journal entries, opening balances), Tax Master, COGS and weighted average costing, banking core, CRM, Sup[...]

**Phase 2** — Recurring invoices, payment reminders, retainer invoices, client portal, Paytm, bank feeds and reconciliation, multi-location price lists, API clients.

**Phase 3** — Project accounting, budgeting, workflow approvals, custom fields and reports, e-invoicing and e-way bill, FIFO/FEFO/LIFO batch allocation, compliance bundle.

---

## Contributing

Read **[CLAUDE.md](./CLAUDE.md)** before writing any code. It holds the binding conventions — LINQ-only data access, entity shape, tenancy rules, the accounting model, and the recipes for addin[...]

It also lists what is still undecided. If your work touches one of those items, ask rather than assume.

Then read **[SPEC.md](./SPEC.md)** for the concrete what-to-build: column-level table definitions, seed data, page specifications and the build order. Each item carries a status — ✅ built, ��[...]
