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
