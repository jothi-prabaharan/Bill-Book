# Master data

**Status: partial.** The global reference masters are built, as are the first three per-organization ones — chart of accounts, sub-accounts and the tax master. The trading masters (contacts, items, warehouses) are not.

## Built — global reference masters (`mst`)

All are seeded, have **no maintenance screen**, and expose read-only endpoints. New rows arrive by EF migration, because a new code with no logic behind it would be unusable anyway.

| Master | Rows | Endpoint |
|---|---|---|
| Countries | 5 | `GET /api/master/countries` |
| States | 37 Indian states/UTs by GST code | `GET /api/master/countries/{id}/states` |
| Currencies | 5 | `GET /api/master/currencies` |
| TransactionTypes | 16 | `GET /api/master/transaction-types` |
| LedgerTypes | 6 | `GET /api/master/ledger-types` |
| LedgerSources | 15 | `GET /api/master/ledger-sources` |
| AccountTypes | 5 | `GET /api/master/account-types` |
| HSN/SAC codes | 129 seeded headings, rest imported | `GET /api/master/hsn-sac` |
| Permissions | 120 (12 modules × 10 actions) | via roles |

HSN/SAC is the exception to "no maintenance screen" — it has a CSV importer, because the detailed codes change with each CBIC notification. See [HSN & SAC codes](#/hsn-sac).

### Transaction types

Keyed by a **three-letter code**, stored directly on every ledger row so a row reads without a join. `IsLedgerPosting` distinguishes documents that post from those that do not.

`QTE` Quote · `BIL` Bill · `POR` Purchase Order · `GRN` Goods Receipt · `SOR` Sales Order · `INV` Invoice · `CRN` Credit Note · `DBN` Debit Note · `JRN` Journal · `SPM` Spend Money · `RCM` Receive Money · `TRM` Transfer Money · `OPB` Opening Balance · `DEP` Depreciation · `STA` Stock Adjustment · `POS` POS Sale

Quotes and orders are commercial documents only — nothing reaches the ledger until they become an invoice or bill.

> A code can **never** change once data exists. Every ledger, journal and allocation row stores it as a plain string with no foreign key, so there is nothing to cascade a rename through.

### Ledger sources

A payment and a refund share the same transaction type — both are Spend or Receive Money — so the **source** is what tells them apart. Refund reports, GST returns and bank reconciliation all filter on it, not on the transaction type.

Payment and refund are paired in opposite directions (`BILLPAYMENT` out / `BILLREFUND` in) so each pair reconciles against the same document.

## The rename rule for system masters

Seeded masters carry two names:

| Column | Editable | Purpose |
|---|---|---|
| `SystemName` | **No** — hidden | The canonical identity. Code, reports and seeds key on this |
| `DisplayName` | Yes | What the UI shows |

So a customer can relabel "Accountant" as "Finance Lead", or "Cost of Goods Sold" as "Direct Cost", **without changing what the row is or what posts to it**. Applies to account types, roles and tax rates.

## Built — per-organization masters (`acc`)

These live in the customer's own database, resolved per request through the tenant directory. Each is seeded when an organization is created.

| Master | Rows at creation | Screen |
|---|---|---|
| Chart of accounts (`acc.Accounts`) | 10 control accounts | Accounting → Chart of accounts |
| Sub-accounts (`acc.SubAccounts`) | none — provisioned by their owner | Accounting → Sub-accounts (read-only) |
| Tax master (`acc.TaxMasters`) | 6 GST rates, effective-dated | Settings → Tax |

See [Chart of accounts](#/chart-of-accounts) and [GST & tax](#/gst).

## Designed, not built

- **Contacts**, **Items**, **UOM**, **Warehouse**, **Item Category**

These belong to the Contacts and Inventory services, neither of which exists yet. Until they do, the only sub-accounts in the system are the tax ones — the contact and item provisioning paths are built and tested by nothing.
