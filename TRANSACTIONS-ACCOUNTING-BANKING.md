# TRANSACTIONS-ACCOUNTING-BANKING.md — the money documents

The six transaction documents owned by **Accounting** and **Banking**, split out of [`TRANSACTIONS.md`](./TRANSACTIONS.md), which keeps the ten owned by Sales, Purchase and Inventory.

`CLAUDE.md` holds the conventions. [`SPEC.md`](./SPEC.md) holds tables and pages. [`master.md`](./master.md) holds the build order up to here.

Same rules as `master.md`: take the first unticked box, check it against its **Done when** line, tick it in the same commit as the work, and strike a task rather than deleting it if it turns out to be wrong.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** A branch invented mid-task splits the work across two places and leaves whichever one nobody merges behind. See *Git — how work reaches main* in `CLAUDE.md`.

**Flow documents, beside this plan and not part of it**: [`FLOW-SALES.md`](./FLOW-SALES.md), [`FLOW-PURCHASE.md`](./FLOW-PURCHASE.md) and [`FLOW-STOCK.md`](./FLOW-STOCK.md) describe the trading side's runtime behaviour. No checkboxes.

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

---

## Foundations owned here

Two of `TRANSACTIONS.md`'s original T0 items exist only for the manual journal, so they moved with it.

- [ ] **T0.5 — `acc.Journals` and `acc.JournalDetails`**
  Designed in SPEC, built nowhere. The manual journal document, and the only place a reversal is paired **line by line** — without which a partially reversed journal cannot be told from a fully reversed one, because both balance.
  Entities, `DbSet`s, Fluent config, the two check constraints, the deferred balance trigger on `Posted` only, and RLS. See T0.7 for whether other documents write here.
  *Done when*: a two-line journal saves as a draft while unbalanced, refuses to post unbalanced, and a reversal links both headers and every line.

- [ ] **T0.7 — Decide whether a document also writes a `Journals` row** *(owner decision)*
  SPEC's `Journals.TransactionTypeCode` reads "`JRN` when hand-written, else the source document's type", which implies every invoice gets a journal header shadowing it. 5.12 established the opposite mechanism: services post to `JournalLedger` directly, keyed by their own document, with no header in Accounting.
  *Recommendation: manual journals only.* A second header per invoice is a second thing to keep in step with the first, and the ledger rows already carry the document type and id. The cost of being wrong is one nullable column later; the cost of building it is a shadow table under every document in the product.
  **This decision reaches across the split** — a yes to the wider reading puts a `Journals` write into every Sales and Purchase document in the other file, which is precisely the reason to answer it before T1 rather than after T3.

---

## Stage T1 — the manual journal (JRN)

The simplest document that posts: no stock, no tax, no contact required. Doing it first proves the extended ledger door on something with nothing else going on. **This is the first stage of the whole plan**, in either file.

- [ ] **T1.1 — Journal entry API** — create, edit while draft, post, reverse. Debit xor credit per line, balance guarded on post, `LedgerSourceId = 12`, `JournalId` set on the ledger rows.
  *Done when*: posting a balanced journal writes ledger rows that appear on T0.6's screens, an unbalanced post is refused, and a reversal offsets it exactly.
- [ ] **T1.2 — Journal entry page** — grid of lines with account and sub-account pickers, running debit/credit totals with the difference shown, draft save, post, reverse.
  *Done when*: a journal can be keyed, saved, posted and reversed without leaving the page, and the totals show the imbalance while it exists.

---

## Stage T6 — money: spend, receive, transfer (SPM, RCM, TRM)

Where the receivable and payable balances raised by Sales and Purchase are actually cleared. `mst.LedgerSources` already distinguishes payment from refund and prepayment from allocation, which is what makes these three types carry nine different meanings.

Needs T3.3 and T4.5 from the other file — there has to be something outstanding before there is anything to pay.

- [ ] **T6.1 — `bnk.*` transaction schema** — payments, receipts and transfers, with lines that allocate to documents.
- [ ] **T6.2 — Spend money (SPM)** — bill payment, vendor prepayment, invoice refund, credit-note refund, chosen by `LedgerSourceId` rather than by transaction type. `Dr Accounts Payable / Cr Bank`, with `MappingTransactionId` and `MappingTransactionTypeCode` pointing at the bill — that pair is the entire mechanism for tracing a payment to what it settles.
  *Done when*: paying a bill clears it from payables aging, and the ledger row names the bill it paid.
- [ ] **T6.3 — Receive money (RCM)** — the mirror, against invoices, with customer prepayments landing in Advance from Customer rather than against a document.
- [ ] **T6.4 — Transfer money (TRM)** — bank to bank or bank to cash. No contact, no sub-account, no control leg; the one source with no counterparty.
- [ ] **T6.5 — Realized FX on settlement** — an extra pair to Realized FX Gain/Loss at `LedgerTypeId = 5`, computed from the difference between the document's `ExchangeRate` and the payment's. **Never from a live rate.**
  *Done when*: an invoice raised at one rate and settled at another leaves no residual balance on the contact, and the difference sits in Realized FX Gain/Loss.
- [ ] **T6.6 — Partial and over-payment** — a payment across several documents, and a receipt exceeding what is owed becoming a prepayment rather than a negative balance.

---

## Stage T8 — opening balances (OPB)

`CLAUDE.md` calls this the highest-risk screen in the system, and it is deliberately late: it is the one document that touches every subledger at once, so it wants all of them finished.

- [ ] **T8.1 — Orchestration** — Accounting drives; Inventory takes opening quantity and unit cost and seeds the weighted average; Contacts takes opening AR and AP **per contact, never a lump sum**, or aging is broken from day one.
- [ ] **T8.2 — The validation** — Opening Balance Equity nets to zero, and finalize is blocked until AR, AP and Inventory subledgers tie to their control accounts.
- [ ] **T8.3 — Read-only after go-live**, and migrated fixed assets skip historical depreciation.
  *Done when*: a trial balance drawn immediately after finalize balances, every subledger ties, and the screen refuses to reopen.

---

## Stage T10 — fixed assets: acquisition, depreciation, disposal (DEP)

**An earlier draft of this stage had a register, a depreciation run and a disposal, and nothing that put an asset on the books.** That is not a small omission: a depreciation run over an empty register posts nothing at all, and a register filled in by hand depreciates an asset the ledger never bought — so `Dr Depreciation Expense / Cr Accumulated Depreciation` accumulates against a Fixed Asset account holding zero, and the balance sheet carries a negative net book value that balances perfectly and is nonsense. Depreciation is only meaningful as the second act. **T10.2 is the acquisition, and it comes first.**

- [ ] **T10.1 — `acc.FixedAssetCategories`, `acc.FixedAssets`, `acc.DepreciationSchedules`** — not designed in SPEC. **The category owns the GL mapping** (Fixed Asset / Accumulated Depreciation / Depreciation Expense); per-asset mapping does not scale.
  The register carries **where the asset came from** — `TransactionTypeCode` and `TransactionId` of the document that capitalised it — so that every row traces to a posting rather than to whoever typed it. That column is what makes T10.5 possible.

- [ ] **T10.2 — Acquisition: capitalise from the document that bought the asset** *(the missing transaction)*
  An asset arrives one of four ways, and three of them are documents that already exist by this stage:
  - **Purchased** — a bill (`BIL`) whose line is marked capital rather than stock or expense. `Dr Fixed Asset / Cr Accounts Payable`, with input GST on capital goods claimed as it is on anything else. This is the common case.
  - **Migrated** — the opening balance (`OPB`), which T8.3 already covers and which skips historical depreciation.
  - **Self-constructed, or transferred out of stock** — a manual journal (`JRN`) moving cost from Inventory or work in progress to Fixed Asset. A jeweller turning a display case out of purchased material is doing exactly this.
  - **Donated or otherwise free** — a journal against Other Income. Rare, and it falls out of the `JRN` path for nothing extra.

  The work is the **capitalisation link**, not a new screen: a capital line on a bill creates the register row and posts to the category's Fixed Asset account instead of Inventory or Expense, in the same transaction as the bill. Created any other way round — a register the user fills in separately — and the asset register and the Fixed Asset control account disagree from the first entry, with nothing to reconcile them against.
  *Done when*: a bill with one capital line and one stock line posts each to its own account, creates exactly one register row naming that bill, and leaves the Fixed Asset control account equal to the register's total cost.

- [ ] **T10.3 — Depreciation run** — a period posting `Dr Depreciation Expense / Cr Accumulated Depreciation` under `DEP`, the latter contra so the balance sheet subtracts it rather than adding it.
  Runs per period over the register, skipping assets already fully depreciated and assets migrated at written-down value. Re-running a period must replace its postings rather than add to them — the posting key from T0.1 already gives that, and without it a second run silently doubles the charge.
  *Blocked on an owner decision already recorded in `CLAUDE.md`*: straight-line only, or books **and** tax depreciation? Two schedules per asset is a different table shape, so it is cheaper to answer than to retrofit.
  *Done when*: an asset acquired mid-period is depreciated from its own acquisition date, a run repeated twice charges once, and accumulated depreciation never exceeds cost.

- [ ] **T10.4 — Disposal** — proceeds against written-down value, gain or loss to the P&L. Four legs, not two: `Dr Accumulated Depreciation` for everything charged to date, `Cr Fixed Asset` at full cost, `Dr Bank or Accounts Receivable` for the proceeds, and the difference to Gain or Loss on Disposal.
  Scrapping is the same posting with no proceeds, so the whole written-down value becomes the loss. A disposal stops depreciation from its date — an asset that is gone must not keep depreciating, which is the failure a register disconnected from the ledger produces silently.
  *Done when*: buying, depreciating and disposing at exactly written-down value leaves the Fixed Asset, Accumulated Depreciation and Gain/Loss accounts all at zero for that asset.

- [ ] **T10.5 — The register ties to the control account**
  Sum of cost in `acc.FixedAssets` equals the Fixed Asset control account; sum of accumulated depreciation equals its own. Shown on the register screen the way T8.2 blocks a finalize that does not tie, because a fixed asset register is a subledger and every other subledger in this product is checked against its control account. This one would otherwise be the exception, and it is the subledger a statutory audit actually opens.
  *Done when*: the two agree after an acquisition, a depreciation run and a disposal, and a deliberate hand-edit to the register is visible as a break rather than absorbed.

**Decision inside this stage** — see the table below: acquisition and disposal have **no transaction type of their own**. `mst.TransactionTypes` seeds sixteen codes and `DEP` is the only one here; SPEC says a new code arrives by EF migration, never at runtime.

---

## Standing requirements

The same list as [`TRANSACTIONS.md`](./TRANSACTIONS.md#standing-requirements), unchanged and deliberately not copied here — documentation in the same commit, `OrgId` and RLS on every per-customer table, `[Authorize]` and `RequireModulePermission` on every endpoint, idempotent postings, `ExchangeRate` snapshot at document date, both check suites green before a box is ticked, and every page working at ~360px.

One requirement copied to two files is one requirement that drifts.

## Open decisions owned here

| # | Question | Needed by | Recommendation |
|---|---|---|---|
| T0.7 | Does every document write an `acc.Journals` row, or only manual journals? | T1 | Manual journals only |
| T10.2 | Do asset acquisition and disposal get transaction type codes of their own? | T10 | No — capitalise from `BIL`, dispose under `INV` or `JRN` |
| — | Fixed assets: straight-line only, or books **and** tax depreciation? | T10 | *(open in `CLAUDE.md`)* |

**On T10.2**, since it is the one raised by this stage rather than inherited. Adding an `FXA`/`FXD` pair to `mst.TransactionTypes` would give the asset register clean provenance under its own codes — but a purchase of a laptop **is** a purchase: same vendor, same input GST, same payment terms, same aging. A new code buys a numbering series, a screen, a lifecycle and a posting path that all duplicate Purchase, to record something Purchase already records. Capitalising from a bill line costs one flag on the line and one register row.

The case against the recommendation, stated honestly: a disposal posted under `INV` puts a non-inventory sale into the sales ledger, where a revenue report has to know to exclude it. That is a real cost, and it is the reason to answer this rather than let it be decided by whoever writes T10.4 first.

The other four are in [`TRANSACTIONS.md`](./TRANSACTIONS.md#open-decisions-gathered).

## Sequencing across the two files

**T1 first, before anything in either file.** A posting nobody can read is a posting nobody checks, and the manual journal is the cheapest document to prove the ledger with — no stock, no tax, no contact. Everything Sales and Purchase post afterwards is verified on the screens T0.6 and T1 put in place.

Then the other file runs T2 → T5, and the rest of this one follows what it produces: **T6** needs invoices and bills to settle, **T8** wants every subledger finished, and **T10** needs the bill from T4.5 — an asset is capitalised from the document that bought it, so the fixed asset register cannot come before the document.

That last dependency is new. T10 was described as waiting on nothing and being last for that reason, which was only true while the stage had no acquisition in it.
