# TRANSACTIONS.md — the trading documents

`CLAUDE.md` holds the conventions. [`SPEC.md`](./SPEC.md) holds tables and pages. [`master.md`](./master.md) holds the build order up to here. **This file holds the plan for the eleven document types owned by Sales, Purchase and Inventory** — the trading half, none of which is built.

The documents owned by **Accounting and Banking** are in [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md) and are not repeated here. **Stage numbers were not renumbered when the two files were split**, so the numbering below skips T1, T6, T8 and T10 — those stages live in that file, and the gaps are deliberate rather than a mistake.

Same rules as `master.md`: take the first unticked box, check it against its **Done when** line, tick it in the same commit as the work, and strike a task rather than deleting it if it turns out to be wrong.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** A branch invented mid-task splits the work across two places and leaves whichever one nobody merges behind. See *Git — how work reaches main* in `CLAUDE.md`.

**Flow documents, beside this plan and not part of it**: [`Sales.md`](./Sales.md), [`Purchase.md`](./Purchase.md) and [`Inventory.md`](./Inventory.md) describe how these documents behave once built. No checkboxes — this file is the work, those are the behaviour.

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
  **Written, unverified.** `Shared.Kernel.Tax` holds the pure calculator (`GstCalculator`), the place-of-supply resolver (`PlaceOfSupply`) and the cached rate client (`ITaxRateProvider`); Accounting serves `GET internal/tax/rates?on={date}`. Sales registers the provider; Purchase registers the same one when it lands.
  **The three sub-decisions, settled:** prices may be **exclusive or inclusive** per line, because MRP pricing is the Indian retail default rather than an edge case; a discount **reduces the taxable value when the branch's `DiscountBeforeTax` says so**, which is a trade discount against a settlement discount; and tax **rounds per component, then sums**, because GSTR-1 reconciles per line and per rate, so the component is the rounded unit and the tax printed against a line is the tax that was filed.
  **The date is the only way in.** There is no "current rates" route: a caller that could omit the date would eventually omit it, and a backdated document taxed at today's figures is a return that has to be amended.
  **A GSTIN contradicting the place of supply is refused, not resolved** — the third of the *Done when* clauses. One of the two is wrong and nothing here can tell which; choosing silently produces a document that balances, prints and posts under the wrong head of tax.
  **`shared-fixtures/tax-fixture.json` is the answer to "how do we know the two implementations agree".** Twelve line cases, two document cases and nine place-of-supply cases, read by `Shared.Kernel.Tests` and by `tax-fixture.spec.ts` beside `line-math.ts`. C# works in `decimal` and TypeScript in integer paise; the rupees-to-paise conversion is the only thing the two sides are allowed to do differently.
  Neither suite has been run — no .NET SDK and no usable `node_modules` in the session that wrote them. Both algorithms were instead re-implemented independently and checked against the fixture, which is a third opinion rather than a substitute for the two.

- [ ] **T0.3 — No document numbering series exist**
  `NumberingSeriesSeed` seeds five master series and says outright that document series arrive with the services that own them. Each service seeds its own on branch creation: `SeriesFor.Document`, `AllowManualOverride = false` — a hand-keyed invoice number is not allowed on an Indian invoice — and reset by financial year, whose start month 5.3 already resolves per branch.
  **The number is taken at creation, so every document has one from the moment it exists** — a draft can be quoted over the phone. `NumberGenerator` already allocates inside the caller's transaction, so a create that fails still gives the number back.
  **The consequence, and it is not optional: a document row is never deleted.** A number issued at creation has been spent, so abandoning a draft **voids** it and keeps the row. That turns an unexplained hole in the series into an answerable "INV-0042 was cancelled on the 3rd, by this user, for this reason" — and consecutive numbering on an Indian invoice is statutory, not tidiness.
  **This reverses the rule as originally written**, which took the number at post and left drafts unnumbered. Both keep the series gapless; the difference is whether the gap is prevented or explained, and the owner chose explained. `acc.Journals` is built under the old rule and is now the odd one out — aligning it is a migration on a live table plus its two check constraints, and wants doing deliberately rather than as a side effect.
  *Done when*: two invoices created concurrently take consecutive numbers, a create that fails takes none, and an abandoned draft is voided with its number still accounted for rather than deleted.

- [ ] **T0.4 — One lifecycle, the same for every type**
  **Draft → ReadyToPost → Posted → Void**, plus Reversed for journals. `Void` covers both an abandoned draft and a posted document taken back out — `PostedAt` being null is what says it never reached the books, so the two cases stay distinguishable without a fifth status.
  `ReadyToPost` is the state a finished document sits in while it waits for whoever posts it — and it is what `sales.approve` and `purchase.approve` are for. Both permissions have been seeded and granted since the beginning and nothing has ever read one. A branch that does not want the step posts straight from `Draft`; nothing forces a document through it.
  **A posted document is never edited** — an invoice is corrected by a credit note, a journal by a reversing journal. Void withdraws the posting, releases any reservation and reverses any stock movement, and is refused once anything downstream points at the document: a paid invoice, an allocated credit note, a received order.
  `RequireModulePermission` maps GET to `.view`, DELETE to `.delete` and **everything else to `.edit`**, which was the right three lines for masters and is not enough here. `sales.void` and `sales.approve` are seeded and granted and would be reachable by anyone holding `sales.edit`. Add an action override to the attribute for the routes that void, approve or print.
  *Done when*: a posted invoice refuses an edit; a void withdraws exactly its own ledger rows and releases exactly its own reservation; a `ReadyToPost` document is still editable and posts without one; a user holding `sales.edit` but not `sales.approve` cannot move a document to `ReadyToPost`; and a user without `sales.void` is refused the void.
  **The rule and the permission are written; the withdrawal is not.** `Shared.Kernel.Documents.DocumentLifecycle` is the whole table in one place — pure, so whether anything downstream points at the document is passed in rather than queried, and testable without a server. Four of the five *Done when* clauses are covered by `DocumentLifecycleTests`; the fifth — that a void withdraws exactly its own ledger rows and releases exactly its own reservation — needs a document that posts, so it belongs to T3.1 and is named there rather than ticked here.
  **`PermissionAction` is an override, not a second check**, and that distinction is the task. `[PermissionAction("void")]` makes a route require `{module}.void` **instead of** `{module}.edit`. AND-ing the two would mean only people who can edit may void, which is the opposite of the separation the permission exists for — a clerk who raises invoices is exactly who should not be able to withdraw one. `RequirePermissionAttribute` already existed and does AND, which is right for the platform settings it guards and wrong here.
  **Three answers worth having written down**, because each is a case nobody asks about until it happens. `ReadyToPost` **is editable** — the reviewer is usually the one who spots the error, and making them send it backwards to fix a typo is how a review step comes to be skipped. A void **always needs a reason**, which is the difference between a gap in the sequence that is explained and a gap that is a hole. And a document is **never deleted**: `CanDelete` exists only to say no in one place, so a service reaching for a delete finds the reason instead of nothing.

- [x] **T0.6 — Nothing displays a ledger, and everything below writes to one**
  `acc.JournalLedger` accepts postings and stock already writes to it, and there is no screen. Build the account ledger and the trial balance **before the first document**, not after: from here every stage is verified by whether a posting is right, and a posting that can only be read with SQL will be checked by nobody. It also closes the presentation half of master.md 4.4, which has been waiting for somewhere to be shown.
  Account ledger — account, date range, running balance, drill to the document. Trial balance — every account, debit and credit totals, and the two agreeing, which is the one number that says the whole system is sound.
  **Settle `acc.vw_LedgerDetail` here.** SPEC flags it: `CREATE VIEW` is not in `CLAUDE.md`'s raw-SQL exception list, and a view that omits `security_invoker = true` bypasses RLS and leaks the general ledger across branches. *Recommendation: don't add the view.* Do the join as a LINQ projection in Accounting and compute the running balance in C# over the ordered, account-scoped, date-ranged result — a ledger screen is always all three of those, so the window function buys less than the exception costs.
  *Done when*: a trial balance built from the stock postings already in the ledger balances, and every posting in it drills back to the movement that wrote it.
  **Done.** `LedgerReportService` + `api/ledger`, and two pages under Accounting. **The view was not added**, per the recommendation — both reads are LINQ projections, and the running balance is accumulated in C# over the ordered, account-scoped, date-ranged result.
  Two things worth recording. Balances are held **in debit terms** — positive is a debit balance — and turned the right way up by the screen, which already loads `mst.AccountTypes` for its normal balance; keeping a second copy of which types are which inside Accounting is exactly how two reports come to disagree. And the trial balance shows each account in **one** column, the side its net falls on, rather than both its totals: two columns of gross totals always agree, because every posting was balanced when it was written, so they would prove nothing.
  Six tests against a real PostgreSQL, over postings written through the door rather than rows the test inserted — a test that hand-wrote the ledger would be checking its own arithmetic.

---

## Stages T2–T7 — sales and purchase → [`SALES.md`](./SALES.md) · [`PURCHASE.md`](./PURCHASE.md)

**Moved.** The quote, sales order, delivery challan, invoice, POS sale and credit note are in [`SALES.md`](./SALES.md); the purchase order, goods receipt, bill and debit note in [`PURCHASE.md`](./PURCHASE.md). Each file carries its own tables, columns, decisions, open questions and tasks, so one module can be built without reading around it.

Task numbers did not change — T2, T3, T4, T5 and T7 are the same tasks, in a different file. **T5.1 (`acc.TransactionRatio`) and T5.4 (the allocation UI) are shared**, and are built once by whichever module reaches them first.

What stays here: **T0**, the foundations both modules wait on, and **T9**, the stock adjustment document, which is Inventory's.

---

## Stage T9 — stock adjustment as a document (STA)

Movements already post as `STA` when they have no document behind them, each filed under its own movement id. What is missing is the document: a sheet of lines with a reason and an approval, rather than one movement at a time.

- [x] **T9.1 — `inv.StockAdjustments` header and lines**, with a reason and an approver, posting through the existing mapping under one document id instead of per movement.
  Built. **The approver is `PostedBy`** — no separate column, because `CreatedBy` (who keyed it) and `PostedBy` (who authorised it) already are the segregation of duties, and a third column repeating one of them is the `BranchId`-beside-`OrgId` mistake. The reason is an enum rather than a per-branch master: the list is the same in every branch, and it is what a shrinkage report groups by, so letting two branches spell "Damage" differently would make that report meaningless across a customer.
  **It posts nothing itself.** Each line writes an ordinary movement carrying the sheet's id and line number, and `StockLedgerMapping` already files a sourced movement under its document — so the accounting fell out of machinery that was already tested, and there is still exactly one place that decides what a movement means in the ledger.
  **No void, unlike a money document.** Voiding a payment withdraws ledger rows and nothing else happened; this would have to un-move stock that moved. Reversing writes a mirror sheet instead, linked from both ends.
- [x] **T9.2 — Physical count** — enter counted quantities, adjust to the difference, and post the sheet as one document.
  *Done when*: a count sheet of twenty items posts as one document with twenty movements, and the ledger shows one adjustment rather than twenty.
  Done, and tested against a real PostgreSQL rather than asserted: a three-item count posts as one document whose movements all carry the sheet's id with their own line numbers — which is the ledger key, and therefore the actual claim. Lines that agree with the books are dropped rather than posted as zero, and the quantity the system held at the moment of counting is snapshotted so the difference can be re-checked later.
  **A second defect, found by testing the read path rather than the write path**: `NetValue` was computed by an instance method called inside an EF projection, which EF refuses at runtime — so both the list and the detail would have thrown the first time the screen opened, and neither the build nor any write-path test could see it. It is one batched query now, which also removes an N+1 that would have run per row.
  **Building it found a real defect in `StockService`**: it opened its own transaction per movement, so a sheet posting several lines inside one transaction threw on the second. It now joins an ambient transaction when there is one and commits only what it opened — without which the all-or-nothing guarantee was not merely untested but impossible.

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
