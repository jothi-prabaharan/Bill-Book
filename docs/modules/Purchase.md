# PURCHASE.md — the `pur` module, end to end

Everything needed to build Purchase: the document chain, every table and column, every decision taken and why, and the task list.

**This file is the single home for `pur.*`.** `SPEC.md` points here rather than repeating the columns, and `TRANSACTIONS.md` points here rather than repeating the tasks. `CLAUDE.md` still holds the conventions that apply to everything.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** See *Git — how work reaches main* in `CLAUDE.md`.

**Status: schema and the purchase order built and verified.** T4.1, T4.2 and T4.3 are done — the twelve `pur` tables with RLS, and the purchase order end to end: API, pages, Gateway route and twelve tests against a real PostgreSQL 16. The goods receipt (T4.4), the bill (T4.5) and the debit note (T5.3) are still designed and uncoded.

Stock receipt, cost layers, batch and serial capture and backdated recosting remain **built and never called by a document** — so every receipt today still lands as an opening balance. The schema landing does not change that; T4.4 does.

---

## 1. The chain

```
POR ──▶ GRN ──▶ BIL ──▶ SPM
order   receipt  bill    payment

        DBN = the way back out
```

**Every arrow is optional, and the shortcuts are the common cases.** A bill entered directly is the most common entry point — a service, or a trader who never raises a receipt.

| Code | Document | Posts | Stock |
|---|---|---|---|
| `POR` | Purchase order | no | **nothing** |
| `GRN` | Goods receipt | yes | **receives** |
| `BIL` | Bill | yes | receives, only if no GRN preceded it |
| `DBN` | Debit note | yes | returns |

`SPM` (spend money) is Banking's, and is already built.

### The five ways this is not a mirror of sales

Copying the sales service and renaming it gets all five wrong.

| | Sales | Purchase |
|---|---|---|
| Order touches stock? | reserves it | **nothing** — it is not there yet |
| Stock moves on | the delivery challan | the **receipt**, which usually precedes the bill |
| Clearing account | Goods Delivered Not Invoiced | **Goods Received Not Invoiced** |
| Tax side | Output GST, a liability | Input GST, an **asset** — reclaimable |
| Line kinds | one, effectively | **three** — stock, expense, capital |

---

## 2. The twelve tables

| Document | Header | Lines | Tax rows |
|---|---|---|---|
| `POR` | `pur.PurchaseOrders` | `pur.PurchaseOrderDetails` | `pur.PurchaseOrderDetailTaxes` |
| `GRN` | `pur.GoodsReceipts` | `pur.GoodsReceiptDetails` | `pur.GoodsReceiptDetailTaxes` |
| `BIL` | `pur.Bills` | `pur.BillDetails` | `pur.BillDetailTaxes` |
| `DBN` | `pur.DebitNotes` | `pur.DebitNoteDetails` | `pur.DebitNoteDetailTaxes` |

**Same three base classes as Sales** — `DocumentHeaderBase`, `DocumentLineBase`, `DocumentLineTaxBase` in `Shared.Kernel`, inherited not copied. They are built by `SALES.md` T2.2, so this module reuses rather than redefines them.

```
AuditableEntity                    CreatedBy · CreatedAt · ModifiedBy · ModifiedAt · xmin
  └─ OrgScopedEntity               OrgId
       ├─ DocumentHeaderBase
       ├─ DocumentLineBase
       └─ DocumentLineTaxBase
```

---

## 3. Header columns

**Identical to Sales** — see [`SALES.md` §3](./SALES.md) for the full list. `ContactId` is the vendor. Only the extras differ.

### Per-table header extras

| Table | Adds |
|---|---|
| `PurchaseOrders` | `ExpectedDate`, `FulfilmentStatus` (Open / PartlyReceived / Closed / Cancelled) |
| `GoodsReceipts` | `PurchaseOrderId?`, `VendorDeliveryNoteNo?`, `VendorDeliveryNoteDate?`, `ReceivedBy` |
| `Bills` | `PurchaseOrderId?`, `GoodsReceiptId?`, **`VendorBillNo` required**, **`VendorBillDate` required**, `PaymentTermId`, `DueDate` required, `LandedCostAmount` |
| `DebitNotes` | `BillId` **required**, `ReasonCode` |

### `VendorBillNo` — the column with no sales equivalent, and the one most likely to be missed

On a sale **we** issue the number. On a purchase the **vendor** does, and input tax credit is claimed against *theirs*. GSTR-2B reconciles on it. So a posted bill carries two numbers that mean different things:

- `DocumentNo` — ours, for internal reference, allocated at creation like every other document
- `VendorBillNo` + `VendorBillDate` — theirs, statutory

**Unique index `(OrgId, ContactId, VendorBillNo, financial year)`** — one vendor cannot bill the same number twice in a year. Catching that at entry is what stops a duplicate ITC claim.

---

## 4. Line columns

**Identical to Sales** — see [`SALES.md` §4](./SALES.md). Only the extras differ.

### Per-table line extras

| Table | Adds |
|---|---|
| `PurchaseOrderDetails` | `ReceivedQuantity`, `BilledQuantity` |
| `GoodsReceiptDetails` | `PurchaseOrderDetailId?`, `AcceptedQuantity`, `RejectedQuantity`, `RejectionReason?` |
| `BillDetails` | `GoodsReceiptDetailId?`, `PurchaseOrderDetailId?`, `ApportionedLandedCost`, `ReturnedQuantity` |
| `DebitNoteDetails` | `BillDetailId` **required** — so stock returns to its original cost layer |

**Only the accepted quantity becomes stock.** `chk_grn_accepted` — `AcceptedQuantity + RejectedQuantity = Quantity`, and a rejection needs a reason.

### `LineType` does real work here

`Stock` / `Expense` / `Capital` lives on `DocumentLineBase`, but purchase is where all three are used:

| Line | Posts to |
|---|---|
| Stock | Inventory, or clears GRNI |
| Expense | the named `AccountId` |
| **Capital** | the category's **Fixed Asset** account, **and creates the register row** |

**A capital line is how every purchased fixed asset gets onto the books.** Nothing else does it. A register filled in by hand would disagree with its control account from the first entry. See `TRANSACTIONS-ACCOUNTING-BANKING.md` T10.2.

---

## 5. Tax rows

**Identical to Sales** — see [`SALES.md` §5](./SALES.md). One difference in meaning, none in shape: these are **Input** GST, an asset, reclaimable — where the sales side is Output GST, a liability.

A vendor who is composition-scheme or unregistered charges no GST and the bill must not claim any. That is a property of the contact, read at the bill.

---

## 6. What each document posts

| Document | Debit | Credit |
|---|---|---|
| `GRN` | Inventory | **Goods Received Not Invoiced** |
| `BIL` against a receipt | GRNI, + Input GST | Accounts Payable |
| `BIL` with no receipt | Inventory / Expense / Fixed Asset, + Input GST | Accounts Payable |
| `DBN` | Accounts Payable | **Purchase Returns** (contra Expense), Input GST reversed |

GRNI is a clearing account: the receipt opens the obligation, the bill closes it. A balance sitting in it is goods held and not yet billed — a number a controller actually wants.

This does **not** change `StockLedgerMapping`, which already refuses to post a receipt carrying a source document on the grounds that Purchase will post it. What moves is *when* Purchase posts: at the receipt rather than only at the bill.

---

## 7. Decisions already taken

Everything in [`SALES.md` §8](./SALES.md) applies here too — the base classes, the numbering rule, the four statuses, names read from masters, tax as rows, `decimal(28,2)`, the three organization settings. Plus:

| Decision | Why |
|---|---|
| A table pair per document type | Same as Sales — receipt and bill links become real foreign keys |
| `VendorBillNo` alongside `DocumentNo` | ITC is claimed against the vendor's number; the unique index refuses a duplicate claim at entry |
| Only accepted quantity becomes stock | A rejected delivery is not inventory |
| `Capital` lines create the fixed asset register row | The register must tie to its control account from the first entry |

---

## 8. Open — answer before the stage that needs them

- ~~**Goods received not invoiced.**~~ **Settled — T4.1 answered yes.** `acc.Accounts` code **2150**, `GoodsReceivedNotInvoiced`, a Liability, seeded off the manual-journal picker for the same reason AR and AP are: it is cleared by the bill that matches the receipt, and a hand posting leaves a residue no document can clear. Postings are as §6 shows. The seed is idempotent per account, which was checked rather than assumed — `AccountService` filters the seed by both `AccountSystemName` and `AccountCode` against what the branch already has — so existing branches pick it up by re-running it. Nothing posts to it yet; T4.4 is what makes it move.
- **Landed cost apportionment.** `LandedCostAmount` on the bill and `ApportionedLandedCost` on the line hold it; whether it is spread by value, weight or quantity is not decided.
- **Purchase price variance.** A receipt opens a cost layer at the order's price; the bill may disagree, after sales have already drawn on that layer. Either **revalue the layer** and let the recosting engine restate those sales, or **post the difference to a variance account** and accept a slightly untrue margin. The recosting machinery already existing tilts this toward revaluation — the expensive half is built.
- **`pur.PurchaseRegister`** — the counterpart to `sal.SalesRegister`, same grain, for ITC claims and GSTR-2B reconciliation against what the vendor filed. Not designed.

---

## 9. Tasks

Numbering follows `TRANSACTIONS.md`.

### Blocked on foundations

**T0.2** tax determination · **T0.3** numbering series · **T0.4** the lifecycle — all in `TRANSACTIONS.md`. And **`SALES.md` T2.2**, which builds the three base classes this module inherits.

### T4 — order, receipt, bill

- [x] **T4.1 — Decide goods-received-not-invoiced.** Answered **yes**; see §8. Account 2150 is in the chart-of-accounts seed. No longer blocks T4.4 or T4.5.
- [x] **T4.2 — `pur.*` schema, entities and migration.** Twelve tables on T2.2's three base classes, with `OrgId` on every one, query filters, RLS and the document series.
  *Done when*: `migrations add` produces an empty migration, RLS policies are in the database, and a second bill carrying a vendor number already used that year is refused.

  **All three checked against PostgreSQL 16, not asserted.** The second `migrations add` came back empty; all twelve tables report `rowsecurity = t` with one policy each; and a duplicate `(vendor, number, year)` is refused by `IX_Bills_VendorBillNo` while the same number in the next financial year is accepted. The RLS policy was exercised as a non-owner role, since RLS does not apply to the table owner — unset org sees 0 rows, the owning org sees its own, another org sees 0.

  Four things worth carrying forward:

  - **The Fluent configuration is shared, not copied.** `Shared.Kernel.Documents.DocumentModelConfiguration` holds the three header/line/tax helpers that were private to `SalesDbContext`; both contexts now call them, so all twenty-seven document tables get their precision, indexes and constraints from one place. Verified schema-neutral for Sales: a probe migration after the refactor came back empty and the `sal` snapshot is unchanged. "Purchase follows the same schema as sales" is now generated rather than asserted.
  - **`Bills.VendorBillFinancialYear` is a stored computed column**, because the unique index needs a year and a vendor legitimately reuses a number after April. Postgres derives it from `VendorBillDate`; C# never writes it. **April is hardcoded** where `Numbering:FinancialYearStartMonth` is configurable — the column exists for input tax credit and the GST year is statutorily April–March, so a branch keeping its books on a different year does not get a different GST year. The March-2027-is-still-FY-2026 boundary is covered.
  - **Parent-child relationships name their navigation.** `HasOne<X>().WithMany(h => h.Lines)`, not `WithMany()`. The empty form declares a *second* relationship beside the collection navigation and EF invents a shadow FK for it — see §11.
  - **`base.OnModelCreating` is called last**, as Inventory and Accounting do. Without it `TenantDbContext` never runs: no OrgId query filter, no OrgId index, and `Version` mapped as a plain `bigint` rather than the `xmin` system column. See §11.
- [x] **T4.3 — Purchase order: API and page.** No posting, **no reservation** — ordering stock does not reserve anything, it is not there yet.

  `PurchaseOrdersController` (list, get, create, update, approve, confirm, void), `PurchaseOrderService`, the `purchase-ui` list and form pages on the shared `bb-document-line-grid`, the Gateway route, and `Purchase.Api.Tests` — twelve tests against a real PostgreSQL 16, all passing.

  The service has **no `IInventoryClient` and no `ILedgerClient`**, and that absence is the task: a sales order has both, ordering from a vendor needs neither. What it does exercise is everything the later documents need — tax determination, numbering, the lifecycle, the batched name lookups — proved somewhere a mistake cannot reach the books.

  Five things worth carrying forward:

  - **`approve` and `confirm` are separate transitions.** `approve` is Draft → ReadyToPost, the review step; `confirm` is → Posted, meaning issued to the vendor. Both carry `[PermissionAction("approve")]`, so both need `purchase.approve` rather than the `purchase.edit` a bare POST would derive. There is no `purchase.post` seeded, and issuing is deliberately filed under approve: committing the company to a spend is the authority being exercised, not editing. `POR` is `IsLedgerPosting = false` in `mst.TransactionTypes`, so "Posted" here means issued and reaches no account.
  - **A draft leaves `PostedAt` null.** `SalesOrderService.CreateAsync` stamps `PostedAt` and `PostedBy` on a document it then saves as `Draft`, which its own `chk_salesorders_posted_stamp` constraint refuses — see §11. A test asserts the purchase side does not.
  - **Create and update share one `ApplyAsync`.** The sales equivalent writes the same hundred lines twice and the two copies have already drifted — only one of them re-resolves the place of supply. Two copies of the arithmetic behind a GST return is one copy that gets corrected and one that does not.
  - **The number is allocated last**, after every validation, so a refused request never spends one. A test asserts a refused order leaves the table empty.
  - **Place of supply matters more here than on the sales side.** A vendor who is unregistered or on the composition scheme has no GSTIN, so there is nothing to fall back to and the order is refused until a place of supply is stated. Correct — intra cannot be told from inter without one — but it makes that field load-bearing on the purchase form in a way it is not on an invoice, and the form says so.
- [ ] **T4.4 — Goods receipt: API and page.** Receives stock at the order's cost, opens the cost layer, posts per T4.1. Batch, expiry and serial capture belong here, in the request, because they are user input and belong in the answer to the caller rather than in a background failure.
  *Done when*: a receipt against an order opens a cost layer at the received cost, a partial receipt leaves the order partly open, and only the accepted quantity becomes stock.
- [ ] **T4.5 — Bill: API and page.** With or without a receipt, with Input GST legs and payment terms driving the due date.
  *Done when*: a bill against a receipt clears GRNI to the paise and moves no stock; a bill with no receipt does move stock; a capital line moves neither and lands on a Fixed Asset account; and payables aging ties to the Accounts Payable control account.

### T5 — debit note

- [ ] **T5.1 — `acc.TransactionRatio`** — shared with Sales, built once by whichever stage arrives first.
- [ ] **T5.3 — Debit note.** `Dr Accounts Payable / Cr Purchase Returns` (contra Expense), Input GST reversed, stock returned to its layers.
- [ ] **T5.4 — Allocation UI** — shared with Sales.

---

## 9a. UI — the same line grid as Sales

**`bb-document-line-grid`** in `libs/shared/ui-components` is **built** and serves purchase documents unchanged — the line shape is identical. Full description in [`SALES.md`](./SALES.md).

What purchase pages add *beside* it, not inside it:

| Page | Adds |
|---|---|
| Goods receipt | accepted / rejected quantity and the rejection reason |
| Bill | `VendorBillNo` and `VendorBillDate` on the header; `LineType` is already in the grid |
| Purchase order | expected date; **no reservation control** — an order reserves nothing |

**T4.3 wired it up first**, on the purchase order form, and it served the purchase side unchanged as predicted — no fork, no purchase-specific variant. The form converts between the grid's integer paise and the API's decimals in one place and sends no totals at all, because the server computes them.

The goods receipt (T4.4), the bill (T4.5) and the debit note (T5.3) each wire it up as they land, adding their own columns beside it rather than inside it.

---

## 10. Standing requirements

Not repeated per task. Full list in `TRANSACTIONS.md`.

Documentation in the same commit · `OrgId` + query filter + RLS on every table including details · `[Authorize]` and `RequireModulePermission` on every endpoint · postings idempotent on their document key · `ExchangeRate` a snapshot · `dotnet build && dotnet test` and `npm run check` green before a box is ticked · every page working at ~360px.

---

## 11. Four defects in `sal` that building `pur` found — **not fixed, and they should be**

Building this module against the sales one surfaced four problems in `sal`. None is Purchase's to fix, none is fixed here, and all four are live on `main`. They are written down because the next person to copy from Sales inherits all four.

**1. `sal` has no row-level security at all.** `Migrations/README-RowLevelSecurity.md` carries the block to paste and it was never pasted: `grep -c "ROW LEVEL SECURITY"` over `20260814075501_AddSalesRegister.cs` returns 0. Every sales document table is unprotected at the database level.

**2. `SalesDbContext` never calls `base.OnModelCreating`.** Inventory and Accounting both end their `OnModelCreating` with it; Sales does not, so `TenantDbContext` never runs over the `sal` model. The consequences: **no global `OrgId` query filter on any sales table**, no `OrgId` index, and `Version` mapped as an ordinary `bigint` instead of the `xmin` system column, so optimistic concurrency silently does nothing.

Taken together, 1 and 2 mean **`sal` currently has neither of the two isolation layers a per-customer schema is supposed to have.** CLAUDE.md calls a missing query filter "the highest-consequence mistake available here", and both defences are absent at once. The fix is one line in the context plus a migration carrying the RLS block; the migration is small, the verification is the `pg_policies` query in §T4.2.

**3. Ten shadow foreign-key columns.** `HasOne<Quote>().WithMany()` with no navigation argument declares a relationship *separate* from the `Lines` collection, so EF adds a second FK column for the navigation. `sal` therefore carries `QuoteId1`, `SalesOrderId1`, `DeliveryChallanId1`, `InvoiceId1`, `CreditNoteId1` and their five detail-tax equivalents — nullable, unindexed, and written instead of the intended column when a header is saved with its `Lines` populated. `pur` avoids it by naming the navigation.

**4. `SalesOrderService.CreateAsync` stamps a draft as posted.** It sets `PostedAt` and `PostedBy` and then saves the document with `Status = Draft`. The header check constraint is `(Status IN ('Posted','Void')) OR PostedAt IS NULL`, so that row cannot be inserted: creating a sales order should throw at the database every time. `PostedAt` being null is also what tells a void draft from a void posting, which is the reason there is no fifth status — so this is not only a failed insert but a lost distinction. Found by writing the equivalent purchase test, which asserts the opposite.

`pur` is clean on all four: RLS verified in the database, `base.OnModelCreating` called last, no `*Id1` column in the migration, and a test asserting a draft carries no posting stamp.

---
