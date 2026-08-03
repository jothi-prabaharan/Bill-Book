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

Today the only thing that sets it is **sub-account provisioning** — giving an account a sub-account marks it used in the same transaction. Journals, documents and opening balances will each set it as they are built; until then, a user-created account with nothing beneath it stays editable.

The account's **currency** is frozen with the rest of the configuration. Changing it on an account that already holds postings would restate every one of them at a different rate.

## Seeded accounts

Twelve written when an organization is created, all `IsSystemDefault`, so locked from birth:

Accounts Receivable · Inventory · Input GST · Accounts Payable · Output GST · Opening Balance Equity · Sales Revenue · Cost of Goods Sold · Realized FX Gain/Loss · Unrealized FX Gain/Loss · **Advance to Vendor** · **Advance from Customer**

The two advance accounts hold money that moved before a document existed, or past what one asked for — an advance paid to a supplier, a customer's deposit, the excess when a payment is rounded up. They are deliberately **not** netted into Accounts Payable or Accounts Receivable: a payable is what you owe against a document, and folding an unapplied advance into it understates the control account and puts the aging out by the same amount.

They can be **renamed for display** — the hidden `AccountSystemName` is what code matches on — but never deleted, and their code and flags never change.

## Sub-accounts

Per-contact, per-item and per-tax detail beneath a control account, so the chart stays small while the ledger keeps a sub-dimension.

**Never created by hand.** They are provisioned by the master that owns them:

| Owner | Creates |
|---|---|
| Contact | 4 — Accounts Receivable, Accounts Payable, Advance to Vendor, Advance from Customer |
| Item | 3 — Inventory, Cost of Goods Sold, Sales Revenue |
| Tax rate | up to 6 — CGST, SGST and IGST beneath **each** of Input GST and Output GST |

For a tax sub-account, three things identify it: the **parent account** gives the direction (input or output), `ReferenceId` gives the rate, and `TaxComponent` gives the component. That is why the unique key includes the component — otherwise CGST, SGST and IGST would collide under one parent.

`AccountTypeId` is copied from the parent account on write and never accepted from a caller; if the two disagreed, a report grouped by type would contradict the same report grouped by account.

All four of a contact's are per contact for the same reason: every one of those balances is answered about a named contact. You refund a particular customer's deposit, not a pooled one, and a control account whose balance cannot be split by contact cannot be reconciled at all.

Provisioning is **idempotent** — per target, not just per call — because the events that trigger it are at-least-once. A contact created before the advance accounts existed gains exactly the two it is missing when provisioning is re-run, and keeps the two it already had. Retiring a master deactivates its sub-accounts rather than deleting them, so history survives.

If a control account cannot be resolved — the chart was never seeded for the organization, or a system account was renamed at the database level — provisioning creates what it can and reports the rest as **missing**. A partial provision is a 409, not a 200: a contact with no Accounts Receivable sub-account would silently drop out of the aging report.

The **Sub-accounts** screen (Accounting → Sub-accounts) lists them grouped under their control account, filterable by owner type. It is read-only in both directions — nothing on it can be created, renamed or retired, because the owning master decides all three.

```
GET    /api/accounts?includeInactive=false
POST   /api/accounts
PUT    /api/accounts/{id}
DELETE /api/accounts/{id}          deactivates; system accounts refused
GET    /api/sub-accounts?referenceType=&referenceId=
POST   /internal/sub-accounts/provision     409 when control accounts are missing
POST   /internal/sub-accounts/deactivate
```
