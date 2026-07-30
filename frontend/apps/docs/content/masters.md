# Master data

**Status: partial.** The global reference masters are built; the per-organization ones are designed but not yet coded.

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
| Permissions | 120 (12 modules × 10 actions) | via roles |

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

## Designed, not built

- **Chart of accounts** (`acc.Accounts`) and sub-accounts — see [Chart of accounts](#/chart-of-accounts)
- **Tax master** — 6 seeded GST rates, effective-dated
- **Contacts**, **Items**, **UOM**, **Branch/Warehouse**, **Item Category**, **HSN/SAC**

The last group lives in per-customer schemas, so it needs tenant connection resolution — a piece of infrastructure that does not exist yet.
