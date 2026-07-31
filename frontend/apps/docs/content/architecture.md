# Architecture

Two halves: a .NET backend and an Nx/Angular frontend.

## Backend

Twelve services, each owning exactly one PostgreSQL schema, plus three workers and a YARP gateway.

| Service | Schema | Owns |
|---|---|---|
| Master | `mst` | Countries, states, currencies, transaction/ledger/account-type masters |
| Platform | `plt` | Customers, organizations, licences, tenant directory, SMTP, config |
| Identity | `idn` | Users, roles, permissions, tokens, OTP |
| Contacts | `con` | Customers and vendors |
| Crm | `crm` | Leads, opportunities |
| Inventory | `inv` | Items, stock, weighted-average costing |
| Sales | `sal` | Invoices, POS |
| Purchase | `pur` | Bills |
| Accounting | `acc` | Chart of accounts, journals, ledger, tax, fixed assets |
| Banking | `bnk` | Accounts, reconciliation |
| Support | `sup` | Helpdesk, SLA |
| Reporting | `rpt` | Reports, GSTR-1/3B |

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
| `IPlatformDirectory` | Identity → Platform | resolve an org's customer, database readiness and licence |
| `IIdentityAdmin` | Platform → Identity | create the owner user during provisioning |
| `IMasterCurrencies` | Platform → Master | currency reference data |

Writes that cross a boundary go via events instead: Sales, Purchase and Banking publish, Accounting consumes and writes the ledger.

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
