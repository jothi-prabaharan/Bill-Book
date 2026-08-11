# SALES.md — the `sal` module, end to end

Everything needed to build Sales: the document chain, every table and column, every decision taken and why, and the task list.

**This file is the single home for `sal.*`.** `SPEC.md` points here rather than repeating the columns, and `TRANSACTIONS.md` points here rather than repeating the tasks. `CLAUDE.md` still holds the conventions that apply to everything.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** See *Git — how work reaches main* in `CLAUDE.md`.

**Status: designed, nothing built.** `backend/Api/Sales/` contains one `Program.cs` and no entities. The stock machinery underneath — reservation, the guarded issue, cost layers, returns to the originating layer, the COGS posting — is **built and has never been called by a document**.

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
| `AllowFreeTextLines`, `DiscountLevel`, `DiscountBeforeTax` on `plt.Organizations` | Structural branch decisions, frozen once the branch has traded. **Built already** |

---

## 9. Open — answer before the stage that needs them

- **What a delivery challan posts.** Issuing as `Dr COGS` at dispatch books cost with no revenue against it. *Recommendation: a `Goods Delivered Not Invoiced` control account — `Dr GDNI / Cr Inventory` on the challan, `Dr COGS / Cr GDNI` on the invoice.* Job work, approval, branch transfer and sample post nothing at all. Mirrors the GRNI question in `PURCHASE.md`.
- **Jewellery line columns** — making charge, wastage, metal rate. A 1:0..1 extension like `inv.ItemJewelleryDetails`, or columns on every line. Settle before the first pair is built.
- **Can a user override `TaxTreatment` on a line?** Default no — it is a property of the goods. But an SEZ or export customer needs `ZeroRated` on a normally-taxable item, and that is driven by the *customer*, not the item.

---

## 10. Tasks

Numbering follows `TRANSACTIONS.md`, so a cross-reference written before this file still resolves.

### Blocked on foundations

These live in `TRANSACTIONS.md` and nothing here starts without them: **T0.2** tax determination · **T0.3** document numbering series · **T0.4** the lifecycle. **T0.1** (the ledger door) and **T0.6** (ledger screens) are done.

### T2 — quote and sales order

- [ ] **T2.2 — The five pairs: base classes, entities, migration.** `DocumentHeaderBase`, `DocumentLineBase` and `DocumentLineTaxBase` in `Shared.Kernel` first, then the fifteen tables inheriting them, with `OrgId` on every one, query filters, RLS, and the document series.
  *Done when*: `migrations add` produces an empty migration and the RLS policies are in the database, not just the model.
- [ ] **T2.3 — Quote: API and page.** Create, edit, print, convert to order, expire. **Uses `bb-document-line-grid`** — the grid is built, so the page wires it up rather than writing one.
  *Done when*: a quote prints, converts, and writes nothing to the ledger or stock. **The batched name lookup lands here** — it is this stage's first real problem, not something to meet at T3.2.
- [ ] **T2.4 — Sales order: API and page, reserving stock.** Confirming calls Inventory's `ReserveAsync`; cancelling or converting releases.
  *Done when*: confirming an order for the last unit makes it unavailable to a second order while leaving on-hand quantity, stock value and the inventory account untouched.

### T3 — invoice

- [ ] **T3.1 — Invoice API: post, void, ledger legs.** Stock issued through the guarded decrement. **Issuing reserved stock is release-then-issue in one transaction.**
  *Done when*: an invoice against a confirmed order releases and issues exactly once; the trial balance still balances; gross profit equals revenue minus the COGS the layers produced.
- [ ] **T3.2 — Invoice page.** `bb-document-line-grid` plus the invoice header, totals panel, draft / ready / post / void, print.
  *Done when*: keyed and posted at 360px, and the tax on screen equals the tax posted.
- [ ] **T3.3 — Outstanding and aging.** Read from the ledger's AR sub-accounts. The input to Banking's allocation.
  *Done when*: an invoice is outstanding at full value the moment it posts, and the buckets tie to the Accounts Receivable control account.
- [ ] **T3.4 — Print and archive.** Syncfusion server-side PDF, PDF/A, blob storage keyed by `SourceType` + `SourceId`.
- [ ] **T3.5 — `sal.SalesRegister`.** Written inside the post's transaction, replaced by key, deleted on void.
  *Done when*: intra- and inter-state invoices register the right halves and `chk_register_tax_split` refuses the wrong one; a re-post leaves no orphans; period taxable value equals the Output GST legs.
- [ ] **T3.6 — `DLC` delivery challan.** Needs a seventeenth `mst.TransactionTypes` row, added by EF migration, and its own numbering series.
  *Done when*: an order part-delivered issues only what shipped and leaves the rest reserved; the invoice against that challan moves no stock; a job-work challan writes a movement and no ledger row.

### T5 — credit note

- [ ] **T5.1 — `acc.TransactionRatio`** (shared with Purchase). Allocation between documents. Allocations must never exceed the target's outstanding balance — a C# guard, since the sum spans rows.
- [ ] **T5.2 — Credit note.** Stock returned via `ReturnsStockMovementId` to the originating layers at their original cost.
  *Done when*: buy, sell, credit-note leaves stock value exactly where it started, and the note allocates against the invoice rather than floating.
- [ ] **T5.4 — Allocation UI** (shared with Purchase). Over-allocation refused while typing, not at save.

### T7 — POS

**No new tables.** T7.1 reuses T3.1's posting.

- [ ] **T7.1 — POS API.** One call: issue stock, post the sale, post the payment. The decrement is synchronous and guarded.
- [ ] **T7.2 — POS screen.** Keyboard and barcode driven, offline-tolerant, in `apps/desktop`. The bulk of the stage.
- [ ] **T7.3 — ESC/POS receipt.** Commands, not PDF. Desktop only — a browser cannot reach a USB or serial printer.

---

## UI — one line grid for every document

**`bb-document-line-grid`**, in `libs/shared/ui-components`. **Built.** All nine document types share one line shape, so they share one grid; what differs between an invoice and a goods receipt is the header around it and the columns each *adds*, which the host page renders beside the grid rather than instead of it.

```
@bill-book/ui-components
  DocumentLineGridComponent   the grid
  DocumentLine                the line view-model, mirroring DocumentLineBase
  line-math                   pure arithmetic, 9 tests
```

**It owns no data and fetches nothing.** Lines in, changed lines out, and the host opens its own item picker — which is what lets one component serve a sales page and a purchase page without knowing which it is on.

| Input | |
|---|---|
| `lines` | the current lines |
| `context` | `isInterState`, the three organization settings, `readonly`, currency decimals |

| Output | |
|---|---|
| `linesChange` | every line, already recalculated |
| `pickItem` | the host opens the picker |

**Amounts are integer paise, not rupees.** TypeScript has no decimal — `number` is a double and `0.1 + 0.2` is not `0.3`. Money that has to tie to a ledger cannot be computed in floating point, so every amount is a whole number of the smallest unit and is divided only for display.

**The arithmetic is separated from the component** (`line-math.ts`) and tested: exclusive and inclusive pricing, discount before and after tax, exempt writing no rows, zero-rated keeping them at rate 0, the intra/inter split, and header totals tying to the sum of the lines. Two of those tests failed on first run and one was a genuine bug — the inclusive back-out divided by the rate scale where the rate is a *percent*, making every inclusive line a hundredth of its true taxable value. That is precisely the kind of error that prints, posts and balances.

**The order of operations must match `Shared.Kernel` exactly** — see T0.2. Two implementations of one sum diverge by a paisa without either looking wrong, and a GST return is where that surfaces months later. The shared JSON fixture that proves they agree is still to be written.

**Nothing integrates it yet, because there are no transaction pages.** Each page wires it up as it lands: T2.3 quote, T2.4 order, T3.2 invoice, T3.6 challan, T5.2 credit note here; T4.3–T4.5 and T5.3 in the other file.

---

## 11. Standing requirements

Not repeated per task. Full list in `TRANSACTIONS.md`.

Documentation in the same commit · `OrgId` + query filter + RLS on every table including details · `[Authorize]` and `RequireModulePermission` on every endpoint · postings idempotent on their document key · `ExchangeRate` a snapshot · `dotnet build && dotnet test` and `npm run check` green before a box is ticked · every page working at ~360px.
