# Chart of accounts

**Status: built.** The first per-customer feature — everything before it lived in the shared master database.

## Two levels, not three

`mst.AccountTypes` (5 fixed) → `acc.Accounts`. Sub-types were removed; `ParentAccountId` supplies any display grouping they used to give, and a parent must share its child's account type so the tree cannot cross report sections.

Because sub-types held the contra flag, **`IsContra` now lives on the account**. Set it where the normal balance runs opposite the type — accumulated depreciation, sales returns, discount given, purchase returns. Reports subtract those; miss it and the report overstates silently.

`AccountTypeId` points at the master database, so no foreign key is possible. It is validated in C#.

## Usage flags

Which pickers an account appears in:

| Flag | Meaning |
|---|---|
| `IsSales` | Selectable on sales documents |
| `IsPurchase` | Selectable on purchase documents |
| `IsPayment` | Selectable as the settlement account on a payment |
| `IsBank` | Is a bank or cash account — bank pickers, reconciliation, transfers |
| `IsContra` | Reports subtract it |
| `IsJE` | Selectable on a manual journal line — **backend-only, never shown in the UI** |
| `IsLock` | Posting freeze. Operational and reversible, unlike the config lock |

`IsJE` is off for the seeded control accounts (AR, AP, Inventory, GST) on purpose: the system posts to those from documents, and a hand-written journal straight to Accounts Receivable would break its tie to the per-contact sub-ledger.

## The configuration lock

An account is **config-locked** when it has been used (`IsUsed`) or is a seeded system account. Locked means these are frozen:

`AccountTypeId` · `AccountCode` · `IsContra` · `IsSales` · `IsPurchase` · `IsPayment` · `IsBank`

Still editable: **display name, active state, posting lock, parent**.

The reason is not tidiness. Re-pointing a used Expense account to Asset would move a year of postings from the profit and loss to the balance sheet without touching a single ledger row — the numbers would change and nothing would say why. The API refuses it; the screen renders those fields read-only and explains itself.

`IsUsed` is set on first reference and never cleared. An account cannot become unused.

## Seeded accounts

Ten written when an organization is created, all `IsSystemDefault`, so locked from birth:

Accounts Receivable · Inventory · Input GST · Accounts Payable · Output GST · Opening Balance Equity · Sales Revenue · Cost of Goods Sold · Realized FX Gain/Loss · Unrealized FX Gain/Loss

They can be **renamed for display** — the hidden `AccountSystemName` is what code matches on — but never deleted, and their code and flags never change.

## Sub-accounts

Per-contact, per-item and per-tax detail beneath a control account, so the chart stays small while the ledger keeps a sub-dimension.

**Never created by hand.** They are provisioned by the master that owns them:

| Owner | Creates |
|---|---|
| Contact | 2 — Accounts Receivable, Accounts Payable |
| Item | 3 — Inventory, Cost of Goods Sold, Sales Revenue |
| Tax rate | up to 6 — CGST, SGST and IGST beneath **each** of Input GST and Output GST |

For a tax sub-account, three things identify it: the **parent account** gives the direction (input or output), `ReferenceId` gives the rate, and `TaxComponent` gives the component. That is why the unique key includes the component — otherwise CGST, SGST and IGST would collide under one parent.

`AccountTypeId` is copied from the parent account on write and never accepted from a caller; if the two disagreed, a report grouped by type would contradict the same report grouped by account.

Provisioning is **idempotent**, because the events that trigger it are at-least-once. Retiring a master deactivates its sub-accounts rather than deleting them, so history survives.

```
GET    /api/accounts?includeInactive=false
POST   /api/accounts
PUT    /api/accounts/{id}
DELETE /api/accounts/{id}          deactivates; system accounts refused
GET    /api/sub-accounts?referenceType=&referenceId=
```
