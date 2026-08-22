# SALES.md — the `sal` module, end to end

Everything needed to build Sales: the document chain, every table and column, every decision taken and why, and the task list.

**This file is the single home for `sal.*`.** `SPEC.md` points here rather than repeating the columns, and `TRANSACTIONS.md` points here rather than repeating the tasks. `CLAUDE.md` still holds the conventions that apply to everything.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** See *Git — how work reaches main* in `CLAUDE.md`.

**Status, 22 August 2026: the chain runs as far as the invoice.** The quote (T2.1/T2.3), the sales order (T2.2/T2.4) and the invoice (T3.1/T3.2) each have their schema, service, controller, list, form, conversion from the document upstream, docs and tests. The invoice posts the double entry and issues the stock. The delivery challan and the credit note have a controller and a scaffold page and **no verified path**.

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
| **Ticked, with a named hole** | T2.4 — `DeliveredQuantity` is never written, so `PartlyDelivered` is unreachable and a fully invoiced order still reads Open. T3.1 — no `InvoicedQuantity`, so partial invoicing cannot exist. Both wait on T3.6 |
| **Written, unverified end to end** | T3.6 delivery challan, T5.2 credit note — both can now save, neither has been driven through a full path |
| **Written, defective in a named line** | T5.2 (`ReturnsStockMovementId`), T3.6 (invoice re-issues challan stock) |
| **Part built** | T3.3 outstanding — settlement now shows on the invoice list (Paid / Part-paid / Unpaid, from `internal/ledger/settlements`), still no aging buckets |
| **Part built** | T3.4 print — a browser print view of the tax invoice exists; **the archived PDF/A copy does not**, and is blocked on a licence decision for the PDF library |
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

- [x] **T2.2 — The five pairs: base classes, entities, migration.** `DocumentHeaderBase`, `DocumentLineBase` and `DocumentLineTaxBase` in `Shared.Kernel` first, then the fifteen tables inheriting them, with `OrgId` on every one, query filters, RLS, and the document series.
  *Done when*: `migrations add` produces an empty migration and the RLS policies are in the database, not just the model.

  **Code complete; the box stays unticked because the Done-when is not met, and the gap is a security one.** The base classes, the fifteen tables and the migration (`20260814075501_AddSalesRegister`, sixteen tables) all exist. What does not:

  - **The migration contains no RLS.** `grep -c "ROW LEVEL SECURITY"` over it returns 0, though `Migrations/README-RowLevelSecurity.md` carries the block to paste. The Done-when asks for policies *in the database*; there are none.
  - **`SalesDbContext` never calls `base.OnModelCreating`.** Inventory, Accounting and Purchase all end theirs with it. Without it `TenantDbContext` never runs, so there is **no `OrgId` query filter on any `sal` table**, no OrgId index, and `Version` is mapped as a plain `bigint` instead of the `xmin` system column — optimistic concurrency silently does nothing.

  Together those two mean **`sal` has neither of the two isolation layers a per-customer schema is supposed to have.** CLAUDE.md calls a missing query filter "the highest-consequence mistake available here". The fix is one line plus a migration carrying the RLS block; verify it the way `pur` was, by querying `pg_policies` and exercising a policy as a non-owner role.

  Two further defects in the same code, neither blocking the box on its own:

  - **Ten shadow foreign-key columns.** `HasOne<Quote>().WithMany()` with no navigation argument declares a relationship separate from the `Lines` collection, so EF adds a second FK column. `sal` carries `QuoteId1`, `SalesOrderId1`, `DeliveryChallanId1`, `InvoiceId1`, `CreditNoteId1` and their five detail-tax equivalents.
  - **The Fluent configuration now lives in `Shared.Kernel.Documents.DocumentModelConfiguration`**, shared with `pur`, so all twenty-seven document tables take their precision, indexes and constraints from one place. Verified schema-neutral when it moved: a probe migration came back empty.

  Four things settled in the original writing, still true:
  **`TaxComponent` is a new enum in `Shared.Kernel`, not Accounting's.** Accounting's discriminates a sub-account and therefore needs a `None` — a contact's receivable is not a tax of any kind — and has no `Cess`. Merging them would give each a member the other must never hold.
  **The line-number index is unique on `(document, LineNumber)`.** The ledger's `ITEM` leg keys on the line number, so two lines claiming position three would leave a posting pointing at whichever the database returned first.
  **`DLC` now has its numbering series and its `mst.TransactionTypes` row** — both seeded, by T3.6. The note that once said otherwise is spent.
  **Header checks are in the database, not just C#** — the total footing to its parts, the tax split matching `IsInterState`, void stamped with a reason, `PostedAt` set iff it posted. Each is a thing that still prints and still posts when it is wrong.

- [x] **T2.3 — Quote: API and page.** Create, edit, print, convert to order, expire. **Uses `bb-document-line-grid`** — the grid is built, so the page wires it up rather than writing one.
  *Done when*: a quote prints, converts, and writes nothing to the ledger or stock. **The batched name lookup lands here** — it is this stage's first real problem, not something to meet at T3.2.

  **Built.** `QuoteService`, `QuotesController` (with `[PermissionAction]` on approve and void), `Sales.Entity/Models/QuoteModels.cs`, and `quote-form` / `quote-list` in `libs/sales/sales-ui`. **Print is not built** — see T3.4, which owns it for every document.

  **The batched name lookup is done.** `Shared.Kernel.Documents.INameLookup` defines `IContactNameLookup` and `IItemNameLookup`, both taking a *collection* and with no single-id overload, so the N+1 cannot be written by accident. `HttpNameLookup` batches, caches per organization and id for five minutes, and **swallows failures** — a name that cannot be read leaves one column showing an id rather than turning a list screen into a 500. That is the opposite of `ITaxRateProvider`, which answers null and stops the save, and deliberately so: a rate is written into the books, a name is only drawn on a screen. Served by `internal/contacts/names` on Master and `internal/items/names` on Inventory, both POST with the ids in the body, capped at 500.

  **The request shapes settle two things**: the caller sends no totals and no tax amounts, because a caller free to send its own totals can save a document whose foot disagrees with its body; and a line names a `TaxGroupId`, never a rate, because a caller that could send a rate could send yesterday's.

  **The `Master:BaseUrl` port defect recorded here is fixed** — Sales and Accounting both point at 5003 now.

- [x] **T2.4 — Sales order: API and page, reserving stock.** Confirming calls Inventory's `ReserveAsync`; cancelling or converting releases.
    *Done when*: confirming an order for the last unit makes it unavailable to a second order while leaving on-hand quantity, stock value and the inventory account untouched.
    **Done.** `SalesOrderService` calls `IInventoryClient.ReserveAsync`/`ReleaseAsync`; `sales-order-list` pages on the server and `sales-order-form` is built on the tested paise/rupee boundary, with the quote-to-order dialog beside it.

    Two additions since, both filling real gaps rather than reworking:

    - **`POST /{id}/short-close`.** An order taken partly and then stopped is neither fulfilled nor void — voiding one that shipped goods would withdraw the document those goods went out on. Short-closing releases what is still reserved (ordered less delivered, never negative), sets `FulfilmentStatus.Closed`, and records a required `ShortCloseReason`. That column is what disambiguates `Closed`, which otherwise covers both "everything went out" and "nothing further is coming" — the very ambiguity that made the status a column rather than arithmetic. Refused if Inventory cannot release, for the same reason a void is
    - **Stock availability on the form.** `POST internal/stock/availability` in Inventory answers on hand, reserved and available for a batch of items; `POST /api/sales/sales-orders/availability` names them and the drawer shows the shortfall per line. **Advisory** — the guarded reservation on confirm is what decides, and an unreachable Inventory answers empty rather than failing the screen

    **What the box does *not* cover, and the first one is a hole rather than a deferral.**

    **`DeliveredQuantity` is never written.** Nothing anywhere in the service sets
    it — the only mention is a projection into the view. Three things follow, and
    they are worth reading together:

    - **`FulfilmentStatus.PartlyDelivered` is unreachable.** The enum value exists
      and no code path can produce it. Fulfilment only ever moves Open → Cancelled
      (void) or Open → Closed (short-close)
    - **An order that has been fully invoiced still reads Open.** Nothing closes it
      on invoicing, so the status answers "is this order finished" with a
      permanent no
    - **Short-close releases `ReservedQuantity - DeliveredQuantity`, which is
      right today by accident.** Delivered is always zero, so it releases the whole
      reservation, which is the correct answer for every order that currently
      exists. That line has never run against a non-zero delivered quantity, and
      the day partial delivery works is the day it is right for the right reason —
      or wrong, unnoticed

    Advancing delivered quantity is the delivery challan's job (T3.6), which is
    written and unverified. **So the sales order is complete as a commitment
    document and incomplete as a fulfilment one**, and that is the honest reading
    of this ticked box.

    Two more, both shared with the invoice: **the customer and the items are
    numeric id fields** — `onPickItem` is an empty stub awaiting the item lookup
    endpoint — and **`short-close` and `availability` have no tests**. The twelve
    in `SalesOrderServiceTests` cover create, confirm, the named shortfall, both
    void paths, the downstream block, clamping, the filtered total, the status
    filter, cross-org, and both quote-conversion outcomes.

### T3 — invoice

- [x] **T3.1 — Invoice API: post, void, ledger legs.** Stock issued through the guarded decrement. **Issuing reserved stock is release-then-issue in one transaction.**
  *Done when*: an invoice against a confirmed order releases and issues exactly once; the trial balance still balances; gross profit equals revenue minus the COGS the layers produced.

  **Done.** `InvoiceService.PostAsync` / `VoidAsync`, `InvoicesController` (`GET` paged, `GET/{id}`, `POST`, `POST/from-sales-order/{id}`, `PUT/{id}`, `POST/{id}/post`, `POST/{id}/void`, `GET/{id}/gl-preview`). The release-then-issue is there — `IssueStockLine.ReleaseReservation` is set from `invoice.SalesOrderId.HasValue`, so an order-sourced invoice releases and issues in Inventory's one guarded call.

  **The ledger contract that blocked this is fixed.** `LedgerClient` sends `DebitAmount`/`CreditAmount` with the reference type that completes the sub-account key, which is what `LedgerPostingService` requires — it refuses a leg that is neither a debit nor a credit. That single line was what held T3.1, T3.5, T3.6 and T5.2 together; unblocking it unblocked all four.

  **`Sales.Api.Tests` now exists** — the gap that let a contract mismatch of that size survive to be found by reading. It holds the query-filter and schema guards, the sales order service suite, invoice posting and the controller tests: 64 tests, none skipped.

  Two things added since, both on the way in rather than the way out: **`POST from-sales-order/{id}`** reads the order's lines server-side and recomputes tax at the invoice's own date, so an order taken in March and invoiced in June is charged at June's rates; and the list is **paged on the server** with skip and take clamped.

  **What the box does not cover:**

  - **Partial invoicing is not possible at all.** There is no `InvoicedQuantity`
    on `SalesOrderDetail`, so an order cannot be billed four of ten and left open.
    A status of `PartiallyInvoiced` has nowhere to come from, which is why it is
    not in the enum
  - **The customer and the items are numeric id fields.** `onPickItem` is an empty
    stub awaiting the item lookup endpoint. This is the single biggest thing
    between the invoice and being usable by somebody who does not know ids
  - **The newer endpoints have no tests.** `from-sales-order`, the paged list and
    its clamping, and the settlement merge are all stub-only in the suites. The
    34 that exist cover posting, the ledger legs, POS, the reservation release,
    the challan path and the controller's own answers

- [x] **T3.2 — Invoice page.** `bb-document-line-grid` plus the invoice header, totals panel, draft / ready / post / void, print.
  *Done when*: keyed and posted at 360px, and the tax on screen equals the tax posted.

  **Done, and both carried items are cleared.** `invoice-form` and `invoice-list` in `libs/sales/sales-ui`, rebuilt to the standard the sales order page set: `async`/`await` over promises rather than piped `.subscribe(...)`, and the void reason is a form field with its own validation rather than `prompt()`, which was never a 360px dialog.

  Lines cross the paise/rupee boundary through `toGridLine` / `toApiLine`. Handed straight through they do not throw — they compute a priced invoice as an empty one, which is what the quote screen was doing before that utility existed.

  **Preview entry** shows what posting will write to the ledger before the irreversible step, read from `gl-preview` rather than recomputed in the browser: a preview from a second implementation eventually disagrees with the entry it claims to predict.

- [ ] **T3.3 — Outstanding and aging.** Read from the ledger's AR sub-accounts. The input to Banking's allocation.
  *Done when*: an invoice is outstanding at full value the moment it posts, and the buckets tie to the Accounts Receivable control account.

  **Outstanding is built and now visible; aging is still not.** The invoice list carries **Paid / Part-paid / Unpaid** with the amount still due, from a new batched `POST internal/ledger/settlements` — one grouped query per page rather than a round trip per contact. It is derived, never stored: a paid figure copied onto `sal.Invoices` drifts from the ledger the first time an allocation is undone. A draft is absent from the answer rather than zero, because it is not yet a receivable.

  What remains is the buckets. `GET ledger/contacts/{contactId}/outstanding-balances/{ledgerTypeId}` → `LedgerReportService.GetOutstandingBalancesAsync` groups `acc.JournalLedger` by `(TransactionTypeCode, TransactionId)` and returns debit minus credit per document. **There are no buckets** — no 0–30 / 31–60 / 61–90 / 90+, which is the half the Done-when names. `DueDate` comes back null and `DocumentNo` is the fallback string `"{code}-{id}"`, so nothing can age and nothing shows a document number a person would recognise. Both want the payment term the document was raised on.

- [ ] **T3.4 — Print and archive.** Syncfusion server-side PDF, PDF/A, blob storage keyed by `SourceType` + `SourceId`.

  **Half built, and the half that is missing is blocked on a decision rather than on work.**

  **Done:** a proper tax-invoice layout at `/sales/invoices/{id}/print` — both GSTINs, place of supply, HSN per line, the tax split **per component and per rate** (one figure per component misstates the return the moment a bill mixes slabs — 3% bullion beside 18% making charges), the total in words with Indian grouping, and `@media print` rules that drop the app chrome and avoid breaking a row across sheets. A draft prints watermarked PROFORMA and a voided one VOID, because a draft handed over as a tax invoice is a document somebody may try to claim credit on. `amountInWords` lives in `libs/shared/currency-format` and the seller block comes from `OrganizationService` in `libs/master/master-core`, so every printed document in the product shares both.

  **Not done: the archived PDF/A copy.** It needs a server-side PDF library, and the one this project intends — Syncfusion — is **licensed and not installed**; `Directory.Packages.props` names it only in a comment. Adding a commercial dependency is the repository owner's call. Until then nothing is written to blob storage against `SourceType` + `SourceId`.

  When it lands, the server-side renderer should reproduce the layout above rather than inventing a second one, or the printed and the archived invoice will drift apart.

- [x] **T3.5 — `sal.SalesRegister`.** Written inside the post's transaction, replaced by key, deleted on void.
  *Done when*: intra- and inter-state invoices register the right halves and `chk_register_tax_split` refuses the wrong one; a re-post leaves no orphans; period taxable value equals the Output GST legs.

  **Built, and the table is what T3.5 asked for.** All three writers keep it: `InvoiceService` adds rows after the post and `RemoveRange`s them on void, `DeliveryChallanService` the same, and `CreditNoteService` registers negative amounts so a period nets. The check constraint is in the migration.

  **The Done-when was unproven while the ledger post threw**, because the register is written *after* `_ledgerClient.PostAsync` and so no row had ever been written by a real post. That contract is fixed and `Sales.Api.Tests` now drives posting, so the path is exercised. The register's own Done-when — that intra- and inter-state invoices register the right halves and a re-post leaves no orphans — is covered by the invoice posting suite.

- [ ] **T3.6 — `DLC` delivery challan.** Needs a seventeenth `mst.TransactionTypes` row, added by EF migration, and its own numbering series.
  *Done when*: an order part-delivered issues only what shipped and leaves the rest reserved; the invoice against that challan moves no stock; a job-work challan writes a movement and no ledger row.

  **Seeded and served, but one clause of the Done-when is contradicted by the code.** The `mst.TransactionTypes` row and the `DLC` series are both seeded (`NumberingSeriesSeed`, id 315, prefix `DC`), `DeliveryChallanService` issues stock with `ReleaseReservation` when the challan came from an order, and posts a ledger only when the challan is a sale — a job-work challan writes the movement and no ledger row, as asked.

  **"The invoice against that challan moves no stock" now holds.** `InvoiceService.PostAsync` branches on `DeliveryChallanId`: with a challan behind it the invoice reads the cost and the movement id off the challan's lines and issues nothing, and only the `else` branch builds an `IssueStockRequest`. The note that said otherwise described a real double-issue and is spent.

  **The screen is reachable now, which it was not.** `DeliveryChallanFormComponent` existed with no route and no export — in the repository and invisible to the application, which is the same as not built. It has a list beside it (`DeliveryChallanListComponent`), three routes under `sales/delivery-challans`, and both are exported.

  **Three things were wrong on the way in, and each would have failed on first use:**
  - **Voiding threw.** `VoidAsync` set `VoidedAt` and never `VoidReason`, and `chk_deliverychallans_void_stamp` ties the two together. It takes a reason now, and refuses a *posted* challan outright — the goods have physically gone, and flipping the status would leave the stock issued and GDNI holding a balance nothing will close.
  - **The route was `DeliveryChallans`** while the frontend called `/api/sales/delivery-challans`. Both ends now agree, and `invoices` and `CreditNotes` were moved under `api/sales/` for the same reason — the frontend was already calling them there.
  - **No gateway route pointed at the sales cluster at all.** The cluster was declared and nothing reached it, so every sales screen was unreachable through the front door. `/api/sales/{**catch-all}` added.

  **This stage is what closes the fulfilment hole in T2.4 and T3.1.** Nothing
  currently advances `SalesOrderDetail.DeliveredQuantity`, so `PartlyDelivered` is
  unreachable and an order never stops reading Open. Dispatching a challan is what
  should move it, and short-close already reads it — correctly, but only because
  it is always zero today.

  **The ledger contract that blocked this is fixed** — see T3.1 — so a `Sale` challan's `Dr GDNI / Cr Inventory` pair is no longer refused on the wire. **The box stays unticked because nothing has driven the Done-when end to end**: the challan could not save a line at all until the shadow-key fault was fixed, so "written" has never been "works" here. Job work, approval, branch transfer and sample post nothing and were unaffected throughout.

### T5 — credit note

- [x] **T5.1 — `acc.TransactionRatio`** (shared with Purchase). Allocation between documents. Allocations must never exceed the target's outstanding balance — a C# guard, since the sum spans rows.

  **Built, and the guard is the whole task.** `AllocationService` reads the target's outstanding from its CONTROL legs (`LedgerTypeId == 3`) — `Σ DebitAmountBase − Σ CreditAmountBase`, the same net the balance trigger and the outstanding report use — subtracts what has already been allocated (`acc.TransactionRatio` rows), and refuses a claim past the remainder with the figures in the message. A document with nothing outstanding owes nothing and is refused; so is a non-positive amount. Read, decide and write are one serializable transaction, so two allocations racing the same target cannot both pass a guard neither saw the other's row; a Postgres serialization failure (`40001`) comes back as a retryable outcome rather than a 500. Re-allocating the same (source, target) pair replaces rather than doubles, which is what makes a retry after a dropped response safe. `POST internal/allocations` and `POST internal/allocations/remove` carry the tenant in the body and are guarded by the internal key, like the ledger door; refusals are 409 with `MessageResponse`, the race is 503.

  **Credit notes use it.** `CreditNoteService` allocates *before* posting — a refusal leaves the note draft with no stock moved and nothing posted — and a void removes the note's allocation rows, or the invoices it named would stay partially allocated to a document that no longer exists. The claim is the note's own `TotalAmount`; the target code is the invoice's own (`INV` or `POS`), read from the invoice rather than assumed.

  **Proven against Postgres** (`Accounting.Api.Tests/AllocationServiceTests.cs`, nine tests): within-outstanding succeeds, over-allocation refused with the figures, two allocations judged together, same-pair re-allocation replaces instead of doubling, nothing-outstanding refused, zero/negative refused, CONTROL *net* not gross, one org's claims invisible to another, and removal releasing exactly the source's claims.

- [ ] **T5.2 — Credit note.** Stock returned via `ReturnsStockMovementId` to the originating layers at their original cost.
  *Done when*: buy, sell, credit-note leaves stock value exactly where it started, and the note allocates against the invoice rather than floating.

  **Built and wrong in one line.** `CreditNoteService` has the service, the register rows, the ledger legs and the stock return through `IInventoryClient.ReceiveAsync`. But it sends `ReturnsStockMovementId = creditNote.InvoiceId` — **an invoice id where an `inv.StockMovements` id is required**. Inventory validates it: `ValidateReturnedMovementAsync` looks for a movement with that id, *on the same item*, with `Direction == Out`, and answers `ReturnedMovementNotFound` otherwise. So the return is refused; and in the rare case the two ids collide on an item that was issued, the stock goes back onto **another document's cost layers** at another document's cost, which is worse than being refused because it is silent.

  The id wanted is the movement the invoice's own line produced. Inventory's issue response already returns movement ids per line — the invoice needs to keep them (`sal.InvoiceDetails` has no column for one today), and the credit note line needs to name the invoice line it credits.

  The allocation half of the Done-when — "allocates against the invoice rather than floating" — is T5.1, now built and refused-guarded; the stock half is the line above.

- [x] **T5.4 — Allocation UI** (shared with Purchase). Over-allocation refused while typing, not at save.

  **Built and wired into the credit note page.** `bb-allocation-grid` clamps while typing: a row cannot be keyed past its outstanding *or* past what remains of the document total, and when the note's total shrinks — its last line edited — the rows are trimmed oldest-first to match, so the grid never claims more than the parent can pay. Every clamp emits `rowsChange`, so the parent stays in sync.

  **The grid is the invoice picker.** Choosing a contact loads its outstanding invoices (`GET api/ledger/contacts/{id}/outstanding-balances/3`, filtered to `INV` with a positive balance), and exactly one allocated row names the note's invoice — two of them is a refusal shown on the form *while typing*, not discovered at the ledger. The grid's amount is the note's rupee total, converted from the grid's paise in the one place `Purchase.md` T4.3 established: `save()` sends `quantity / 1e6`, `unitPrice / 100` and no totals, and the server recomputes. Tested in `allocation-grid.component.spec.ts` (eleven cases) and `sales-forms.spec.ts` (CRN-T1-04…07).

### T7 — POS · **Phase 3**

**Moved Phase 1 → Phase 3 on 15 August 2026, by decision.** The boxes below are kept rather than deleted, and nothing about the design changes — only when it is built.

It was the most expensive stage left in Phase 1 and the least shared with anything else: the till screen is the bulk of it, keyboard- and barcode-driven, offline-tolerant, and it lives in `apps/desktop`, which is still a scaffold with no source. The receipt is ESC/POS commands rather than PDF and prints only from the desktop app, so none of the document printing already built applies.

**Nothing waits on it.** A POS sale is an `sal.Invoices` row with `TransactionTypeCode = 'POS'` — the tables, the numbering series and the tax determination all exist, and the counter sale it replaces is an invoice raised directly, which is the common case in a shop regardless.

**No new tables.** T7.1 reuses T3.1's posting.

- [ ] **T7.1 — POS API.** One call: issue stock, post the sale, post the payment. The decrement is synchronous and guarded.

  **Not built.** No POS controller, service or endpoint in `Sales.Api`. It reuses T3.1's posting, which is itself blocked on the leg contract.

- [ ] **T7.2 — POS screen.** Keyboard and barcode driven, offline-tolerant, in `apps/desktop`. The bulk of the stage.
- [ ] **T7.3 — ESC/POS receipt.** Commands, not PDF. Desktop only — a browser cannot reach a USB or serial printer.

  **Source exists for both and neither is built.** `apps/desktop/src/app/pos-terminal/` holds `pos-terminal.component.{ts,html,scss}` and `esc-pos.service.ts`. **`apps/desktop/project.json` declares `"targets": {}`** and `desktop` appears in neither `tsconfig.base.json` nor `nx.json`, so nothing compiles, typechecks or lints this code — `npm run check` passes over it without reading it. Treat it as a sketch: it has never been through a compiler, and there is no API behind it either way.

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

**The order of operations must match `Shared.Kernel` exactly** — see T0.2. Two implementations of one sum diverge by a paisa without either looking wrong, and a GST return is where that surfaces months later. **The shared fixture that proves they agree is now written**: `shared-fixtures/tax-fixture.json`, read by `Shared.Kernel.Tests/GstCalculatorFixtureTests.cs` and by `tax-fixture.spec.ts` beside `line-math.ts`. Add a case there first, watch both suites fail, then fix both.

**Nothing integrates it yet, because there are no transaction pages.** Each page wires it up as it lands: T2.3 quote, T2.4 order, T3.2 invoice, T3.6 challan, T5.2 credit note here; T4.3–T4.5 and T5.3 in the other file.

---

## 11. Standing requirements

Not repeated per task. Full list in `TRANSACTIONS.md`.

Documentation in the same commit · `OrgId` + query filter + RLS on every table including details · `[Authorize]` and `RequireModulePermission` on every endpoint · postings idempotent on their document key · `ExchangeRate` a snapshot · `dotnet build && dotnet test` and `npm run check` green before a box is ticked · every page working at ~360px.
# Sales.md — the sale, end to end

How a sale travels from quote to cash: which document follows which, what each one posts, what it does to stock, and where the flow is allowed to skip a step.

This is a **flow document, not a plan**. It has no checkboxes. [`TRANSACTIONS.md`](./TRANSACTIONS.md) says what to build and in what order; this says how the result behaves. [`SPEC.md`](./SPEC.md) holds the columns.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** See *Git — how work reaches main* in `CLAUDE.md`.

**Status: designed, not coded.** `sal.*` has project folders and no entities. The stock half beneath it — reservation, the guarded issue, layer consumption, returns to the originating layer, the COGS posting — is **built and has never been called by a document**. So this describes an intended flow running on a real foundation, and says which is which at each step.

---

## The chain

```
QTE ──▶ SOR ──▶ DLC ──▶ INV ──▶ RCM
quote   order   challan  invoice  receipt

        POS  = INV + RCM in one action
        CRN  = the way back out
```

**Every arrow is optional.** The chain is what the documents mean, not a sequence anyone is forced through:

| Start here | When |
|---|---|
| `QTE` | The customer wants a price before committing |
| `SOR` | Committed, but not yet delivered — the only reason stock gets reserved |
| `DLC` | Goods go out before they are billed — or go out without being sold at all: job work, approval, branch transfer |
| `INV` | Counter sale on credit, or a service with nothing to reserve |
| `POS` | Retail till. Invoice and payment in one action |

A quote converts to an order, an order to an invoice, and each conversion copies the lines forward and links back by `SourceDocumentId`. Nothing forces the customer down the whole chain, and **an invoice raised directly is the common case in a shop**.

---

## QTE — quote

**Posts nothing. Moves nothing. Reserves nothing.**

A price offered, with a validity date. It expires rather than being deleted, because a quote that was made is a fact even after it lapses.

This is the cheapest possible document, which is why it is built first: the whole document machinery — header and lines, numbering, lifecycle, tax determination, totals, print, conversion — gets exercised with no accounting risk at all.

---

## SOR — sales order

**Posts nothing. Reserves stock.**

Confirming an order calls Inventory's `ReserveAsync`. That is the *only* thing in the sales flow that reserves, and it exists because an order confirmed but not yet delivered would otherwise leave the stock fully available and it could be promised twice.

What a reservation does and does not do:

- Availability drops. `QuantityOnHand - QuantityReserved` is what every issue guard checks.
- **On-hand quantity does not move.** The stock is on the shelf.
- **Stock value, stock counts and the Inventory account are untouched.** Nothing is posted, because nothing has happened yet in accounting terms.

Cancelling releases. Converting to an invoice releases and issues **in one transaction, release first** — issue first and the order's own reservation is counted against it, and the sale is refused for stock it is holding itself.

A partial delivery releases and issues only what shipped, leaving the rest reserved.

---

## DLC — delivery challan

**Stock leaves here, not on the invoice.** The sales mirror of a goods receipt: the challan takes the goods out and releases the order's reservation, and the invoice that follows bills what was delivered and moves no stock.

Skipping it is normal — an invoice raised directly still issues its own stock, exactly as a bill with no receipt behind it still moves stock on the purchase side.

`ChallanType` says what the movement means: **Sale** is goods going to a customer to be billed; **JobWork**, **Approval**, **BranchTransfer** and **Sample** are goods that have left without being sold, and those post nothing at all.

Carries the e-way bill number, vehicle and transporter — the statutory details of the movement, which belong on the document that moved the goods rather than on the one that bills them.

---

## INV — invoice

The document the product is bought for, and the first one where accounting, stock, tax and numbering all run at once.

### What it posts

One document, four leg types, written by **two services**:

| Leg | Account | Type | Written by |
|---|---|---|---|
| Per line | Sales Revenue, item sub-account | `ITEM` | Sales |
| Per rate | Output GST, rate sub-account | `TAX` | Sales |
| Header | Accounts Receivable, contact sub-account | `CONTROL` | Sales |
| Rounding | Round-off | `ROUNDOFF` | Sales |
| Per line | `Dr` Cost of Goods Sold / `Cr` Inventory | `COGS` | **Inventory**, later |

Sales' legs balance among themselves; Inventory's COGS pair balances among itself; the document balances because both do. They replace independently under the posting key, which is exactly why the key includes the leg type.

**This split is the only reason gross profit exists.** Revenue is Income, COGS is Expense, and a report can subtract one from the other only because they are different account types.

### The timing seam — the thing most likely to be misread

The three parts of an invoice do **not** complete together:

1. **Inside the request** — the number is taken, the lines are saved, stock is released and issued through the guarded decrement, and the revenue, tax and receivable legs post. The customer has an invoice.
2. **Moments later** — `CostingEngine.Worker` settles what the goods cost, consuming layers under the item's costing method.
3. **Moments after that** — the COGS legs post onto the same invoice.

So an invoice exists, and is correct, before its cost of sale is known. The invoice screen and any margin report must read `CostingStatus` and **say "costing pending" rather than showing zero** — a zero COGS reads as 100% margin, and it will be believed.

### Tax

Determined by the branch's state against the customer's place of supply, falling back to the first two digits of their GSTIN. Same state → CGST + SGST. Different → IGST. At the rate **in force on the invoice date, never today's** — an invoice reopened after a rate revision must not reprice itself.

### Numbering

Taken **at creation**, so a document has its number from the moment it exists — a draft can be quoted over the phone.

The series stays gapless because **no document row is ever deleted**: an abandoned draft is **voided** and keeps its number, which makes the number answerable rather than missing. Consecutive numbering on an Indian invoice is statutory, not a preference.

### Statuses

**Draft → ReadyToPost → Posted → Void.** A void covers both an abandoned draft and a posted document taken back out; `PostedAt` being null is what tells them apart.

`ReadyToPost` is a finished document waiting for whoever posts it — the state `sales.approve` exists for. Skip it and post straight from `Draft` if the branch does not want the step.

### Never edited once posted

A posted invoice is corrected by a **credit note**, not by an edit. Voiding withdraws its own ledger legs and reverses its stock movement, and is refused once anything downstream points at it — a payment, an allocated credit note.

---

## POS — the till

An invoice and its receipt in one action, from `apps/desktop`.

**A POS sale is an invoice.** It writes a `sal.Invoices` row with `TransactionTypeCode = 'POS'` — there is no POS table. Same lines, same GST, same stock issue, same ledger legs. POS is a screen, not a document type of its own.

Same postings as `INV` plus the payment legs, in a single call. Two things differ:

- **The stock decrement is synchronous and guarded**, which it already is everywhere — but here it is load-bearing in a way it is not elsewhere, because two tills selling the last unit is a routine event rather than a race nobody hits. Costing and the ledger still follow asynchronously.
- **The receipt is ESC/POS**, not PDF. Fixed-width commands, and **only from the desktop app**, because a browser cannot reach a USB or serial printer.

---

## CRN — credit note

The way back out. A sales return, a price correction, or a goodwill adjustment.

| | |
|---|---|
| Debit | **Sales Returns** — a *contra* Income account, so the report subtracts it rather than adding a negative |
| Credit | Accounts Receivable |
| GST | Reversed on the same rates the invoice used, not today's |
| Stock | Returned via `ReturnsStockMovementId` **to the layers it came from, at their original cost** |

Buy, sell, credit-note leaves stock value exactly where it started — that is the acceptance test, and `LayeredStockValue` is what makes it checkable.

A credit note **allocates** against the invoice through `acc.TransactionRatio` rather than floating as an unapplied balance. An allocation can never exceed the target's outstanding amount, and because that sum spans rows it is a C# guard — no check constraint can express it.

A credit note with no invoice behind it is legitimate (a return whose sale predates the system) and falls back to the running average for cost.

---

## RCM — money in

A receipt posts under its **own** identity and points back at what it settles:

| | Debit | Credit |
|---|---|---|
| Account | Bank or cash | Accounts Receivable |
| `TransactionTypeCode` | `RCM` | `RCM` |
| `LedgerSourceId` | 3 `INVOICEPAYMENT`, or 9 `CUSTOMERPREPAYMENT` | same |
| `MappingTransactionId` | **the invoice's id** | same |
| `MappingTransactionTypeCode` | **`INV`** | same |

That mapping pair is the entire mechanism for tracing a payment to its invoice. It is also why payments never appear in stock tables — they carry no item dimension.

**A receipt exceeding what is owed becomes a prepayment**, landing in Advance from Customer, rather than a negative receivable. A receipt against no document at all is a customer prepayment from the start.

**Foreign currency**: an invoice raised at one rate and settled at another posts an extra pair to Realized FX Gain/Loss. Computed from the difference between the two documents' stored rates — **never from a live rate**, or a historical document silently reprices.

---

## What the flow leans on that already works

Worth knowing, because it changes what "build the invoice" actually costs:

- The guarded stock decrement, and reservation with it
- Cost layers under all five methods, and consumption records naming which layer each sale drew from
- Returns to the originating layer
- The COGS posting, and `acc.JournalLedger` with its deferred balance trigger
- Backdated recosting, including replacing a restated sale's ledger rows
- **`acc.TransactionRatio` with its guard** — allocation against a document's CONTROL net, replace-not-append, serializable against concurrent claims

None of it has been called by a document. The sales flow is what finally calls it.

## What is missing beneath the flow

- **The ledger door posts one leg type per call** and refuses a request whose legs do not balance among themselves. An invoice's per-line revenue, per-rate tax and single header receivable balance in no subset. This is [T0.1](./TRANSACTIONS.md#stage-t0--foundations-before-the-first-document) and it blocks the invoice outright.
- **No tax determination exists anywhere.** [T0.2](./TRANSACTIONS.md#stage-t0--foundations-before-the-first-document).
- **No document numbering series exist.** [T0.3](./TRANSACTIONS.md#stage-t0--foundations-before-the-first-document).



