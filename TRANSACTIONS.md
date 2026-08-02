# TRANSACTIONS.md — the plan for every transaction document

`CLAUDE.md` holds the conventions. [`SPEC.md`](./SPEC.md) holds tables and pages. [`master.md`](./master.md) holds the build order up to here. **This file holds the plan for the sixteen document types** — the half of the product that moves money and stock, none of which is built.

Same rules as `master.md`: take the first unticked box, check it against its **Done when** line, tick it in the same commit as the work, and strike a task rather than deleting it if it turns out to be wrong.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** A branch invented mid-task splits the work across two places and leaves whichever one nobody merges behind. See *Git — how work reaches main* in `CLAUDE.md`.

---

## Scope — the sixteen types

Every row of `mst.TransactionTypes`, which is seeded and applied. Three post nothing; thirteen reach the ledger.

| Code | Document | Owner | Posts | Moves stock | Stage |
|---|---|---|---|---|---|
| JRN | Manual journal | Accounting | yes | no | T1 |
| QTE | Quote | Sales | no | no | T2 |
| SOR | Sales order | Sales | no | reserves | T2 |
| INV | Invoice | Sales | yes | issues | T3 |
| POR | Purchase order | Purchase | no | no | T4 |
| GRN | Goods receipt | Purchase | yes | receives | T4 |
| BIL | Bill | Purchase | yes | no¹ | T4 |
| CRN | Credit note | Sales | yes | returns | T5 |
| DBN | Debit note | Purchase | yes | returns | T5 |
| SPM | Spend money | Banking | yes | no | T6 |
| RCM | Receive money | Banking | yes | no | T6 |
| TRM | Transfer money | Banking | yes | no | T6 |
| POS | POS sale | Sales | yes | issues | T7 |
| OPB | Opening balance | Accounting | yes | receives | T8 |
| STA | Stock adjustment | Inventory | yes | adjusts | T9 |
| DEP | Depreciation | Accounting | yes | no | T10 |

¹ A bill against a goods receipt moves no stock — the receipt already did. A bill with no receipt behind it does, and is the common case for services and for a trader who never raises a GRN.

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

None of these is a document. All six are things a document immediately needs and none of which exists, and each one found later is a schema change in the same commit as a screen.

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

- [ ] **T0.5 — `acc.Journals` and `acc.JournalDetails`**
  Designed in SPEC, built nowhere. The manual journal document, and the only place a reversal is paired **line by line** — without which a partially reversed journal cannot be told from a fully reversed one, because both balance.
  Entities, `DbSet`s, Fluent config, the two check constraints, the deferred balance trigger on `Posted` only, and RLS. See T0.7 for whether other documents write here.
  *Done when*: a two-line journal saves as a draft while unbalanced, refuses to post unbalanced, and a reversal links both headers and every line.

- [ ] **T0.6 — Nothing displays a ledger, and everything below writes to one**
  `acc.JournalLedger` accepts postings and stock already writes to it, and there is no screen. Build the account ledger and the trial balance **before the first document**, not after: from here every stage is verified by whether a posting is right, and a posting that can only be read with SQL will be checked by nobody. It also closes the presentation half of master.md 4.4, which has been waiting for somewhere to be shown.
  Account ledger — account, date range, running balance, drill to the document. Trial balance — every account, debit and credit totals, and the two agreeing, which is the one number that says the whole system is sound.
  **Settle `acc.vw_LedgerDetail` here.** SPEC flags it: `CREATE VIEW` is not in `CLAUDE.md`'s raw-SQL exception list, and a view that omits `security_invoker = true` bypasses RLS and leaks the general ledger across branches. *Recommendation: don't add the view.* Do the join as a LINQ projection in Accounting and compute the running balance in C# over the ordered, account-scoped, date-ranged result — a ledger screen is always all three of those, so the window function buys less than the exception costs.
  *Done when*: a trial balance built from the stock postings already in the ledger balances, and every posting in it drills back to the movement that wrote it.

- [ ] **T0.7 — Decide whether a document also writes a `Journals` row** *(owner decision)*
  SPEC's `Journals.TransactionTypeCode` reads "`JRN` when hand-written, else the source document's type", which implies every invoice gets a journal header shadowing it. 5.12 established the opposite mechanism: services post to `JournalLedger` directly, keyed by their own document, with no header in Accounting.
  *Recommendation: manual journals only.* A second header per invoice is a second thing to keep in step with the first, and the ledger rows already carry the document type and id. The cost of being wrong is one nullable column later; the cost of building it is a shadow table under every document in the product.

---

## Stage T1 — the manual journal (JRN)

The simplest document that posts: no stock, no tax, no contact required. Doing it first proves the extended ledger door on something with nothing else going on.

- [ ] **T1.1 — Journal entry API** — create, edit while draft, post, reverse. Debit xor credit per line, balance guarded on post, `LedgerSourceId = 12`, `JournalId` set on the ledger rows.
  *Done when*: posting a balanced journal writes ledger rows that appear on T0.6's screens, an unbalanced post is refused, and a reversal offsets it exactly.
- [ ] **T1.2 — Journal entry page** — grid of lines with account and sub-account pickers, running debit/credit totals with the difference shown, draft save, post, reverse.
  *Done when*: a journal can be keyed, saved, posted and reversed without leaving the page, and the totals show the imbalance while it exists.

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
  *Done when*: a bill against a receipt clears GRNI to the paise and moves no stock; a bill with no receipt does move stock; and payables aging ties to the Accounts Payable control account.

---

## Stage T5 — credit and debit notes (CRN, DBN)

A sales return and a purchase return. Both reverse value and both put stock back **on the layers it came from** — 4.5 built that and nothing calls it yet.

- [ ] **T5.1 — `acc.TransactionRatio`** — allocation between documents, designed in SPEC and unbuilt. Built here rather than in T0 because an allocation table with nothing allocating cannot be tested. Allocations must never exceed the target's outstanding balance, and the sum spans rows, so that is a C# guard and cannot be a check constraint.
- [ ] **T5.2 — Credit note** — `Dr Sales Returns` (contra Income, so the report subtracts it) `/ Cr Accounts Receivable`, with the GST reversed on the same rates as the invoice, and stock returned via `ReturnsStockMovementId` to the originating layers.
  *Done when*: buy, sell, credit-note leaves stock value exactly where it started and the ledger with it; and the note allocates against the invoice rather than floating as an unapplied balance.
- [ ] **T5.3 — Debit note** — the purchase mirror: `Dr Accounts Payable / Cr Purchase Returns` (contra Expense), Input GST reversed, stock returned to its layers.
- [ ] **T5.4 — Allocation UI** — apply a note across one or several documents, with the outstanding balance shown and over-allocation refused at the point of typing rather than at save.

---

## Stage T6 — money: spend, receive, transfer (SPM, RCM, TRM)

Where the receivable and payable balances built above are actually cleared. `mst.LedgerSources` already distinguishes payment from refund and prepayment from allocation, which is what makes these three types carry nine different meanings.

- [ ] **T6.1 — `bnk.*` transaction schema** — payments, receipts and transfers, with lines that allocate to documents.
- [ ] **T6.2 — Spend money (SPM)** — bill payment, vendor prepayment, invoice refund, credit-note refund, chosen by `LedgerSourceId` rather than by transaction type. `Dr Accounts Payable / Cr Bank`, with `MappingTransactionId` and `MappingTransactionTypeCode` pointing at the bill — that pair is the entire mechanism for tracing a payment to what it settles.
  *Done when*: paying a bill clears it from payables aging, and the ledger row names the bill it paid.
- [ ] **T6.3 — Receive money (RCM)** — the mirror, against invoices, with customer prepayments landing in Advance from Customer rather than against a document.
- [ ] **T6.4 — Transfer money (TRM)** — bank to bank or bank to cash. No contact, no sub-account, no control leg; the one source with no counterparty.
- [ ] **T6.5 — Realized FX on settlement** — an extra pair to Realized FX Gain/Loss at `LedgerTypeId = 5`, computed from the difference between the document's `ExchangeRate` and the payment's. **Never from a live rate.**
  *Done when*: an invoice raised at one rate and settled at another leaves no residual balance on the contact, and the difference sits in Realized FX Gain/Loss.
- [ ] **T6.6 — Partial and over-payment** — a payment across several documents, and a receipt exceeding what is owed becoming a prepayment rather than a negative balance.

---

## Stage T7 — POS sale (POS)

An invoice and its receipt in one action, from `apps/desktop`, which today has no source files at all.

- [ ] **T7.1 — POS API** — one call that issues stock, posts the sale and posts the payment. The stock decrement is **synchronous and guarded**, per `CLAUDE.md`, or two tills oversell the last unit; costing and the ledger follow asynchronously as they already do.
- [ ] **T7.2 — POS screen** — keyboard and barcode driven, offline-tolerant, whole thing in `apps/desktop`.
- [ ] **T7.3 — ESC/POS receipt** — commands, not PDF; fixed-width; desktop only, because a browser cannot reach a USB or serial printer.
  *Done when*: a sale rings up, prints and decrements stock with the network to Accounting down, and reconciles when it returns.

---

## Stage T8 — opening balances (OPB)

`CLAUDE.md` calls this the highest-risk screen in the system, and it is deliberately late: it is the one document that touches every subledger at once, so it wants all of them finished.

- [ ] **T8.1 — Orchestration** — Accounting drives; Inventory takes opening quantity and unit cost and seeds the weighted average; Contacts takes opening AR and AP **per contact, never a lump sum**, or aging is broken from day one.
- [ ] **T8.2 — The validation** — Opening Balance Equity nets to zero, and finalize is blocked until AR, AP and Inventory subledgers tie to their control accounts.
- [ ] **T8.3 — Read-only after go-live**, and migrated fixed assets skip historical depreciation.
  *Done when*: a trial balance drawn immediately after finalize balances, every subledger ties, and the screen refuses to reopen.

---

## Stage T9 — stock adjustment as a document (STA)

Movements already post as `STA` when they have no document behind them, each filed under its own movement id. What is missing is the document: a sheet of lines with a reason and an approval, rather than one movement at a time.

- [ ] **T9.1 — `inv.StockAdjustments` header and lines**, with a reason and an approver, posting through the existing mapping under one document id instead of per movement.
- [ ] **T9.2 — Physical count** — enter counted quantities, adjust to the difference, and post the sheet as one document.
  *Done when*: a count sheet of twenty items posts as one document with twenty movements, and the ledger shows one adjustment rather than twenty.

---

## Stage T10 — fixed assets and depreciation (DEP)

Last because nothing else waits on it, and because it still carries an open question in `CLAUDE.md`.

- [ ] **T10.1 — `acc.FixedAssetCategories`, `acc.FixedAssets`, `acc.DepreciationSchedules`** — not designed in SPEC. **The category owns the GL mapping** (Fixed Asset / Accumulated Depreciation / Depreciation Expense); per-asset mapping does not scale.
- [ ] **T10.2 — Depreciation run** — a period posting `Dr Depreciation Expense / Cr Accumulated Depreciation`, the latter contra so the balance sheet subtracts it.
- [ ] **T10.3 — Disposal** — proceeds against written-down value, gain or loss to the P&L.
  *Blocked on an owner decision already recorded in `CLAUDE.md`*: straight-line only, or books **and** tax depreciation? Two schedules per asset is a different table shape, so it is cheaper to answer than to retrofit.

---

## Standing requirements

These apply to every stage above and are not repeated in the tasks.

- **Documentation ships in the same commit** — a page under `frontend/apps/docs/content/`, its status in `docs.manifest.ts`, and a bullet under **Unreleased** in `release-notes.md`.
- **Every per-customer table** gets `OrgId`, a global query filter and an RLS policy. The filter is the first line of defence, not the last.
- **Every endpoint** carries `[Authorize]` and `RequireModulePermission`, and validates the caller's `OrgId` against the target's, returning `Forbid()` rather than `NotFound()`.
- **Every posting is idempotent** on its document key, so a retried request replaces rather than doubles.
- **`ExchangeRate` is a snapshot at document date**, never looked up live, on every document that carries one.
- **Both checks pass before a box is ticked** — `dotnet build && dotnet test` in `backend/`, `npm run check` in `frontend/`.
- **Every page works at ~360px**: grids become card lists, forms stack, modals become full-screen sheets.

## Open decisions, gathered

Answering these before the stage that needs them is much cheaper than after. Four are recommendations above waiting for a yes; two are already in `CLAUDE.md`.

| # | Question | Needed by | Recommendation |
|---|---|---|---|
| T0.7 | Does every document write an `acc.Journals` row, or only manual journals? | T1 | Manual journals only |
| T2.1 | One discriminated `sal` document table, or a table pair per type? | T2 | One pair, discriminated |
| T4.1 | Does a goods receipt post to a GRNI clearing account? | T4 | Yes, and seed the account |
| T0.6 | `acc.vw_LedgerDetail` as a database view, or a LINQ projection? | T0 | LINQ projection; don't grow the raw-SQL exception list |
| — | Fixed assets: straight-line only, or books **and** tax depreciation? | T10 | *(open in `CLAUDE.md`)* |
| — | Should a branch declare its trade, so documents and settings narrow themselves? | any | *(open in `CLAUDE.md`, master.md 5.14)* |

## Sequencing, and the one place it is arguable

T1 before everything because a posting nobody can read is a posting nobody checks. T2 before T3 because the document machinery is worth getting wrong somewhere that posts nothing.

**Sales before Purchase is the arguable one.** The natural demonstration is buy then sell, and a goods receipt is a simpler document than an invoice — no reservation, no layer consumption, no COGS. Taking Purchase first would also let the invoice be tested against stock that arrived at a real cost rather than an opening balance.

Sales is put first anyway, on two grounds: the invoice is the screen this product is bought for, and the sale path already has the most machinery built behind it — the guarded decrement, layer consumption, returns to the originating layer and the COGS posting all exist and have never been called by a document. The cost of the choice is that stock has to be seeded through opening receipts until T4 lands, which already works.

Swapping T3 and T4 costs nothing structural if the owner prefers the buy-then-sell loop.
