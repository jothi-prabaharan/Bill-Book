# SPEC.md — Tables & Pages

Build spec for **RetailErp**. Read `CLAUDE.md` first for conventions and hard rules; this file is the concrete what-to-build.

**Status key**: ✅ built · 🔨 designed, not built · 📋 scoped only, needs design

---

# PART 1 — TABLES

All columns below are in addition to the four inherited from `AuditableEntity`:
`CreatedBy` (Guid, required) · `CreatedAt` (DateTimeOffset, required) · `ModifiedBy` (Guid?) · `ModifiedAt` (DateTimeOffset?)

---

## MASTER DATABASE

### `mst.Countries` ✅
Seeded reference data. Referenced by `plt.Organizations` (real FK, same database) and by per-customer Contacts (unenforced id — cross-database FK impossible).

| Column | Type | Rules |
|---|---|---|
| CountryId | int | PK, **not** identity — explicit ids for seeding |
| CountryCode | string(2) | Required, unique. ISO 3166-1 alpha-2 |
| CountryName | string(100) | Required |
| CurrencyCode | string(3) | Required. ISO 4217 |
| PhoneCode | string(5)? | e.g. `+91`, `+1` |
| IsActive | bool | Default true |

Navigation: `ICollection<State> States`

**Seed**: IN/India/INR/+91, US/United States/USD/+1, GB/United Kingdom/GBP/+44, AE/United Arab Emirates/AED/+971, SG/Singapore/SGD/+65

### `mst.States` ✅
| Column | Type | Rules |
|---|---|---|
| StateId | int | PK, not identity |
| CountryId | int | Required, FK → Countries |
| StateCode | string(5) | Required. **For India this is the 2-digit GST state code** |
| StateName | string(100) | Required |
| IsActive | bool | Default true |

Unique index: (CountryId, StateCode)

**Seed** — all 37 Indian states/UTs by GST code: 01 Jammu and Kashmir, 02 Himachal Pradesh, 03 Punjab, 04 Chandigarh, 05 Uttarakhand, 06 Haryana, 07 Delhi, 08 Rajasthan, 09 Uttar Pradesh, 10 Bihar, 11 Sikkim, 12 Arunachal Pradesh, 13 Nagaland, 14 Manipur, 15 Mizoram, 16 Tripura, 17 Meghalaya, 18 Assam, 19 West Bengal, 20 Jharkhand, 21 Odisha, 22 Chhattisgarh, 23 Madhya Pradesh, 24 Gujarat, 26 Dadra and Nagar Haveli and Daman and Diu, 27 Maharashtra, 29 Karnataka, 30 Goa, 31 Lakshadweep, 32 Kerala, 33 Tamil Nadu, 34 Puducherry, 35 Andaman and Nicobar Islands, 36 Telangana, 37 Andhra Pradesh, 38 Ladakh, 97 Other Territory

---

### `mst.TransactionTypes` 🔨
Every document type that can post to the ledger. **Two-digit id plus a short alpha code.** Referenced from per-customer tables by unenforced id — cross-database FK is impossible, so validate in C#.

| Column | Type | Rules |
|---|---|---|
| TransactionTypeId | int | PK, **not** identity. Two digits, 01–99 |
| Code | string(5) | Required, unique. e.g. `INV`, `BL`, `SM` |
| Name | string(50) | Required |
| Module | string(30) | Required. sales / purchase / banking / accounting / inventory |
| DocumentPrefix | string(5)? | Seeds document numbering, e.g. `INV-` |
| IsLedgerPosting | bool | False for non-financial documents (quote, order) |
| IsActive | bool | Default true |

**Seed** — ids `02`, `06`, `10`, `11` carry over from the legacy system and must not be renumbered:

| Id | Code | Name | Module | Posts |
|---|---|---|---|---|
| 01 | QT | Quote | sales | no |
| 02 | BL | Bill | purchase | yes |
| 03 | PO | Purchase Order | purchase | no |
| 04 | GRN | Goods Receipt | purchase | yes |
| 05 | SO | Sales Order | sales | no |
| 06 | INV | Invoice | sales | yes |
| 07 | CN | Credit Note | sales | yes |
| 08 | DN | Debit Note | purchase | yes |
| 09 | JV | Journal | accounting | yes |
| 10 | SM | Spend Money | banking | yes |
| 11 | RM | Receive Money | banking | yes |
| 12 | RF | Refund | banking | yes |
| 13 | OB | Opening Balance | accounting | yes |
| 14 | DP | Depreciation | accounting | yes |
| 15 | SA | Stock Adjustment | inventory | yes |
| 16 | POS | POS Sale | sales | yes |

`Code` is what appears in document numbers and on screen; `TransactionTypeId` is what the ledger stores.

### `mst.LedgerTypes` 🔨
**Which leg** of a document a ledger row represents.

| Column | Type | Rules |
|---|---|---|
| LedgerTypeId | int | PK, not identity |
| Code | string(20) | Required, unique |
| Name | string(50) | Required |

**Seed**: 1 `ITEM` Line item · 2 `TAX` Tax · 3 `CONTROL` AP / AR / bank / cash control leg · 4 `COGS` Cost of goods sold · 5 `FX` Realized exchange gain or loss · 6 `ROUNDOFF` Rounding

### `mst.LedgerSources` 🔨
**What produced** the ledger row.

| Column | Type | Rules |
|---|---|---|
| LedgerSourceId | int | PK, not identity |
| Code | string(20) | Required, unique |
| Name | string(50) | Required |

**Seed**: 1 `TRANSACTION` Document posting · 2 `PAYMENT` Payment · 3 `REFUND` Refund · 4 `JOURNAL` Manual journal · 5 `OPENINGBALANCE` Opening balance · 6 `PREPAYMENT` Prepayment applied · 7 `ALLOCATION` Credit-note allocation

---

### `plt.Customers` ✅
The account/billing entity. **One Customer = one physical database.**

| Column | Type | Rules |
|---|---|---|
| CustomerId | Guid | PK |
| CustomerCode | string(10) | Required, unique. 10-digit sequential, zero-padded (`D10`), generated in C# |
| CountryPrefix | string(2) | Required, default `IN` |
| Name | string(200) | Required |
| BillingEmail | string(200) | Required, email |
| Status | enum→string(20) | Provisioning / Active / Suspended / Trial |
| PlanTier | string(30) | Required, default `Standard` |

Navigation: `ICollection<Organization> Organizations`, `CustomerDatabase? CustomerDatabase`

Database name = `CountryPrefix + CustomerCode` → `IN0000000001`

### `plt.Organizations` ✅
A set of books. Many per Customer, sharing that Customer's database, separated by `OrgId`.

| Column | Type | Rules |
|---|---|---|
| OrgId | Guid | PK |
| CustomerId | Guid | Required, FK → Customers |
| Name | string(200) | Required |
| BaseCurrency | string(3) | Required, default `INR` |
| FinancialYearStartMonth | int | Range 1–12, default 4 (April) |
| Gstin | string(15)? | |
| Pan | string(10)? | |
| Tan | string(10)? | |
| Tin | string(15)? | |
| Cin | string(21)? | |
| UdyamNumber | string(20)? | |
| LogoUrl | string(500)? | Blob storage path — never store the image itself |
| Status | enum→string(20) | Provisioning / Active / Suspended / Trial |
| AddressLine1 | string(200)? | |
| AddressLine2 | string(200)? | |
| City | string(100)? | |
| StateId | int? | FK → mst.States |
| PostalCode | string(10)? | |
| CountryId | int | Required, FK → mst.Countries |
| PhoneNumber | string(20)? | Regex `^\d{2,}[\s\-]?\d{3,}$` — STD code mandatory |
| MobileNumber | string(20)? | No regex — lengths vary by country |
| Email | string(200)? | Email |
| Website | string(200)? | Url |

Unique index: (CustomerId, Name)

**Validate `StateId`'s StateCode matches Gstin's first 2 digits** — a mismatch silently breaks CGST/SGST vs IGST.

### `plt.CustomerDatabases` ✅
Tenant directory.

| Column | Type | Rules |
|---|---|---|
| CustomerId | Guid | PK **and** FK → Customers (one-to-one) |
| DatabaseName | string(63) | Required, unique. Postgres identifier limit |
| ConnectionSecretRef | string(200) | Required. **Key Vault reference — never the raw connection string** |
| Status | enum→string(20) | Provisioning / Ready / Failed |
| ProvisionedAt | DateTimeOffset? | |

### `plt.ApiClients` 📋
| Column | Type | Rules |
|---|---|---|
| ApiClientId | Guid | PK |
| OrgId | Guid | Required |
| ClientId | string(100) | Required, unique |
| ClientSecretHash | string(500) | Required. **Hashed; shown once at creation** |
| Name | string(200) | Required |
| Scopes | string(1000) | Comma-separated, e.g. `read:inventory,write:sales` |
| RateLimitTier | string(30) | |
| IsActive | bool | Default true |

### `plt.PlatformAdminUsers` 📋
Operator staff, separate from tenant users in `idn`.

---

### `idn.Users` ✅
| Column | Type | Rules |
|---|---|---|
| UserId | Guid | PK |
| Email | string(200) | Required, unique, email |
| PasswordHash | string(500) | Required. BCrypt work factor 12. Empty for invited users until they set one |
| DisplayName | string(200) | Required |
| MobileNumber | string(20)? | |
| EmailConfirmed | bool | |
| MobileConfirmed | bool | |
| TwoFactorEnabled | bool | |
| IsActive | bool | Default true |
| ThemePreference | string(10) | Default `System`. Light / Dark / System |
| FailedLoginCount | int | Lockout at 5 |
| LockedOutUntil | DateTimeOffset? | 15-minute lockout |
| LastLoginAt | DateTimeOffset? | |

### `idn.Roles` ✅
| Column | Type | Rules |
|---|---|---|
| RoleId | int | PK, identity |
| CustomerId | Guid? | **Null = built-in system role**; set = customer-defined |
| Name | string(100) | Required |
| Description | string(300)? | |
| IsSystemRole | bool | System roles are read-only |
| IsActive | bool | Default true |

Unique index: (CustomerId, Name)

**Seed**: 1 Owner, 2 Administrator, 3 Accountant, 4 Sales, 5 Viewer — all `IsSystemRole = true`, `CustomerId = null`

### `idn.Permissions` ✅
| Column | Type | Rules |
|---|---|---|
| PermissionId | int | PK, identity |
| Code | string(100) | Required, unique. Format `{module}.{action}` |
| Module | string(50) | Required |
| Description | string(200)? | |

**Seed**: 12 modules × 4 actions = 48 permissions.
Modules: dashboard, contacts, crm, inventory, sales, purchase, accounting, banking, reports, settings, support, platform
Actions: view, create, edit, delete

Role grants: Owner + Administrator → everything except `platform.*` · Viewer → all `.view` · Accountant → accounting, banking, reports, purchase · Sales → sales, contacts, crm

### `idn.RolePermissions` ✅
| Column | Type | Rules |
|---|---|---|
| RolePermissionId | long | PK, identity |
| RoleId | int | Required, FK |
| PermissionId | int | Required, FK |

Unique index: (RoleId, PermissionId)

### `idn.UserOrganizationRoles` ✅
**The pivot that makes multi-org access work.** One login, different roles per organization.

| Column | Type | Rules |
|---|---|---|
| UserOrganizationRoleId | long | PK, identity |
| UserId | Guid | Required, FK → Users |
| OrgId | Guid | Required (no FK — Organizations owned by Platform service) |
| RoleId | int | Required, FK → Roles |
| IsActive | bool | Default true. Revoke by setting false, don't delete |

Unique index: (UserId, OrgId, RoleId)

### `idn.RefreshTokens` ✅
| Column | Type | Rules |
|---|---|---|
| RefreshTokenId | long | PK, identity |
| UserId | Guid | Required, FK |
| TokenHash | string(500) | Required, indexed. **SHA-256 — never plaintext** |
| ExpiresAt | DateTimeOffset | Required. 7 days |
| RevokedAt | DateTimeOffset? | Set on rotation, logout, or password reset |
| IpAddress | string(45)? | |
| UserAgent | string(300)? | |

### `idn.LoginHistories` ✅
| Column | Type | Rules |
|---|---|---|
| LoginHistoryId | long | PK, identity |
| UserId | Guid | Required, FK |
| OrgId | Guid? | |
| LoginAt | DateTimeOffset | Required |
| IsSuccessful | bool | |
| FailureReason | string(200)? | |
| IpAddress | string(45)? | |
| UserAgent | string(300)? | |

### `idn.PasswordResetTokens` ✅
Also used for **user invitations** — same mechanism, longer expiry.

| Column | Type | Rules |
|---|---|---|
| PasswordResetTokenId | long | PK, identity |
| UserId | Guid | Required, FK |
| TokenHash | string(500) | Required, indexed |
| ExpiresAt | DateTimeOffset | Required. 1 hour for reset, 7 days for invitation |
| UsedAt | DateTimeOffset? | Single-use |

### `rat.CurrencyRates` 📋 / `rat.MetalRates` 📋
Dated history, not just today's rate. Manual override always available.

---

## PER-CUSTOMER DATABASE

Every table below needs `OrgId` (Guid, required) + EF Core global query filter + Postgres RLS policy.

### `acc.AccountTypes` 🔨
Reference data, **no `OrgId`** — identical for every organization.

| Column | Type | Rules |
|---|---|---|
| AccountTypeId | int | PK, not identity |
| Name | string(20) | Required, unique |
| NormalBalance | enum | Debit / Credit |
| ReportSection | enum | BalanceSheet / ProfitAndLoss |
| SortOrder | int | |

**Seed**: 1 Asset/Debit/BalanceSheet · 2 Liability/Credit/BalanceSheet · 3 Equity/Credit/BalanceSheet · 4 Income/Credit/ProfitAndLoss · 5 Expense/Debit/ProfitAndLoss

### `acc.AccountSubTypes` 🔨
Reference data, **no `OrgId`**.

| Column | Type | Rules |
|---|---|---|
| AccountSubTypeId | int | PK, not identity |
| AccountTypeId | int | Required, FK |
| Name | string(50) | Required |
| IsContra | bool | Normal balance opposite its type — reports subtract |
| SortOrder | int | |

Unique index: (AccountTypeId, Name)

**Seed**:
- Asset: Cash, Bank, Accounts Receivable, Inventory, Prepaid Expense, Advance to Vendor, Other Current Asset, Fixed Asset, Accumulated Depreciation *(contra)*, Input GST
- Liability: Accounts Payable, Credit Card, Advance from Customer, Output GST, TDS Payable, Other Current Liability, Long-term Liability
- Equity: Capital, Drawings, Retained Earnings, Opening Balance Equity
- Income: Operating Revenue, Sales Returns *(contra)*, Discount Given *(contra)*, Other Income
- Expense: Cost of Goods Sold, Purchase Returns *(contra)*, Operating Expense, Payroll Expense, Rent, Depreciation, Other Expense

### `acc.Accounts` 🔨
The Chart of Accounts. Seeded **per organization** at org creation.

| Column | Type | Rules |
|---|---|---|
| AccountId | long | PK, identity |
| OrgId | Guid | Required |
| AccountTypeId | int | Required, FK. **Denormalized — always derive from subtype on write** |
| AccountSubTypeId | int | Required, FK |
| AccountCode | string(20) | Required |
| AccountName | string(200) | Required |
| ParentAccountId | long? | Self-FK |
| CurrencyCode | string(3)? | Null = org base currency |
| IsSystemDefault | bool | Seeded control accounts — cannot be deleted |
| IsActive | bool | Default true |

Unique index: (OrgId, AccountCode)

**Seed at org creation**: Accounts Receivable, Accounts Payable, Inventory, Input GST, Output GST, Sales Revenue, Cost of Goods Sold, Realized FX Gain/Loss, Unrealized FX Gain/Loss, Opening Balance Equity — all `IsSystemDefault = true`

### `acc.SubAccounts` 🔨
Per-contact and per-item detail under a parent control account. Keeps the CoA small.

| Column | Type | Rules |
|---|---|---|
| SubAccountId | long | PK, identity |
| OrgId | Guid | Required |
| AccountTypeId | int | Required, FK. **Denormalized from parent Account** |
| AccountSubTypeId | int | Required, FK. **Denormalized from parent Account** |
| AccountId | long | Required, FK → Accounts |
| ReferenceType | enum→string(20) | Contact / Item |
| ReferenceId | long | Polymorphic pointer, no FK |
| SubAccountName | string(200) | Required |
| IsActive | bool | Default true |

Unique index: (AccountId, ReferenceType, ReferenceId)

**Auto-created**: each Contact → 2 (Accounts Receivable, Accounts Payable). Each Item → 3 (Inventory, Cost of Goods Sold, Sales Revenue).

### `acc.TaxMasters` 🔨
| Column | Type | Rules |
|---|---|---|
| TaxRateId | long | PK, identity |
| OrgId | Guid | Required |
| TaxName | string(50) | Required |
| TotalRate | decimal(5,2) | Required |
| CgstRate | decimal(5,2) | Required. Check: `CgstRate = SgstRate` |
| SgstRate | decimal(5,2) | Required. Check: `CgstRate + SgstRate = TotalRate` |
| IgstRate | decimal(5,2) | Required. Check: `IgstRate = TotalRate` |
| CessRate | decimal(5,2) | Default 0 |
| EffectiveFrom | DateOnly | Required |
| EffectiveTo | DateOnly? | Null = currently in effect |
| IsActive | bool | Default true |

**Seed at org creation**: GST 0% · 5% (2.5+2.5) · 12% (6+6) · 18% (9+9) · 28% (14+14) · **3% Bullion (1.5+1.5)**

### `acc.Journals` 🔨
Manual journal header.

| Column | Type | Rules |
|---|---|---|
| JournalId | long | PK, identity |
| OrgId | Guid | Required |
| JournalNo | string(30) | Required |
| JournalDate | DateOnly | Required |
| CurrencyCode | string(3) | Required |
| ExchangeRate | decimal(18,8) | Default 1. **Snapshot at JournalDate — never live** |
| Reference | string(200)? | |
| Memo | string? | Unbounded text |
| TransactionTypeId | int | Required → `mst.TransactionTypes`, no FK. `09` when hand-written, else the source document's type |
| SourceId | long? | Source document header, polymorphic, no FK |
| Status | enum→string(10) | Draft / Posted / Reversed |
| PostedAt | DateTimeOffset? | |
| PostedBy | Guid? | |
| ReversesJournalId | long? | Self-FK. Set on the **reversing** journal |
| ReversedByJournalId | long? | Self-FK. Set on the **reversed** journal |

Unique index: (OrgId, JournalNo) · Indexes: (OrgId, JournalDate), (OrgId, TransactionTypeId, SourceId)

### `acc.JournalDetails` 🔨
Journal lines. Debit and credit are mutually exclusive per line.

| Column | Type | Rules |
|---|---|---|
| JournalDetailId | long | PK, identity |
| JournalId | long | Required, FK, cascade delete |
| LineNumber | int | Required |
| AccountId | long | Required, FK → Accounts |
| SubAccountId | long? | FK → SubAccounts. Null for bank/GST/equity lines |
| DebitAmount | decimal(18,2) | Default 0 |
| CreditAmount | decimal(18,2) | Default 0 |
| DebitAmountBase | decimal(18,2) | Default 0 |
| CreditAmountBase | decimal(18,2) | Default 0 |
| BranchId | long? | **Reporting dimension only** |
| LineMemo | string(300)? | |
| ReversesJournalDetailId | long? | Self-FK. The original line this row reverses |
| ReversedByJournalDetailId | long? | Self-FK. The line that reversed this row |

Unique index: (JournalId, LineNumber) · Indexes: (ReversesJournalDetailId)

**Check constraints**:
- `chk_debit_credit_exclusive`: `(DebitAmount > 0 AND CreditAmount = 0) OR (CreditAmount > 0 AND DebitAmount = 0)`
- `chk_amounts_non_negative`: all four amounts ≥ 0

**No `OrgId`** — scoped via parent Journal.

**Deferred constraint trigger** (raw SQL in migration, no LINQ equivalent): on insert/update/delete, if parent status is `Posted`, sum(DebitAmountBase) must equal sum(CreditAmountBase). `DEFERRABLE INITIALLY DEFERRED` so multi-line inserts don't trip on intermediate state.

**Reversal is line-paired, not just header-paired.** `Journals` links the two documents; `JournalDetails` links each individual line to the line it offsets. Without the detail-level pair, a partially reversed journal cannot be told apart from a fully reversed one, and a reversal that omits a line still balances — so nothing catches it.

### `acc.JournalLedger` 🔨
**The single posting target.** Every financial document in the system — invoice, bill, payment, refund, journal, opening balance, depreciation, stock adjustment — writes its double-entry legs here and nowhere else. This is what reports read.

| Column | Type | Rules |
|---|---|---|
| LedgerId | long | PK, identity |
| OrgId | Guid | Required |
| LedgerDate | DateOnly | Required. Posting date |
| AccountId | long | Required, FK → Accounts. The GL account being hit |
| SubAccountId | long? | FK → SubAccounts. Set only for AP, AR and Inventory legs |
| TransactionTypeId | int | Required → `mst.TransactionTypes`, no FK |
| TransactionId | long | Required. Source document header |
| TransactionDetailId | long | Required, default 0. Source document line; `0` when the leg is not line-level |
| DebitAmount | decimal(18,2) | Default 0. Transaction currency |
| CreditAmount | decimal(18,2) | Default 0. Transaction currency |
| DebitAmountBase | decimal(18,2) | Default 0. `ROUND(DebitAmount / ExchangeRate, 2)` |
| CreditAmountBase | decimal(18,2) | Default 0. `ROUND(CreditAmount / ExchangeRate, 2)` |
| CurrencyCode | string(3) | Required |
| ExchangeRate | decimal(18,8) | Default 1. **Snapshot at LedgerDate — never live** |
| TaxExchangeRate | decimal(18,8)? | Tax may settle at a different rate |
| ContactId | long? | Customer or vendor. No FK — owned by Contacts |
| LedgerTypeId | int | Required → `mst.LedgerTypes`. Which leg this is |
| LedgerSourceId | int | Required → `mst.LedgerSources`. What produced it |
| SourceDocumentId | long? | Provenance |
| TransactionDesc | string(500)? | Description shown in the ledger |
| MappingTransactionId | long? | **Links a payment back to its document** |
| MappingTransactionTypeId | int? | **Type of the mapped document** |
| BranchId | long? | **Reporting dimension only** |
| JournalId | long? | Set when `LedgerSourceId = 4` (Journal) |

Indexes: (OrgId, LedgerDate) · (OrgId, AccountId, LedgerDate) · (OrgId, TransactionTypeId, TransactionId) · (OrgId, MappingTransactionTypeId, MappingTransactionId) · (OrgId, ContactId) · (OrgId, SubAccountId)

**Check constraints**: same two as `JournalDetails` — debit/credit exclusive, all four amounts ≥ 0.

**Deferred constraint trigger**: sum(DebitAmountBase) = sum(CreditAmountBase) per (`OrgId`, `TransactionTypeId`, `TransactionId`). `DEFERRABLE INITIALLY DEFERRED`.

#### Document posting

One row per leg, all under the document's own `TransactionTypeId`:

| Leg | Account | LedgerTypeId |
|---|---|---|
| Line item | Item's GL account | 1 `ITEM` |
| Tax | Tax GL account | 2 `TAX` |
| AP / AR control | Accounts Payable or Accounts Receivable | 3 `CONTROL` |
| COGS + Inventory | COGS and Inventory accounts | 4 `COGS` |

Posted documents only — a draft or void document writes nothing.

#### Payment posting and the mapping pair

A payment posts under its **own** identity (`10` Spend Money for a bill payment, `11` Receive Money for an invoice receipt) and points back at the document it settles:

| | Debit row | Credit row |
|---|---|---|
| `AccountId` | Accounts Payable — clears the liability | Bank or cash account |
| `TransactionTypeId` | `10` | `10` |
| `TransactionId` | the payment id | the payment id |
| `TransactionDetailId` | payment line if line-level, else `0` | `0` |
| `LedgerTypeId` | 3 `CONTROL` | 3 `CONTROL` |
| `LedgerSourceId` | 2 `PAYMENT`, or 6 `PREPAYMENT` | same |
| **`MappingTransactionId`** | **the bill's `TransactionId`** | same |
| **`MappingTransactionTypeId`** | **`02`** (Bill) | same |

That pairing is the whole mechanism for tracing a payment to its bill or invoice. It is also why payments never appear in stock tables — they carry no item dimension.

**Foreign-currency settlement** posts an extra pair to the Realized FX Gain/Loss account with `LedgerTypeId = 5`, mapped in the opposite direction (`MappingTransactionId` = the gain/loss source). Compute the gain or loss from the difference between the document's `ExchangeRate` and the payment's — never from a live rate.

**Idempotency.** Service Bus is at-least-once, so a consumer must dedup before inserting or a redelivered event doubles the ledger. Dedup on the source event id, and treat a document's ledger rows as a single atomic set — delete and re-post rather than patch.

### `acc.TransactionRatio` 🔨
Allocation between documents — a credit note applied across invoices, or a prepayment drawn down. Written alongside the ledger rows, never instead of them.

| Column | Type | Rules |
|---|---|---|
| TransactionRatioId | long | PK, identity |
| OrgId | Guid | Required |
| TransactionTypeId | int | Required. The allocating document, e.g. `07` Credit Note |
| TransactionId | long | Required |
| TransactionDetailId | long | Default 0 |
| MappingTransactionTypeId | int | Required. The target document, e.g. `06` Invoice |
| MappingTransactionId | long | Required |
| MappingTransactionDetailId | long | Default 0 |
| AllocatedAmount | decimal(18,2) | Required. Transaction currency |
| AllocatedAmountBase | decimal(18,2) | Required. Base currency |
| Ratio | decimal(9,6) | Proportion of the target line consumed |
| AllocationDate | DateOnly | Required |
| CurrencyCode | string(3) | Required |
| ExchangeRate | decimal(18,8) | Default 1 |

Indexes: (OrgId, TransactionTypeId, TransactionId) · (OrgId, MappingTransactionTypeId, MappingTransactionId)

Allocations must never exceed the target's outstanding balance. Enforce in C# — the sum spans rows, so no check constraint can express it.

### `acc.vw_LedgerDetail` — combined transaction view 🔨
One flattened read model over `JournalLedger`, joining the names that reports and the ledger screen need, so every transaction type is queried the same way regardless of which service wrote it.

Resolves: `mst.TransactionTypes` (code, name), `mst.LedgerTypes`, `mst.LedgerSources`, `acc.Accounts` (code, name, type, subtype), `acc.SubAccounts` (name), and the mapped document's transaction-type code. `ContactId` stays an id — Contacts is another service, so resolve names in C#, **batched**.

Adds `RunningBalanceBase`, computed as a window function over (`OrgId`, `AccountId`) ordered by `LedgerDate`, `LedgerId`.

Mapped as an EF Core **keyless entity**. Two things it must have:

- **`security_invoker = true`** on the view. Without it the view runs as its owner and **bypasses the RLS policies on `JournalLedger`**, which would leak the general ledger across organizations. This is not optional.
- The EF global query filter on `OrgId` applies to the keyless entity as well — belt and braces alongside RLS.

> **⚠ `CREATE VIEW` is not in `CLAUDE.md`'s raw-SQL exception list.** That list is `CREATE DATABASE`, RLS policies, triggers and `set_config`. A view needs raw SQL in a migration, so either the list grows to include views, or this becomes a LINQ projection in the Reporting service instead of a database object. Decide before implementing.

### Not yet designed 📋
`acc.FixedAssets`, `acc.FixedAssetCategories`, `acc.DepreciationSchedules` · `con.*` Contacts · `crm.*` · `inv.*` · `sal.*` · `pur.*` · `bnk.*` · `sup.*` · `rpt.*` · `ntf.*` · `aud.AuditLog`

---

# PART 2 — PAGES

## Shell (all apps) 🔨
`libs/app-shell`

- **Desktop (≥768px)**: left icon rail — all primary nav items + "More" overflow, org switcher and avatar in top bar
- **Mobile (<768px)**: bottom tab bar — top 4 items + "More" sheet
- Same nav model both ways; breakpoint = Angular CDK handset
- Theme toggle: Light / Dark / System, persisted to `idn.Users.ThemePreference`

**Every page must work at ~360px**: grids → card lists, multi-column forms → single column, modals → full-screen sheets.

---

## Auth pages (`apps/web`, `apps/portal`) 🔨

### Login
`POST /api/auth/login` → email + password. On success shows the org list; if only one org, auto-selects it.
- Errors: invalid credentials (generic message — never say which field), account locked (show unlock time), no org access
- Link to Forgot password

### Organization selector
`POST /api/auth/select-organization` with `X-PreAuth-Token` header → access + refresh token.
- Shows org name and the user's role in each
- Skipped when the user has exactly one org

### Forgot password
`POST /api/auth/forgot-password` → **always shows the same confirmation**, even for unknown emails.

### Reset password
`POST /api/auth/reset-password` with token from the email link. Min 8 chars, confirm field. On success all sessions are revoked — redirect to login.

### Accept invitation
Same endpoint as reset-password. Invited users have an empty `PasswordHash` until this completes.

---

## Trial signup (`apps/web`, public) 🔨
`POST /api/customers/signup`

Fields: CompanyName, OrganizationName, DisplayName, Email, Password, CountryId (dropdown from `/api/master/countries`), StateId (dependent dropdown), BaseCurrency (defaults from country).

**After submit**: shows a "setting up your account" state and polls `GET /api/customers/{id}/status` until `CanLogin = true`. Provisioning creates a physical database — this is eventually consistent and login must be blocked until ready.

---

## User management (`apps/web` → Settings) 🔨
- **List**: `GET /api/users` — scoped to current org. Columns: DisplayName, Email, Role, LastLoginAt, status. Mobile → card list.
- **Add**: `POST /api/users` — Email, DisplayName, MobileNumber, RoleId. **Sends an invitation link; never a temporary password.**
- **Revoke**: `DELETE /api/users/{id}` — sets `IsActive = false` on the org assignment. Cannot revoke yourself.

## Role master (`apps/web` → Settings) 🔨
- **List**: `GET /api/roles` — system roles + this customer's own. Show user count per role.
- **Create/Edit**: `POST` / `PUT /api/roles` — Name, Description, permission checkbox matrix grouped by module (`GET /api/roles/permissions`).
- **System roles are read-only** — show but disable editing.
- **Delete**: soft delete. Blocked (409) if assigned to any active user.

## Organization settings (`apps/web` → Settings) 📋
Tabs: Profile (name, logo upload, address, contact) · Statutory (GSTIN, PAN, TAN, TIN, CIN, Udyam) · Financial (base currency, FY start month, AP/AR due days, discount type) · Preferences (theme).

Validate `StateId`'s code matches GSTIN's first two digits.

## Chart of accounts (`apps/web` → Accounting) 📋
- Tree view grouped by AccountType → AccountSubType, with a flat searchable list toggle
- Create/edit: AccountCode, AccountName, AccountSubTypeId (grouped dropdown), ParentAccountId, CurrencyCode
- **AccountTypeId is derived from the selected subtype — never a separate input**
- `IsSystemDefault` accounts cannot be deleted; deactivate instead
- Mobile: accordion by type

## Tax master (`apps/web` → Settings) 📋
- List: TaxName, TotalRate, CGST/SGST split, IGST, EffectiveFrom/To, active
- Create/edit: enter TotalRate → **CGST and SGST auto-fill as half each, IGST as the full rate**
- Effective-dated: editing a rate creates a new row and expires the old one rather than overwriting

## Journal entry (`apps/web` → Accounting) 📋
- Header: JournalNo (auto), JournalDate, CurrencyCode, ExchangeRate (auto from rate table at JournalDate, overridable), Reference, Memo
- Line grid: Account, SubAccount (optional), Debit, Credit, Branch, Memo
- **Running debit/credit totals with a difference indicator — Post disabled until balanced**
- Debit and credit are mutually exclusive per line: entering one clears the other
- Posted entries are read-only, with a Reverse action
- Mobile: line-per-card, not a horizontal-scrolling grid

## Ledger (`apps/web` → Accounting) 📋
Reads `acc.vw_LedgerDetail`. One screen for every transaction type, because they all post to the same table.

- Filters: date range, account, sub-account, contact, transaction type, ledger source, branch
- Columns: LedgerDate, TransactionType code, document number, description, contact, debit, credit, running balance
- **Currency toggle**: transaction currency or base currency — the view carries both
- Drill-through: a row opens its source document, resolved from `TransactionTypeId` + `TransactionId`
- **Payment rows show what they settle**, resolved through `MappingTransactionTypeId` + `MappingTransactionId` — a Spend Money row links back to its bill
- Reversals show paired against the original, from the journal reversal mapping
- Mobile: card list with debit/credit and running balance; filters in a full-screen sheet

## Platform admin (`apps/admin`) 📋
Customer list with provisioning status · Organization list per customer · API client management (secret shown once at creation) · Provisioning progress and failure retry.

---

# PART 3 — BUILD ORDER

1. **Fix the blockers first** — `AuthController.ResolveCustomerIdAsync` returns null, so login cannot complete. Implement `ISecretStore`, `IEventPublisher`, `IEmailSender` (or register no-op stubs so DI resolves). Then get a first successful `dotnet build` — this code has never been compiled.
2. **Accounting service** — the four chart-of-accounts tables, TaxMasters, Journals + JournalDetails, then `JournalLedger` and the combined view, with seed data. The three `mst` master tables (TransactionTypes, LedgerTypes, LedgerSources) come first — everything downstream stores their ids
3. **Contacts service** — needed before Sales/Purchase can reference anyone
4. **Inventory service** — UOM, warehouses, items, batch tracking, shared stock pool
5. **Sales / Purchase** — document chains, both publishing events that Accounting consumes
6. **Banking, CRM, Support, Reporting**
7. **Background workers** — Notification, CostingEngine, RateSync
8. **Gateway** (YARP), then the Angular workspace

Frontend can start in parallel once Identity's endpoints are working — the shell, auth pages, and signup only need Identity and Platform.
