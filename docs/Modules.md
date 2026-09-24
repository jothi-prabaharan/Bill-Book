# --- Master.md ---
# master.md — build order

The order to build things in, and how to tell when each one is actually done.

`CLAUDE.md` holds the conventions. [`SPEC.md`](./SPEC.md) holds the tables and pages. This file holds **what to do next**, one item at a time. [`TRANSACTIONS.md`](./TRANSACTIONS.md) and [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md) continue it for the sixteen document types — Stage 6 below, in their own files because together they are the larger half of the product.

## How to use this file

1. Take the **first unticked box**. The order is deliberate — later stages assume earlier ones.
2. Do it, and check it against its **Done when** line. That line is the test; "it compiles" is not the same as "it works".
3. Tick the box **in the same commit as the work**, the way release notes and docs already work here.
4. If a task turns out to be wrong or unnecessary, strike it and say why rather than deleting it. The reason is worth more than the tidiness.
5. **Mark who is on a box, not just whether it's done.** Before starting, change `- [ ]` to `- [~] working (AI name)` so two sessions don't pick up the same item. On completion, change it to `- [x] completed (AI name) — YYYY-MM-DD`. This applies to every checklist in this file. **The work order is [`docs/TASKS.md`](./TASKS.md), not this file**: it holds every pending task as one queue, with the lane and claim rules that let several agents work at once without conflict. Claim the card there first; tick the matching box here in the same commit as the work.

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
| **Part built** | T3.4 print — the browser print view exists, and since 6 September a **template master and server-side renderer** exist behind it (`docs/Master.md` stage 7). The per-document print route is not built, blocked on a tenancy decision. An archived copy **is** written: posting an invoice renders it with PDFsharp and saves it to storage — but as plain PDF rather than PDF/A, in a fixed layout rather than the template, for invoices only, and with no endpoint to fetch it (TK-22, D-11 in `docs/TASKS.md`) |
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

**Four fixed-asset reports — no longer blocked on the register.** Depreciation Schedule, Disposal Schedule, Fixed Asset Reconciliation and Fixed Assets Schedule read the fixed-asset register, and the register now exists: `acc.FixedAssetCategories`, `acc.FixedAssets`, `acc.DepreciationSchedules` (Books and Tax) and `acc.AssetTransactions`. What is missing on the reporting side is a read model for each in `ReportingDbContext` and a source per report. Two of them are still only half-meaningful until owner decisions D-19 and D-20 (`docs/TASKS.md`) are answered: capitalisation and disposal post nothing to the ledger yet, so a reconciliation of the register against its control account, or a disposal schedule with a gain or loss, has nothing to tie to. See the fixed-assets roadmap note in `CLAUDE.md`.

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

**Stage P is built** (TK-80, TK-81, TK-82; 24 September 2026). Printing serves the template API,
seeds every branch, renders pushed payloads, and sales invoices print through it; Master's copy
and `con.PrintTemplates` are gone. What follows is the design as it was argued, kept so the
argument does not have to be reconstructed from a diff.

---

## Where things stand

| | |
|---|---|
| **Built** | The eighth service, `Printing`, on `prt`: the ten-route template API, `api/print/render`, branch seeding, the renderer and sanitiser, the twelve-type catalogue; the editor (Settings › Print templates); the sales invoice's print route, with PROFORMA/VOID stamped by the renderer |
| **Not built** | Print routes for the other eleven document types; PDF/A; the amount in words and the place-of-supply state name on the invoice payload |
| **Decided** | Full move; callers push the payload under the user's token; `con.PrintTemplates` dropped and branches re-seeded rather than copied (D-13: nothing deployed) |

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


# --- Platform.md ---
# One customer, many applications

RetailErp is no longer the only product. **HRMS**, **Payroll** and **School** are designed next to it,
and each is sold on its own. A customer may buy one app, several, or all four (owner's decisions,
23 September 2026). Payroll became an app of its own the same day, sellable **without** HRMS.

This section is the platform all four share. It is **stage H0**, built before any HRMS table,
because nothing in a second app works without per-app licences, tokens and menus.

**Nothing in this section is built.**

## Where things stand

| | |
|---|---|
| **Built** | One customer (`mst.Customers`), many branches (`mst.Organizations`), users, roles, permissions, the two-step login, branch switching, one licence per customer, the shard registry |
| **Planned here** | An `App` on roles, licences and refresh tokens, and a set of apps on permissions and menus; per-app sign-in; an app switcher; an Applications page to start another app's trial; per-app signup; per-app seeding |
| **Decided** | One customer and one set of branches across every app; one licence **per app**; each app has its own public signup with a 14-day trial; the customer and branch screens are shared, not copied |
| **Waiting on the owner** | Pricing per app (per user, per branch or per employee); how a platform operator gets `platform.*` (already in Undecided in `CLAUDE.md`) |

## The model

- **One customer, one set of branches, every app.**
  - `mst.Customers` is the head office and `mst.Organizations` its branches, exactly as today.
  - There is no per-app customer or branch table.
  - **A branch is a legal trading unit, not an app's**, so one `OrgId` can run several apps. A school
    campus runs School, HRMS and Payroll on the same branch; a shop runs RetailErp and Payroll.
- **Isolation does not change.** Every app's tables carry `CustomerId` and `OrgId` with the query
  filter and a FORCEd RLS policy, in the same shard. An app is not a tenant boundary; a customer
  and a branch are.
- **Which apps a user can open, in which branch**, is already modelled: `UserOrganizationRole` names
  a role, and the role names its app. No new table.

## `App`

`Master.Entity/Enums/App.cs` is a `[Flags]` enum: `RetailErp = 1`, `School = 2`, `Hrms = 4`,
`Payroll = 8`.

It is **not** `Vertical`. `Vertical` is the trade inside RetailErp (General, Pharma, Jewellery) and
stays as it is.

**A role belongs to one app; a permission or a menu may belong to several.** That difference is the
whole design. A screen shared by several apps — users, roles, branches, settings, and the employee
master HRMS and Payroll both use — has one permission code (`Permission.Code` is unique) and one
menu row, marked with every app that shows it. Duplicating those codes per app would give two
permissions for one screen and two answers to "can this person edit users".

| Table | Change | Why |
|---|---|---|
| `mst.Roles` | `App App`, one value, required, indexed | Owner of RetailErp and Owner of Payroll are different rows |
| `mst.Permissions` | `App Apps`, one or more flags, required | `users.*`, `roles.*`, `organizations.*`, `settings.*` are every app's; `employee.*` is HRMS's and Payroll's; `sales.*` is RetailErp's only |
| `mst.Menus` | `App Apps`, one or more flags, required | The branches screen appears in every app; the employee list in HRMS and Payroll; the ledger in RetailErp only |
| `mst.Licenses` | `App App`, one value; unique on (`CustomerId`, `App`) | One licence per app per customer |
| `mst.RefreshTokens` | `App App`, one value | A refresh mints a token for the same app, as `OrgId` already makes it mint one for the same branch |
| `RolePermission`, `UserOrganizationRole` | none | They reach the app through `RoleId`. A second copy of the same fact could disagree — the reasoning that removed `BranchId` |

**The grant rule**: a role may be granted a permission only when `permission.Apps` includes
`role.App`. Checked in C# on write, and asserted over every seed.

Every existing role and licence becomes `RetailErp`. Every existing permission and menu becomes
`RetailErp`, except the shared screens — users, roles, branches, organization settings, currencies,
configuration and SMTP — which become all four.

**Two magic strings become enums while this is open** (hard rule 7): `Customer.PlanTier` and
`TenantDatabase.PlanType`.

## Licences

Each licence has its own `LicenseType` (Trial, Paid), start and expiry, grace days, `MaxUsers` and
`MaxOrganizations`, all on the row that already exists. What changes is that there is one per app:

- Expiring one app's licence locks that app only. A customer whose RetailErp trial lapses keeps
  running payroll.
- **HRMS and Payroll share the employee master.** Either licence unlocks it; neither needs the other.
  Buying the second later adds its screens over the same employee records — nothing is re-entered.
- A user counts once per app they hold a role in, against that app's `MaxUsers`.
- A branch counts once per app it is used in, against that app's `MaxOrganizations`.
- `apps/admin` shows and edits licences per app, per customer.

## Sign-in is per app

The two-step login stays. The second step says which app it is for.

- `SelectOrganizationRequest` gains `App`.
- The branch list returned by `POST /api/auth/login` is filtered to branches where the user holds a
  role **in the app being signed into**. A request carries the app from the page that sends it.
- The access token carries:
  - an `app` claim;
  - `license_status` and `license_expiry` **from that app's licence**;
  - `permission[]` built only from roles of that app.
- **Services check the `app` claim**, per controller, with `[RequireApp(...)]` naming the apps it
  serves. The Hrm service's employee controllers take `App.Hrms | App.Payroll`; its recruitment and
  lifecycle controllers take `App.Hrms` alone; Master's user, role and branch controllers take all
  four. An HRMS token cannot call a RetailErp endpoint even if a permission name matched.
  `EndpointGuardAudit` gains that question: every controller names its apps.
- **Switching app** is the same move as switching branch today: mint a new token. The refresh-token
  family is per app.

## Shared master pages — one page, every app

**Every master page that is not about one app's trade is shared by all apps** (owner's decision,
23 September 2026). There is one users page, one roles page, one branches page, and so on: every app
mounts the same page from the same lib, reads the same rows through the same API, and never carries
a copy. A change to the users page is a change in all four apps at once.

**The rule, for these pages and for every master page added later:**

1. **The page lives in a shared lib** (`libs/master/master-ui` or `libs/shared/auth`), never in an
   app or in one app's feature lib. An app only adds its route.
2. **Its menu row carries every app that shows it** in `Menus.Apps`, and its permission codes carry
   the same set in `Permissions.Apps` — one code per screen, not one per app.
3. **Its controller names the same apps** in `[RequireApp(...)]`, so any of those apps' tokens can
   call it.
4. **The data is the customer's, not the app's.** A user, a branch or a currency added in Payroll is
   the same row RetailErp sees.
5. **A page that is shared by only some apps says which, and why**, in the table below. The default
   for a new master page is all four; narrowing it needs a reason written down.

| Page | Lib (already exists unless noted) | Owned by | Apps |
|---|---|---|---|
| Sign-in, branch selection, forgot password, accept invitation, trial expired | `libs/shared/auth` — `pages/` | Master | All |
| Signup | `libs/shared/auth` — `pages/signup` | Master | All — each app's page passes its `App` |
| Branch switcher and app switcher | `libs/app-shell` — `topbar/` (the app switcher is new) | Master | All |
| My profile and change password | new, `libs/shared/auth` | Master | All |
| **Users and invitations** | `libs/master/master-ui` — `users/` | Master | All |
| **Roles and permissions** | `libs/master/master-ui` — `roles/` | Master | All |
| **Branches** (create, edit) | `libs/master/master-ui` — `organizations/` | Master | All |
| **Organization settings** | `libs/master/master-ui` — `organization-settings/` | Master | All |
| Currencies | `libs/master/master-ui` — `org-currencies/` | Master | All |
| Configuration (formats, preferences) | `libs/master/master-ui` — `configurations/` | Master | All |
| SMTP settings | `libs/master/master-ui` — `smtp-settings/` | Master | All |
| API clients | `libs/master/master-ui` — `api-clients/` | Master | All |
| **Applications** (licences, start a trial) | new, `libs/master/master-ui` | Master | All |
| Numbering series | `libs/accounting/accounting-ui` — `numbering-series/` | Accounting | All — every app numbers its documents (`EMP`, `PAY`, `ADM`, `FDM`, …) from the one shared table; the page moves to `libs/master/master-ui` so no app imports an accounting lib for it, and keeps calling Accounting's API |
| Print templates (editor not yet built) | `libs/master/master-ui` when built | Master, moving to Printing (stage P) | All — payslips, Form 16, HR letters and fee receipts are templates like invoices |
| Contacts, contact person roles | `libs/master/master-ui` — `contacts/`, `contact-person-roles-*` | Master | RetailErp and School — customers, vendors and guardians. HRMS and Payroll have employees, not trade contacts |
| HSN/SAC codes | `libs/master/master-ui` — `hsn-sac/` | Master | RetailErp and School — GST on goods and on the rare taxable fee head |
| **Employee master** and organisation setup (departments, designations, grades, locations) | `libs/hrm/hrm-ui` | Hrm | HRMS, Payroll and School — School's class teachers and technicians are employees, so a School licence alone shows the employee list without leave or pay |

**How the shared pages behave inside one app**, so a shared page is not a confusing one:

- **Roles** lists and creates only the current app's roles, and offers only permissions whose `Apps`
  include it. A RetailErp Owner and a Payroll Owner are different rows, and editing one never
  touches the other.
- **Users** lists every user of the customer, with an **Apps** column showing where each holds a
  role. Inviting or editing from inside an app assigns that app's roles; the user record, password
  and sign-in are shared.
- **Branches** is identical everywhere. Creating a branch seeds it for every app the customer is
  licensed for (Signup and seeding, below), not only for the app it was created from.
- **Licence limits** count per app: a user counts against an app's `MaxUsers` only when they hold a
  role in it; a branch counts against an app's `MaxOrganizations` only when that app is used there.

The licence guard (`libs/shared/auth/src/lib/license.guard.ts`) reads the current app's licence.

New, in the shared libs:

- **App switcher** in the `libs/app-shell` topbar. It lists the apps the customer holds a licence
  for **and** the user holds a role in, and opens the other app's URL, which mints its own token.
- **`APP_ID`**, an injection token each app provides to `libs/app-shell`. `menu.service.ts` sends it
  as `GET /api/menu?app=Hrms`. `shell-screens.ts`, the rail drawn before the menu answers, becomes
  input from the app rather than a retail list.
- **Applications page** (`libs/master/master-ui`). Each app's licence and status, and **Start trial**
  on one the customer does not have yet.
- **"Current context" endpoint.** New apps do not decode the JWT (owner's rule). They read the
  signed-in user's name, branch, app, licence and permissions from Master, without internal ids.
  `apps/web` keeps `token-claims.ts` for now.

## How role, app and permission map

**Six rows decide what a person can do, and each answers one question:**

```
User ──< UserOrganizationRole >── Role ──< RolePermission >── Permission
          (UserId, OrgId, RoleId)   (App: one)                  (Code, Apps: one or more)

Menu (Apps: one or more) ──< MenuPermission (PermissionCode)
```

| Question | Answered by |
|---|---|
| Which apps can this person open, in this branch? | `UserOrganizationRole` → `Role.App` |
| What can they do in this app? | The permissions of their roles whose `Role.App` is this app |
| Which menu entries do they see? | `Menu.Apps` includes the app **and** they hold one of its `MenuPermission` codes |
| Can this app call this endpoint at all? | `[RequireApp(...)]` on the controller, against the token's `app` |
| May the customer use this app? | That app's row in `mst.Licenses` |
| Their own profile? | Nothing — `[Authorize]` only, and always their own record |

**Worked example.** Priya works at the Chennai branch of a customer who bought RetailErp and Payroll.

| Row | Value |
|---|---|
| Role 3 | Accountant, `App = RetailErp` — granted `accounting.view`, `settings.view` |
| Role 41 | Payroll Admin, `App = Payroll` — granted `payroll.*`, `employee.*`, `settings.view`, `settings.edit`. Granting it `accounting.view` is **refused**: that permission's `Apps` is RetailErp only |
| UserOrganizationRole | Priya · Chennai · 3, and Priya · Chennai · 41 |

- **Signing in to `apps/payroll`** in Chennai takes her roles there whose app is Payroll — role 41 only
  — and mints a token with `app = Payroll`, role 41's permissions and the Payroll licence. Signing in
  to `apps/web` mints another with role 3's. One login, two tokens, each carrying only its own app.
- **The users page in Payroll**: the `usr` menu row is flagged for all apps and needs `settings.view`,
  which she holds, so it shows. It lists every user of the customer with an Apps column; hers reads
  "RetailErp, Payroll". Editing a user offers only Payroll's roles.
- **The Tax Master from a Payroll token**: its menu row is RetailErp only, so it never shows; a typed
  request is refused by `[RequireApp(RetailErp)]` with 403. **The app check is what refuses it** —
  `settings.view` is shared, so the permission alone would have let it through.
- **Her profile**: `libs/shared/auth`, reached from the topbar's avatar menu rather than the rail, so
  it has no menu row. `GET/PUT /api/me/profile` is `[Authorize]` and `[RequireApp(All)]` with no
  module permission, resolves the user from the token's `sub`, never from the URL, and is a named
  exemption in `EndpointGuardAudit` beside the menu and formats endpoints.

## Page validation in the shell

**Every page under the shell is validated by the shell, not by the app that mounts it**, and a page
that declares nothing is refused (owner's decisions, 23 September 2026).

What is on `main` today, and why it is not enough:

- `permissionGuard` in `libs/shared/auth/src/lib/license.guard.ts` reads `data.permission` off a route
  and sends the user to `/dashboard` when the token lacks it.
- `apps/web/src/app/app.routes.ts` attaches it **by hand** (`canActivateChild: [licenseActiveGuard,
  permissionGuard]`). A new app that forgets the line has no page validation at all.
- **A route that declares no permission is allowed.** A page nobody remembered to tag is open to
  every signed-in user, and nothing notices.
- It checks the permission only, not the app, and reads it from the decoded JWT, which the new apps
  may not do.

**The design:**

1. **The shell owns the route.** `libs/app-shell` exports `shellRoutes({ app, children })`, which
   returns the shell route with its guards already attached as `canActivate` and `canActivateChild`.
   An app passes its `APP_ID` and its page routes; it never lists a guard itself.
2. **Every page declares its access** in `data.access`, one of:
   - `{ permission: 'payroll.view' }` — the user must hold it in the current app;
   - `{ signedIn: true }` — any signed-in user of the app: the dashboard, the profile page;
   - optionally `apps: [...]` on a shared page's route, when a shared lib exports routes that only
     some apps may mount (contacts: RetailErp and School).
3. **The shell's page guard checks, in order**, and stops at the first failure:

   | # | Check | Source | On failure |
   |---|---|---|---|
   | 1 | Signed in | `AuthService` | `/login` |
   | 2 | The current app's licence is active | Current context | `/expired` |
   | 3 | The page's `apps`, when given, include the current app | `APP_ID` | No-access page |
   | 4 | The page declares `access` at all | the route | No-access page — **deny by default** |
   | 5 | The user holds `access.permission` in the current app | Current context | No-access page |

4. **The source is the current-context endpoint**, not the decoded token. A `SessionContextService` in
   `libs/shared/auth` fetches it once under the shell, holds it in a signal, and refetches on a branch
   or app switch. `permissionGuard` and `licenseActiveGuard` are rewritten over it and `token-claims.ts`
   is retired with them.
5. **A refused page shows why**, on a shared no-access page inside the shell ("You don't have access
   to this page in Payroll — ask your administrator for *Payroll: view*"), rather than bouncing to the
   dashboard silently as today. It never names a role, because roles are customer-defined and the
   permission is what is actually missing.
6. **Actions inside a page use the same answer.** A shared `*bbIfCan="'payroll.post'"` structural
   directive in `libs/shared/auth` hides a button the user cannot use, reading the same
   `SessionContextService`, so a page and its buttons can never disagree about one permission.
7. **The menu and the guard agree by construction.** The menu comes from `GET /api/menu?app=`,
   filtered by the same app and permissions; the guard exists for what the menu cannot stop — a typed
   URL, a bookmark, a link from another app.
8. **An audit fails the build** when any route under the shell declares no `access`. `libs/app-shell`
   exports `auditShellRoutes(routes)`, and each app's route spec calls it — the frontend twin of the
   backend's `EndpointGuardAudit`, for the same reason: a missing tag is invisible in a file nobody
   is reading.
9. **All of this is the user-interface half.** The server's `[RequireApp]` and
   `[RequireModulePermission]` decide everything; the shell only stops a user walking into a page
   that will answer 403 on its first request.

**Moving `apps/web` onto it** is part of H0.3: its routes already carry `data.permission`, which
becomes `data.access`; the dashboard becomes `{ signedIn: true }`; the hand-attached guards go.

## Signup, buying another app, and seeding

- **Each app has its own public signup page**, on one `SignupService`.
  - `SignupRequest` gains `App`.
  - Signup creates the customer, the first branch, the owner user, the **Owner role for that app**,
    and that app's **14-day trial** licence.
- **Starting another app** from the Applications page, or from `apps/admin`:
  - creates the app's trial licence;
  - grants the signed-in owner that app's Owner role in every branch;
  - seeds the app's master data into **every existing branch**.
- **Seeding is per app.**
  - A branch is seeded with Accounting plus every app its customer is licensed for.
  - Accounting is always seeded, because payroll and school fees post there.
  - Seeding stays idempotent — the same retry `apps/admin` already uses — so adding an app later
    only adds what that app is missing.
- **A customer with HRMS only** still has a chart of accounts, and payroll still posts journals to
  it. The ledger screens belong to RetailErp and are not in HRMS's menu. Payroll adds a journal
  export (Tally XML and CSV) for the customer's own accountant. If the customer buys RetailErp
  later, the payroll history is already in their books.

## Sharding

Carried over from the earlier design and unchanged in substance. The owner's model is:

- **100 customers per pooled database.** The 101st causes a new pool to be provisioned.
- **An Elite customer gets a physical database of their own.**

`main` has the shard registry and allocator but not that model:

- `mst.TenantDatabases` counts `MaxOrganizations`, not customers.
- `PlanType` is a free string.
- When every shard is full, signup answers 503 rather than provisioning another.

H0 fixes all three:

- a `PlanType` enum;
- a customer-count capacity;
- an allocator that provisions a new pool when the last one fills;
- an Elite path that allocates a shard with a capacity of one.

## Stage H0

- [ ] **H0.1 — `App` in `mst`.** The flags enum; `App` on roles, licences and refresh tokens; `Apps`
  on permissions and menus; the enums for plan tier and plan type; seeds marked; the grant rule.

  **It starts by fixing the admin migration drift** recorded in `CLAUDE.md` — Master cannot start on
  a fresh database because the menu seed changed after the admin migration was squashed. H0.1 changes
  that same seed, so the admin migration is re-squashed first and
  `dotnet ef migrations has-pending-model-changes` must come back clean before and after.

  *Done when*: granting a Payroll-only permission to a RetailErp role is refused; a Payroll role can
  be granted `users.view`; and `apps/web` is unchanged for every existing user.
- [ ] **H0.2 — Per-app sign-in and licences.** Per-app login filtering, the `app` claim,
  licence claims per app, per-app refresh families, the service-side `app` check, and the
  current-context endpoint.

  *Done when*: an HRMS token calling a RetailErp endpoint gets 403; a Payroll token reads employees
  but not recruitment; an expired RetailErp licence leaves Payroll working.
- [ ] **H0.3 — Shell and shared master pages.** `APP_ID`, `GET /api/menu?app=`, the app switcher,
  the Applications page, and every page in the shared master pages table flagged, guarded and
  mounted per its row — including moving the numbering series page to `libs/master/master-ui`.

  It also builds **page validation in the shell** (above): `shellRoutes`, `data.access` on every page,
  the five-step page guard over the current-context endpoint, the no-access page, `*bbIfCan`, and
  `auditShellRoutes`, with `apps/web` moved onto it.

  *Done when*: a user created from `apps/payroll` appears in `apps/web`'s users page with both apps
  in its Apps column; no app's source tree contains a copy of a shared page; a typed URL to a page the
  user lacks the permission for shows the no-access page; and removing `data.access` from any shell
  route fails that app's route spec.
- [ ] **H0.4 — Signup and seeding per app**, and starting another app's trial.

  *Done when*: signing up for Payroll then starting HRMS gives one customer, one branch, two
  licences and one set of employees, with HRMS's master data seeded into the existing branch.
- [ ] **H0.5 — Sharding.** Per the section above.
- [ ] **H0.6 — `apps/hrms` and `apps/payroll`.** Two empty apps that sign in, select a branch, draw
  their own menus, and switch to each other and to `apps/web`.

# --- Hrms.md ---
# HRMS & Payroll

**Two apps, each sold on its own** (owner's decisions, 23 September 2026):

- **`apps/hrms`** — the employee lifecycle: hire, onboard, organise, time and leave, expense claims,
  performance, exit.
- **`apps/payroll`** — pay: salary, runs, payslips, Indian statutory (PF, ESI, PT, LWF, gratuity,
  bonus), income tax and Form 16, loans, full & final settlement, bank files and the journal.

A customer may buy either, or both. **Payroll works without HRMS**, and neither app asks for anything
twice when a customer has both:

| | Payroll alone | Payroll with HRMS |
|---|---|---|
| **Employees** | The shared employee master (`hrm`), shown inside `apps/payroll` | The same records, also shown in `apps/hrms` |
| **Paid days and loss of pay** | Entered or imported per month in Payroll (`pay.MonthlyAttendanceInput`) | Read from HRMS's daily attendance and approved leave (`tla`) |
| **Leave encashment, F&F leave days** | Entered on the encashment or settlement | Read from HRMS's leave balances |
| **Self-service** | Payslips, Form 16 and tax declarations in `apps/payroll` | The same, plus leave, attendance and claims in `apps/hrms` |

The two share one design section because they share one employee master, one set of conventions and
one set of services; which app shows a screen is a matter of the `App` flags in the Platform section,
not of separate tables.

- **They are built first**, before School, and their stage H0 is the Platform section above.
- **School consumes it.** Teachers, office staff and technicians are employees;
  `sis.Sections.ClassTeacherEmployeeId` and `wrk.WorkOrders.AssignedEmployeeId` point here.
- **The rules this section states** — column conventions, endpoint shape, frontend, tenancy — are
  the ones School refers back to.

**Nothing in this section is built.** It was designed on 23 September 2026 and deepened the same day,
when the owner decided to sell HRMS and Payroll as separate apps.

## Where things stand

| | |
|---|---|
| **Built** | Nothing |
| **Planned here** | Six services, one per schema — `hrm`, `tla`, `pay`, `rec`, `prf`, `clm` — HR and payroll report sources in Reporting, and two apps, `apps/hrms` and `apps/payroll`, on the shared shell |
| **Depends on** | The Platform section (H0); Accounting's internal posting API (built); the print templates, for letters, payslips and Form 16 |
| **Decided** | HRMS and Payroll are two apps, each sold on its own, Payroll fully standalone; one shared employee master; built before School; one service per schema; employees are not contacts; payroll posts through Accounting and never writes GL rows; a customer without RetailErp has its journals posted to a hidden ledger and exported for their accountant |
| **Waiting on the owner** | The open questions at the end of this section |

## Decisions

| Decision | Why |
|---|---|
| **Payroll is an app of its own, sellable without HRMS** | The owner's decision. Standalone payroll is a common purchase for a business that keeps HR on paper |
| **One employee master, owned by the `Hrm` service, shared by HRMS, Payroll and School** | Payroll cannot run without employees, a school has to name its teachers, and a customer with several apps must not enter anyone twice. The master's permissions (`employee.*`) and menus carry all three apps' flags — see Platform § Shared master pages |
| **Payroll takes paid days from HRMS when it is licensed, and from its own monthly input when not** | One run, two sources for the same figures; the run records which it used. A customer who adds HRMS later switches source from the next unposted month |
| **Expense claims stay in HRMS** | Paid through a payroll line when Payroll is licensed, or as a Spend Money otherwise |
| **An employee is not a `con.Contact`** | Contacts are trade counterparties, visible across sales and purchase screens. Salary, PAN, bank details, family and date of birth are not trade data |
| **An employee may link to a `mst.Users` row** (`UserId Guid?`) | For self-service. Many employees (cleaners, drivers) never sign in, so the link is optional |
| **Salary is its own permission** | `payroll.view` is distinct from `hrm.view`. Most HR users may see an employee record; far fewer may see what they earn |
| **Payroll posts one journal per run through Accounting** | The rule every money document follows. A posted run is never edited; a correction is a reversal and a re-run |
| **A customer without RetailErp still posts journals** | Accounting is seeded for every branch (Platform § Signup). The ledger is simply not in the Payroll or HRMS menu, and a journal export serves their external accountant |
| **Statutory settings are effective-dated rows**, not constants | PF, ESI, PT, LWF, gratuity, bonus and tax rules are revised; a past month must recompute to the figures in force then, as the Tax Master already does for GST |
| **Approvals are one approver chain per request type** | Reporting manager, then optionally an HR or finance approver. It covers leave, regularisation, overtime, requisitions, offers and claims. Configurable workflows are the Phase 3 engine and are not waited on |
| **Keys are `{Entity}Id long`**, not a bare `Id` | The house convention (`Lead.LeadId`). `CustomerId`, `OrgId` and `UserId` stay `Guid` |
| **`DateOnly` for dates, `TimeOnly` for a time of day, `DateTimeOffset` only for an exact instant** | A joining date has no time zone; a shift start is a time of day; a punch is an instant |
| **Money, rates, days and quantities are `decimal(18,4)`** in Fluent config | Half days and pro-rated days are fractions; display rounding comes from the branch's currency |
| **Every `string` carries `[MaxLength]`; booleans are `Is`/`Has`/`Can`; enums, never magic strings** | The house style |
| **The existing frontend stack** — signals, async/await, shared `ui-components`, `--color-*` tokens | One shell should not carry two visual languages |
| **HR and payroll reports are Reporting sources**, each flagged with `App.Hrms` or `App.Payroll` | The Reporting service already carries grids, filters, Excel and CSV export for 41 reports; a second report engine would be a second answer to every formatting question |

## Service map

| Service | Schema | Port | Serves app | Owns | Calls |
|---|---|---|---|---|---|
| `Hrm` | `hrm` | 4509 | HRMS; the employee master also Payroll | Organisation setup, employees and everything about them, lifecycle (onboarding, exit), letters, assets issued, announcements | Master (user link, print templates) |
| `TimeLeave` | `tla` | 4510 | HRMS | Holidays, shifts, rosters, weekly offs, punches, daily attendance, regularisation, overtime, comp-off, leave policy, balances, applications | Hrm (employees) |
| `Payroll` | `pay` | 4511 | Payroll | Components, structures, salaries, revisions and arrears, one-off pay, loans, runs, payslips, statutory settings and returns, income tax, F&F, bank files, journal posting and export | Hrm, TimeLeave (when HRMS is licensed), Claims (likewise), Accounting |
| `Recruitment` | `rec` | 4512 | HRMS | Requisitions, openings, candidates, pipeline, interviews, offers | Hrm (creates the employee), Master (print templates) |
| `Performance` | `prf` | 4513 | HRMS | Review cycles, goals, competencies, self-evaluation, level reviews, calibration, appraisal outcomes | Hrm (employees, approval chains), Payroll (revision, when licensed) |
| `Claims` | `clm` | 4514 | HRMS | Claim categories and limits, expense claims, approval, payout | Hrm, Payroll (payout in a run, when licensed), Accounting (payout as Spend Money) |

Every cross-service id is an unenforced `long`, validated in C# through the owning service (hard
rule 8). Each service is the usual three projects under `backend/Api/{Service}/` with a test project
under `backend/tests/`.

## Columns

Every table below also carries, and the tables do not repeat:
`CustomerId Guid` and `OrgId Guid` (from `OrgScopedEntity`, with the query filter), and the four
nullable audit columns (from `AuditableEntity`). Every table's `{Entity}Id` is `long`, identity.
`string(n)` means `[MaxLength(n)]`. `money` means `decimal(18,4)`. Every approval-bearing row carries
the approval summary described under "Approvals" (`ApprovalStatus`, `CurrentStepLabel`,
`CurrentApproverEmployeeId`) and does not repeat it; the steps themselves are `ApprovalSteps` rows.

### `hrm` — organisation

**Department** / **Designation** / **Grade** / **CostCentre** — `Code string(20)` unique per OrgId,
`Name string(100)`, `IsActive bool`.

- Department also has `HeadEmployeeId long?` and `ParentDepartmentId long?` (no cycles).
- Grade also has `SortOrder int` — leave policies, claim limits and salary structures key off it.

**WorkLocation**

| Column | Type | Rules |
|---|---|---|
| Code | string(20) | Unique per OrgId |
| Name | string(100) | |
| StateId | int | Unenforced — `mst.States`. Drives PT and LWF |
| AddressLine1 / City | string(200) / string(100) | |
| Latitude / Longitude | decimal(9,6)? | For the mobile check-in geo-fence |
| GeoFenceMetres | int? | Null means no fence |

### `hrm` — employee

**Employee**

| Column | Type | Rules |
|---|---|---|
| EmployeeCode | string(30) | Numbering series `EMP`. Unique per OrgId, never reused |
| FirstName / MiddleName / LastName | string(100) | Middle is optional. Any script |
| DateOfBirth | DateOnly | 14 or older at joining |
| Gender | enum | Male, Female, Other, NotStated |
| MaritalStatus | enum | Single, Married, Widowed, Divorced, NotStated |
| BloodGroup | string(5)? | |
| DepartmentId / DesignationId / GradeId | long | FK |
| WorkLocationId | long | FK |
| CostCentreId | long? | FK |
| ReportsToEmployeeId | long? | FK, self. No cycles — checked in C# |
| JoiningDate | DateOnly | |
| ProbationEndDate | DateOnly? | Confirmation is due on it |
| ConfirmationDate | DateOnly? | |
| NoticePeriodDays | int | Defaults from the grade |
| EmploymentType | enum | Permanent, Probation, Contract, PartTime, Intern, Consultant |
| EmployeeStatus | enum | Onboarding, Active, OnNotice, Exited |
| ExitDate | DateOnly? | Required at Exited |
| UserId | Guid? | Unenforced — `mst.Users`. Unique per OrgId when set |
| WorkEmail / PersonalEmail | string(255)? | |
| Phone | string(20) | Phone rule |
| Pan | string(10)? | Format-checked. Masked on lists |
| Aadhaar | string(12)? | Masked everywhere except its own edit field |
| Uan | string(12)? | PF Universal Account Number |
| PfNumber / EsiNumber | string(30)? | |
| IsPfApplicable / IsEsiApplicable / IsPtApplicable / IsLwfApplicable | bool | Per-employee overrides of the branch's statutory settings |
| PayGroupId | long? | Unenforced — `pay.PayGroups` |
| PhotoAttachmentKey | string(500)? | `IFileStorage` |

**EmployeeAddress** — `EmployeeId`, `AddressKind` (enum: Current, Permanent), `AddressLine1`/
`AddressLine2 string(200)`, `City string(100)`, `StateId int`, `PostalCode string(10)`. One per kind.

**EmployeeContact** (emergency) — `EmployeeId`, `Name string(200)`, `Relationship` (enum), `Phone
string(20)`, `IsPrimary bool`.

**EmployeeFamilyMember** — `EmployeeId`, `Name string(200)`, `Relationship` (enum: Spouse, Child,
Father, Mother, Sibling, Other), `DateOfBirth DateOnly?`, `IsDependent bool`, `IsEsiCovered bool`.

**EmployeeNominee** — `EmployeeId`, `FamilyMemberId long`, `NominationKind` (enum: Pf, Gratuity,
Insurance), `SharePercent money`. The shares for one kind add up to 100.

**EmployeeEducation** — `EmployeeId`, `Qualification string(100)`, `Institution string(200)`,
`YearOfPassing int`, `Grade string(20)?`.

**PreviousEmployment** — `EmployeeId`, `Employer string(200)`, `FromDate`/`ToDate DateOnly`,
`LastDesignation string(100)?`. The tax figures from a previous employer this year are in
`pay.PreviousEmployerIncome`, not here.

**EmployeeBankDetail** — `EmployeeId`, `AccountHolder string(200)`, `AccountNo string(30)`, `Ifsc
string(11)`, `BankName string(100)`, `IsPrimary bool`. Exactly one primary; salary goes there.

**EmploymentHistory** — `EmployeeId`, `EffectiveDate DateOnly`, `ChangeKind` (enum: Joined,
Confirmed, Promotion, Transfer, Redesignation, GradeChange, ManagerChange, Exit), and the new
`DepartmentId`, `DesignationId`, `GradeId`, `WorkLocationId`, `ReportsToEmployeeId`, `Remarks
string(500)?`. Appended, never updated: the employee row is the present, this is the past.

**EmployeeDocument** — `EmployeeId`, `DocumentKind` (enum: Pan, Aadhaar, Passport, Resume,
OfferLetter, Certificate, Other), `AttachmentKey string(500)`, `ValidUntil DateOnly?`. An expiring
document raises a reminder 30 days out.

**AssetIssue** — `EmployeeId`, `AssetName string(200)`, `AssetTag string(30)?`, `IssuedDate DateOnly`,
`ReturnedDate DateOnly?`, `RecoveryAmount money?`. An unreturned asset blocks exit clearance and can
become an F&F deduction.

### `hrm` — lifecycle, letters, announcements

**ChecklistTemplate** + **ChecklistTemplateItem** — `Name string(100)`, `ChecklistKind` (enum:
Onboarding, Exit); items `Title string(200)`, `OwnerRole` (enum: Hr, Manager, It, Finance, Admin),
`SortOrder int`.

**EmployeeChecklist** + **EmployeeChecklistItem** — one per employee per kind, copied from the
template; items carry `IsDone bool`, `DoneDate DateOnly?`, `Remarks string(500)?`.

**Separation**

| Column | Type | Rules |
|---|---|---|
| EmployeeId | long | FK. One open separation per employee |
| SeparationKind | enum | Resignation, Termination, Retirement, Death, EndOfContract, Absconding |
| RequestDate | DateOnly | |
| LastWorkingDate | DateOnly | Defaults to request + notice period |
| NoticeShortfallDays | money | Computed; feeds notice recovery in F&F |
| IsNoticeWaived | bool | |
| Reason | string(500) | |
| ExitInterviewNotes | string(2000)? | |
| SeparationStatus | enum | Submitted → Approved → ClearancePending → Cleared → Settled; Withdrawn from Submitted or Approved |

At `Settled`, the employee becomes `Exited`, the linked login's HRMS roles are deactivated and its
refresh-token families revoked (owner's recommendation, recorded as decided).

**Letters** are print templates, not tables: offer, appointment, confirmation, increment,
experience, relieving, and F&F statement. The document types are added to `DocumentTypeCatalog`; the
issued copy is archived by `SourceType` + `SourceId` like every other printed document.

**Announcement** — `Title string(200)`, `Body string(4000)`, `PublishDate`/`ExpiryDate DateOnly`,
`Audience` (enum: Everyone, Department, Location, Grade), `AudienceRefId long?`, `IsPinned bool`.

**PolicyDocument** — `Title string(200)`, `AttachmentKey string(500)`, `EffectiveDate DateOnly`,
`IsAcknowledgementRequired bool`; **PolicyAcknowledgement** — `PolicyDocumentId`, `EmployeeId`,
`AcknowledgedAt DateTimeOffset`.

### `tla` — time and attendance

**HolidayList** + **Holiday** — a list per `WorkLocationId` per calendar year; holidays carry
`HolidayDate DateOnly` (unique per list), `Name string(100)`, `IsOptional bool`.
`MaxOptionalPerYear int` on the list caps how many optional holidays an employee may take.

**Shift**

| Column | Type | Rules |
|---|---|---|
| Code | string(20) | Unique per OrgId |
| StartTime / EndTime | TimeOnly | End before start means the shift crosses midnight |
| BreakMinutes | int | |
| GraceInMinutes / GraceOutMinutes | int | Late-in and early-out tolerance |
| HalfDayBelowMinutes | int | Worked time under this is a half day |
| AbsentBelowMinutes | int | Worked time under this is absent |
| IsNightShift | bool | |

**WeeklyOffPolicy** — `Name string(100)` and seven day rules, each (enum: Working, Off,
AlternateOff, HalfDay), with `AlternateWeeks string(20)?` such as `2,4` for second and fourth
Saturdays.

**ShiftRoster** — `EmployeeId`, `FromDate`/`ToDate DateOnly`, `ShiftId`, `WeeklyOffPolicyId`. No two
rows for one employee overlap. With no roster row, the employee's grade default applies.

**Punch** — `EmployeeId`, `PunchedAt DateTimeOffset`, `PunchSource` (enum: Biometric, Mobile, Web,
Import), `DeviceCode string(50)?`, `Latitude`/`Longitude decimal(9,6)?`, `IsInsideFence bool?`.
Raw and append-only. Biometric devices map to employees through **BiometricDeviceUser**
(`DeviceCode`, `DeviceUserId string(30)`, `EmployeeId`).

**DailyAttendance**

| Column | Type | Rules |
|---|---|---|
| EmployeeId | long | Unique per date |
| AttendanceDate | DateOnly | |
| ShiftId | long? | From the roster in force |
| FirstIn / LastOut | DateTimeOffset? | Derived from punches |
| WorkedMinutes / LateMinutes / EarlyOutMinutes / OvertimeMinutes | int | Derived |
| AttendanceStatus | enum | Present, Absent, HalfDay, OnLeave, Holiday, WeeklyOff, OnDuty, CompOff |
| AttendanceSource | enum | Derived, Manual, Regularised |
| IsLocked | bool | Set when the payroll run covering the date posts |

Derived by a hosted service in `TimeLeave.Api` from punches, roster, holidays and approved leave. It
recomputes a day whenever any of those change, until the day is locked.

**RegularisationRequest** — `EmployeeId`, `AttendanceDate DateOnly`, `RequestedIn`/`RequestedOut
DateTimeOffset?`, `RequestedStatus` (enum), `Reason string(500)`, approval columns. Approval rewrites
the day with source `Regularised`.

**OvertimeRequest** — `EmployeeId`, `AttendanceDate DateOnly`, `Minutes int`, `OvertimeRate money`
(multiplier, e.g. 2.0), approval columns. Only approved overtime is paid.

**CompOffCredit** — `EmployeeId`, `EarnedDate DateOnly`, `Days money`, `ExpiryDate DateOnly`,
`AvailedDays money`. Earned by an approved day worked on a holiday or weekly off.

### `tla` — leave

**LeaveType**

| Column | Type | Rules |
|---|---|---|
| Code | string(10) | `CL`, `SL`, `EL`, `ML`, `PL`, `LOP`, `COFF`. Unique per OrgId |
| Name | string(100) | |
| IsPaid | bool | Unpaid leave is loss of pay in payroll |
| IsHalfDayAllowed | bool | |
| IsAttachmentRequiredAboveDays | money? | E.g. a medical certificate for sick leave over 2 days |
| Gender | enum? | Maternity and paternity leave |

**LeavePolicy** — which rules a leave type follows, for a grade and location, from a date:

| Column | Type | Rules |
|---|---|---|
| LeaveTypeId | long | FK |
| GradeId / WorkLocationId | long? | Null means all |
| EffectiveFrom | DateOnly | The most specific policy in force applies |
| AnnualQuota | money | Days |
| AccrualKind | enum | Upfront, Monthly, Quarterly |
| IsProratedOnJoining | bool | |
| CarryForwardKind | enum | Lapse, CarryForward, Encash |
| MaxCarryForward | money? | |
| MaxEncashPerYear | money? | |
| MinDaysPerApplication / MaxDaysPerApplication | money? | |
| NoticeDays | int | How far ahead it must be applied for |
| IsSandwichRule | bool | Holidays and weekly offs between leave days count as leave |
| CanApplyInProbation | bool | |

**LeaveBalance** — `EmployeeId`, `LeaveTypeId`, `LeaveYear int`, `Opening`/`Accrued`/`Taken`/
`Encashed`/`Lapsed`/`Adjusted money`. Unique triple. `Taken` moves on approval with a guarded
conditional update, so two approvals cannot both spend the last day. Accrual and year-end rollover
run in the same hosted service, claiming each employee-year once.

**LeaveApplication**

| Column | Type | Rules |
|---|---|---|
| EmployeeId / LeaveTypeId | long | FK |
| FromDate / ToDate | DateOnly | To ≥ From; notice and min/max days from the policy |
| FromHalf / ToHalf | enum | Full, FirstHalf, SecondHalf |
| Days | money | Computed on write, honouring holidays, weekly offs and the sandwich rule |
| Reason | string(500) | |
| AttachmentKey | string(500)? | When the policy requires one |
| LeaveStatus | enum | Draft → Submitted → Approved / Rejected; Cancelled from Draft, Submitted, or Approved before it starts |

**LeaveEncashment** — `EmployeeId`, `LeaveTypeId`, `LeaveYear int`, `Days money`, approval columns,
`PayrollRunId long?`. Paid through the next run.

### `pay` — salary setup

**PayGroup** — `Code string(20)`, `Name string(100)`, `PayFrequency` (enum: Monthly), `PayDay int`,
`AttendanceCutoffDay int`. Employees are paid by group, so contract staff can run apart from
permanent staff.

**SalaryComponent**

| Column | Type | Rules |
|---|---|---|
| Code | string(20) | `BASIC`, `HRA`, `SPL`, `CONV`, `PF_EE`, `PF_ER`, `ESI_EE`, `ESI_ER`, `PT`, `LWF_EE`, `TDS`, `LOAN`, `ADV` |
| Name / PayslipLabel | string(100) | |
| ComponentKind | enum | Earning, Deduction, EmployerContribution, Reimbursement |
| CalculationKind | enum | Fixed, PercentOfBasic, PercentOfGross, PercentOfCtc, Formula, Statutory, Balancing |
| Value | money? | For Fixed and Percent kinds |
| Formula | string(500)? | For `Formula` only. A small, whitelisted expression over other component codes — evaluated, never executed |
| IsTaxable | bool | |
| IsPartOfPfWage / IsPartOfEsiWage | bool | Which earnings the statutory rates apply to |
| IsProratedByAttendance | bool | Reduced for loss-of-pay days |
| IsVisibleOnPayslip | bool | |
| LedgerAccountId | long | Unenforced — `acc.Accounts`. Expense for earnings and employer contributions, liability for deductions |
| SortOrder | int | |

**SalaryStructure** + **SalaryStructureLine** — a named, grade-linked set of components with their
values. Exactly one line may be `Balancing` (usually Special Allowance): it takes whatever is left of
the CTC.

**EmployeeSalary** — `EmployeeId`, `SalaryStructureId`, `EffectiveFrom DateOnly`, `AnnualCtc money`,
and **EmployeeSalaryLine** with the computed monthly amount per component. A revision is a new row
with a later date; payroll reads the row in force for each day of the month, so a mid-month revision
pays both rates.

**SalaryRevision** — `EmployeeId`, `EffectiveFrom DateOnly`, `OldCtc`/`NewCtc money`,
`RevisionReason` (enum: Appraisal, Promotion, Correction, Market), `PerformanceReviewId long?`,
approval columns. Approval writes the new `EmployeeSalary`. A back-dated effective date makes the next
run pay **arrears** for every posted month since, as separate payslip lines.

**OneTimePayment** — `EmployeeId`, `SalaryComponentId`, `PayMonth DateOnly`, `Amount money`, `Remarks
string(200)?`. Bonus, incentive, one-off deductions.

**SalaryHold** — `EmployeeId`, `FromMonth DateOnly`, `ReleasedInRunId long?`, `Reason string(200)`.
Held pay is computed and posted but not paid until released.

**EmployeeLoan** — `EmployeeId`, `LoanKind` (enum: Loan, SalaryAdvance), `LoanDate DateOnly`,
`Principal money`, `InterestRate money`, `InstalmentAmount money`, `StartMonth DateOnly`,
`Outstanding money`, `LoanStatus` (enum: Active, Closed, WrittenOff), approval columns.
**LoanRepayment** records each deduction or manual repayment.

**MonthlyAttendanceInput** — the paid-days source for a customer **without** HRMS:

| Column | Type | Rules |
|---|---|---|
| EmployeeId | long | Unenforced — `hrm.Employees`. Unique per pay month |
| PayMonth | DateOnly | First of the month |
| PaidDays / LopDays | money | Paid + LOP ≤ days in the month |
| OvertimeHours | money | Paid at the component's overtime rate |
| LeaveEncashDays | money | Paid in this month's run |
| InputSource | enum | Manual, Import |
| IsLocked | bool | Set when the run covering the month posts |

Entered on a monthly grid or imported from a spreadsheet. With HRMS licensed, the grid is read-only
and shows what `tla` supplied. A run records its `PaidDaysSource` (enum: TimeLeave, MonthlyInput).

### `pay` — statutory

All settings are **effective-dated rows per OrgId** (the Tax Master pattern), seeded with the rates in
force.

**PfSetting** — `EffectiveFrom`, `WageCeiling money` (15,000), `EmployeeRate` (12%), `EmployerEpfRate`
(3.67%), `EmployerEpsRate` (8.33%), `EdliRate`, `AdminChargeRate money`, `IsRestrictToCeiling bool`,
`EstablishmentCode string(30)`.

**EsiSetting** — `EffectiveFrom`, `WageCeiling money` (21,000), `EmployeeRate` (0.75%),
`EmployerRate` (3.25%) money, `EmployerCode string(30)`. Contribution periods (April–September,
October–March) are respected: an employee covered at the start of a period stays covered to its end.

**ProfessionalTaxSlab** — `StateId int`, `EffectiveFrom DateOnly`, `Gender` (enum?, some states differ),
`SalaryFrom`/`SalaryTo`/`Amount money`, `Month int?` (February's higher figure in some states).
Seeded per state.

**LwfSetting** — `StateId int`, `EffectiveFrom DateOnly`, `EmployeeAmount`/`EmployerAmount money`,
`DeductionMonths string(30)` (e.g. `6,12`). Seeded per state.

**GratuitySetting** — `EffectiveFrom`, `MinYears money` (5), `DaysPerYear money` (15), `DivisorDays
money` (26), `MaxAmount money` (20,00,000), `IsMonthlyProvision bool`.

**BonusSetting** — `EffectiveFrom`, `EligibilityWageCeiling money`, `CalculationCeiling money`,
`MinPercent`/`MaxPercent money` (8.33 / 20), `MinDaysWorked int` (30).

**StatutoryReturn** — `ReturnKind` (enum: PfEcr, EsiMonthly, PtReturn, LwfReturn, Tds24Q),
`PeriodFrom`/`PeriodTo DateOnly`, `GeneratedFileKey string(500)`, `ReturnStatus` (enum: Generated,
Filed), `AcknowledgementNo string(50)?`, `FiledDate DateOnly?`. The file is generated from posted
payslips only.

### `pay` — income tax on salary

**TaxSlab** — `FinancialYear string(7)` (`2026-27`), `TaxRegime` (enum: Old, New), `AgeBand` (enum:
Below60, Senior, SuperSenior), `IncomeFrom`/`IncomeTo money`, `RatePercent money`. Seeded per year.
**TaxRule** holds the year's standard deduction, rebate limit, cess and surcharge bands.

**TaxDeclaration**

| Column | Type | Rules |
|---|---|---|
| EmployeeId | long | Unique per employee per year |
| FinancialYear | string(7) | |
| TaxRegime | enum | Old, New. Locked once the first run of the year posts, unless HR unlocks |
| DeclarationStatus | enum | Open, Submitted, ProofsOpen, ProofsSubmitted, Verified, Locked |

**TaxDeclarationLine** — `Section` (enum: S80C, S80CCD1B, S80D, S80E, S80G, S80TTA, S24B, Hra, Lta,
Other), `Description string(200)`, `DeclaredAmount`/`ProofAmount`/`ApprovedAmount money`,
`ProofAttachmentKey string(500)?`. Section limits are enforced in C# from the year's `TaxRule`.

**RentDetail** — `TaxDeclarationId`, `FromMonth`/`ToMonth DateOnly`, `MonthlyRent money`, `IsMetro
bool`, `LandlordName string(200)`, `LandlordPan string(10)?` (required above the annual limit).

**PreviousEmployerIncome** — `EmployeeId`, `FinancialYear`, `Gross`/`Exemptions`/`ProfessionalTax`/
`TdsDeducted money`.

**Monthly TDS.** Each run projects the year's taxable income (salary to date + remaining months +
declarations or approved proofs + previous employer), computes the year's tax under the chosen
regime, subtracts TDS already deducted, and spreads the rest over the remaining months.

**Form 16 Part B** and **Form 12BA** are print templates over the year's posted payslips and approved
declarations. **24Q** quarterly data is a `StatutoryReturn`. Part A comes from TRACES and is uploaded,
not generated.

### `pay` — the run

**PayrollRun**

| Column | Type | Rules |
|---|---|---|
| RunNo | string(30) | Numbering series `PAY` |
| PayGroupId | long | FK |
| PayMonth | DateOnly | First of the month. One non-reversed run per pay group per month |
| PayDate | DateOnly | |
| RunKind | enum | Regular, OffCycle, FullAndFinal |
| PaidDaysSource | enum | TimeLeave, MonthlyInput — which one the run read |
| PayrollStatus | enum | Draft → Processed → Approved → Posted → Paid; Reversed |
| TotalGross / TotalDeductions / TotalNet / TotalEmployerCost | money | Computed, stored |
| JournalId | long? | Unenforced — the posted JE |

**Payslip** — `PayrollRunId`, `EmployeeId` (unique pair), `WorkingDays`/`PaidDays`/`LopDays`/
`ArrearDays money`, `Gross`/`Deductions`/`Net`/`EmployerCost money`, `IsHeld bool`, `BankDetailId
long?`, `EmailedAt DateTimeOffset?`.

**PayslipLine** — `SalaryComponentId`, `Amount money`, `LineKind` (enum: Regular, Arrear, OneTime,
Reimbursement, LoanRecovery, Statutory). The component's `Name`, `ComponentKind` and `LedgerAccountId`
are **snapshotted** on the line, so a later rename or remapping never rewrites an old payslip.

**Lifecycle.**

- **Process** reads paid days — from `tla` up to the pay group's cutoff when HRMS is licensed, from
  `MonthlyAttendanceInput` otherwise — salaries in force, one-time
  payments, approved claims and encashments, loan instalments and statutory settings, and fills the
  payslips. Draft and Processed runs may be re-processed.
- **Approve** needs `payroll.approve` — a different person from whoever processed it, when the branch
  says so.
- **Post** sends one journal to Accounting and locks the month's attendance.
- **Paid** is recorded when the bank file has gone and the salaries are paid.
- A posted run is never edited. A correction is a **reversal** (a line-paired reversing JE) and a new
  run.

**Posting.** Through Accounting's internal posting API:

- `Dr` each earning's and employer contribution's expense account;
- `Cr` Salary Payable (net), PF Payable, ESI Payable, PT Payable, LWF Payable, TDS Payable, Employee
  Loans (asset) and Reimbursements Payable;
- a monthly gratuity provision, when enabled: `Dr Gratuity Expense / Cr Gratuity Provision`.

Paying the salaries is a Spend Money in Accounting against Salary Payable — for a customer without RetailErp,
recorded from Payroll's **Mark paid** action, which calls the same Accounting API.

**Outputs.**

- **Bank advice and bulk transfer file**, per bank format (**BankFileFormat**: `Code`, `Name`,
  `FileKind` enum: Csv, FixedWidth, Xlsx, and a column map). Generated from held-excluded payslips.
- **Payslips** as a print template, emailed as PDF on posting when the branch says so.
- **Journal export**: the posted journal in **Tally XML** and **CSV**, for an accountant outside the
  product.

### `pay` — full & final settlement

**FullAndFinalSettlement** — `EmployeeId`, `SeparationId long` (unenforced — `hrm`), `LastWorkingDate
DateOnly`, `SettlementStatus` (enum: Draft, Approved, Posted, Paid), `PayrollRunId long?`, approval
columns. **FnfLine** — `FnfLineKind` (enum: SalaryToLwd, LeaveEncashment, Gratuity, Bonus, Arrears,
Reimbursement, NoticeRecovery, LoanRecovery, AssetRecovery, Tds, Other), `Amount money`, `IsDeduction
bool`.

It pays through an off-cycle run of kind `FullAndFinal`, so it posts the same way every salary does.
The statement is a print template. Posting it moves the separation to `Settled`.

### `rec` — recruitment

**JobRequisition** — `DepartmentId`, `DesignationId`, `GradeId`, `WorkLocationId` (unenforced),
`Openings int`, `EmploymentType` (enum), `MinCtc`/`MaxCtc money`, `Justification string(1000)`,
`IsReplacement bool`, `ReplacesEmployeeId long?`, approval columns.

**JobOpening** — `JobRequisitionId`, `Title string(200)`, `Description string(4000)`, `OpeningStatus`
(enum: Draft, Open, OnHold, Closed, Filled), `PublishedDate`/`ClosingDate DateOnly?`.

**Candidate** — `FirstName`/`LastName string(100)`, `Email string(255)`, `Phone string(20)`,
`CurrentEmployer string(200)?`, `CurrentCtc`/`ExpectedCtc money?`, `NoticePeriodDays int?`,
`CandidateSource` (enum: Portal, Referral, Agency, CareersPage, WalkIn), `ReferredByEmployeeId long?`,
`ResumeAttachmentKey string(500)?`. Unique on email per OrgId.

**Application** — `JobOpeningId`, `CandidateId` (unique pair), `Stage` (enum: Applied, Screening,
Interview, Offer, Hired, Rejected, Withdrawn), `RejectionReason string(500)?`.

**InterviewRound** — `ApplicationId`, `RoundNo int`, `RoundKind` (enum: Telephonic, Technical, Hr,
Managerial), `ScheduledAt DateTimeOffset`, `InterviewerEmployeeId long`, `Rating int?` (1–5),
`Feedback string(2000)?`, `Outcome` (enum: Pending, Pass, Fail, NoShow).

**Offer** — `ApplicationId`, `OfferedCtc money`, `SalaryStructureId long`, `JoiningDate DateOnly`,
`OfferStatus` (enum: Draft, Approved, Sent, Accepted, Declined, Revoked), approval columns. The letter
is a print template.

**Accepting an offer** creates the employee through the Hrm API (status `Onboarding`), their
`EmployeeSalary` through Payroll, and the onboarding checklist. It is idempotent on the application
id, so a retry creates one employee.

### `prf` — performance and appraisal

**The flow of one appraisal:**

```
Goal setting → Self-evaluation → Approval levels (configurable: e.g. Lead → Project Lead → Manager → HR)
            → Calibration (optional) → Released to employee → Acknowledged → Salary revision (Payroll)
```

**ReviewCycle**

| Column | Type | Rules |
|---|---|---|
| Name | string(100) | "FY 2026-27 Annual" |
| PeriodFrom / PeriodTo | DateOnly | The performance period being assessed |
| CycleKind | enum | Annual, HalfYearly, Quarterly, Probation |
| RatingScaleId | long | FK |
| GoalWeightPercent / CompetencyWeightPercent | money | Add to 100; the final score blends the two |
| GoalSettingDueDate / SelfEvaluationDueDate / ReviewDueDate | DateOnly | Deadlines per phase |
| IsSelfEvaluationRequired | bool | When false, the appraisal starts at level 1 |
| IsPeerFeedbackEnabled | bool | |
| IsCalibrationEnabled | bool | |
| CycleStatus | enum | Draft, GoalSetting, SelfEvaluation, InReview, Calibration, Released, Closed |

**Eligibility** — employees joined before a cut-off date and not on notice are enrolled when the
cycle opens; HR can add or remove anyone. Each enrolment is one **PerformanceReview**.

**RatingScale** + **RatingLevel** — `Score int`, `Label string(50)`, `Description string(200)`
(e.g. 1 Needs improvement … 5 Outstanding).

**Competency** + **CompetencyGroup** — the behaviours rated beside goals ("Ownership", "Teamwork"),
each with a description per rating level; **CycleCompetency** picks which apply to a cycle, per grade.

**Goal** — `ReviewCycleId`, `EmployeeId`, `Title string(200)`, `Description string(1000)?`,
`Weightage money` (weights add to 100 per employee per cycle), `Measure string(200)?`, `Target
string(200)?`. Set by the employee or the manager; approved through the same chain as the appraisal
before self-evaluation opens, when the cycle says so.

**PerformanceReview** — one per employee per cycle:

| Column | Type | Rules |
|---|---|---|
| ReviewCycleId / EmployeeId | long | Unique pair |
| ReviewStatus | enum | NotStarted, SelfEvaluationDraft, SelfEvaluationSubmitted, InApproval, SentBack, Calibration, Released, Acknowledged, Closed |
| SelfSubmittedAt | DateTimeOffset? | |
| FinalGoalScore / FinalCompetencyScore / FinalScore | money? | Computed from the last level that rated |
| FinalRatingLevelId | long? | The score mapped onto the scale; changed only by a level with `CanEdit`, or by calibration |
| RecommendedIncreasePercent | money? | |
| IsPromotionRecommended | bool | |
| RecommendedDesignationId | long? | |
| ReleasedAt / AcknowledgedAt | DateTimeOffset? | |
| EmployeeAcknowledgementComment | string(2000)? | The employee may record disagreement; it does not reopen the review |

Plus the approval summary columns (Approvals, above).

#### Self-evaluation

**The employee assesses themselves first**, and every approval level sees what they wrote.

**SelfEvaluation** — one per review:

| Column | Type | Rules |
|---|---|---|
| PerformanceReviewId | long | Unique |
| OverallSelfRatingLevelId | long? | Required on submit when the cycle asks for an overall rating |
| Achievements | string(4000) | "What did you achieve this period?" — required on submit |
| Challenges | string(4000)? | |
| Strengths | string(2000)? | |
| AreasToImprove | string(2000)? | |
| TrainingNeeds | string(2000)? | Feeds a training-needs report for HR |
| CareerAspirations | string(2000)? | |
| IsSubmitted | bool | Locked once true |

**GoalSelfAssessment** — per goal: `GoalId`, `SelfRatingLevelId`, `AchievementPercent money?`,
`Comments string(2000)`, and **SelfEvidence** attachments (`AttachmentKey string(500)`, `Title
string(200)`) — the documents, reports or screenshots that back the claim.

**CompetencySelfAssessment** — per competency: `CompetencyId`, `SelfRatingLevelId`, `Comments
string(1000)?`.

The rules:

- **Draft, then submit.** The form saves as a draft as often as the employee likes; submitting
  checks every goal and competency is rated and every required text is filled, then locks it and
  starts the approval chain at level 1.
- **The deadline is soft.** After `SelfEvaluationDueDate` the form still accepts a submission, and HR
  sees who is overdue; HR can also **start the chain without it**, recorded as such, when an employee
  is on long leave.
- **Reopening** is HR's alone (`performance.reopen`), before level 1 has acted, and is logged.
- **Nobody edits the employee's words.** Approvers add their own ratings and comments beside the
  self-evaluation; they never change it.
- **Self-service only.** The employee reaches it from `apps/hrms` self-service, through
  `/api/me/appraisals/...`, resolved from the token — never from an employee id in the URL.

#### Approval levels

The chain is the **Appraisal** workflow from Approvals — for example *Lead → Project Lead → Manager →
HR* — configured per department or grade, with any number of levels, each named by the customer.

**LevelReview** — what each level recorded, kept whole, never overwritten:

| Column | Type | Rules |
|---|---|---|
| PerformanceReviewId | long | FK |
| ApprovalStepId | long | The step it belongs to; unique |
| Sequence / Label | int / string(50) | Copied from the step |
| ReviewerEmployeeId | long | |
| RatingLevelId | long? | Only when the level has `CanEdit` |
| IncreasePercent | money? | Likewise |
| IsPromotionRecommended | bool? | Likewise |
| Comments | string(4000) | Required when the level says so |
| Decision | enum | Approved, SentBack, Rejected |

**LevelGoalRating** and **LevelCompetencyRating** hold that level's per-goal and per-competency
ratings, beside the employee's own.

- **Each level sees everything before it** — the self-evaluation and every earlier level's ratings
  and comments — side by side, and rates without being able to change what earlier levels wrote.
- **The final figures come from the last level that rated.** A level without `CanEdit` (HR, say, as
  a sign-off) approves or sends back only.
- **Send back** returns the review to the previous level — or to the employee to revise their
  self-evaluation, from level 1 — with a mandatory comment, and the chain resumes from there.
- **Peer feedback**, when enabled, is gathered in parallel with the chain (**PeerFeedback**:
  reviewer, comments, optional rating), visible to every level and never to the employee by name.

#### Calibration, release and outcome

- **Calibration** (optional): HR sees the rating distribution per department against **CalibrationGuide**
  targets (e.g. 10% / 20% / 40% / 20% / 10%) and may move final ratings, each move recorded in
  **CalibrationAdjustment** (from, to, reason, by whom).
- **Release**: HR releases a department or the whole cycle; only then does the employee see the final
  rating, the comments each level chose to share, and the increment.
- **Acknowledgement**: the employee acknowledges, optionally recording disagreement.
- **Outcome**: closing the cycle raises a `SalaryRevision` in Payroll for each recommended increase —
  itself approved through the SalaryRevision chain — and, when a promotion is recommended and approved,
  a Promotion entry in `hrm.EmploymentHistory`. The increment letter is a print template. When Payroll
  is not licensed, the increment is recorded on the review and nothing is sent.

### `clm` — expense claims

**ClaimCategory** — `Code string(20)`, `Name string(100)`, `IsReceiptRequired bool`,
`LedgerAccountId long` (unenforced), `IsTaxable bool`.

**ClaimLimit** — `ClaimCategoryId`, `GradeId long?`, `LimitPeriod` (enum: PerClaim, Monthly, Yearly),
`Amount money`.

**ExpenseClaim** — `ClaimNo string(30)` (series `CLM`), `EmployeeId`, `ClaimDate DateOnly`,
`TotalAmount`/`ApprovedAmount money`, `ClaimStatus` (enum: Draft, Submitted, Approved, Rejected, Paid),
`PayoutMode` (enum: Payroll, Direct), `PayrollRunId long?`, `SpendMoneyId long?`, approval columns.

**ExpenseClaimLine** — `ClaimCategoryId`, `ExpenseDate DateOnly`, `Description string(500)`, `Amount
money`, `ReceiptAttachmentKey string(500)?`. Limits are checked on submit; the excess is refused, not
warned.

Paid either on the next payslip (`PayoutMode.Payroll`, as a `Reimbursement` line) or directly as a
Spend Money in Accounting (`Direct`). The branch sets the default.

## Approvals

**Every approval is a chain of levels the customer configures** (owner's decision, 23 September
2026): how many levels, in what order, what each is called, and who stands at each. An appraisal
might go *Lead → Project Lead → Manager → HR*; leave might go *Manager* alone; a claim above ₹10,000
might add *Finance*. Nothing about the number or names of levels is fixed in code.

### Configuration

**ApprovalWorkflow** — one chain for one kind of request, for a group of employees:

| Column | Type | Rules |
|---|---|---|
| Name | string(100) | "Engineering appraisal", "Default leave" |
| RequestKind | enum | Leave, Regularisation, Overtime, CompOff, LeaveEncashment, Appraisal, SalaryRevision, Loan, JobRequisition, Offer, Claim, Separation, FullAndFinal |
| DepartmentId / GradeId / WorkLocationId | long? | Who it applies to. Null means all. The most specific match wins; one fallback workflow per kind (all three null) is required |
| EffectiveFrom | DateOnly | A changed chain applies to requests submitted from this date; requests already in flight keep theirs |
| IsActive | bool | |

**ApprovalWorkflowLevel** — the levels, in order:

| Column | Type | Rules |
|---|---|---|
| ApprovalWorkflowId | long | FK |
| Sequence | int | 1, 2, 3…; unique per workflow |
| Label | string(50) | What the customer calls the level: "Lead", "Project Lead", "Manager", "HR" |
| ApproverKind | enum | How the person is found — see below |
| ReportingDepth | int? | For `ReportingChain`: 1 = direct manager, 2 = their manager, … |
| RelationshipTypeId | long? | For `Relationship`: which named relation ("Lead", "Project Lead") |
| RoleId | int? | For `RoleHolder`: any holder of this role in the branch, e.g. HR Admin |
| EmployeeId | long? | For `NamedEmployee`: one fixed person |
| AboveAmount | money? | The level applies only when the request's amount exceeds this (claims, loans, revisions) |
| IsOptional | bool | Skipped, not blocked, when no approver can be resolved |
| CanEdit | bool | Whether this level may change the request (for an appraisal: ratings and increment), or only approve, reject or send back |
| IsCommentRequired | bool | |
| EscalateAfterDays | int? | Past this, the step is routed to the approver's own manager and both are notified |

**ApproverKind** (enum):

| Value | Finds | Typical label |
|---|---|---|
| `ReportingChain` | Walks `ReportsToEmployeeId` up `ReportingDepth` steps | Manager, Skip-level manager |
| `Relationship` | The person named for this employee under a relationship type (below) | Lead, Project Lead, Mentor |
| `DepartmentHead` | `Department.HeadEmployeeId` of the employee's department | Department Head |
| `RoleHolder` | Anyone holding the role in the branch — the first to act takes it | HR, Finance |
| `NamedEmployee` | One fixed person | Principal, CFO |

**Lead and Project Lead are relationships, not the reporting line.** An employee reports to one
manager, but may also have a lead and a project lead who are not in that line. Those are held as data,
so a customer can add whatever relations their organisation uses:

- **RelationshipType** — `Code string(20)`, `Name string(50)` ("Lead", "Project Lead", "Mentor").
  Seeded with Lead and Project Lead; the customer adds more.
- **EmployeeRelationship** — `EmployeeId`, `RelationshipTypeId`, `RelatedEmployeeId`,
  `FromDate DateOnly`, `ToDate DateOnly?`. One active row per employee per type. Edited on the
  employee's page, or in bulk by import when a project team changes.

### At run time

When a request is submitted, its chain is **resolved and snapshotted**: each level becomes an
**ApprovalStep** with the actual person, so a later change of manager, lead or workflow does not move
a request already in flight.

**ApprovalStep** — `RequestKind`, `RequestId long`, `Sequence int`, `Label string(50)` (copied),
`ApproverEmployeeId long?` (null for `RoleHolder`, filled by whoever acts), `RoleId int?`,
`StepStatus` (enum: Waiting, Pending, Approved, Rejected, SentBack, Skipped, Escalated),
`ActedByUserId Guid?`, `ActedAt DateTimeOffset?`, `Comments string(2000)?`, `DueDate DateOnly?`.

The rules:

- **One step is `Pending` at a time**, in sequence; the rest wait. Approving moves to the next level;
  the last approval approves the request.
- **Reject** ends the request. **Send back** returns it to the previous level — or to the employee
  from level 1 — with a mandatory comment, and the chain resumes from there.
- **Skip rules**, applied when the chain is resolved: a level whose approver cannot be found and is
  `IsOptional` is `Skipped`; a level whose approver is the requester, or the same person as the level
  before, is `Skipped` so nobody approves twice or approves their own request. A required level that
  cannot be resolved blocks submission, and says which level and why.
- **Delegation**: **ApprovalDelegate** (`EmployeeId`, `DelegateEmployeeId`, `FromDate`/`ToDate`) —
  while it is active, the delegate acts in the approver's place, and the step records both.
- **Escalation** runs in the owning service's hosted service, claiming each overdue step once with a
  guarded status update.
- **The request row keeps a summary** for lists: `ApprovalStatus` (enum: Draft, InApproval, Approved,
  Rejected), `CurrentStepLabel string(50)?`, `CurrentApproverEmployeeId long?`.

### Where it lives

- **Configuration and resolution belong to `Hrm`**, which owns the employees, the reporting line and
  the relationships the chain is resolved from. It serves
  `POST internal/approval-chains/resolve` (request kind, employee, amount) → the list of steps.
- **Steps are stored by the service that owns the request** — `tla` for leave, `prf` for appraisals,
  `clm` for claims — in its own `ApprovalSteps` table, mapped from one shared shape in
  `Shared.Kernel.Approvals` (base entity, state machine and refusal messages), so every service
  moves steps the same way and none reads another's tables (rule 8).
- **One approvals inbox** in `apps/hrms` (and `apps/payroll` for its own kinds) lists "waiting on me"
  by asking each service's `GET /api/approvals/mine`. An approver sees only steps routed to them or
  to a role they hold; HR Admin sees all.
- **The configuration screen** — Settings › Approval workflows — lists workflows per request kind;
  levels are added, removed and reordered by drag, each with its label and approver kind.

## Reports

Reporting sources, each flagged with the app it serves — Payroll for the pay and statutory groups, HRMS for the rest — on the existing grid, filters and Excel/CSV writers:

- **People**: headcount by department, location and grade; joiners and leavers; attrition rate;
  probation due; birthdays and work anniversaries; document expiry.
- **Time**: daily attendance; monthly muster roll; late and early-out; overtime; regularisations.
- **Leave**: leave register; balances; encashment; team availability.
- **Pay**: salary register; payslip summary; CTC report; month-on-month variance; bank advice; held
  salaries; arrears; loans outstanding.
- **Statutory**: PF, ESI, PT and LWF statements; gratuity provision; bonus register; TDS summary and
  projection.
- **Recruitment**: pipeline by stage; time to hire; source effectiveness; offer acceptance.
- **Claims**: claims by category and employee; pending approvals.
- **Registers under the Shops and Establishments Acts** — muster roll and wages register — as print
  templates over the same data.

## Endpoints

The same shape on every controller, in this product and in School: `[Authorize]` and
`[RequireModulePermission("{module}")]` at class level. The tenant is checked against the token,
cross-org access returns `Forbid()` (never `NotFound()`), and `[RequireApp(...)]` names the apps a controller serves — `Hrms`, `Payroll`, or both for the employee master.

| Route | Method | Action |
|---|---|---|
| `GET /api/{resource}` | `GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, …filters)` | `{module}.view` |
| `GET /api/{resource}/{id:long}` | `GetById(long id)` | `{module}.view` |
| `POST /api/{resource}` | `Create([FromBody] Create{X}Request request)` | `{module}.create` |
| `PUT /api/{resource}/{id:long}` | `Update(long id, [FromBody] Update{X}Request request)` | `{module}.edit` |
| `POST /api/{resource}/{id:long}/{verb}` | lifecycle action | `[PermissionAction("{verb}")]` |

There is no DELETE on a document row, the same as the rest of the product; masters deactivate.

- **Lifecycle verbs**: `submit`, `approve`, `reject`, `cancel`, `withdraw` on every request;
  `process`, `approve`, `post`, `reverse`, `markpaid` on a run; `accept`, `decline` on an offer;
  `lock` and `unlock` on a tax declaration and on attendance.
- **Self-service routes** under `/api/me/...` — profile, payslips, Form 16, tax declaration, leave,
  attendance, punches, claims, documents, announcements — each guarded by its own app (payslips and
  tax by Payroll, the rest by HRMS), resolve the employee from the token's `sub`
  → `Employee.UserId`. They never take an employee id from the URL.
- **Manager routes** under `/api/team/...` return only the caller's direct and indirect reports.

**Permission modules** and the apps they belong to:

| Module | Apps |
|---|---|
| `employee` — the employee master and organisation setup | HRMS, Payroll, School |
| `hrm` — lifecycle, letters, assets, announcements | HRMS |
| `timeleave`, `recruitment`, `performance`, `claims`, `team`, `selfservice` | HRMS |
| `payroll`, `statutory`, `incometax`, `payselfservice` | Payroll |

**System roles**:

- **HRMS**: Owner, HR Admin, HR Executive, Recruiter, Manager (the `team` module, approvals routed to
  them), Employee (`selfservice` only).
- **Payroll**: Owner, Payroll Admin, Payroll Executive, Employee (`payselfservice` only — payslips,
  Form 16, tax declarations).

A person using both apps holds one role in each.

## Frontend

- **Two apps on the same shell**, each with `libs/shared/{auth, api-client, ui-components,
  currency-format, theming}` and the shared customer, branch, user and role screens from the Platform
  section:
  - **`apps/hrms`**, `APP_ID = Hrms`.
  - **`apps/payroll`**, `APP_ID = Payroll`.
- **Libs**, one pair per service: `libs/hrm/*`, `libs/time-leave/*`, `libs/payroll/*`,
  `libs/recruitment/*`, `libs/performance/*`, `libs/claims/*`. **The employee master pages live in
  `libs/hrm/hrm-ui` and are mounted by both apps**; the menu decides which of them each app shows.
- **Audiences, chosen by role rather than by further apps:**
  - `apps/hrms` — HR (full menus); Manager (team calendar, team attendance, approvals inbox);
    Employee (profile, leave, attendance and punch-in, claims, documents, announcements).
  - `apps/payroll` — Payroll Admin and Executive (full menus); Employee (payslips, Form 16, tax
    declaration and proofs).
  - The employee screens are laid out mobile-first in both.
- **`-core` libs stay Ionic-compatible**, so employee self-service can become a mobile app without a
  rewrite.
- **UI rules**, for this app and School alike:
  - existing shared components only — when a screen needs one `ui-components` lacks, the build stops
    and asks. The likely ones are the monthly attendance grid, the team leave calendar and the
    recruitment pipeline board;
  - pages use `.page.ts` / `.list.ts` / `.dialog.ts`, `templateUrl` + `styleUrl`, `inject()`, signals;
  - formats from `GET /api/formats`;
  - field errors above their input, rule errors in the shared message box;
  - no inline styles, `var(--color-*)` only;
  - every page works at ~360px.

## Tenancy and security

The rules below hold for every service in this product and in School.

- **The DbContext** derives from `TenantDbContext` and calls `base.OnModelCreating` — `SalesDbContext`
  once did not, and lost its query filter and `xmin` concurrency for its whole life.
- **RLS is written into each service's migration** — ENABLE, **FORCE**, and a policy on `CustomerId`
  and `OrgId` — copying the `prt` migration, the only schema that currently has RLS from a clean build.
- **Each test project links `tests/Shared/RlsAudit.cs` and runs `EndpointGuardAudit`.** The RLS test
  must be seen red against a dropped database before it counts.
- **Writes use `BeginScopeAsync`**, and every failure goes through `SqlErrorCatalog` into the
  service's own `{schema}.ErrorLogs`. No curated message names a salary figure.
- **Sensitive fields.** PAN, Aadhaar, bank account numbers, salary figures and tax declarations are
  masked on every list, and shown in full only on the detail screen to a holder of `payroll.view`
  (or to the employee, for their own).
- **Seeding at branch creation**, per licensed app:
  - either app: organisation defaults (one department, designation, grade, location) and the `EMP`
    series;
  - **HRMS**: leave types and a default policy; a default shift and weekly-off policy; claim
    categories; checklist templates; the `CLM` series;
  - **Payroll**: salary components, a default structure and pay group; statutory settings, PT and
    LWF slabs for every state, tax slabs and rules for the year; bank file formats; the `PAY` series;
  - each app's role and menu rows, and the permissions flagged with it.

## Open questions

- **Pricing.** *Recommend per active employee per month for HRMS* — the market norm — with the
  licence's `MaxUsers` read as employees for this app.
- **Biometric devices.** Which makes to support, and whether devices push to an endpoint or a
  connector pulls from them. *Recommend push, with ZKTeco and eSSL as the first two.*
- **A separate mobile app for employees.** *Recommend later*: the `-core` libs keep it open.
- **Statutory rules outside India.** *Recommend not in v1*; effective-dated rows leave room.
- **Payroll for a customer with many branches** — one run per branch, or a consolidated run across
  them. *Recommend per branch*: each branch is its own set of books and its own PF/ESI registration.

## Stages

H0 is the Platform section above. Each stage says which app it belongs to. **The first sellable
Payroll** is H0, H1, H4, H5, H6 and the settlement half of H7, plus Payroll's self-service in H8.
**The first sellable HRMS** is H0–H3, H7 and H8.

- [ ] **H1 — Core HR** *(both apps: the shared employee master)*. Organisation setup, employee master with every child table, history,
  documents, assets, announcements, policy documents.

  *Done when*: an employee is created with family, nominees and bank details, linked to a user and
  listed; RLS and the guard audit pass from a dropped database.
- [ ] **H2 — Leave** *(HRMS)*. Types, policies, accrual and rollover, balances, applications, encashment,
  approvals. **The configurable approval engine is built here**, because leave is the first request
  that needs it: workflows, levels, relationships (Lead, Project Lead), snapshotting, skip rules,
  send back, delegation, escalation — used by every request kind after it.

  *Done when*: two simultaneous approvals cannot overspend a balance; the sandwich rule counts a
  weekend between two leave days; and changing a workflow leaves requests already in flight on their
  old chain.

- [ ] **H3 — Time and attendance** *(HRMS)*. Holidays, shifts, rosters, weekly offs, punches, daily derivation,
  regularisation, overtime, comp-off, locking.

  *Done when*: a biometric import derives a late-marked half day, and a regularisation approval
  corrects it.
- [ ] **H4 — Payroll core** *(Payroll)*. Components, structures, salaries, revisions with arrears,
  one-time pay, holds, loans, the monthly attendance input, runs, payslips, posting, reversal, bank
  file, journal export.

  *Done when*: a run posts one balanced JE, Salary Payable ties to the unpaid net, a back-dated
  revision pays arrears in the next run, and a reversal restores both; the same run with no HRMS
  licence reads the monthly input, and with one reads `tla`.
- [ ] **H5 — Statutory** *(Payroll)*. PF, ESI, PT, LWF, gratuity provision, bonus, and the return files.

  *Done when*: the ECR file for a month matches the posted payslips to the rupee.
- [ ] **H6 — Income tax** *(Payroll)*. Slabs and rules, declarations and proofs, projection and monthly TDS,
  Form 16 Part B, Form 12BA, 24Q.

  *Done when*: a mid-year joiner with a previous employer's income is taxed the same under both
  a monthly run and a year-end recomputation.
- [ ] **H7 — Lifecycle and exit** *(HRMS: checklists, separation, clearance, letters; Payroll: F&F)*.
  A Payroll-only customer records the last working day on the settlement itself.

  *Done when*: settling an exit pays through a `FullAndFinal` run, and the employee's login stops
  working.
- [ ] **H8 — Self-service and approvals** *(both)*. HRMS's employee and manager screens and the
  approval inbox; Payroll's payslips, Form 16 and tax declarations.
- [ ] **H9 — Expense claims** *(HRMS)*.
- [ ] **H10 — Recruitment and onboarding** *(HRMS)*.

  *Done when*: accepting an offer twice creates one employee.
- [ ] **H11 — Performance** *(HRMS)*. Cycles, goals, competencies, self-evaluation, multi-level
  review over the configurable Appraisal chain, peer feedback, calibration, release, acknowledgement,
  and the revision and promotion it raises.

  *Done when*: a four-level chain (Lead → Project Lead → Manager → HR) configured for one department
  and a two-level one for another route their reviews differently; a level sent back returns to the
  one before it; the employee's self-evaluation is unchanged after every level has acted; and a
  manager who is also the lead is asked once, not twice.
- [ ] **H12 — Reports** *(both — each report flagged with the app it serves)*.

Each stage is built migration → seed → API → UI, and is committed to `main` with its docs page and
release-notes bullet in the same commit.
# --- School.md ---
# School — management and campus maintenance

A third product on the same platform as RetailErp, built after HRMS & Payroll. It covers running a school (students, classes,
admissions, attendance, fees, exams, a parent portal) and maintaining its campus (facilities, work
orders, preventive maintenance, AMC contracts). Staff, leave and salary are **not** here: they are
the HRMS and Payroll apps in the section before this one. School uses their shared employee
master for teachers and technicians.

**School is built after HRMS & Payroll** (owner's decision, 23 September 2026). It relies on HRMS
for three things:

- **H0** — the Platform section — builds per-app licences, sign-in, menus, signup and the shard fix.
- **H1** builds the employee master that class teachers and work-order assignees point at.
- **HRMS states the shared rules** — column conventions, endpoint shape, frontend, tenancy — and
  this section does not repeat them.

**Nothing in this section is built.** It is a design, written on the owner's instruction of
23 September 2026 to design first and build later. No stage has been started.

## Where things stand

| | |
|---|---|
| **Built** | Nothing |
| **Planned here** | Eight services, one per schema — `sis` `adm` `att` `fee` `fac` `wrk` `ppm` `amc` — an `apps/school` Nx app on the shared shell, and parent pages in `apps/portal` |
| **Depends on** | The Platform section (H0: per-app licences, sign-in, menus, signup) and HRMS H1 (the `Employee` master); the `IsGuardian` contact flag (S0, below); Accounting's internal posting API |
| **Decided** | Built after HRMS & Payroll; one service per schema; `adm` rather than reusing `cus` Leads; the existing frontend stack; `apps/school` separate from `apps/web` |
| **Waiting on the owner** | The open questions at the end of this section, and which of the uncovered features in "What this does not cover yet" join the design |

## Decisions

| Decision | Why |
|---|---|
| **One service per schema** — eight services | The owner's choice. Its cost is stated plainly under "What it costs" below |
| **Every table lives in the tenant database**, in its own three-letter schema | The same shard every other per-customer schema is in. Identity — users, roles, permissions — stays in `mst` in the master database, exactly as for RetailErp. Nothing school-specific goes in `mst` except permission and menu rows |
| **Admissions get their own schema, `adm`**, not `cus` Leads | The prompt asked for `crm`; `crm` was folded into `cus` long ago. An admission has stages a sales lead does not (application, documents, test, offer) and ends in a Student, not a Contact |
| **Keys are `{Entity}Id long`**, not a bare `Id` | The prompt said `Id`; every table in the product is `{Entity}Id` (`Lead.LeadId`, `InvoiceId`). One convention across both products. `CustomerId`, `OrgId` and `UserId` stay `Guid` |
| **`DateOnly` for dates, `DateTimeOffset` only for an exact instant** | A date of birth or an attendance date has no time zone; an audit stamp does |
| **Money, tax and quantity are `decimal(18,4)`** in Fluent config | The prompt's rule. Rounding for display comes from the branch's currency, not the column |
| **Every `string` carries `[MaxLength]`; every boolean is `Is`/`Has`/`Can`** | The prompt's rule, already the house style |
| **The existing frontend stack** — signals, async/await, shared `ui-components`, `--color-*` tokens | The prompt named RxJS, Bootstrap and FontAwesome. None of the three is the stack the other 28 pages use, and a second visual language on one shell is worse than either. No new UI library is added |
| **`apps/school` is a separate app** on `libs/app-shell` and `libs/shared/*` | The owner's choice. The shell is already app-agnostic except for the menu, which the `App` flag fixes |
| **New apps do not decode the JWT** | The prompt's rule. `libs/shared/auth/src/lib/token-claims.ts` does decode it today, for `apps/web`'s menu; the school libs instead read a backend "current context" endpoint returning names, roles and permissions without internal ids. Whether `apps/web` moves too is not part of this design |
| **A campus is an `OrgId`** | A branch is already "one place the business trades from, and one complete set of books". A second campus is a second branch, seeded the same way. No `CampusId` column — for the same reason there is no `BranchId` |
| **Students are not contacts; guardians are** | The guardian pays, receives the fee demand and signs in to the portal, so a guardian is a `con.Contact` with `IsGuardian`. A student is a `sis` row pointing at up to two guardian contacts |

The prompt referred to `docs/project-structure.md`, `docs/coding-standards.md` and
`docs/ai-agent-structure-rules.md`. **None of the three exists.** `CLAUDE.md` and
`docs/Architecture.md` are what they would have said.

## Service map

| Service | Schema | Port | Owns | Calls |
|---|---|---|---|---|
| `Sis` | `sis` | 4515 | Academic years, classes, sections, subjects, students, guardians-of-student, enrolments, exams, marks | Master (guardian contact) |
| `Admission` | `adm` | 4516 | Enquiries, applications, application documents | Sis (create student on admit), Master (create guardian contact) |
| `Attendance` | `att` | 4517 | **Student** attendance — staff attendance is HRMS | Sis (roll of a section) |
| `Fee` | `fee` | 4518 | Fee heads, structures, concessions, demands, receipts, allocations | Sis (enrolment), Accounting (posting), Master (guardian contact) |
| `Facility` | `fac` | 4519 | Buildings, spaces, facility assets | — |
| `WorkOrder` | `wrk` | 4520 | Work orders, tasks, parts used | Facility, Inventory (parts), Hrm (assignee) |
| `Preventive` | `ppm` | 4521 | Maintenance plans and their schedule | Facility, WorkOrder (generates) |
| `Amc` | `amc` | 4522 | Annual maintenance contracts, covered assets, visits | Facility, Master (vendor contact), WorkOrder (a visit may raise one) |

Every id that crosses a service is an unenforced `long`, validated in C# by calling the owning
service's API (hard rule 8). The layout is the usual three projects per service under
`backend/Api/{Service}/`, a test project per service under `backend/tests/`, and a Gateway route
set per service.

## Columns

Every table below also carries, and the tables do not repeat:
`CustomerId Guid` and `OrgId Guid` (from `OrgScopedEntity`, with the query filter), and the four
nullable audit columns (from `AuditableEntity`). Every table's `{Entity}Id` is `long`, identity.
`string(n)` means `[MaxLength(n)]`. `money` means `decimal(18,4)`.

### `sis`

**AcademicYear**

| Column | Type | Rules |
|---|---|---|
| Code | string(20) | `2026-27`. Unique per OrgId |
| StartDate / EndDate | DateOnly | End after start; years in one OrgId do not overlap |
| IsCurrent | bool | Exactly one per OrgId — filtered unique index |
| IsClosed | bool | A closed year refuses new enrolments, attendance and marks |

**SchoolClass** (Grade) — `Class` is a C# keyword, hence the name

| Column | Type | Rules |
|---|---|---|
| Code | string(20) | `LKG`, `I`, `X`. Unique per OrgId |
| Name | string(100) | |
| SortOrder | int | Promotion goes to the next SortOrder |
| IsActive | bool | |

**Section**

| Column | Type | Rules |
|---|---|---|
| AcademicYearId | long | FK |
| SchoolClassId | long | FK |
| Name | string(20) | `A`, `B`. Unique per year + class |
| Capacity | int? | Enrolment beyond it is refused, not warned |
| ClassTeacherEmployeeId | long? | Unenforced — HRMS `hrm.Employees` |
| RoomSpaceId | long? | Unenforced — `fac.Spaces` |

**Subject**

| Column | Type | Rules |
|---|---|---|
| Code | string(20) | Unique per OrgId |
| Name | string(100) | |
| SubjectKind | enum | Core, Language, Elective, CoCurricular |
| IsActive | bool | |

**Student**

| Column | Type | Rules |
|---|---|---|
| AdmissionNo | string(30) | From a numbering series `ADM`. Unique per OrgId, never reused |
| FirstName / LastName | string(100) | Tamil and other scripts allowed — `varchar` counts characters |
| DateOfBirth | DateOnly | |
| Gender | enum | Male, Female, Other, NotStated |
| AdmissionDate | DateOnly | |
| StudentStatus | enum | Active, Alumni, Withdrawn, Transferred |
| LeavingDate | DateOnly? | Required when status leaves Active |
| BloodGroup | string(5)? | |
| NationalId | string(20)? | APAAR / Aadhaar. Stored, masked on every list screen |
| PhotoAttachmentId | long? | `IFileStorage` |
| SourceApplicationId | long? | Unenforced — `adm.Applications`, set on admit |

**StudentGuardian**

| Column | Type | Rules |
|---|---|---|
| StudentId | long | FK |
| ContactId | long | Unenforced — a `con.Contact` with `IsGuardian`. Validated through Master |
| Relationship | enum | Father, Mother, Guardian, Other |
| IsPrimary | bool | Exactly one per student. The primary guardian is the one invoiced |
| HasPortalAccess | bool | Whether a portal link may be issued |

**Enrolment**

| Column | Type | Rules |
|---|---|---|
| StudentId | long | FK |
| AcademicYearId | long | FK. One enrolment per student per year |
| SectionId | long | FK. Must belong to the same year |
| RollNo | int? | Unique per section |
| EnrolmentStatus | enum | Active, Promoted, Detained, Withdrawn |

**Exam**, **ExamSubject**, **ExamMark**

| Column | Type | Rules |
|---|---|---|
| Exam.AcademicYearId | long | FK |
| Exam.Name | string(100) | `Term 1` |
| Exam.StartDate / EndDate | DateOnly | |
| Exam.ExamStatus | enum | Planned, MarksOpen, Published, Locked. Marks are writable only while MarksOpen |
| ExamSubject.ExamId / SubjectId / SchoolClassId | long | Unique triple |
| ExamSubject.MaxMarks / PassMarks | money | Pass ≤ Max |
| ExamMark.ExamSubjectId / EnrolmentId | long | Unique pair |
| ExamMark.Marks | money? | 0 ≤ Marks ≤ MaxMarks; null with `IsAbsent` |
| ExamMark.IsAbsent | bool | |

### `adm`

**Enquiry**

| Column | Type | Rules |
|---|---|---|
| EnquiryDate | DateOnly | |
| ChildName | string(200) | |
| DateOfBirth | DateOnly? | |
| SeekingClassId | long | Unenforced — `sis.SchoolClasses` |
| AcademicYearId | long | Unenforced — `sis.AcademicYears` |
| ParentName | string(200) | |
| Phone | string(20) | Phone rule: local without prefix, foreign with `+` |
| Email | string(255)? | |
| EnquirySource | enum | WalkIn, Website, Referral, Advertisement, Other |
| EnquiryStatus | enum | Open, FollowUp, Converted, Lost |
| FollowUpDate | DateOnly? | |

**Application**

| Column | Type | Rules |
|---|---|---|
| ApplicationNo | string(30) | Numbering series `APL` |
| EnquiryId | long? | FK |
| ApplicationDate | DateOnly | |
| ChildFirstName / ChildLastName | string(100) | |
| DateOfBirth | DateOnly | |
| SeekingClassId / AcademicYearId | long | Unenforced, as above |
| ApplicationStage | enum | Submitted → DocumentsVerified → Assessed → Offered → Admitted, or Rejected / Withdrawn |
| AssessmentScore | money? | |
| AdmittedStudentId | long? | Unenforced — the `sis.Students` row admit created |
| ApplicationFee | money | Collected as a `fee` receipt, not here |

**ApplicationDocument** — `ApplicationId`, `DocumentKind` (enum: BirthCertificate, TransferCertificate,
ReportCard, Photo, AddressProof, Other), `AttachmentKey string(500)`, `IsVerified bool`.

### `att`

**StudentAttendance**

| Column | Type | Rules |
|---|---|---|
| AttendanceDate | DateOnly | Inside the section's academic year; not in the future |
| SectionId | long | Unenforced — `sis.Sections` |
| EnrolmentId | long | Unenforced. Unique per date + enrolment (+ period, when timetables exist) |
| AttendanceStatus | enum | Present, Absent, Late, HalfDay, Leave, Holiday |
| Remarks | string(200)? | |

**AttendanceLock** — `SectionId`, `AttendanceDate`, `IsLocked`. A locked day refuses edits except by
a holder of `attendance.unlock`.

### `fee`

**FeeHead**

| Column | Type | Rules |
|---|---|---|
| Code | string(20) | `TUITION`, `TRANSPORT`, `EXAM`. Unique per OrgId |
| Name | string(100) | |
| IncomeAccountId | long | Unenforced — `acc.Accounts`, Income type. Validated through Accounting |
| IsRefundable | bool | A caution deposit is; it posts to a liability instead |
| HsnSacCode | string(8)? | SAC for the rare taxable head |

**FeeStructure** and **FeeStructureLine**

| Column | Type | Rules |
|---|---|---|
| FeeStructure.AcademicYearId / SchoolClassId | long | Unenforced. Unique pair + Name |
| FeeStructure.Name | string(100) | `Day scholar`, `Hosteller` |
| FeeStructureLine.FeeHeadId | long | FK |
| FeeStructureLine.Amount | money | > 0 |
| FeeStructureLine.Frequency | enum | OneTime, Monthly, Quarterly, Termly, Annual |
| FeeStructureLine.DueDay | int | 1–28, day of the period |

**FeeConcession** — `StudentId` (unenforced), `FeeHeadId`, `ConcessionKind` (enum: Percent, Amount),
`Value money`, `Reason string(200)`, `ValidFrom`/`ValidTo DateOnly`, `IsApproved bool`.

**FeeDemand** (the invoice) and **FeeDemandLine**

| Column | Type | Rules |
|---|---|---|
| FeeDemand.DemandNo | string(30) | Numbering series `FDM`, gapless — `NumberGenerator` in the caller's transaction |
| FeeDemand.StudentId / EnrolmentId | long | Unenforced |
| FeeDemand.ContactId | long | Unenforced — the primary guardian, **snapshotted** at issue |
| FeeDemand.DemandDate / DueDate | DateOnly | |
| FeeDemand.DocumentStatus | enum | `Shared.Kernel.Documents` lifecycle: Draft → Posted → Void. A posted demand is never edited |
| FeeDemand.CurrencyCode / ExchangeRate | string(3) / money | Snapshot at demand date |
| FeeDemand.TotalAmount / ConcessionAmount / NetAmount | money | Computed on write, stored |
| FeeDemand.JournalId | long? | Unenforced — the posted JE |
| FeeDemandLine.FeeHeadId | long | FK |
| FeeDemandLine.Amount / ConcessionAmount | money | Concession ≤ Amount |

**FeeReceipt** and **FeeReceiptAllocation**

| Column | Type | Rules |
|---|---|---|
| FeeReceipt.ReceiptNo | string(30) | Numbering series `FRC` |
| FeeReceipt.ContactId | long | Unenforced |
| FeeReceipt.ReceiptDate | DateOnly | |
| FeeReceipt.PaymentMode | enum | Cash, Cheque, Upi, Card, BankTransfer |
| FeeReceipt.BankAccountId | long | Unenforced — `acc.BankAccounts` |
| FeeReceipt.Amount | money | > 0 |
| FeeReceipt.Reference | string(50)? | Cheque / UTR number |
| FeeReceiptAllocation.FeeReceiptId / FeeDemandId | long | Allocation ≤ the demand's open balance |
| FeeReceiptAllocation.Amount | money | |

**Posting.** Fee posts through Accounting's internal posting API, the way
`Sales.Api/Services/LedgerClient.cs` does — never by writing GL rows.

- Demand: `Dr Accounts Receivable (guardian sub-account) / Cr Fee Income per head`, with concession as
  `Dr Discount Given` (contra).
- Receipt: `Dr Bank / Cr Accounts Receivable`.
- Unallocated money: an overpayment advance sub-account.

### `fac`

**Building** — `Code string(20)`, `Name string(100)`, `Floors int`, `IsActive bool`.

**Space**

| Column | Type | Rules |
|---|---|---|
| BuildingId | long | FK |
| Code | string(20) | `B1-204`. Unique per OrgId |
| Name | string(100) | |
| SpaceKind | enum | Classroom, Laboratory, Office, Toilet, Hall, Playground, Store, Other |
| Floor | int | |
| Capacity | int? | |

**FacilityAsset**

| Column | Type | Rules |
|---|---|---|
| AssetTag | string(30) | Unique per OrgId |
| Name | string(200) | `Split AC 1.5T`, `RO plant` |
| AssetCategory | enum | Electrical, Plumbing, Hvac, Furniture, It, Lab, Vehicle, Civil, Other |
| SpaceId | long? | FK |
| Make / Model / SerialNo | string(100) | |
| PurchaseDate / WarrantyUntil | DateOnly? | |
| PurchaseCost | money? | |
| AssetStatus | enum | InUse, UnderRepair, Idle, Disposed |
| FixedAssetId | long? | Reserved for the Phase 2 register — see open questions |

### `wrk`

**WorkOrder**

| Column | Type | Rules |
|---|---|---|
| WorkOrderNo | string(30) | Numbering series `WRK` |
| Title | string(200) | |
| Description | string(2000)? | |
| WorkOrderSource | enum | Complaint, Preventive, Amc, Inspection |
| Priority | enum | Low, Medium, High, Critical |
| FacilityAssetId / SpaceId | long? | Unenforced — `fac`. At least one of them |
| ReportedDate | DateOnly | |
| DueDate | DateOnly? | |
| AssignedEmployeeId | long? | Unenforced — `hrm.Employees` |
| AmcContractId | long? | Unenforced — `amc.Contracts`; the vendor attends instead |
| PreventivePlanId | long? | Unenforced — `ppm.Plans` |
| WorkOrderStatus | enum | See lifecycle |
| CompletedDate | DateOnly? | Required at Completed |
| LabourCost | money | |

**WorkOrderTask** — `WorkOrderId`, `Description string(500)`, `IsDone bool`, `SortOrder int`.

**WorkOrderPart** — `WorkOrderId`, `ItemId long` (unenforced — `inv.Items`), `WarehouseId long`,
`Quantity money`, `UnitCost money` (from Inventory at issue). Issuing a part is a stock movement
Inventory performs, synchronously and guarded, like any other issue.

**Lifecycle.** `Open → Assigned → InProgress ⇄ OnHold → Completed → Closed`, and `Cancelled` from
Open or Assigned.

- The header is editable only while `Open`. Anything later is a status change or a task tick, never
  an edit.
- `Closed` is terminal and needs `wrk.close`.
- Every refusal is one `DocumentLifecycle`-style message.

### `ppm`

**PreventivePlan**

| Column | Type | Rules |
|---|---|---|
| Name | string(200) | `Quarterly AC service` |
| FacilityAssetId / SpaceId | long? | Unenforced — one of them |
| Frequency | enum | Daily, Weekly, Monthly, Quarterly, HalfYearly, Yearly |
| Interval | int | Every *n* of Frequency; ≥ 1 |
| StartDate | DateOnly | |
| EndDate | DateOnly? | |
| NextDueDate | DateOnly | Advanced when an occurrence is generated |
| LeadDays | int | Raise the work order this many days early |
| DefaultAssigneeEmployeeId | long? | Unenforced |
| IsActive | bool | |

**PreventiveOccurrence** — `PreventivePlanId`, `DueDate DateOnly`, `WorkOrderId long?`
(unenforced), `OccurrenceStatus` (enum: Scheduled, Raised, Done, Skipped). Unique on plan + due date,
which is what makes generation idempotent: a second run finds the row and raises nothing.

**Generation.** A hosted service in `Preventive.Api` generates occurrences, claiming each due plan
with a guarded conditional update, the same pattern `CostingEngine.Worker` uses — not a broker.

### `amc`

**AmcContract**

| Column | Type | Rules |
|---|---|---|
| ContractNo | string(30) | The vendor's number |
| VendorContactId | long | Unenforced — `con.Contact` with `IsVendor` |
| StartDate / EndDate | DateOnly | End after start |
| ContractValue | money | Billed through Purchase as a bill; never posted from here |
| BillingFrequency | enum | Upfront, Quarterly, HalfYearly, Annual |
| VisitsPerYear | int | |
| AmcCoverage | enum | Comprehensive, NonComprehensive — whether parts are covered |
| RenewalReminderDays | int | |
| ContractStatus | enum | Draft, Active, Expired, Terminated. Expiry is derived from EndDate, stored for the list filter |

**AmcCoveredAsset** — `AmcContractId`, `FacilityAssetId` (unenforced). An asset may be under only one
active contract.

**AmcVisit** — `AmcContractId`, `VisitDate DateOnly`, `VisitKind` (enum: Scheduled, Breakdown),
`Remarks string(1000)?`, `WorkOrderId long?`.

## Endpoints

The shape, guards and `Forbid()` rule are those in HRMS & Payroll § Endpoints. The lifecycle verbs
here are `post`, `void`, `assign`, `complete`, `close`, `admit` and `publish`.

The modules seeded into `mst.Permissions` with `App = School` are `sis`, `admission`, `attendance`,
`fee`, `facility`, `workorder`, `preventive` and `amc`.

## Frontend

- **`apps/school`**, a new Nx app, bootstrapped like `apps/web`:
  - `libs/app-shell` as its authenticated root;
  - `libs/shared/{auth, api-client, ui-components, currency-format, theming}` for everything else.
- **One lib pair per service**, mirroring `backend/Api/`, e.g. `libs/sis/{sis-core, sis-ui}` and
  `libs/work-order/{work-order-core, work-order-ui}`.
  - `-core` stays Ionic-compatible.
  - Pages use `.page.ts` / `.list.ts` / `.dialog.ts`, `templateUrl` + `styleUrl`, `inject()`, signals.
- **The UI rules are those in HRMS & Payroll § Frontend.** The attendance register and a timetable
  grid are the components most likely to be missing from `ui-components`, so those screens stop
  and ask.
- **Parent portal.** New routes in `apps/portal` for fee demands, receipts, attendance and published
  marks.
  - Controllers take `[RequirePortalAccess]`.
  - A guardian's link is minted by `JwtTokenService.CreatePortalToken` against their `ContactId`.
- **Every page works at ~360px.**

## Tenancy and security, per service

The rules in HRMS & Payroll § Tenancy and security apply unchanged.

**Seeding at branch creation.**

- Fee heads.
- Numbering series `ADM`, `APL`, `FDM`, `FRC` and `WRK`.
- Permission, menu and role rows — the roles are Principal, Office Admin, Accountant, Teacher,
  Maintenance and Viewer — all with `App = School`.
- `SchoolClass` rows LKG–XII; the owner may delete what they do not teach.

## What it costs, stated plainly

**Eight services here, plus the six HRMS services before it, take the product from seven (eight with
Printing) to twenty-one or twenty-two.**

- The history in `CLAUDE.md` is three merges made because pairs of services turned out to be two
  halves of one job.
- `fac`/`wrk`/`ppm`/`amc` are that shape: a work order cannot be raised without an asset, and a
  plan and a contract exist only to raise work orders.
- `sis`/`att`/`fee` are that shape too.
- Every such read becomes an HTTP call, and no two of them can share a transaction.
  - Admitting a student creates a Student in `sis` and marks the application in `adm`. If the
    second write fails, the first is already committed.
  - It needs a compensating step, or an idempotent retry keyed on `SourceApplicationId`. The design
    takes the retry.

This is the owner's decision and the design follows it. It is written down so that if these services
are merged later — as the last three pairs were — nobody has to rediscover why.

## What this does not cover yet

Features a working school usually needs that none of the eight schemas holds. The owner asked to be
told; none of them is designed until one is picked.

| Feature | Why it matters | Would need |
|---|---|---|
| **Timetable** | Period-wise attendance and teacher load both need it | A new schema, or `sis`; a grid component `ui-components` lacks |
| **Report cards** | What marks are *for* | Grading scales in `sis`; a print template per class |
| **Certificates** — TC, bonafide, character | Issued daily by the office | Print templates plus a certificate register with a numbering series |
| **Parent communication** — circulars, SMS, email | Absence alerts, fee reminders | `Notification.Worker`, which today holds only a `PaymentReminderWorker` that logs and sends nothing (TK-20) |
| **Homework / assignments** | | A new schema |
| **Student documents and ID cards** | | `IFileStorage` (built); a print template |
| **Transport** — routes, stops, vehicles, transport fee | Very common in Indian schools | A new service under the one-per-schema rule; its fee is a `fee` head |
| **Hostel** and **Library** | | A new service each |
| **School store** — uniforms, books | | Nothing new: Inventory + Sales already do it |

## Open questions

- **Do exams belong in `sis` or in their own `exm` schema?**
  *Recommend `sis`: marks are meaningless without enrolments, and splitting them would be a ninth
  service for one table's worth of rules.*
- **Academic year against financial year.** An April–March school year matches the financial year;
  a June–May one does not.
  *Recommend keeping them independent — fee income is recognised by demand date, so reports never
  need to align the two.*
- **GST on fees.** Most school education is exempt, but some heads (transport by a third party,
  uniforms) may not be.
  *Recommend a nullable SAC on `FeeHead` and exempt by default, using `Shared.Kernel.Tax` when a
  head is taxable, rather than a second tax path.*
- **Is a `FacilityAsset` the Phase 2 fixed asset?**
  *Recommend no, but linked. A chair is maintained and never depreciated separately; an AC is
  both. `FixedAssetId` is the link, filled when the register exists.*

## Stages

- [ ] **S0 — School prerequisites.** Starts after H0 (Platform) and HRMS H1.
  - `IsGuardian` on `con.Contact`.
  - School permission, menu and role seeds with `App = School`.
  - School signup with its 14-day trial, and School seeding per branch (Platform § Signup).
  - An empty `apps/school` on the shell that H0 made app-aware, mounting the shared master pages
    (Platform § Shared master pages) and the shared employee master.

  *Done when*: `apps/school` shows only School menus, and a guardian contact can be created and
  filtered.
- [ ] **S1 — Sis.** Scaffold, schema, seeds, API, pages for years, classes, sections, subjects,
  students with guardians, and enrolment.

  *Done when*: a student is admitted directly, enrolled in a section and listed. RLS and the guard
  audit pass from a dropped database.
- [ ] **S2 — Admission.** Enquiry → application → admit, creating the student through Sis.

  *Done when*: admitting twice creates one student.
- [ ] **S3 — Attendance.** The daily register per section, and locking.

  *Done when*: a locked day refuses an edit from a teacher and accepts one from `attendance.unlock`.
- [ ] **S4 — Fee.** Heads, structures, concessions, demand generation per enrolment, receipts,
  allocation, and posting.

  *Done when*: a demand and its receipt post balanced JEs, and the guardian's AR sub-account ties
  to the open demands.
- [ ] **S5 — Facility.** Buildings, spaces and assets.
- [ ] **S6 — WorkOrder.** The lifecycle, tasks, and parts issued from Inventory.

  *Done when*: editing an Assigned work order is refused, and issuing a part moves stock.
- [ ] **S7 — Preventive.** Plans, and occurrence generation by the hosted service.

  *Done when*: running generation twice raises one work order per occurrence.
- [ ] **S8 — Amc.** Contracts, covered assets, visits, and renewal reminders.
- [ ] **S9 — Parent portal.** Guardian pages for demands, receipts, attendance and published marks.

Every stage is built in the same order: migration, then seed, then API with field and business
validation, then UI. Each is committed to `main` as it stands up, with its docs page in
`frontend/apps/docs/content/` and a release-notes bullet in the same commit.
