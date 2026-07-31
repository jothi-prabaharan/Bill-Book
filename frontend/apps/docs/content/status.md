# Build status

An honest inventory. The implemented services compile; nothing has been run against a database yet — see [Running locally](#/running-locally). The full solution does **not** build, because the eight unbuilt services are empty project shells with no entry point — build `Bill-Book.Debug.slnf` instead.

## Built

| Area | What exists |
|---|---|
| `Shared.Kernel` | `AuditableEntity`, audit interceptor, `ICurrentUser`, secret/event/email interfaces |
| Identity | 9 entities, DbContext, 5 roles + 120 permissions seeded |
| Platform | 7 entities, DbContext, config seed |
| Master | Countries, states, currencies + 4 reference masters and HSN/SAC, all seeded, read-only API |
| Auth | Login, org selection, OTP forgot-password, reset — services and controller |
| Signup | Public signup, Trial licence, background provisioner, status polling |
| Currencies | Per-org activation with base-currency protection |
| Accounting | Chart of accounts, sub-accounts, tax master — entities, per-request tenant resolution, APIs and screens |
| Frontend | Teams-style shell, login, signup, OTP wizard, trial-expired page, currency settings, chart of accounts, sub-accounts, tax master |
| Tooling | 41-project solution, 34 Nx projects, VS Code one-press debug, YARP gateway |

## Not built

- **Eight of twelve services** — Contacts, Crm, Inventory, Sales, Purchase, Banking, Support, Reporting
- **The rest of Accounting** — journals, the ledger and its combined view, fixed assets, opening balances
- **Three workers** — Notification, CostingEngine, RateSync
- Real SMTP, Key Vault and Service Bus implementations
- Migrations — none generated yet
- A context pane in the shell, so `/accounting/chart-of-accounts` and `/accounting/sub-accounts` are reachable only by typing the URL

## Known gaps

- `JournalDetails` is the only per-customer table without `OrgId` (it scopes via its parent journal)
- `acc.vw_LedgerDetail` must set `security_invoker = true` or it bypasses row-level security
- `CREATE VIEW` is not in the raw-SQL exception list, so the ledger view needs a decision
- No SMS provider, so mobile OTP cannot deliver
