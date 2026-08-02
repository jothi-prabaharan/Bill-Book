# TRANSACTIONS.md — the trading documents

`CLAUDE.md` holds the conventions. [`SPEC.md`](./SPEC.md) holds tables and pages. [`master.md`](./master.md) holds the build order up to here. **This file holds the plan for the ten document types owned by Sales, Purchase and Inventory** — the trading half, none of which is built.

The six owned by **Accounting and Banking** — journal, spend, receive, transfer, opening balance, depreciation — are in [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md). **Stage numbers were not renumbered when the two were split**: T0, T2, T3, T4, T5, T7 and T9 are here, T1, T6, T8 and T10 are there, and each gap below says so. The sixteen together are every row of `mst.TransactionTypes`.

Same rules as `master.md`: take the first unticked box, check it against its **Done when** line, tick it in the same commit as the work, and strike a task rather than deleting it if it turns out to be wrong.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** A branch invented mid-task splits the work across two places and leaves whichever one nobody merges behind. See *Git — how work reaches main* in `CLAUDE.md`.

---

## Scope — the sixteen types, and which file each is in

Every row of `mst.TransactionTypes`, which is seeded and applied. Three post nothing; thirteen reach the ledger.

| Code | Document | Owner | Posts | Moves stock | Stage | File |
|---|---|---|---|---|---|---|
| JRN | Manual journal | Accounting | yes | no | T1 | *money* |
| QTE | Quote | Sales | no | no | T2 | here |
| SOR | Sales order | Sales | no | reserves | T2 | here |
| INV | Invoice | Sales | yes | issues | T3 | here |
| POR | Purchase order | Purchase | no | no | T4 | here |
| GRN | Goods receipt | Purchase | yes | receives | T4 | here |
| BIL | Bill | Purchase | yes | no¹ | T4 | here |
| CRN | Credit note | Sales | yes | returns | T5 | here |
| DBN | Debit note | Purchase | yes | returns | T5 | here |
| SPM | Spend money | Banking | yes | no | T6 | *money* |
| RCM | Receive money | Banking | yes | no | T6 | *money* |
| TRM | Transfer money | Banking | yes | no | T6 | *money* |
| POS | POS sale | Sales | yes | issues | T7 | here |
| OPB | Opening balance | Accounting | yes | receives | T8 | *money* |
| STA | Stock adjustment | Inventory | yes | adjusts | T9 | here |
| DEP | Depreciation | Accounting | yes | no | T10 | *money* |

¹ A bill against a goods receipt moves no stock — the receipt already did. A bill with no receipt behind it does, and is the common case for services and for a trader who never raises a GRN.

*money* = [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md). The cut is not by service alone: those six are the documents that trade nothing — no item, no price, no GST, no cost layer — which is why they need almost none of the foundations below.

---

## What these land on

Worth stating, because the foundations below are the gaps in it rather than a rewrite of it.

- **`acc.JournalLedger` and `LedgerPostingService`** — the single posting target and the one door into it. Accounts are named, never numbered; a posting is replaced by key, never appended to; balance is checked in the service, by an insert-time constraint and by a deferred trigger at `COMMIT`.
- **Stock** — a guarded conditional decrement, reserve and release, cost layers under five costing methods, returns to the originating layer, and backdated recosting. `inv.StockMovements` is idempotent on `(OrgId, SourceType, SourceId, SourceLineId)`, which is the key every document below writes through.
- **`NumberGenerator`** — takes a number inside the caller's transaction, so a failed insert gives the number back.
- **Masters** — 16 transaction types, 6 ledger types, 15 ledger sources, effective-dated GST rates, payment terms, a chart of accounts with ten control accounts, and AR/AP sub-accounts per contact and Inventory/COGS/Revenue sub-accounts per item, all seeded at branch creation.
- **`sales.*` and `purchase.*` permissions** are already seeded and granted to the system roles. Nothing needs adding to the matrix.

---

## Stage T0 — foundations, before the first document

None of these is a document. All five are things a document immediately needs and none of which exists, and each one found later is a schema change in the same commit as a screen.

**T0.5 (`acc.Journals`) and T0.7 (does a document write a `Journals` row?) moved** to [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md#foundations-owned-here) — both exist only for the manual journal. The five below stayed because Sales and Purchase need them too, and a shared prerequisite belongs with the shared prerequisites.

- [ ] **T0.1 — The ledger door takes one leg type per call. A document has four.**
  `PostLedgerRequest` carries a single `LedgerTypeId` and a single `TransactionDetailId` for the whole request, and refuses the request unless its own legs balance. An invoice's `ITEM` legs are per-line credits to Sales Revenue, its `TAX` legs are per-rate credits to Output GST, and the one debit to Accounts Receivable is a header-level `CONTROL` leg at detail `0`. **No subset of those balances on its own**, so an invoice cannot be posted through the door as it stands. Stock never hit this because a stock posting is exactly two legs, one type, one amount — balanced by construction, as its own comment says.
  Move `LedgerTypeId` and `TransactionDetailId` onto the leg; check balance across the request rather than per key; scope the replace to `(TransactionTypeCode, TransactionId)` intersected with the leg types present in the request. That last part is what preserves the property 5.12 deliberately built — Sales' revenue legs and Inventory's COGS legs sit on one invoice and replace independently. Withdrawal (an empty leg list, used by void) then has to name the leg types explicitly, since there are no legs to infer them from.
  *Done when*: a three-line invoice with two GST rates, one receivable leg and a round-off posts in one call; re-posting it replaces its own rows and leaves the COGS rows Inventory wrote untouched; and voiding it withdraws its four leg types and no others.

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

- [ ] **T0.6 — Nothing displays a ledger, and everything below writes to one**
  `acc.JournalLedger` accepts postings and stock already writes to it, and there is no screen. Build the account ledger and the trial balance **before the first document**, not after: from here every stage is verified by whether a posting is right, and a posting that can only be read with SQL will be checked by nobody. It also closes the presentation half of master.md 4.4, which has been waiting for somewhere to be shown.
  Account ledger — account, date range, running balance, drill to the document. Trial balance — every account, debit and credit totals, and the two agreeing, which is the one number that says the whole system is sound.
  **Settle `acc.vw_LedgerDetail` here.** SPEC flags it: `CREATE VIEW` is not in `CLAUDE.md`'s raw-SQL exception list, and a view that omits `security_invoker = true` bypasses RLS and leaks the general ledger across branches. *Recommendation: don't add the view.* Do the join as a LINQ projection in Accounting and compute the running balance in C# over the ordered, account-scoped, date-ranged result — a ledger screen is always all three of those, so the window function buys less than the exception costs.
  *Done when*: a trial balance built from the stock postings already in the ledger balances, and every posting in it drills back to the movement that wrote it.

---

## Stage T1 — the manual journal (JRN) → [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md#stage-t1--the-manual-journal-jrn)

**Still the first stage of the plan, in either file.** No stock, no tax, no contact — the cheapest document to prove the extended ledger door with, and what makes every posting below checkable on a screen rather than in SQL.

---

## Stage T2 — the sales skeleton: quote and order (QTE, SOR)

Neither posts. That is the point of taking them first — the document machinery (header/lines, numbering, lifecycle, tax, totals, print, conversion) gets built and tested with no accounting risk at all.

- [ ] **T2.1 — Decide the `sal` document shape** *(owner decision, and the largest one here)*
  Five sales documents share perhaps ninety per cent of their columns: contact, dates, currency and rate, addresses, lines with item/quantity/rate/discount/tax, totals, terms, status. *Recommendation: one `sal.SalesDocuments` + `sal.SalesDocumentDetails` pair discriminated by `TransactionTypeCode`*, with the type-specific columns nullable — validity date on a quote, delivery date on an order, due date on an invoice. One list screen, one numbering path, and conversion becomes a copy with a `SourceDocumentId`.
  The cost is a wide table with nullable columns whose applicability lives in C#. The alternative is ten tables and five near-identical services, and every cross-document report joining all of them.
- [ ] **T2.2 — `sal.*` schema, entities and migration** — per T2.1, with `OrgId`, query filters, RLS and the document series from T0.3.
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

---

## Stage T4 — purchase: order, receipt, bill (POR, GRN, BIL)

Mirrors T2–T3 and reuses the tax component, the numbering and the lifecycle unchanged. The one genuinely new question is what a receipt posts before its bill arrives.

- [ ] **T4.1 — Decide goods-received-not-invoiced** *(owner decision)*
  A receipt puts stock on the shelf; the bill that values it may come days later. Posting nothing at receipt leaves the inventory asset understated for those days — the stock exists and the books do not know. *Recommendation: seed a **Goods Received Not Invoiced** control account (Liability), post `Dr Inventory / Cr GRNI` at receipt, and `Dr GRNI / Cr Accounts Payable` (plus `Dr Input GST`) at the bill.* A bill with no receipt behind it debits Inventory directly.
  This changes `StockLedgerMapping`, which today returns no posting for a sourced receipt on the grounds that Purchase will post it — that stays true, but Purchase now posts at the receipt rather than only at the bill. It also adds an account to the chart-of-accounts seed, which is idempotent per account since 1.4, so existing branches pick it up by re-running the seed.
- [ ] **T4.2 — `pur.*` schema, entities and migration** — same shape decision as T2.1, applied to POR/GRN/BIL/DBN.
- [ ] **T4.3 — Purchase order: API and page** — no posting, no reservation. Ordering stock does not reserve anything; it is not there yet.
- [ ] **T4.4 — Goods receipt: API and page** — receives stock at the order's cost, opens the cost layer, posts per T4.1. Batch, expiry and serial capture belong here, in the request, because they are user input.
  *Done when*: a receipt against an order opens a cost layer at the received cost, and a partial receipt leaves the order partly open.
- [ ] **T4.5 — Bill: API and page** — with or without a receipt, with the Input GST legs and payment terms driving the due date.
  **A bill line is stock, expense or capital**, and the third is how every purchased fixed asset gets onto the books — it posts to a Fixed Asset account and creates the register row, rather than to Inventory. The line flag belongs here; what it then does is [T10.2](./TRANSACTIONS-ACCOUNTING-BANKING.md#stage-t10--fixed-assets-acquisition-depreciation-disposal-dep) in the other file, and T10 cannot start until this task exists.
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

## Stage T6 — money: spend, receive, transfer (SPM, RCM, TRM) → [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md#stage-t6--money-spend-receive-transfer-spm-rcm-trm)

Where the receivable and payable balances T3 and T4 raise are actually cleared. It needs T3.3 and T4.5 first — there has to be something outstanding before there is anything to pay — and it consumes the `acc.TransactionRatio` that T5.1 builds.

---

## Stage T7 — POS sale (POS)

An invoice and its receipt in one action, from `apps/desktop`, which today has no source files at all.

- [ ] **T7.1 — POS API** — one call that issues stock, posts the sale and posts the payment. The stock decrement is **synchronous and guarded**, per `CLAUDE.md`, or two tills oversell the last unit; costing and the ledger follow asynchronously as they already do.
- [ ] **T7.2 — POS screen** — keyboard and barcode driven, offline-tolerant, whole thing in `apps/desktop`.
- [ ] **T7.3 — ESC/POS receipt** — commands, not PDF; fixed-width; desktop only, because a browser cannot reach a USB or serial printer.
  *Done when*: a sale rings up, prints and decrements stock with the network to Accounting down, and reconciles when it returns.

---

## Stage T8 — opening balances (OPB) → [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md#stage-t8--opening-balances-opb)

Accounting orchestrates it, which is why it sits in the other file, but it calls **into** this one: Inventory takes the opening quantity and unit cost that seed the weighted average, and Contacts takes opening AR and AP per contact.

---

## Stage T9 — stock adjustment as a document (STA)

Movements already post as `STA` when they have no document behind them, each filed under its own movement id. What is missing is the document: a sheet of lines with a reason and an approval, rather than one movement at a time.

- [ ] **T9.1 — `inv.StockAdjustments` header and lines**, with a reason and an approver, posting through the existing mapping under one document id instead of per movement.
- [ ] **T9.2 — Physical count** — enter counted quantities, adjust to the difference, and post the sheet as one document.
  *Done when*: a count sheet of twenty items posts as one document with twenty movements, and the ledger shows one adjustment rather than twenty.

---

## Stage T10 — fixed assets: acquisition, depreciation, disposal (DEP) → [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md#stage-t10--fixed-assets-acquisition-depreciation-disposal-dep)

Nothing in this file waits on it, but **it waits on T4.5**: an asset is capitalised from the bill that bought it, off a capital line. A disposal with proceeds may also post under `INV` — that is the open decision in T10.2.

---

## Standing requirements

These apply to every stage in **both** files and are not repeated in the tasks. This is the only copy — [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md) points here rather than restating them, because one requirement copied to two files is one requirement that drifts.

- **Documentation ships in the same commit** — a page under `frontend/apps/docs/content/`, its status in `docs.manifest.ts`, and a bullet under **Unreleased** in `release-notes.md`.
- **Every per-customer table** gets `OrgId`, a global query filter and an RLS policy. The filter is the first line of defence, not the last.
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
| T0.6 | `acc.vw_LedgerDetail` as a database view, or a LINQ projection? | T0 | LINQ projection; don't grow the raw-SQL exception list |
| — | Should a branch declare its trade, so documents and settings narrow themselves? | any | *(open in `CLAUDE.md`, master.md 5.14)* |

Two more are in [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md#open-decisions-owned-here), and **one of them reaches back into this file**: if T0.7 is answered the wider way, every Sales and Purchase document here also writes an `acc.Journals` row. That is why it wants answering before T1 rather than at T3.

## Sequencing, and the one place it is arguable

T1 — in the other file — before everything, because a posting nobody can read is a posting nobody checks. Then T2 before T3 here, because the document machinery is worth getting wrong somewhere that posts nothing.

**Sales before Purchase is the arguable one.** The natural demonstration is buy then sell, and a goods receipt is a simpler document than an invoice — no reservation, no layer consumption, no COGS. Taking Purchase first would also let the invoice be tested against stock that arrived at a real cost rather than an opening balance.

Sales is put first anyway, on two grounds: the invoice is the screen this product is bought for, and the sale path already has the most machinery built behind it — the guarded decrement, layer consumption, returns to the originating layer and the COGS posting all exist and have never been called by a document. The cost of the choice is that stock has to be seeded through opening receipts until T4 lands, which already works.

Swapping T3 and T4 costs nothing structural if the owner prefers the buy-then-sell loop.
