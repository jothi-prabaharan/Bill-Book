# TRANSACTIONS.md — the trading documents

`CLAUDE.md` holds the conventions. [`SPEC.md`](./SPEC.md) holds tables and pages. [`master.md`](./master.md) holds the build order up to here. **This file holds the plan for the eleven document types owned by Sales, Purchase and Inventory** — the trading half, none of which is built.

The documents owned by **Accounting and Banking** are in [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md) and are not repeated here. **Stage numbers were not renumbered when the two files were split**, so the numbering below skips T1, T6, T8 and T10 — those stages live in that file, and the gaps are deliberate rather than a mistake.

Same rules as `master.md`: take the first unticked box, check it against its **Done when** line, tick it in the same commit as the work, and strike a task rather than deleting it if it turns out to be wrong.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** A branch invented mid-task splits the work across two places and leaves whichever one nobody merges behind. See *Git — how work reaches main* in `CLAUDE.md`.

**Flow documents, beside this plan and not part of it**: [`FLOW-SALES.md`](./FLOW-SALES.md), [`FLOW-PURCHASE.md`](./FLOW-PURCHASE.md) and [`FLOW-STOCK.md`](./FLOW-STOCK.md) describe how these documents behave once built. No checkboxes — this file is the work, those are the behaviour.

---

## Scope — the eleven trading documents

The rows of `mst.TransactionTypes` owned by Sales, Purchase and Inventory. Three post nothing; eight reach the ledger. All eleven trade something — an item, a price, GST, a cost layer — which is what separates them from the money documents in the other file and why they need the foundations below.

| Code | Document | Owner | Posts | Moves stock | Stage |
|---|---|---|---|---|---|
| QTE | Quote | Sales | no | no | T2 |
| SOR | Sales order | Sales | no | reserves | T2 |
| DLC | Delivery challan | Sales | yes | **issues** | T3 |
| INV | Invoice | Sales | yes | issues | T3 |
| POR | Purchase order | Purchase | no | no | T4 |
| GRN | Goods receipt | Purchase | yes | receives | T4 |
| BIL | Bill | Purchase | yes | no¹ | T4 |
| CRN | Credit note | Sales | yes | returns | T5 |
| DBN | Debit note | Purchase | yes | returns | T5 |
| POS | POS sale | Sales | yes | issues | T7 |
| STA | Stock adjustment | Inventory | yes | adjusts | T9 |

¹ A bill against a goods receipt moves no stock — the receipt already did. A bill with no receipt behind it does, and is the common case for services and for a trader who never raises a GRN.

---

## What these land on

Worth stating, because the foundations below are the gaps in it rather than a rewrite of it.

- **`acc.JournalLedger` and `LedgerPostingService`** — the single posting target and the one door into it. Accounts are named, never numbered; a posting is replaced by key, never appended to; balance is checked in the service, by an insert-time constraint and by a deferred trigger at `COMMIT`.
- **Stock** — a guarded conditional decrement, reserve and release, cost layers under five costing methods, returns to the originating layer, and backdated recosting. `inv.StockMovements` is idempotent on `(OrgId, SourceType, SourceId, SourceLineId)`, which is the key every document below writes through.
- **`NumberGenerator`** — takes a number inside the caller's transaction, so a failed insert gives the number back.
- **Masters** — 16 transaction types seeded (`DLC` is the seventeenth and arrives with T3.6), 6 ledger types, 15 ledger sources, effective-dated GST rates, payment terms, a chart of accounts with ten control accounts, and AR/AP sub-accounts per contact and Inventory/COGS/Revenue sub-accounts per item, all seeded at branch creation.
- **`sales.*` and `purchase.*` permissions** are already seeded and granted to the system roles. Nothing needs adding to the matrix.

---

## Stage T0 — foundations, before the first document

None of these is a document. All five are things a document immediately needs and none of which exists, and each one found later is a schema change in the same commit as a screen.

**T0.5 (`acc.Journals`) and T0.7 (does a document write a `Journals` row?) moved** to [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md#foundations-owned-here) — both exist only for the manual journal. The five below stayed because Sales and Purchase need them too, and a shared prerequisite belongs with the shared prerequisites.

- [x] **T0.1 — The ledger door takes one leg type per call. A document has four.**
  `PostLedgerRequest` carries a single `LedgerTypeId` and a single `TransactionDetailId` for the whole request, and refuses the request unless its own legs balance. An invoice's `ITEM` legs are per-line credits to Sales Revenue, its `TAX` legs are per-rate credits to Output GST, and the one debit to Accounts Receivable is a header-level `CONTROL` leg at detail `0`. **No subset of those balances on its own**, so an invoice cannot be posted through the door as it stands. Stock never hit this because a stock posting is exactly two legs, one type, one amount — balanced by construction, as its own comment says.
  Move `LedgerTypeId` and `TransactionDetailId` onto the leg; check balance across the request rather than per key; scope the replace to `(TransactionTypeCode, TransactionId)` intersected with the leg types present in the request. That last part is what preserves the property 5.12 deliberately built — Sales' revenue legs and Inventory's COGS legs sit on one invoice and replace independently. Withdrawal (an empty leg list, used by void) then has to name the leg types explicitly, since there are no legs to infer them from.
  *Done when*: a three-line invoice with two GST rates, one receivable leg and a round-off posts in one call; re-posting it replaces its own rows and leaves the COGS rows Inventory wrote untouched; and voiding it withdraws its four leg types and no others.
  **Done**, and all three cases are tests against a real PostgreSQL rather than assertions in prose.
  **One correction to the plan as written.** "Scope the replace to `(TransactionTypeCode, TransactionId)` intersected with the leg types present" would have been a data-loss bug: Inventory posts a document **one movement at a time**, so a document-wide COGS replace has line two delete line one's rows. What was built instead keys the replace on the `(leg type, line)` pairs the request actually names — which still gives Sales and Inventory independent replacement on one invoice, the property this task exists to preserve. **Withdrawal stays document-wide** by leg type, because a void has no legs to name pairs with and its whole claim is that none of those legs exist any more. The asymmetry is deliberate and documented at the call site.
  Also added: a leg may name its account by **id** as well as by system name. Only seeded control accounts have a system name, and a manual journal posts to accounts that have none — the id stays barred to callers outside Accounting, for the reason the model always gave.
  **`LedgerSourceId` moved onto the leg too**, in the same shape as `LedgerTypeId` but for a different reason. The leg type moved because no subset of a document's legs balances on its own; the source moved because **one document can be several things at once**. An overpayment settles a bill with part of itself and leaves the rest as an advance — stamp the whole document one source and a payables report filtering on bill payments misses the part that was one. See T6.2 in the other file.

- [ ] **T0.2 — There is no tax determination anywhere**
  `CLAUDE.md` requires **one** component shared by Sales and Purchase. Same state → CGST + SGST, different state → IGST, decided from the branch's own state against the contact's place of supply, falling back to the first two digits of the GSTIN when place of supply is unset. Rates come from `acc.TaxMasters` **as in force on the document date, never today's** — an invoice edited after a rate revision must not reprice itself.
  Split it: a **pure calculator in `Shared.Kernel.Tax`** that takes lines, rates and the two state codes and returns the per-line and per-rate breakdown, and a rate lookup served by Accounting (`GET internal/tax/rates?on={date}`) cached per branch and date. Pure because this is the piece that fails silently — a wrong split still balances, still prints, and is only caught by a GSTR-1 that a human has to reconcile.
  Three sub-decisions to settle here rather than per document: inclusive vs exclusive pricing, whether a line discount reduces the taxable value (it does), and whether tax rounds per line then sums or sums then rounds. Pick one, test it, and write it down.
  *Done when*: the same item and rate produce CGST + SGST intra-state and IGST inter-state, at the rate in force on the document date; the sum of line taxes equals the header tax to the paise; and a contact whose GSTIN state code contradicts its place of supply is refused rather than posted.

- [ ] **T0.3 — No document numbering series exist**
  `NumberingSeriesSeed` seeds five master series and says outright that document series arrive with the services that own them. Each service seeds its own on branch creation: `SeriesFor.Document`, `AllowManualOverride = false` — a hand-keyed invoice number is not allowed on an Indian invoice — and reset by financial year, whose start month 5.3 already resolves per branch.
  **The number is taken at post, not at draft.** A draft that is never posted must not consume a number in a series that has to be gapless, and a user who opens a new invoice and closes it has done exactly that. Drafts carry no number and show as "unnumbered" until posted.
  *Done when*: two invoices posted concurrently take consecutive numbers, a post that fails takes none, and abandoning a draft leaves the series where it was.

- [ ] **T0.4 — One lifecycle, the same for every type**
  Draft → Posted → Void, plus Reversed for journals. **A posted document is never edited** — an invoice is corrected by a credit note, a journal by a reversing journal. Void withdraws the posting, releases any reservation and reverses any stock movement, and is refused once anything downstream points at the document: a paid invoice, an allocated credit note, a received order.
  `RequireModulePermission` maps GET to `.view`, DELETE to `.delete` and **everything else to `.edit`**, which was the right three lines for masters and is not enough here. `sales.void` and `sales.approve` are seeded and granted and would be reachable by anyone holding `sales.edit`. Add an action override to the attribute for the routes that void, approve or print.
  *Done when*: a posted invoice refuses an edit; a void withdraws exactly its own ledger rows and releases exactly its own reservation; and a user holding `sales.edit` but not `sales.void` is refused the void.

- [x] **T0.6 — Nothing displays a ledger, and everything below writes to one**
  `acc.JournalLedger` accepts postings and stock already writes to it, and there is no screen. Build the account ledger and the trial balance **before the first document**, not after: from here every stage is verified by whether a posting is right, and a posting that can only be read with SQL will be checked by nobody. It also closes the presentation half of master.md 4.4, which has been waiting for somewhere to be shown.
  Account ledger — account, date range, running balance, drill to the document. Trial balance — every account, debit and credit totals, and the two agreeing, which is the one number that says the whole system is sound.
  **Settle `acc.vw_LedgerDetail` here.** SPEC flags it: `CREATE VIEW` is not in `CLAUDE.md`'s raw-SQL exception list, and a view that omits `security_invoker = true` bypasses RLS and leaks the general ledger across branches. *Recommendation: don't add the view.* Do the join as a LINQ projection in Accounting and compute the running balance in C# over the ordered, account-scoped, date-ranged result — a ledger screen is always all three of those, so the window function buys less than the exception costs.
  *Done when*: a trial balance built from the stock postings already in the ledger balances, and every posting in it drills back to the movement that wrote it.
  **Done.** `LedgerReportService` + `api/ledger`, and two pages under Accounting. **The view was not added**, per the recommendation — both reads are LINQ projections, and the running balance is accumulated in C# over the ordered, account-scoped, date-ranged result.
  Two things worth recording. Balances are held **in debit terms** — positive is a debit balance — and turned the right way up by the screen, which already loads `mst.AccountTypes` for its normal balance; keeping a second copy of which types are which inside Accounting is exactly how two reports come to disagree. And the trial balance shows each account in **one** column, the side its net falls on, rather than both its totals: two columns of gross totals always agree, because every posting was balanced when it was written, so they would prove nothing.
  Six tests against a real PostgreSQL, over postings written through the door rather than rows the test inserted — a test that hand-wrote the ledger would be checking its own arithmetic.

---

## Stage T2 — the sales skeleton: quote and order (QTE, SOR)

Neither posts. That is the point of taking them first — the document machinery (header/lines, numbering, lifecycle, tax, totals, print, conversion) gets built and tested with no accounting risk at all.

- [x] **T2.1 — Decide the `sal` document shape** *(owner decision, and the largest one here)*
  Five sales documents share perhaps ninety per cent of their columns: contact, dates, currency and rate, addresses, lines with item/quantity/rate/discount/tax, totals, terms, status. *Recommendation: one document table pair discriminated by `TransactionTypeCode`*, with the type-specific columns nullable — validity date on a quote, delivery date on an order, due date on an invoice. One list screen, one numbering path, and conversion becomes a copy.
  The cost is a wide table with nullable columns whose applicability lives in C#. The alternative is ten tables and five near-identical services, and every cross-document report joining all of them.
  **Answered, then reversed. The decision is a table pair per document type**, not one discriminated pair: `sal.Quotes`, `sal.SalesOrders`, `sal.DeliveryChallans`, `sal.Invoices`, `sal.CreditNotes`, each with its details **and a tax child table under those details**. Fifteen tables — **a POS sale is an `Invoices` row, not a table of its own**; see T7. Columns are in [`SPEC.md`](./SPEC.md).
  The first answer was `sal.Sales` / `sal.SalesDetails`; the owner changed it. **Recorded rather than rewritten**, because the reasoning on both sides still applies to `pur.*` in T4.2 and to anyone who revisits this.
  **What a table per type buys.** A conversion link becomes a real foreign key — `Invoices.SalesOrderId`, `CreditNotes.InvoiceId` — instead of one polymorphic column the database cannot enforce. Type-specific columns become `NOT NULL` where they belong: a quote must have `ValidUntil`, an invoice must have `DueDate`, a credit-note line must name the invoice line it reverses.
  **What they cost, and the thing to get right first.** The columns are identical across all five pairs, so they go in **base classes in `Shared.Kernel`** — `DocumentHeaderBase`, `DocumentLineBase` and `DocumentLineTaxBase` — and are inherited, never copied. Hand-maintained copies of a GST split is how a column comes to mean one thing on an invoice and another on a credit note. And every cross-document read now unions five tables: customer history, the day book, the monthly sales report, the register. Write that union once as a projection or it gets copied into every screen.
  **One knock-on already applied**: `sal.SalesRegister` loses its foreign key. It is fed by two tables now, so it keys on `(TransactionTypeCode, SourceId)` with no FK — which means cascade delete is gone and a void has to delete its register rows explicitly.
  **A line does not need an item.** `ItemId` is nullable, so a line can be a description, a quantity and a unit price — a service, freight, a one-off charge. Such a line touches no stock, gets no COGS leg, and posts to a named `AccountId` rather than through an item's sub-accounts, which means **item-level revenue reporting will not see it**. A service you sell repeatedly belongs in the item master as `ItemType = Service` instead, where it gets a code, a default rate and a sub-account.
  **`TaxTreatment` on every line — Taxable / ZeroRated / NilRated / Exempt / NonGst**, snapshotting the item's `TaxPreference`. Charging nothing and being outside the tax are different facts and GSTR-1 reports them in different tables, so a zero amount cannot stand in for either. It is orthogonal to `IsPriceInclusive`, which says only how the price was quoted. On purchase it does a second job: ITC is not claimable on an exempt supply, and the proportional reversal is computed from the lines.
  **A line's tax is rows, not columns.** Intra-state is two components and inter-state is one, so fixed `Cgst`/`Sgst`/`Igst` columns are a shape that only ever half-applies. Each detail table gets a tax child table carrying the component, the **resolved GST sub-account**, the rate and the amount — which is what the `TAX` ledger leg posts against, so the line records where it went instead of the posting re-deriving it. It also makes a **zero-rated supply legible**: with flat columns a 0% intra-state line and a 0% inter-state line are identical, and GSTR-1 has to tell them apart.
  *Open, not blocking*: jewellery lines want making charge, wastage and metal rate. With a table per type that is five extensions or five sets of columns — settle it before the first pair is built.
- [ ] **T2.2 — The five `sal.*` pairs and their tax rows: base classes, entities and migration** — per the SPEC entry T2.1 wrote. `DocumentHeaderBase` and `DocumentLineBase` in `Shared.Kernel` first, then the fifteen tables inheriting them — five header/detail pairs plus a tax child table per detail, with `OrgId` on **every** table, query filters, RLS and the document series from T0.3.
  *Done when*: `migrations add` produces an empty migration and the RLS policies are present in the database, not just the model.
- [ ] **T2.3 — Quote: API and page** — create, edit, print, convert to order, expire.
  *Done when*: a quote prints, converts to an order, and writes nothing to the ledger or to stock.
- [ ] **T2.4 — Sales order: API and page, reserving stock** — confirming an order calls Inventory's `ReserveAsync`; cancelling or converting releases. 5.13 built the guarded reserve for exactly this and nothing has called it yet.
  *Done when*: confirming an order for the last unit makes it unavailable to a second order while leaving on-hand quantity, stock value and the inventory account untouched; and cancelling gives it back.

---

## Stage T3 — the invoice (INV)

The flagship screen, and the first document where accounting, stock, tax and numbering all run at once.

- [ ] **T3.1 — Invoice API: post, void, and the ledger legs**
  `Dr Accounts Receivable` (CONTROL, contact sub-account) / `Cr Sales Revenue` per line (ITEM, item sub-account) / `Cr Output GST` per rate (TAX, rate sub-account), plus ROUNDOFF where the total rounds. Stock is issued synchronously through the existing guarded decrement; Inventory's costing worker settles COGS and posts the `Dr COGS / Cr Inventory` legs onto the same document, which is the two-writers case T0.1 preserves.
  **Issuing reserved stock is release-then-issue in one transaction** — issue first and the order's own reservation is counted against it.
  *Done when*: an invoice raised against a confirmed order releases its reservation and issues the stock exactly once; the trial balance still balances; and gross profit on the item equals revenue minus the COGS the layers actually produced.
- [ ] **T3.2 — Invoice page** — contact, item lines with live tax by place of supply, totals panel, draft/post/void, print.
  *Done when*: an invoice can be keyed and posted at 360px, and the tax shown on screen equals the tax posted.
- [ ] **T3.3 — Outstanding and aging** — what a contact owes, per document, read from the ledger's AR sub-accounts. The input to T6's allocation.
  *Done when*: an invoice appears as outstanding at its full value the moment it is posted, and the aging buckets tie to the Accounts Receivable control account.
- [ ] **T3.4 — Document print and archive** — Syncfusion server-side PDF, PDF/A, archived to blob storage keyed by `SourceType` + `SourceId`. `IFileStorage` is done, both implementations.
  *Done when*: a posted invoice prints identically today and after a re-post, and the archived copy is retrievable by document id.
- [ ] **T3.5 — `sal.SalesRegister`** — the sales register, designed in [`SPEC.md`](./SPEC.md). **Not a ledger and it posts nothing**: `acc.JournalLedger` stays the single posting target and the trial balance still sums one table. This carries taxable value and tax split at the grain a GST return is filed in — `(SaleId, HsnSacCode, GstRate)` — which is why it is stored rather than derived: B2B is reported per invoice per rate and the HSN summary per HSN per rate, and neither falls out of a header row or a line row.
  Written **inside the post's own transaction**, replaced by `(type, document)` on a re-post, deleted on void. That discipline is the only thing standing between a denormalisation and two truths, so it is not optional and not a background job.
  `SupplyType` is classified once at post rather than at filing — the rule reads the party's GSTIN, the place of supply and the invoice value together, and re-deriving it later against a contact who has since registered would move a supply between return sections.
  Extended by T5.2, which adds the credit-note rows and the link back to the invoice they amend.
  *Done when*: an intra-state and an inter-state invoice register with the correct halves of the split and `chk_register_tax_split` refuses the wrong one; a re-post leaves no orphan rows; a void leaves none at all; and register taxable value for a period equals the Output GST legs in the ledger for that period.

- [ ] **T3.6 — `DLC` delivery challan** — *the step the sales chain was missing*
  Purchase has order → **receipt** → bill. Sales had only order → invoice, so **stock could leave only on an invoice**. That breaks deliver-today-invoice-later, part deliveries against one order, goods sent on approval, branch transfers and job work — and an e-way bill hangs off the challan, not the invoice.
  `sal.DeliveryChallans` and `sal.DeliveryChallanDetails`, designed in [`SPEC.md`](./SPEC.md). The challan issues the stock and releases the order's reservation; the invoice that follows bills what was delivered and moves no stock, exactly as a bill against a goods receipt moves none.
  **Needs a seventeenth `mst.TransactionTypes` row**, `DLC`, added by EF migration — a code added at runtime would have no posting logic behind it. Its own numbering series comes with it.
  **What it posts is an open decision, and the exact mirror of T4.1.** Issuing as `Dr COGS` at delivery books cost with no revenue against it. *Recommendation: a `Goods Delivered Not Invoiced` control account (Asset) — `Dr GDNI / Cr Inventory` on the challan, `Dr COGS / Cr GDNI` on the invoice.* A challan for job work, approval or a branch transfer posts nothing: nothing was sold.
  *Done when*: an order part-delivered on a challan issues only what shipped and leaves the rest reserved; the invoice raised against that challan moves no stock; and a job-work challan writes a stock movement and no ledger row.
---

## Stage T4 — purchase: order, receipt, bill (POR, GRN, BIL)

Mirrors T2–T3 and reuses the tax component, the numbering and the lifecycle unchanged. The one genuinely new question is what a receipt posts before its bill arrives.

- [ ] **T4.1 — Decide goods-received-not-invoiced** *(owner decision)*
  A receipt puts stock on the shelf; the bill that values it may come days later. Posting nothing at receipt leaves the inventory asset understated for those days — the stock exists and the books do not know. *Recommendation: seed a **Goods Received Not Invoiced** control account (Liability), post `Dr Inventory / Cr GRNI` at receipt, and `Dr GRNI / Cr Accounts Payable` (plus `Dr Input GST`) at the bill.* A bill with no receipt behind it debits Inventory directly.
  This changes `StockLedgerMapping`, which today returns no posting for a sourced receipt on the grounds that Purchase will post it — that stays true, but Purchase now posts at the receipt rather than only at the bill. It also adds an account to the chart-of-accounts seed, which is idempotent per account since 1.4, so existing branches pick it up by re-running the seed.
- [ ] **T4.2 — `pur.*` schema, entities and migration** — the same per-type split as T2.1: `pur.PurchaseOrders`, `pur.GoodsReceipts`, `pur.Bills`, `pur.DebitNotes`, each with its details and tax rows — twelve tables, all on T2.2's three base classes. Columns are designed in [`SPEC.md`](./SPEC.md).
  **`VendorBillNo` is the column with no sales equivalent.** On a sale we issue the number; on a purchase the vendor does, and input tax credit is claimed against theirs. So a posted bill needs both — `DocumentNo` for internal reference and `VendorBillNo` + `VendorBillDate` for the return — with a unique index on `(OrgId, ContactId, VendorBillNo, financial year)` so one vendor cannot bill the same number twice and a duplicate ITC claim is refused at entry.
  **`LineType` is stock, expense or capital, and lives in `DocumentLineBase`, not on `BillDetails` alone.** On a bill the third puts a purchase on the fixed asset register (T10.2); on an **invoice** it is how a fixed asset is disposed of (T10.4). Every other sales line is `Stock`. Only the *accepted* quantity on a receipt becomes stock.
  *Done when*: `migrations add` produces an empty migration, RLS policies are in the database, and a second bill carrying a vendor number already used that year is refused.
- [ ] **T4.3 — Purchase order: API and page** — no posting, no reservation. Ordering stock does not reserve anything; it is not there yet.
- [ ] **T4.4 — Goods receipt: API and page** — receives stock at the order's cost, opens the cost layer, posts per T4.1. Batch, expiry and serial capture belong here, in the request, because they are user input.
  *Done when*: a receipt against an order opens a cost layer at the received cost, and a partial receipt leaves the order partly open.
- [ ] **T4.5 — Bill: API and page** — with or without a receipt, with the Input GST legs and payment terms driving the due date.
  **A bill line is stock, expense or capital**, and the third is how every purchased fixed asset gets onto the books — it posts to a Fixed Asset account and creates the register row, rather than to Inventory. The line flag belongs here; what it then does is [T10.2](./TRANSACTIONS-ACCOUNTING-BANKING.md#stage-t10--fixed-assets-acquisition-depreciation-disposal-dep) in the other file. **T10 is now Phase 2**, so this task no longer blocks it — but the flag is still Phase 1, because a bill has to be able to say a line is capital whether or not the register exists to receive it yet.
  *Done when*: a bill against a receipt clears GRNI to the paise and moves no stock; a bill with no receipt does move stock; a capital line moves neither and lands on a Fixed Asset account; and payables aging ties to the Accounts Payable control account.

---

## Stage T5 — credit and debit notes (CRN, DBN)

A sales return and a purchase return. Both reverse value and both put stock back **on the layers it came from** — 4.5 built that and nothing calls it yet.

- [ ] **T5.1 — `acc.TransactionRatio`** — allocation between documents, designed in SPEC and unbuilt. Built here rather than in T0 because an allocation table with nothing allocating cannot be tested. Allocations must never exceed the target's outstanding balance, and the sum spans rows, so that is a C# guard and cannot be a check constraint.
- [ ] **T5.2 — Credit note** — `Dr Sales Returns` (contra Income, so the report subtracts it) `/ Cr Accounts Receivable`, with the GST reversed on the same rates as the invoice, and stock returned via `ReturnsStockMovementId` to the originating layers.
  *Done when*: buy, sell, credit-note leaves stock value exactly where it started and the ledger with it; and the note allocates against the invoice rather than floating as an unapplied balance.
- [ ] **T5.3 — Debit note** — the purchase mirror: `Dr Accounts Payable / Cr Purchase Returns` (contra Expense), Input GST reversed, stock returned to its layers.
- [ ] **T5.4 — Allocation UI** — apply a note across one or several documents, with the outstanding balance shown and over-allocation refused at the point of typing rather than at save.

---

## Stage T7 — POS sale (POS)

An invoice and its receipt in one action, from `apps/desktop`, which today has no source files at all.

**No new tables. POS is a UI module.** A till sale is a row in `sal.Invoices` with `TransactionTypeCode = 'POS'` — same lines, same GST, same stock issue, same ledger legs as an invoice. `sal.PosSales` was designed and then removed at the owner's direction, and the reasoning holds: two tables for one document means two places to fix a GST bug. The POS-only columns — till, cashier, payment mode, tendered, change — are nullable on `sal.Invoices`.

- [ ] **T7.1 — POS API** — one call that issues stock, posts the sale and posts the payment, writing an `Invoices` row. The stock decrement is **synchronous and guarded**, per `CLAUDE.md`, or two tills oversell the last unit; costing and the ledger follow asynchronously as they already do.
  It reuses T3.1's invoice posting rather than repeating it. What is genuinely new is the payment in the same call and the till fields, not the document.
- [ ] **T7.2 — POS screen** — keyboard and barcode driven, offline-tolerant, whole thing in `apps/desktop`. This is the bulk of the stage.
- [ ] **T7.3 — ESC/POS receipt** — commands, not PDF; fixed-width; desktop only, because a browser cannot reach a USB or serial printer.
  *Done when*: a sale rings up, prints and decrements stock with the network to Accounting down, and reconciles when it returns.

---

## Stage T9 — stock adjustment as a document (STA)

Movements already post as `STA` when they have no document behind them, each filed under its own movement id. What is missing is the document: a sheet of lines with a reason and an approval, rather than one movement at a time.

- [ ] **T9.1 — `inv.StockAdjustments` header and lines**, with a reason and an approver, posting through the existing mapping under one document id instead of per movement.
- [ ] **T9.2 — Physical count** — enter counted quantities, adjust to the difference, and post the sheet as one document.
  *Done when*: a count sheet of twenty items posts as one document with twenty movements, and the ledger shows one adjustment rather than twenty.

---

## Standing requirements

These apply to every stage in **both** files and are not repeated in the tasks. This is the only copy — [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md) points here rather than restating them, because one requirement copied to two files is one requirement that drifts.

- **Documentation ships in the same commit** — a page under `frontend/apps/docs/content/`, its status in `docs.manifest.ts`, and a bullet under **Unreleased** in `release-notes.md`.
- **Every table inherits `AuditableEntity`** — all four audit columns plus the `xmin` concurrency token, on every table these stages add, with **no exemptions**. The ones worth naming because they get overlooked: **detail tables** (every document line table below), join tables, and seeded reference rows all carry them. Per-customer tables inherit `OrgScopedEntity`, which extends `AuditableEntity`, so they get both from one base class. Never set the values by hand — `AuditSaveChangesInterceptor` writes them, and `CreatedBy IS NULL` is what marks a row as seed data rather than something a user typed. CLAUDE.md hard rule 6; column-level detail in [`SPEC.md`](./SPEC.md).
- **Every per-customer table** gets `OrgId`, a global query filter and an RLS policy — **detail tables included**. SPEC has `acc.JournalDetails` scoped through its parent header instead, and T0.5 overrode that when it built the table: scoping through a parent means no EF query filter at all and an RLS policy that has to subquery the header, which is strictly weaker than the two lines every other table gets for nothing. Every detail table in the product carries its own `OrgId` — follow that, not SPEC. The filter is the first line of defence, not the last.
- **Every endpoint** carries `[Authorize]` and `RequireModulePermission`, and validates the caller's `OrgId` against the target's, returning `Forbid()` rather than `NotFound()`.
- **Every posting is idempotent** on its document key, so a retried request replaces rather than doubles.
- **`ExchangeRate` is a snapshot at document date**, never looked up live, on every document that carries one.
- **Both checks pass before a box is ticked** — `dotnet build && dotnet test` in `backend/`, `npm run check` in `frontend/`.
- **Every page works at ~360px**: grids become card lists, forms stack, modals become full-screen sheets.

## Open decisions, gathered

Answering these before the stage that needs them is much cheaper than after.

| # | Question | Needed by | Recommendation |
|---|---|---|---|
| T2.1 | One discriminated `sal` document table, or a table pair per type? | T2 | One pair, discriminated |
| T4.1 | Does a goods receipt post to a GRNI clearing account? | T4 | Yes, and seed the account |
| ~~T0.6~~ | ~~`acc.vw_LedgerDetail` as a database view, or a LINQ projection?~~ | ~~T0~~ | **Settled: LINQ projection.** The view was not added and the raw-SQL exception list did not grow |
| — | Should a branch declare its trade, so documents and settings narrow themselves? | any | *(open in `CLAUDE.md`, master.md 5.14)* |

Two more are in [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md#open-decisions-owned-here), and **one of them reached back into this file**: T0.7, whether every document also writes an `acc.Journals` row. **It has been answered — manual journals only.** Nothing in this file writes a `Journals` row; every document here posts straight to `JournalLedger` under its own type and id, exactly as 5.12 established.

## Sequencing, and the one place it is arguable

T1 — in the other file — before everything, because a posting nobody can read is a posting nobody checks. Then T2 before T3 here, because the document machinery is worth getting wrong somewhere that posts nothing.

**Sales before Purchase is the arguable one.** The natural demonstration is buy then sell, and a goods receipt is a simpler document than an invoice — no reservation, no layer consumption, no COGS. Taking Purchase first would also let the invoice be tested against stock that arrived at a real cost rather than an opening balance.

Sales is put first anyway, on two grounds: the invoice is the screen this product is bought for, and the sale path already has the most machinery built behind it — the guarded decrement, layer consumption, returns to the originating layer and the COGS posting all exist and have never been called by a document. The cost of the choice is that stock has to be seeded through opening receipts until T4 lands, which already works.

Swapping T3 and T4 costs nothing structural if the owner prefers the buy-then-sell loop.
