# Build status

An honest inventory. Nothing here has been through a compiler yet — see [Running locally](#/running-locally).

## Built

| Area | What exists |
|---|---|
| `Shared.Kernel` | `AuditableEntity`, audit interceptor, `ICurrentUser`, secret/event/email interfaces |
| Identity | 9 entities, DbContext, 5 roles + 120 permissions seeded |
| Platform | 7 entities, DbContext, config seed |
| Master | Countries, states, currencies + 4 reference masters, all seeded, read-only API |
| Auth | Login, org selection, OTP forgot-password, reset — services and controller |
| Signup | Public signup, Trial licence, background provisioner, status polling |
| Currencies | Per-org activation with base-currency protection |
| Frontend | Teams-style shell, login, signup, OTP wizard, trial-expired page, currency settings |
| Tooling | 41-project solution, 34 Nx projects, VS Code one-press debug, YARP gateway |

## Not built

- **Nine of twelve services** — Contacts, Crm, Inventory, Sales, Purchase, Accounting, Banking, Support, Reporting
- **Accounting**, which is fully specified but has no code: chart of accounts, journals, the ledger and its combined view, tax master
- **Three workers** — Notification, CostingEngine, RateSync
- Real SMTP, Key Vault and Service Bus implementations
- Migrations — none generated yet
- User and role management screens

## Known gaps

- `JournalDetails` is the only per-customer table without `OrgId` (it scopes via its parent journal)
- `acc.vw_LedgerDetail` must set `security_invoker = true` or it bypasses row-level security
- `CREATE VIEW` is not in the raw-SQL exception list, so the ledger view needs a decision
- No SMS provider, so mobile OTP cannot deliver
