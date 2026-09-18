# --- Master.md ---
# master.md — build order

The order to build things in, and how to tell when each one is actually done.

`CLAUDE.md` holds the conventions. [`SPEC.md`](./SPEC.md) holds the tables and pages. This file holds **what to do next**, one item at a time. [`TRANSACTIONS.md`](./TRANSACTIONS.md) and [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md) continue it for the sixteen document types — Stage 6 below, in their own files because together they are the larger half of the product.

## How to use this file

1. Take the **first unticked box**. The order is deliberate — later stages assume earlier ones.
2. Do it, and check it against its **Done when** line. That line is the test; "it compiles" is not the same as "it works".
3. Tick the box **in the same commit as the work**, the way release notes and docs already work here.
4. If a task turns out to be wrong or unnecessary, strike it and say why rather than deleting it. The reason is worth more than the tidiness.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** A branch invented mid-task splits the work across two places and leaves whichever one nobody merges behind. See *Git — how work reaches main* in `CLAUDE.md`.

---

## Where things stand

Verified on 23 August 2026, by reading the repository rather than from memory.

**Built** — Master, Accounting, Contacts, Inventory, Banking. 28 pages, and every endpoint behind an authentication and permission check.
**Consolidated** — Platform and Identity schemas have been fully folded into the Master module (`mst`). `AdminDbContext` handles all core configuration, licenses, SMTP settings, organization currencies, and tenant data. The `Platform.Api` and `Identity.Api` modules were merged into `Master.Api` to reduce service overhead.

**Both halves are verified now.** The backend builds with zero warnings under `TreatWarningsAsErrors`, its 110 tests pass, every EF snapshot matches its model, and all 33 migrations are applied to a real PostgreSQL — 24 in a customer database, 9 in the master. The frontend's `npm run check` runs lint, a typecheck, 41 tests and both builds, and is green. The SDK was never actually blocked — see 0.2.

**Nothing is blocked by tooling any more.** What is left is an owner's decision — 5.14, 5.16, 5.19. **4.4 is closed**: the account ledger and the trial balance are built (T0.6), so what is posted finally has somewhere to be read. Reserved quantity (5.13) and the stock-to-ledger posting (5.12) were both held for Sales and have been built ahead of it instead: each is a schema change plus a guard, and a schema change is the wrong thing to be doing in the same commit as a first screen.

**The transaction plan has started.** T0.1, T0.5, T0.6, T0.7, T1.1 and T1.2 are done — the ledger door now takes a whole document's legs in one call, the manual journal exists, and both ledger screens are in place. The next substantial build is **Sales**, the next thing on the Phase 1 roadmap; `sal.*` is still marked *not designed* in SPEC, so it starts with a schema, and it now arrives to a general ledger that accepts postings **and can be read**. Its own foundations — T0.2 (tax determination), T0.3 (its numbering series) and T0.4 (the lifecycle) — are still unbuilt and come first. See [`TRANSACTIONS.md`](./TRANSACTIONS.md).

---

## Stage 0 — Make the build real

Until this stage is finished, every claim about this repository is "written", not "works". Nothing below it can be trusted, so nothing below it should be started.


# --- Accounting.md ---
# TRANSACTIONS-ACCOUNTING-BANKING.md — the money documents

The six transaction documents owned by **Accounting** and **Banking**, split out of [`TRANSACTIONS.md`](./TRANSACTIONS.md), which keeps the ten owned by Sales, Purchase and Inventory.

`CLAUDE.md` holds the conventions. [`SPEC.md`](./SPEC.md) holds tables and pages. [`master.md`](./master.md) holds the build order up to here.

Same rules as `master.md`: take the first unticked box, check it against its **Done when** line, tick it in the same commit as the work, and strike a task rather than deleting it if it turns out to be wrong.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** A branch invented mid-task splits the work across two places and leaves whichever one nobody merges behind. See *Git — how work reaches main* in `CLAUDE.md`.

**Flow documents, beside this plan and not part of it**: [`Sales.md`](./Sales.md), [`Purchase.md`](./Purchase.md) and [`Inventory.md`](./Inventory.md) describe the trading side's runtime behaviour. No checkboxes.

**Stage numbers are the ones from `TRANSACTIONS.md` and have not been renumbered.** T1, T6, T8 and T10 live here; T0, T2, T3, T4, T5, T7 and T9 live there. The gaps in each file are the point — they say where the missing stage went, and every cross-reference written before the split still resolves.

---

## Scope — the six

| Code | Document | Owner | Posts | Moves stock | Stage |
|---|---|---|---|---|---|
| JRN | Manual journal | Accounting | yes | no | T1 |
| SPM | Spend money | Banking | yes | no | T6 |
| RCM | Receive money | Banking | yes | no | T6 |
| TRM | Transfer money | Banking | yes | no | T6 |
| OPB | Opening balance | Accounting | yes | receives | T8 |
| DEP | Depreciation | Accounting | yes | no | T10 |

**`DEP` is the only code T10 owns, and a depreciation run is the middle act of three.** Acquisition and disposal have no transaction type of their own and are not missing from `mst.TransactionTypes` by oversight — they ride on `BIL`, `OPB`, `JRN` and `INV`. T10.2 says which, and the decision table says why.

What these six have in common, and what makes them a coherent file rather than an arbitrary cut: **none of them sells or buys anything.** There is no item being traded, no price, no GST determination and no cost layer. A journal, a payment, a transfer, an opening balance and a depreciation run each move value between accounts that already exist. That is why they need almost none of the machinery `TRANSACTIONS.md` spends its first four foundations building — and why the manual journal is the right first document in the whole plan.

The exception is OPB, which receives stock. It is here because Accounting orchestrates it and it is a book-opening act, not a purchase.

---

## What this file depends on

These live in [`TRANSACTIONS.md`](./TRANSACTIONS.md) and are prerequisites here. They stayed there because Sales and Purchase need them too, and a shared prerequisite belongs with the shared prerequisites.

| | Needed by |
|---|---|
| **T0.1** — the ledger door takes one leg type per call, and a document has four | every stage here |
| **T0.3** — no document numbering series exist | T1, T6 |
| **T0.4** — one lifecycle for every type, and `.void` reachable by `.edit` | every stage here |
| **T0.6** — nothing displays a ledger, and everything writes to one | T1 above all — it is what makes a posting checkable |
| **T5.1** — `acc.TransactionRatio`, allocation between documents | T6 |
| **T4.5** — the bill, which is what a purchased fixed asset arrives on | T10.2 |
| **T3.1** — the invoice, if a disposal with proceeds posts under `INV` | T10.4, pending the T10.2 decision |

**T0.2**, tax determination, is the one foundation only partly needed here. Five of these six documents carry no GST at all — but a fixed asset bought on a bill does, since input credit on capital goods is claimable, and that GST is determined by T0.2 on the Purchase side rather than here.

### T5.1's read side and the settlement workspace — built 3 September 2026

The guard itself was built with Sales (see the ticked T5.1 in [`SALES.md`](./Sales.md)); what was added here is everything around it that a person needs to actually settle a document.

- `GET /api/allocations` — a page of allocations, newest first, optionally narrowed to one contact and excluding voided rows unless asked. The contact filter goes through the CONTROL legs, because the documents themselves live in Sales and Purchase and this service only knows who a document belongs to through its ledger.
- `GET /api/allocations/{id}` — one allocation with the target's live posted / allocated / available figures beside it. A cross-branch id is `Forbid()`, not `NotFound()`, so the id space cannot be used to probe what another branch holds.
- `GET /api/allocations/open-documents/{contactId}` — both sides of the workspace. **Split by the direction the CONTROL net runs rather than by document type**: a net running debit is something owed and belongs on the target side, one running credit is money held and belongs on the source side. That is what lets one endpoint serve a customer and a vendor, and stops a document type nobody thought of from falling off the screen.
- `POST /api/allocations/{id}/void` — releases one claim, `accounting.void` rather than `accounting.edit`, reason required.

**Allocations are now voided rather than deleted**, which changed the guard: every query that asks what a target still owes filters `IsVoided` out, or a released claim would go on occupying a balance nobody could then spend. Voiding a *source document* releases its claims the same way. The row staying is the point — what an invoice was settled against before a credit note was withdrawn stays answerable, the same reasoning that makes a document row a void rather than a delete.

**Settlement status is derived, never stored.** `Unallocated` / `PartiallyPaid` / `Paid` is computed from the ledger less live allocations at the moment it is asked for. It is deliberately *not* written back onto `sal.Invoices` or `pur.Bills`: Accounting cannot write another service's tables, and a stored flag is a second copy of the truth that drifts. It is also deliberately not added to `DocumentStatus` — that enum is the *lifecycle* (Draft / ReadyToPost / Posted / Void) and `DocumentLifecycle`'s rules fall through to "this document has been voided" for anything they do not recognise, so a `Paid` invoice would have reported itself as voided across all nine document types.

`TransactionRatio.Amount` also gained an explicit `decimal(18,2)`. It had no precision configured, so the column was unbounded `numeric` while every figure it is checked against is two decimals.

The screen is **Accounts › Settle documents** (`accounting/allocations/:contactId`): the two panels, the running arithmetic, and the status pills previewing where each document would land.

---

## Foundations owned here

Two of `TRANSACTIONS.md`'s original T0 items exist only for the manual journal, so they moved with it.


# --- Inventory.md ---
# Inventory Module

**Schema:** `inv`

## Overview
Handles stock levels, reservations, adjustments, and the physical count of inventory. Integrates with Accounting for inventory valuation (weighted average) and Sales/Purchase for stock movements.

## Task Checklist

# --- Customer.md ---
# Customer / Contacts Module

**A naming collision worth stating up front, found 24 August 2026.** This file's title and the section below it document `con` — Contacts, mapped by the **Master** service's `ContactsDbContext`, and built. That is a different thing from the **Customer** service (`Customer.Api`, schema `cus`, CRM + Support), which is unbuilt and is documented in its own section further down this file. `mst.Customer` is a third, unrelated thing again — the head office / tenant. Three names, three concepts. Read `con` below as Contacts; read `cus` further down as the Customer service.

**Schema:** `con`

## Overview
Manages customer and vendor profiles, contact roles, prepayments, and outstanding balances. Coordinates with the Accounting module's AR/AP subledgers.

## Task Checklist

# --- Sales.md ---
# SALES.md — the `sal` module, end to end

Everything needed to build Sales: the document chain, every table and column, every decision taken and why, and the task list.

**This file is the single home for `sal.*`.** `SPEC.md` points here rather than repeating the columns, and `TRANSACTIONS.md` points here rather than repeating the tasks. `CLAUDE.md` still holds the conventions that apply to everything.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** See *Git — how work reaches main* in `CLAUDE.md`.

**Status, 22 August 2026: the chain runs as far as the invoice, and T2.1, T2.2/T2.4 and T3.1/T3.2 are closed.** Quote, sales order and invoice are done as documents — raised, converted from the one upstream, confirmed or posted, voided, and in the order's case closed short. **Partial fulfilment is deferred to T3.6** and is the one thing to know before reading a ticked box here as "everything works": nothing writes `DeliveredQuantity` or `InvoicedQuantity` yet, so an order cannot be shipped or billed in part.

 The quote (T2.1/T2.3), the sales order (T2.2/T2.4) and the invoice (T3.1/T3.2) each have their schema, service, controller, list, form, conversion from the document upstream, docs and tests. The invoice posts the double entry and issues the stock. The delivery challan and the credit note have a controller and a scaffold page and **no verified path**.

**Three things that were blocking this file's own status text are now fixed**, and are worth knowing because each was invisible for the same reason — nothing had ever written to these tables:

- `SalesDbContext` shipped without `base.OnModelCreating`, so no `sal` table had an OrgId query filter, an OrgId index or `xmin` concurrency. RLS refused every cross-branch read throughout, so nothing leaked, but the first line of defence was absent everywhere
- **Every header-to-line relationship was mapped twice**, once correctly and once by a shadow key EF invented (`QuoteId1`, `SalesOrderId1`, eight more). Lines went into the shadow column and left the real `NOT NULL` one at zero, so **no sales document with lines could be saved at all**, in any of the five types. Fixed in `BindDocumentLineNavigations`
- **The ledger leg contract is fixed.** `LedgerClient` now sends `DebitAmount`/`CreditAmount` as Accounting requires. This is the shared line that four boxes below were blocked on

`sal` has its migrations, RLS included.

---

## 1. The chain

```
QTE ──▶ SOR ──▶ DLC ──▶ INV ──▶ RCM
quote   order   challan  invoice  receipt

        POS = INV + payment, one action
        CRN = the way back out
```

**Every arrow is optional.** An invoice raised directly is the common case in a shop.

| Code | Document | Posts | Stock |
|---|---|---|---|
| `QTE` | Quote | no | no |
| `SOR` | Sales order | no | **reserves** |
| `DLC` | Delivery challan | yes | **issues** |
| `INV` | Invoice | yes | issues, if no challan preceded it |
| `POS` | POS sale | yes | issues |
| `CRN` | Credit note | yes | returns |

`RCM` (receive money) is Banking's, and is already built. See `TRANSACTIONS-ACCOUNTING-BANKING.md`.

---

## 2. The ten tables

| Document | Header | Lines | Tax rows |
|---|---|---|---|
| `QTE` | `sal.Quotes` | `sal.QuoteDetails` | `sal.QuoteDetailTaxes` |
| `SOR` | `sal.SalesOrders` | `sal.SalesOrderDetails` | `sal.SalesOrderDetailTaxes` |
| `DLC` | `sal.DeliveryChallans` | `sal.DeliveryChallanDetails` | `sal.DeliveryChallanDetailTaxes` |
| `INV` · `POS` | `sal.Invoices` | `sal.InvoiceDetails` | `sal.InvoiceDetailTaxes` |
| `CRN` | `sal.CreditNotes` | `sal.CreditNoteDetails` | `sal.CreditNoteDetailTaxes` |

Fifteen tables, plus `sal.SalesRegister`. **A POS sale is an `Invoices` row** with `TransactionTypeCode = 'POS'` — POS is a screen, not a document type.

**Three base classes in `Shared.Kernel`, inherited not copied:**

```
AuditableEntity                    CreatedBy · CreatedAt · ModifiedBy · ModifiedAt · xmin
  └─ OrgScopedEntity               OrgId
       ├─ DocumentHeaderBase
       ├─ DocumentLineBase
       └─ DocumentLineTaxBase
```

The audit columns and `OrgId` arrive through that chain and are not repeated in the lists below.

---

## 3. Header columns

| Column | Type | Rules |
|---|---|---|
| {Doc}Id | long | PK, identity |
| TransactionTypeCode | string(3) | Fixed per table. `Invoices` holds `INV` or `POS` |
| DocumentNo | string(30) | **Required from creation.** Plain unique index |
| DocumentDate | DateOnly | The date every snapshot is taken at |
| ContactId | long | No FK — Contacts is another service |
| ContactGstin | string(15)? | **Snapshot.** Not a label — a document filed under one registration cannot later claim another |
| BillingAddress / ShippingAddress | string? | Snapshots. Where the goods actually went |
| PlaceOfSupplyStateId | int | Snapshot |
| IsInterState | bool | **Stored, not re-derived.** Decides whether lines carry CGST+SGST or IGST |
| CurrencyCode | string(3) | |
| ExchangeRate | decimal(18,8) | Snapshot at document date, never live |
| SubTotal / DiscountAmount / TaxableAmount | decimal(28,2) | Sums of the lines |
| CgstAmount / SgstAmount / IgstAmount / CessAmount | decimal(28,2) | Sums of the tax rows |
| RoundOffAmount | decimal(28,2) | **Signed** — the only amount that can be negative |
| TotalAmount / TotalAmountBase | decimal(28,2) | |
| Status | enum→string(12) | Draft / ReadyToPost / Posted / Void |
| PostedAt / PostedBy | DateTimeOffset? / Guid? | |
| VoidedAt / VoidedBy / VoidReason | DateTimeOffset? / Guid? / string(300)? | Reason **required** |
| Notes / TermsAndConditions | string? | |

**`ContactName` is deliberately absent** — read from Contacts when the document is read, so a corrected name shows everywhere including on documents already raised.

### Per-table header extras

| Table | Adds |
|---|---|
| `Quotes` | `ValidUntil` **required** |
| `SalesOrders` | `DeliveryDate`, `FulfilmentStatus` (Open / PartlyDelivered / Closed / Cancelled) |
| `DeliveryChallans` | `SalesOrderId?`, `ChallanType` (Sale / JobWork / Approval / BranchTransfer / Sample), `DispatchDate`, `VehicleNo?`, `TransporterName?`, `EwayBillNo?`, `EwayBillDate?` |
| `Invoices` | `PaymentTermId?`, `DueDate?`, `QuoteId?`, `SalesOrderId?`, `DeliveryChallanId?`<br>POS rows only: `TillId?`, `CashierUserId?`, `PaymentMode?`, `TenderedAmount?`, `ChangeAmount?` |
| `CreditNotes` | `InvoiceId` **required**, `ReasonCode` |

Every conversion link is a **real foreign key**, which is the main gain from a table per type.

### Header checks

- `PostedAt` set iff the document ever posted
- `VoidedAt` and `VoidReason` set iff `Void`, and set together
- `ExchangeRate > 0`
- All amounts ≥ 0 **except** `RoundOffAmount`
- `TotalAmount = TaxableAmount + Cgst + Sgst + Igst + Cess + RoundOff`
- On `Invoices`: type is `INV` or `POS`; POS needs a till and payment mode, INV needs a due date

---

## 4. Line columns

| Column | Type | Rules |
|---|---|---|
| {Doc}DetailId | long | PK, identity |
| {Doc}Id | long | FK, cascade delete |
| LineNumber | int | Unique within the document |
| ItemId | long? | **Nullable** — null makes it a free-text line. No FK |
| HsnSacCode | string(8)? | **Snapshot**, or typed directly on a free-text line |
| Description | string(500)? | **Required when `ItemId` is null** |
| WarehouseId | long? | Location only — never partitions stock |
| Quantity | decimal(18,6) | As entered |
| UomId | long? | Null on a free-text line |
| ConversionFactor | decimal(18,6) | Stored, not re-derived. Default 1 |
| BaseQuantity | decimal(18,6) | In the item's inventory unit |
| UnitPrice | decimal(28,6) | Per **entered** unit |
| IsPriceInclusive | bool | Inclusive back-computes `taxable = inclusive ÷ (1 + rate)` |
| DiscountPercent / DiscountAmount | decimal(9,6)? / decimal(28,2) | |
| GrossAmount / TaxableAmount | decimal(28,2) | Discount reduces the taxable value |
| TaxTreatment | enum→string(10) | **Taxable / ZeroRated / NilRated / Exempt / NonGst.** Snapshot of the item's `TaxPreference` |
| TaxMasterId / TaxGroupId | long? | Null unless Taxable or ZeroRated |
| TaxAmount | decimal(28,2) | Total only — the split is rows |
| LineType | enum→string(10) | **Stock / Expense / Capital.** Capital on an invoice is an asset disposal |
| AccountId | long? | The account a non-item line posts to. Required on a free-text or Expense line |
| FixedAssetCategoryId | long? | Required when Capital |
| LineTotal | decimal(28,2) | `TaxableAmount + TaxAmount` |
| ItemBatchId | long? | One lot per line |
| LineNotes | string(300)? | |

**`ItemCode` and `ItemName` are deliberately absent** — read from Inventory, same reason as the contact name.

### Per-table line extras

| Table | Adds |
|---|---|
| `QuoteDetails` | — |
| `SalesOrderDetails` | `ReservedQuantity`, `DeliveredQuantity` |
| `DeliveryChallanDetails` | `SalesOrderDetailId?`, `InvoicedQuantity` |
| `InvoiceDetails` | `SalesOrderDetailId?`, `ReturnedQuantity` |
| `CreditNoteDetails` | `InvoiceDetailId` **required** — so stock returns to its original cost layer |

### Line checks

- `Quantity > 0`
- `BaseQuantity = Quantity × ConversionFactor`
- `DiscountAmount <= GrossAmount`
- `LineTotal = TaxableAmount + TaxAmount`
- `ItemId IS NOT NULL OR Description IS NOT NULL`
- `ItemId IS NULL` ⇒ `AccountId` set and `LineType <> 'Stock'`
- Exempt / NilRated / NonGst ⇒ `TaxAmount = 0` and **no tax rows**; ZeroRated ⇒ rows at rate 0
- Expense ⇒ `AccountId`; Capital ⇒ `FixedAssetCategoryId`; Stock ⇒ `ItemId`

---

## 5. Tax rows

One child table per detail table. Grain is **(line, component)**.

| Column | Type | Rules |
|---|---|---|
| {Doc}DetailTaxId | long | PK, identity |
| {Doc}DetailId | long | FK, cascade delete |
| TaxComponent | enum→string(6) | **Cgst / Sgst / Igst / Cess** |
| SubAccountId | long | **The resolved GST sub-account.** What the `TAX` ledger leg posts against |
| Rate | decimal(9,4) | Snapshot at document date |
| TaxableAmount / Amount / AmountBase | decimal(28,2) | |

Unique on `({Doc}DetailId, TaxComponent)`. CGST and SGST may not sit on the same line as IGST.

**Why rows and not columns:** intra-state is two components, inter-state is one, and cess is a third — a fixed set of columns only ever half-applies. It also makes a **zero-rated supply legible**, which flat columns cannot: at 0% every amount is zero and nothing says which side it was, and GSTR-1 has to tell them apart.

---

## 6. `sal.SalesRegister`

Not a ledger. `acc.JournalLedger` stays the single posting target. This is the source for **GSTR-1**, the sales report and the day book.

**Grain: `(TransactionTypeCode, SourceId, HsnSacCode, GstRate)`** — B2B is filed per invoice per rate, the HSN summary per HSN per rate. Both fall out of one `GROUP BY`; neither falls out of a header or a line.

Columns: `SourceId` (no FK — fed by `Invoices` and `CreditNotes`), `DocumentNo`, `DocumentDate`, `ContactId`, `ContactGstin`, `PlaceOfSupplyStateId`, `IsInterState`, `SupplyType` (B2B / B2CL / B2CS / Export / SezWithPay / SezWithoutPay / Nil / Exempt / NonGst), `ReverseCharge`, `HsnSacCode`, `GstRate`, `Quantity`, `UqcCode`, `TaxableAmount`, the four tax amounts, `TotalAmount`, `CurrencyCode`, `ExchangeRate`, `TaxableAmountBase`, `OriginalInvoiceId/No/Date`.

**Write discipline is the whole guard against drift:** written inside the post's own transaction, replaced by `(type, document)` on a re-post, deleted on void. Its period total must tie to the Output GST legs in the ledger.

`chk_register_tax_split` is the constraint that earns its place: intra-state forbids IGST, inter-state forbids CGST and SGST. A wrong determination still balances, still prints, still posts — the return is where it would otherwise surface, months later.

---

## 7. What each document posts

**Invoice / POS** — one document, four leg types, **two services**:

| Leg | Account | Type | Written by |
|---|---|---|---|
| Per line | Sales Revenue, item sub-account | `ITEM` | Sales |
| Per rate | Output GST, rate sub-account | `TAX` | Sales |
| Header | Accounts Receivable, contact sub-account | `CONTROL` | Sales |
| Rounding | Round-off | `ROUNDOFF` | Sales |
| Per line | `Dr` COGS / `Cr` Inventory | `COGS` | **Inventory**, asynchronously |

**Credit note** — `Dr Sales Returns` (contra Income) / `Cr Accounts Receivable`, GST reversed at the invoice's rates, stock back to its original layers.

**Delivery challan** — open decision, see §9.

**The timing seam.** An invoice is complete and correct *before* its cost of sale is known: the number, lines, stock issue and revenue legs happen in the request; costing settles moments later; the COGS legs post after that. **The screen must read `CostingStatus` and say "costing pending" rather than showing zero** — a zero COGS reads as 100% margin and will be believed.

---

## 8. Decisions already taken

| Decision | Why |
|---|---|
| A table pair per document type, not one discriminated table | Conversion links become real foreign keys; type-specific columns are `NOT NULL` where they belong |
| POS has no table — it is an `Invoices` row | Same document, different screen. Two tables for one document means two places to fix a GST bug |
| `DocumentNo` from creation, not at post | A draft can be quoted. **Consequence: no document row is ever deleted** — abandoning voids it and keeps the number accounted for |
| Four statuses, no `Cancelled` | `PostedAt` being null already says a void never reached the books |
| Names read from masters, not stored | A corrected name should show everywhere. **Cost: batched cross-service lookup on every list** |
| GSTIN, HSN, tax rates and addresses stay snapshotted | They are what was *filed* or *delivered*, not what a thing is called |
| Tax as child rows, not columns | Two components intra-state, one inter-state; and 0% must stay legible |
| `LineType` in the base, not just on bills | A `Capital` line on an invoice is a fixed-asset disposal |
| `ItemId` nullable | Services and one-off charges. Such a line moves no stock, gets no COGS, posts to a named account |
| `TaxTreatment` on every line | Exempt is not zero; GSTR-1 reports them in different tables |
| `decimal(28,2)` money, `decimal(28,6)` unit price | 28 is C#'s decimal ceiling, not Postgres's |
| `AllowFreeTextLines`, `DiscountLevel`, `DiscountBeforeTax` on `mst.Organizations` | Structural branch decisions, frozen once the branch has traded. **Built already** |

---

## 9. Open — answer before the stage that needs them

- **What a delivery challan posts.** Issuing as `Dr COGS` at dispatch books cost with no revenue against it. *Recommendation: a `Goods Delivered Not Invoiced` control account — `Dr GDNI / Cr Inventory` on the challan, `Dr COGS / Cr GDNI` on the invoice.* Job work, approval, branch transfer and sample post nothing at all. Mirrors the GRNI question in `PURCHASE.md`.
- **Jewellery line columns** — making charge, wastage, metal rate. A 1:0..1 extension like `inv.ItemJewelleryDetails`, or columns on every line. Settle before the first pair is built.
- **Can a user override `TaxTreatment` on a line?** Default no — it is a property of the goods. But an SEZ or export customer needs `ZeroRated` on a normally-taxable item, and that is driven by the *customer*, not the item.

---

## 10. Tasks

Numbering follows `TRANSACTIONS.md`, so a cross-reference written before this file still resolves.

**Re-marked against the code on 22 August 2026, not against memory.** The ledger leg contract that blocked four boxes is fixed, and the shadow-key fault that stopped every save is fixed, so the "written but blocked" column has largely emptied into "ticked".

| | |
|---|---|
| **Ticked** | T2.3 quote, T2.4 sales order, T3.1 invoice API, T3.2 invoice page, T3.5 sales register, T5.1 allocation guard, T5.4 allocation grid |
| **Deferred to T3.6, by scope** | Partial fulfilment across the chain: `DeliveredQuantity` and `InvoicedQuantity` are unwritten, so `PartlyDelivered` is unreachable and an order cannot be shipped or billed in part. This is the challan's work, not the order's or the invoice's |
| **Written, unverified end to end** | T3.6 delivery challan, T5.2 credit note — both can now save, neither has been driven through a full path |
| **Written, defective in a named line** | T5.2 (`ReturnsStockMovementId`), T3.6 (invoice re-issues challan stock) |
| **Part built** | T3.3 outstanding — settlement now shows on the invoice list (Paid / Part-paid / Unpaid, from `internal/ledger/settlements`), still no aging buckets |
| **Part built** | T3.4 print — the browser print view exists, and since 6 September a **template master and server-side renderer** exist behind it (`docs/Master.md` stage 7). Neither the per-document print route nor **the archived PDF/A copy** is built; they are blocked on different things — a tenancy decision and the PDF licence |
| **Not built** | T7.1 POS till screen (Phase 3); the item and customer pickers on every sales form, which wait on the item lookup endpoint |

---

## 10a. Field coverage — what each screen and service actually carries

Audited 15 August 2026 by comparing, for all five documents: the entity columns, the backend save request, the frontend request interface, and the form controls that fill it. **The quote and the sales order carry their whole surface. The other three carry a fraction of it**, and two of the gaps stop a save outright.

### Two blockers, both proven against a real database

`Sales.Api.Tests.DocumentLineFieldTests` builds a line exactly as the services build one and watches the database refuse it.

| Column | Set by | Never set by | Consequence |
|---|---|---|---|
| `BaseQuantity` | quote, sales order | **invoice, challan, credit note** | Defaults to 0 while `ConversionFactor` defaults to 1, so `chk_*_base_quantity` reads `0 = round(10 × 1, 6)` and refuses. **The first line of any of these three documents cannot be saved.** |
| `LineNumber` | quote, sales order | **invoice, challan, credit note** | Appears nowhere in the three services, so every line takes 0 and `IX_*_Line` refuses the second. **A multi-line document cannot be saved even once `BaseQuantity` is fixed.** |

These were independent of the ledger contract in T3.1: they failed at `SaveAsync`, long before anything was posted. The shadow-key fault behind that is fixed in `BindDocumentLineNavigations` — see the status note at the top of this file.

### The line request is five fields where the line has eighteen

`SaveInvoiceLineRequest`, `SaveDeliveryChallanLineRequest` and `SaveCreditNoteLineRequest` carry `ItemId`, `Quantity`, `UnitPrice`, `DiscountPercent` and `TaxGroupIds` — nothing else. What that costs, beyond the two blockers above:

- **No `TaxTreatment`** — every line is `Taxable` by default, so an exempt supply, a nil-rated one and a zero-rated export are all unreachable. They are filed in different GSTR-1 tables.
- **No `LineType`, `Description` or `AccountId`** — a free-text line is impossible, though `AllowFreeTextLines` is a branch setting and the check constraints permit one. A service or a delivery charge cannot go on an invoice.
- **No `HsnSacCode`** — required on the face of a GST invoice.
- **No `IsPriceInclusive`** — MRP pricing is the Indian retail default, not an edge case.
- **No `UomId`, `ConversionFactor`, `WarehouseId`, `ItemBatchId`, `LineNotes`.**

The frontend is ahead of the backend here rather than behind: `SaveDeliveryChallanLineRequest` on the client already carries `hsnSacCode`, `description`, `accountId`, `taxTreatment` and `taxMasterId`, and the server has nowhere to put them, so they are serialized and dropped.

### Header fields with no way to fill them

| Document | In the request, no form control |
|---|---|
| Quote | — |
| Sales order | `quoteId` |
| **Delivery challan** | `salesOrderId`, `transporterName`, `ewayBillNo`, `ewayBillDate` |
| **Invoice** | `quoteId`, `salesOrderId`, `deliveryChallanId`, `paymentTermId`, and the five POS columns |
| Credit note | — |

Two of those matter beyond tidiness:

- **`salesOrderId` on the challan has no control**, and `ReleaseReservation` is set from `SalesOrderId.HasValue`. A challan raised from the screen therefore never releases a reservation — which is exactly the clause T3.6's *Done when* turns on.
- **`deliveryChallanId` on the invoice has no control**, so the "invoice against a challan moves no stock" branch, which the service implements correctly, cannot be reached from the UI.

The POS columns having no control is expected — POS is Phase 3 and has no screen.

### Header fields the request never had

`ContactGstin` and `PlaceOfSupplyStateCode` are on `SaveQuoteRequest` and `SaveSalesOrderRequest` and on **none** of the other three. `TermsAndConditions` likewise. So an invoice cannot state its own place of supply and can only fall back to the contact's registration — and an invoice is the document the GST return is filed from.
| **Never compiled** | T7.2 POS screen, T7.3 ESC/POS — `apps/desktop` has no build target |

**One defect used to account for four of those boxes, and it is fixed.** `Sales.Api`'s `LedgerLegRequest` carried a single `Amount` where Accounting's carries `DebitAmount` and `CreditAmount` and rejects a leg that is neither, so Sales had never successfully posted to the general ledger. `LedgerClient` now matches the contract. Written up under T3.1.

**There is no `Sales.Api.Tests` project.** Accounting, Inventory, Purchase and `Shared.Kernel` each have one. Sales posts documents to the ledger and moves stock, and has no tests at all — which is how a contract mismatch this size stayed hidden.

### Blocked on foundations

These live in `TRANSACTIONS.md`. **T0.1** (the ledger door) and **T0.6** (ledger screens) were already done; **T0.2** tax determination, **T0.3** document numbering series and **T0.4** the lifecycle are now written too — all three unverified, per the standing caveats in `CLAUDE.md`. Nothing in T2 is blocked any longer.

### T2 — quote and sales order


# --- Purchase.md ---
# PURCHASE.md — the `pur` module, end to end

Everything needed to build Purchase: the document chain, every table and column, every decision taken and why, and the task list.

**This file is the single home for `pur.*`.** `SPEC.md` points here rather than repeating the columns, and `TRANSACTIONS.md` points here rather than repeating the tasks. `CLAUDE.md` still holds the conventions that apply to everything.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** See *Git — how work reaches main* in `CLAUDE.md`.

**Status: every document in the module is built and verified.** T4.1 through T4.5, T5.1 (built by Sales, shared) and T5.3 are done — the twelve `pur` tables with RLS, and the purchase order, goods receipt, bill and debit note end to end: APIs, pages, Gateway route and forty-one tests against a real PostgreSQL 16.

`POR → GRN → BIL` runs end to end, with `DBN` as the way back out: an order commits nothing, a receipt puts stock on the shelf against Goods Received Not Invoiced, a bill clears that account and owes the vendor with input credit claimed against their own number, and a debit note sends goods back and reverses both.

**T5.4, the allocation UI, is built** — shared with Sales, wired into the credit note page there. The debit note does not allocate by decision (see T5.3): a return against a fully paid bill is legitimate, and the money that comes back is a money document's job, not an allocation's.

**One case is deliberately refused rather than guessed at:** a bill priced differently from the receipt it bills. Purchase price variance is undecided (§8) and shipping a guess would leave a residue in the clearing account, so the bill is rejected with both figures named. That is the next decision worth making here.

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
- **Landed cost apportionment.** `LandedCostAmount` on the bill and `ApportionedLandedCost` on the line hold it; whether it is spread by value, weight or quantity is not decided. Both columns exist and stay zero until it is.
- **Purchase price variance — now blocking, not merely open.** A receipt opens a cost layer at the order's price; the bill may disagree, after sales have already drawn on that layer. Either **revalue the layer** and let the recosting engine restate those sales, or **post the difference to a variance account** and accept a slightly untrue margin. The recosting machinery already existing tilts this toward revaluation — the expensive half is built.

  **T4.5 shipped refusing the case rather than guessing at it.** A bill whose unit cost differs from the receipt's is rejected with `PriceVarianceUndecided`, naming both figures. That keeps the money accounted for, but it means a vendor who bills a price different from the one received cannot be entered against that receipt at all — they have to be billed without it, which loses the GRNI clearing. This is the next decision worth making in the module.
- **`pur.PurchaseRegister`** — the counterpart to `sal.SalesRegister`, same grain, for ITC claims and GSTR-2B reconciliation against what the vendor filed. Not designed.

---

## 9. Tasks

Numbering follows `TRANSACTIONS.md`.

### Blocked on foundations

**T0.2** tax determination · **T0.3** numbering series · **T0.4** the lifecycle — all in `TRANSACTIONS.md`. And **`SALES.md` T2.2**, which builds the three base classes this module inherits.

### T4 — order, receipt, bill


# --- Reporting.md ---
# Reporting Module

**Schema:** `rpt` (report catalog and saved layouts) + read-only queries across `acc`, `inv`, `sal`, `pur`, `con`

**Status source of truth:** §8 is the only report-status section in this document, and it is a reconciliation against source rather than a checklist. Report completion must be verified against source files.

**Current verified status (4 September 2026):** **41 report sources are wired end to end** — source, DI registration, catalog seed and test coverage, each of the four asserted against the assembly by `ReportLayerCertificationTests`. Of those, **34 are among the 46 entries in `reports.json`** and 7 are extra reports the engine made cheap to add. **12 of the 46 are not implemented**, and §8.2 says which and why.

**The "20 of 46" this document used to carry was wrong in both directions and is corrected below.** It undercounted what was built by fourteen and mis-stated what remained. Recounted by diffing `reports.json` against the report keys the sources declare, which is a thirty-second answer and worth redoing rather than carrying forward.

## 1. Decisions already taken

Six questions were settled before this was written. They are recorded here because each one closes off a large branch of design, and re-opening one means re-reading everything below it.

| Question | Decision | What it buys |
|---|---|---|
| What renders the grid | **In-house `bb-report-grid`**, in `libs/shared/ui-components` | No licence, no vendor bundle, and consistent reporting UI |
| Where filter / sort / group / pivot / page execute | **Server-side, always** | One code path, one place `OrgId` is enforced, one place totals are computed |
| Which libraries may be used | **No new packages.** Only packages already pinned by the project | Avoids unnecessary vendor dependencies |
| How Excel is produced | **Server-side, over the full result set**, via `DocumentFormat.OpenXml` | Export matches the current query and is not limited to the visible page |
| How CSV is produced | **Server-side, over the full result set**, using the report's declared columns and current query state | Lightweight interchange format suitable for accounting/data workflows |
| What PDF export does | **Not supported / intentionally skipped** | No PDF implementation is required for Reporting |
| Whether layouts persist | **Yes — `rpt.ReportDetails`** | Users can save and reuse report layouts |

The grid is therefore **dumb by design**: it holds no data, fetches nothing, and computes no total. It receives a column definition, a query state and a page of rows, and it emits a new query state.

## 2. The one decision still open

**Reporting has to read `acc`, `inv`, `sal`, `pur` and `con`. Hard rule 8 says a service never crosses a boundary by referencing another service's `DbContext`.**

A report engine cannot ask for its data over HTTP. Account Transaction joins ledger rows to accounts, sub-accounts, contacts and tax masters and then pages the result; done across multiple API calls it would prevent efficient server-side paging. The recommended approach remains a read-only `ReportingDbContext` mapping over the required tables with migrations excluded and tenant filtering re-applied.

## 3. The common grid — `bb-report-grid`

### 3.1 What it must do

**Filter.** Per-column, typed filters. Filters combine with AND and are displayed as removable chips. Report-level parameters remain separate from column filters.

**Order.** Multi-column sorting is supported.

**Column select.** Users can select and reorder report columns.

**Group.** Groups can nest and return server-calculated subtotals.

**Pivot.** Rows, columns and values can be configured with aggregates. The server returns declared pivot columns.

**Export — Excel and CSV.** Export re-runs the current report query with the current filters, sort, grouping, pivot and selected columns, **without paging**. Excel is produced server-side using the existing OpenXML dependency. CSV is a plain tabular export of the same result. **PDF is intentionally not part of the Reporting requirement.**

**Pagination.** Server-side page sizes 25 / 50 / 100 / 200.

**Fixed header rows.** The header is sticky.

**Fixed columns.** Reports may configure leading columns as frozen.

### 3.2 What it deliberately does not do

- **No HTTP.** The host page or `reporting-core` fetches; the grid receives data.
- **No formatting policy of its own.** Formatting comes from the report column definition and currency context.
- **No aggregate arithmetic.** Totals arrive from the server.
- **No inline editing.** Reports are read-only.
- **No PDF generation or PDF export.** PDF is explicitly out of scope.

### 3.3 Component API

```text
bb-report-grid
  inputs
    definition   ReportDefinition
    state        ReportQueryState
    result       ReportResult | null
    busy         boolean
    currency     CurrencyContext
    freezeHeader boolean = true
    freezeColumns number = 0
  outputs
    stateChange  ReportQueryState
    export       ExportFormat           xlsx | csv
    rowActivate  ReportRow
```

One input carries the whole query state and one output replaces it. The host owns the state, so the URL, a saved view and the browser back button can all work through the same object.

### 3.4 Files

```text
libs/shared/ui-components/src/lib/report-grid/
  report-grid.component.ts|html|scss
  report-column.model.ts
  report-query.model.ts
  report-result.model.ts
  column-chooser.dialog.ts|html|scss
  filter-bar.component.ts|html|scss
  group-panel.component.ts|html|scss
  pivot-panel.component.ts|html|scss
  report-pager.component.ts|html|scss

libs/reporting/reporting-core/src/lib/
  report-catalog.service.ts
  report-query.service.ts
  saved-view.service.ts
  report-state.ts
  models/

libs/reporting/reporting-ui/src/lib/
  report-list/report-list.page.ts
  report-host/report-host.page.ts
  saved-views/saved-view.dialog.ts
```

**One host page serves every report.** A report is data — a key, a column list and a parameter set — so forty-six reports do not require forty-six pages. A report that needs something the generic host cannot express gets its own page and still hosts the same grid.

### 3.5 Responsive reporting rule

**Reports MUST remain table/grid based on desktop, tablet and mobile. Reports must NOT be converted into transaction/master-style cards.**

On narrow screens the report grid may use horizontal scrolling, compact columns, sticky important columns and other table-specific responsive techniques. The reporting data model, query and business logic remain the same across breakpoints.

## 4. The query contract

### 4.1 Endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/reports` | The catalog: key, title, module, description, whether the caller may run it |
| `GET` | `/api/reports/{key}` | One report's parameters and full column metadata |
| `POST` | `/api/reports/{key}/query` | Run the report and return one page |
| `POST` | `/api/reports/{key}/export?format=xlsx` | Export the current query as Excel without paging |
| `POST` | `/api/reports/{key}/export?format=csv` | Export the current query as CSV without paging |
| `GET` | `/api/reports/{key}/views` | Saved layouts visible to the caller |
| `POST` / `PUT` / `DELETE` | `/api/reports/{key}/views[/{id}]` | Manage saved layouts |

`format` supports **`xlsx` and `csv` only**. `pdf` is not supported and must not be added to the report export UI or API contract unless the project requirement is explicitly changed later.

### 4.2 Request

```jsonc
{
  "parameters": { "from": "2026-04-01", "to": "2026-06-30", "asAt": null,
                  "warehouseId": null, "includeZeroBalances": false },
  "columns":  ["date", "accountCode", "account", "debit", "credit"],
  "filters":  [ { "column": "accountType", "operator": "In", "values": [1, 5] },
                { "column": "debit", "operator": "GreaterThan", "value": 0 } ],
  "sorts":    [ { "column": "date", "direction": "Asc" },
                { "column": "accountCode", "direction": "Asc" } ],
  "groupBy":  ["accountType"],
  "pivot":    null,
  "page":     { "number": 1, "size": 50, "includeCount": true },
  "freeze":   { "header": true, "columns": 1 }
}
```

`parameters` is report-specific and declared by the report's metadata; everything else is generic. When `pivot` is present, `groupBy` and `columns` are ignored — a pivot declares its own shape.

### 4.3 Response

```jsonc
{
  "reportKey": "account-movement",
  "generatedAt": "2026-08-17T09:14:22Z",
  "currency": { "code": "INR", "decimals": 2 },
  "columns": [ { "key": "date", "header": "Date", "type": "Date", "align": "left" } ],
  "rows":    [ { "date": "2026-04-02", "accountCode": "1100", "debit": 15000.00 } ],
  "groupFooters": [ { "path": ["Asset"], "rowCount": 214,
                      "aggregates": { "debit": 981200.00, "credit": 44100.00 } } ],
  "grandTotal":   { "rowCount": 12480, "aggregates": { "debit": 0, "credit": 0 } },
  "page": { "number": 1, "size": 50, "totalRows": 12480, "totalPages": 250 },
  "truncated": false
}
```

`grandTotal` spans the whole result, not only the current page.

## 5. Export requirements

### 5.1 Excel (XLSX)

- Server-side generation.
- Uses the current report filters, sorting, grouping, pivot and selected columns.
- Ignores pagination so the export contains the full result set subject to the configured export row cap.
- Frozen header row.
- Report-defined column widths.
- Number/date/currency formats matching the report column metadata.
- Group subtotals and grand totals where applicable.
- Uses the existing `DocumentFormat.OpenXml` dependency; do not introduce another spreadsheet package.

### 5.2 CSV

- Server-side generation.
- Uses the same query state as the screen.
- Ignores pagination and exports the full result set subject to the configured export row cap.
- Uses the selected report columns in their display order.
- Proper CSV escaping for commas, quotes and line breaks.
- UTF-8 output so accounting data and multilingual customer/item names are preserved.
- Where grouping/pivoting is active, CSV follows the same declared result columns returned by the report engine.

### 5.3 PDF

**Not required. Do not implement PDF export.**

The Reporting UI must expose only:

- **Export Excel**
- **Export CSV**

No PDF button, menu item, endpoint, placeholder or future PDF dependency is required.

## 6. Security and tenancy

All report queries and exports must enforce the authenticated user's organization/tenant scope. Report permissions must be checked before catalog access, query execution, saved-view access and exports. Export endpoints must enforce the same authorization as the corresponding report query.

## 7. Performance

Filtering, sorting, grouping, pivoting and pagination remain server-side. Export queries must use the same query semantics as the visible report while removing paging. Export row limits must be enforced server-side to protect the API from unbounded downloads.

## 8. Report Catalog and Status

### 8.1 Status rules

A report is **Completed (100%)** only when its schema/data source, backend query, frontend rendering, validations and authorization are implemented and verified against the source code. A report that exists only in design documentation or an old checklist is not complete.

### 8.2 The reconciliation

`reports.json` holds **46 distinct `(ReportGroup, ReportName)` pairs**, and that file is where the number 46 comes from. Four of the forty-six declare **no columns at all** — Balance Sheet, Cash Flow Statement – Direct, Profit & Loss and Business Performance. The first three are statement reports with pages of their own and are built; the fourth is a bare name with no column list, which is not a report definition to implement.

| | Count |
|---|---|
| Entries in `reports.json` | **46** |
| Implemented | **41** |
| Not implemented | **5** |
| Implemented beyond `reports.json` | **7** |
| **Report sources wired end to end** | **48** |

The seven beyond the specification are Account Movement, Customer Statement, Vendor Statement, GSTR-1 Summary, Sales Register, Purchase Register and Warehouse Tracking Detail. They are real reports in the product; they simply are not in the file the count is taken from, which is why 34 + 7 = 41 rather than 41 of 46.

#### The five not implemented

**Four fixed-asset reports — blocked, not pending.** Depreciation Schedule, Disposal Schedule, Fixed Asset Reconciliation and Fixed Assets Schedule all read a fixed-asset register that does not exist: the register is Phase 2 and is itself blocked on two open schema decisions (whether acquisition and disposal get transaction codes of their own, and straight-line only versus books **and** tax depreciation). Nothing can be built here until those are answered — see the roadmap note in `CLAUDE.md`.

**One is not a report.** *Business Performance*, under a group called *Financial performance*, appears in `reports.json` as a group and a name with no columns, no sub-group and nothing else. There is no specification to implement. It needs a business decision about what it is before it can be engineering work.

*(Note: The seven sales/purchase settlement tracking reports were implemented in September 2026. They are fully wired and functional.)*

### 8.3 What "wired" is asserted to mean

`ReportLayerCertificationTests` asks the assembly, not a list, and fails the build if a report exists in fewer than four places:

```
source class  →  DI registration  →  catalog seed row per column  →  covered by the source theories
```

Plus, per report, from the suites beside it: unique column keys, aggregates only on money and quantity columns, groupable columns typed as text, a declared permission beyond `reports.view`, and seed rows matching the source's declared keys exactly in both directions.

**A report is not complete because its source compiles.** Fifteen tracker and finance reports were once written, registered nowhere and listed in no test, and 239 tests passed over the gap — every test was a theory over a list, and a report absent from the list is a report no theory runs on. That is the failure the certification suite exists to make impossible.

### 8.4 Export status

Both formats are built and asserted, and neither has query semantics of its own — the writers take the `ReportResultView` the engine produced, so an export cannot disagree with the screen it was taken from.

| | Status |
|---|---|
| Excel (`format=xlsx`) | **Built.** `ExcelReportWriter`, on the already-pinned OpenXML dependency |
| CSV (`format=csv`) | **Built.** `CsvReportWriter`, RFC 4180, UTF-8 with a BOM, invariant numbers |
| PDF (`format=pdf`) | **Refused by design**, and outside the reporting requirement |

The CSV half was a requirement this document recorded as decided while `ExportFormat` carried only `Xlsx` and `Pdf`; `?format=csv` was an unreachable branch of an enum. It is implemented now, with 12 tests over quoting, encoding, multilingual text, null handling and column order.

### 8.5 Column presentation

Every column entry in `reports.json` carries the seven presentation properties
`ReportColumnView` exposes — `DataType`, `Alignment`, `Width`, `IsFilterable`,
`IsSortable`, `IsGroupable`, `IsPivotable`. The file held only
`ReportGroup` / `ReportSubGroup` / `ReportName` / `ColumnName` before, so a
report's specification said which columns it has and nothing about how any of
them behaves; the two halves of that answer then lived in different places, and
the seeded catalog was the only one of them anybody could read.

`rpt.ReportColumns` carries the same seven under the same names, so the
imported specification in the database says what the file says. `ReportColumn`
was reshaped to match — `DataType` is a `ColumnDataType` rather than free text,
and `IsGroup` / `IsSort` / `IsFilter` / `FilterType` gave way to `IsGroupable`,
`IsSortable`, `IsFilterable` and `IsPivotable`, with `Alignment` and `Width`
added beside them.

**`reports.json` itself is still a specification, not a runtime input.** Nothing
loads the file; the migration carries its values. Nor is `rpt.ReportColumns` the
catalog a branch runs on — that is `rpt.ReportDetails`, seeded per branch from
`ReportCatalogSeeder` and renamed, reordered or switched off by whoever owns
those books, and `ReportLayerCertificationTests` still asserts over it. What the
properties buy is that a report's specification and its catalog row can now be
compared at all, column for column, rather than one of them being silent.

**The migration drops the old columns and adds the new ones rather than renaming
them.** EF scaffolded renames matched by position — `IsGroup` would have become
`IsPivotable` and `IsFilter` `IsGroupable`, each old value landing under a name
meaning something else — and an in-place `varchar → integer` cast of `DataType`,
which Postgres refuses on the seeded `'Text'` values. That migration would have
failed on any database that had already run the initial one.

The values follow the conventions the seeder already set:

| Property | Rule |
|---|---|
| `DataType` | `Money` for an amount — including every `(%CurCode%)` and `(Source)` pair; `Quantity` for stock, which carries more decimals; `Percent` for a rate stated as a percentage (18 is 18%); `Rate` for an exchange rate; `Enum` for a closed set — a status, a type, a costing or depreciation method, a ledger source; `Link` for a document number that opens its document; `Number` for a count or an internal id; `Date`, `DateTime`, `Boolean`, `Text` as they read |
| `Alignment` | `Right` for anything numeric, `Center` for a tick, `Left` for everything else |
| `Width` | Pixels, sized to fit the header — the widest headers, such as *Unit Sales Price (%CurCode%)*, are capped at 280 |
| `IsFilterable` / `IsSortable` | True everywhere except the internal id columns, which are never offered, and *Running Balance*, which is computed over the ordered result and so does not exist as a value to filter or sort by |
| `IsGroupable` | Text-typed dimensions only, which is the engine's own constraint — `ReportColumn.Of` refuses a non-text grouping key, because grouping concatenates in SQL and a date or an enum would be rendered in a format nobody chose. Free text (a description, a narration, an address, a reference) is excluded: it has as many groups as rows |
| `IsPivotable` | A subset of groupable, restricted to low-cardinality dimensions — a currency, a category, a warehouse, a status. Contacts and items are deliberately absent: `PivotBuilder.MaxColumns` caps the column axis at 200, and a pivot across a few thousand contacts is a spreadsheet nobody can read |

Four of the forty-six reports declare no columns at all (§8.2) and so carry none
of these.

## 9. Delivery checklist


# --- Printing.md ---
# Printing

The print template master, the renderer, and the plan to move both into a service of their own.

**Nothing in stage P is built.** What *is* built is recorded in [`Master.md`](./Master.md) stage 7,
because that is where the code lives today. This file is the design for taking it out, written
down so the argument does not have to be reconstructed from a diff.

---

## Where things stand

| | |
|---|---|
| **Built** | `con.PrintTemplates`, the ten-route API, the twelve-type catalogue, the renderer, `PrintTemplateId` on all fourteen document headers. See [`Master.md`](./Master.md) stage 7 |
| **Not built** | The per-document print route, the Angular editor, PDF/A |
| **Planned here** | Extracting all of it into an eighth service, `Printing`, on schema `prt` |
| **Decided** | Full move; callers push the payload under the user's token; build in slices on approval |
| **Waiting on the owner** | Whether anything has been deployed — P2 drops a table. See the warning there |

---

## Why a service at all, when this repository merged twelve into seven

`CLAUDE.md` records the three merges and the reason each time: two services were two halves of one
job. **An eighth has to answer that, not ignore it.** The answer is that printing is unlike all
three, and in the specific way each merge turned on:

- **Nothing in printing shares a transaction with anything else.** A template is read and a
  document is rendered; no ledger row, no stock movement, no numbering series is taken in the same
  breath. Accounting and Banking merged precisely *because* they could not share a transaction
  while separate. That pressure does not exist here.
- **It is the only part of the product that will grow a native dependency** — a PDF engine,
  possibly a headless browser, fonts. Keeping that out of the seven services that post ledger rows
  is worth a boundary by itself.
- **It is the only work that is CPU-bound and bursty.** A hundred-page statement run should not
  compete for the same process as a till taking a sale.

**This is an argument, not a fact.** If it turns out wrong, the reversal is cheaper than the three
merges were: printing owns one table and reads nobody else's.

## The decision the whole design turns on

`Master.md` 7.7 asks how an internal call carries its branch. Today Printing would need one:
Master holds the template, the document belongs to Sales, and rule 8 forbids Sales reading `con`.
`TenantMiddleware` fills the tenant context **only for an authenticated request**, so an
`[InternalOnly]` endpoint has no branch to scope to.

**Callers push the payload under the caller's own token.** Sales builds `{singles, lists}` from
its own data and POSTs it to Printing with the user's bearer token; Printing resolves the
template for that branch and renders.

Tenancy then travels in the JWT exactly as it does for every other user-facing request. No
internal endpoint has to invent a branch, and no service reads another's tables.

**7.7 is deleted rather than inherited, and that is the strongest argument for the split.** Pulling
the template the other way — Sales asking Printing for it — would have relocated the same question
onto a new boundary and settled nothing.

---

## What moves, and what deliberately does not

`Shared.Kernel.Printing` splits along the line between **contract** and **machinery**.

**Stays in `Shared.Kernel.Printing`** — what both sides must agree on: `PrintPayload`,
`PrintFormatContext`, `PlaceholderCatalog`, `DocumentTypeCatalog`, `PlaceholderType`,
`PlaceholderKind`, `MergeTags`.

Sales has to know an item row is keyed `Item.ItemName` to build a payload at all, and Printing has
to know the same to resolve it. That is a shared contract in the sense `Shared.Kernel.Tax` is, and
splitting it would leave two lists to keep in step.

**Moves to `Printing.Entity`** — the stored shape: `PrintTemplate`, `PrintSettings`,
`PrintContent`, `SegmentMargin(s)`, `PrintSegment`, `SegmentPosition`, `PrinterType`, `PaperSize`,
`PrintJson`, and the request/response models.

**Moves to `Printing.Api`** — the machinery nobody else calls: `PrintRenderer`,
`PrintSubstitution`, `PrintSettingsValidator`, `SegmentSanitizer`, `PrintGeometry`, `PrintMetrics`,
`DefaultLayoutGenerator`, `SamplePayload`, `MaskFormatter`.

**Does not move**: `PrintTemplateId` on the fourteen document headers. It is an unenforced `long`
and goes on pointing at a row in another schema exactly as it did — the schema's name changes and
nothing else does.

**`HtmlSanitizer` and `AngleSharp` move with the sanitiser**, off `Shared.Kernel`. That is a real
gain on its own: seven services currently carry an HTML parser none of them touches.

---

## Stage P — the extraction
