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
