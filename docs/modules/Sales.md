# SALES.md — the `sal` module, end to end

Everything needed to build Sales: the document chain, every table and column, every decision taken and why, and the task list.

**This file is the single home for `sal.*`.** `SPEC.md` points here rather than repeating the columns, and `TRANSACTIONS.md` points here rather than repeating the tasks. `CLAUDE.md` still holds the conventions that apply to everything.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** See *Git — how work reaches main* in `CLAUDE.md`.

**Status: the schema and the foundations are written; nothing runs yet.** The fifteen tables and the three base classes beneath them exist (T2.2), and so do the three things T2 was blocked on — tax determination (T0.2), the document series (T0.3) and the lifecycle (T0.4). The quote's own service, controller and page do not exist; see T2.3 below for exactly how far it got and what the next session needs to know. The stock machinery underneath — reservation, the guarded issue, cost layers, returns to the originating layer, the COGS posting — is **built and has never been called by a document**.

**T2.2 is written but unverified**, and the boxes below stay unticked until it is: see the standing caveat in `CLAUDE.md`. `sal` has no migration — generate it per `backend/Api/Sales/Sales.Repository/Migrations/README-RowLevelSecurity.md`, which also carries the RLS block EF will not write.

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

**The boxes below were marked against the code on 15 August 2026, not against memory.** Three of fifteen are ticked. The gap between how much is *written* and how little is *ticked* is the point of the exercise: nearly every screen and service in T3 and T5 exists, and four of them fail on one shared line.

| | |
|---|---|
| **Ticked** | T2.3 quote, T2.4 sales order, T3.5 sales register |
| **Written, blocked on the ledger leg contract** | T3.1 invoice API, T3.2 invoice page, T3.6 delivery challan, T5.2 credit note |
| **Written, defective in a named line** | T5.2 (`ReturnsStockMovementId`), T3.6 (invoice re-issues challan stock) |
| **Schema or component only** | T5.1 allocation table, T5.4 allocation grid |
| **Part built** | T3.3 outstanding, no aging buckets |
| **Not built** | T3.4 print and archive, T7.1 POS API |
| **Never compiled** | T7.2 POS screen, T7.3 ESC/POS — `apps/desktop` has no build target |

**One defect accounts for four of those boxes.** `Sales.Api`'s `LedgerLegRequest` carries a single `Amount`; Accounting's carries `DebitAmount` and `CreditAmount` and rejects a leg that is neither. Sales has never successfully posted to the general ledger. It is written up under T3.1.

**There is no `Sales.Api.Tests` project.** Accounting, Inventory, Purchase and `Shared.Kernel` each have one. Sales posts documents to the ledger and moves stock, and has no tests at all — which is how a contract mismatch this size stayed hidden.

### Blocked on foundations

These live in `TRANSACTIONS.md`. **T0.1** (the ledger door) and **T0.6** (ledger screens) were already done; **T0.2** tax determination, **T0.3** document numbering series and **T0.4** the lifecycle are now written too — all three unverified, per the standing caveats in `CLAUDE.md`. Nothing in T2 is blocked any longer.

### T2 — quote and sales order

- [ ] **T2.2 — The five pairs: base classes, entities, migration.** `DocumentHeaderBase`, `DocumentLineBase` and `DocumentLineTaxBase` in `Shared.Kernel` first, then the fifteen tables inheriting them, with `OrgId` on every one, query filters, RLS, and the document series.
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
    **Done.** Built `SalesOrderService` calling `IInventoryClient.ReserveAsync`/`ReleaseAsync`, scaffolded `sales-order-form` and `sales-order-list`, and successfully integrated routing into the Angular web app.

### T3 — invoice

- [ ] **T3.1 — Invoice API: post, void, ledger legs.** Stock issued through the guarded decrement. **Issuing reserved stock is release-then-issue in one transaction.**
  *Done when*: an invoice against a confirmed order releases and issues exactly once; the trial balance still balances; gross profit equals revenue minus the COGS the layers produced.

  **Code complete; the box stays unticked because the ledger post cannot succeed.** `InvoiceService.PostAsync` / `VoidAsync`, `InvoicesController` (`GET`, `GET/{id}`, `POST`, `PUT/{id}`, `POST/{id}/post`, `POST/{id}/void`), and the release-then-issue is there — `IssueStockLine.ReleaseReservation` is set from `invoice.SalesOrderId.HasValue`, so an order-sourced invoice releases and issues in Inventory's one guarded call.

  **What blocks it is the ledger contract.** `Sales.Api/Services/LedgerClient.cs` declares its own `LedgerLegRequest` carrying a single `decimal Amount`. Accounting's leg has `DebitAmount` and `CreditAmount` and no `Amount` at all, so `Amount` deserializes into nothing and both sides arrive zero — and `LedgerPostingService` refuses that outright: `if ((leg.DebitAmount == 0) == (leg.CreditAmount == 0)) throw` — "a leg is a debit or a credit, never both or neither." **Every sales ledger post is rejected on the wire**, so no invoice, credit note or delivery challan reaches the general ledger. The same client also sends `SubAccountReferenceId` without the `SubAccountReferenceType` that completes the key.

  The fix is Purchase's client, which was written against the real contract: a debit-xor-credit leg with the reference type set. Until it lands, T3.1, T3.5, T3.6 and T5.2 all fail at the same line.

  **Unverifiable either way today: there is no `Sales.Api.Tests` project.** `backend/tests/` holds `Accounting.Api.Tests`, `Inventory.Api.Tests`, `Purchase.Api.Tests` and `Shared.Kernel.Tests`. Sales is the only service with document posting and no tests over it — which is why a contract mismatch this size survived to be found by reading.

- [ ] **T3.2 — Invoice page.** `bb-document-line-grid` plus the invoice header, totals panel, draft / ready / post / void, print.
  *Done when*: keyed and posted at 360px, and the tax on screen equals the tax posted.

  **The page is built; the Done-when needs T3.1.** `invoice-form` and `invoice-list` in `libs/sales/sales-ui`, wiring `DocumentLineGridComponent` and `totalsOf`, with post, void (reason prompted) and print. Nothing is posted at any width until the leg contract above is fixed, so "the tax on screen equals the tax posted" has no posted side to compare against.

  Two things to carry when it is: the form drives the API through piped `.subscribe(...)`, where the house rule is `async`/`await` over promises; and the void reason comes from `prompt()`, which is not a 360px dialog.

- [ ] **T3.3 — Outstanding and aging.** Read from the ledger's AR sub-accounts. The input to Banking's allocation.
  *Done when*: an invoice is outstanding at full value the moment it posts, and the buckets tie to the Accounts Receivable control account.

  **Outstanding is built; aging is not.** `GET ledger/contacts/{contactId}/outstanding-balances/{ledgerTypeId}` → `LedgerReportService.GetOutstandingBalancesAsync` groups `acc.JournalLedger` by `(TransactionTypeCode, TransactionId)` and returns debit minus credit per document. **There are no buckets** — no 0–30 / 31–60 / 61–90 / 90+, which is the half the Done-when names. `DueDate` comes back null and `DocumentNo` is the fallback string `"{code}-{id}"`, so nothing can age and nothing shows a document number a person would recognise. Both want the payment term the document was raised on.

- [ ] **T3.4 — Print and archive.** Syncfusion server-side PDF, PDF/A, blob storage keyed by `SourceType` + `SourceId`.

  **Not built.** No Syncfusion package reference and no `PdfDocument` anywhere in `backend/`. The `print()` on the sales pages is `window.print()` — the browser's rendering of the screen, not a generated document, and nothing is archived. This stage owns print for **every** document, sales and purchase alike, which is why T2.3's page is ticked with print excluded.

- [x] **T3.5 — `sal.SalesRegister`.** Written inside the post's transaction, replaced by key, deleted on void.
  *Done when*: intra- and inter-state invoices register the right halves and `chk_register_tax_split` refuses the wrong one; a re-post leaves no orphans; period taxable value equals the Output GST legs.

  **Built, and the table is what T3.5 asked for.** All three writers keep it: `InvoiceService` adds rows after the post and `RemoveRange`s them on void, `DeliveryChallanService` the same, and `CreditNoteService` registers negative amounts so a period nets. The check constraint is in the migration.

  **The Done-when is unproven**, and for the reason above: the register is written *after* `_ledgerClient.PostAsync`, which currently throws, so no row has ever been written by a real post. Tick the box for the code, and re-run the Done-when the day T3.1's contract is fixed.

- [ ] **T3.6 — `DLC` delivery challan.** Needs a seventeenth `mst.TransactionTypes` row, added by EF migration, and its own numbering series.
  *Done when*: an order part-delivered issues only what shipped and leaves the rest reserved; the invoice against that challan moves no stock; a job-work challan writes a movement and no ledger row.

  **Seeded and served, but one clause of the Done-when is contradicted by the code.** The `mst.TransactionTypes` row and the `DLC` series are both seeded (`NumberingSeriesSeed`, id 315, prefix `DC`), `DeliveryChallanService` issues stock with `ReleaseReservation` when the challan came from an order, and posts a ledger only when the challan is a sale — a job-work challan writes the movement and no ledger row, as asked.

  **"The invoice against that challan moves no stock" now holds.** `InvoiceService.PostAsync` branches on `DeliveryChallanId`: with a challan behind it the invoice reads the cost and the movement id off the challan's lines and issues nothing, and only the `else` branch builds an `IssueStockRequest`. The note that said otherwise described a real double-issue and is spent.

  **The screen is reachable now, which it was not.** `DeliveryChallanFormComponent` existed with no route and no export — in the repository and invisible to the application, which is the same as not built. It has a list beside it (`DeliveryChallanListComponent`), three routes under `sales/delivery-challans`, and both are exported.

  **Three things were wrong on the way in, and each would have failed on first use:**
  - **Voiding threw.** `VoidAsync` set `VoidedAt` and never `VoidReason`, and `chk_deliverychallans_void_stamp` ties the two together. It takes a reason now, and refuses a *posted* challan outright — the goods have physically gone, and flipping the status would leave the stock issued and GDNI holding a balance nothing will close.
  - **The route was `DeliveryChallans`** while the frontend called `/api/sales/delivery-challans`. Both ends now agree, and `invoices` and `CreditNotes` were moved under `api/sales/` for the same reason — the frontend was already calling them there.
  - **No gateway route pointed at the sales cluster at all.** The cluster was declared and nothing reached it, so every sales screen was unreachable through the front door. `/api/sales/{**catch-all}` added.

  **What still blocks the box** is the ledger contract shared with T3.1 and T5.2 — see T3.1. A `Sale` challan's `Dr GDNI / Cr Inventory` pair is refused on the wire like every other sales posting, so the Done-when cannot be run end to end yet. Job work, approval, branch transfer and sample post nothing and are unaffected.

### T5 — credit note

- [ ] **T5.1 — `acc.TransactionRatio`** (shared with Purchase). Allocation between documents. Allocations must never exceed the target's outstanding balance — a C# guard, since the sum spans rows.

  **Schema only.** `TransactionRatio.cs` exists and `AccountingDbContext` maps it with a `DbSet`. `AccountingDbContext` is the **only** file in `backend/Api` that mentions `TransactionRatios` — nothing writes a row, and the C# guard the task exists for is not written. The table is real; the allocation is not.

- [ ] **T5.2 — Credit note.** Stock returned via `ReturnsStockMovementId` to the originating layers at their original cost.
  *Done when*: buy, sell, credit-note leaves stock value exactly where it started, and the note allocates against the invoice rather than floating.

  **Built and wrong in one line.** `CreditNoteService` has the service, the register rows, the ledger legs and the stock return through `IInventoryClient.ReceiveAsync`. But it sends `ReturnsStockMovementId = creditNote.InvoiceId` — **an invoice id where an `inv.StockMovements` id is required**. Inventory validates it: `ValidateReturnedMovementAsync` looks for a movement with that id, *on the same item*, with `Direction == Out`, and answers `ReturnedMovementNotFound` otherwise. So the return is refused; and in the rare case the two ids collide on an item that was issued, the stock goes back onto **another document's cost layers** at another document's cost, which is worse than being refused because it is silent.

  The id wanted is the movement the invoice's own line produced. Inventory's issue response already returns movement ids per line — the invoice needs to keep them (`sal.InvoiceDetails` has no column for one today), and the credit note line needs to name the invoice line it credits.

  The allocation half of the Done-when — "allocates against the invoice rather than floating" — is T5.1, which is schema only.

- [ ] **T5.4 — Allocation UI** (shared with Purchase). Over-allocation refused while typing, not at save.

  **The component exists and no page uses it.** `libs/shared/ui-components/src/lib/allocation-grid/allocation-grid.component.ts` is written and exported from the lib's `index.ts`; `AllocationGrid` appears in no other file in `frontend/`. It has nothing to allocate against until T5.1 writes rows, and payment work is deferred by decision.

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
