# TRANSACTIONS-ACCOUNTING-BANKING.md — the money documents

The six transaction documents owned by **Accounting** and **Banking**, split out of [`TRANSACTIONS.md`](./TRANSACTIONS.md), which keeps the ten owned by Sales, Purchase and Inventory.

`CLAUDE.md` holds the conventions. [`SPEC.md`](./SPEC.md) holds tables and pages. [`master.md`](./master.md) holds the build order up to here.

Same rules as `master.md`: take the first unticked box, check it against its **Done when** line, tick it in the same commit as the work, and strike a task rather than deleting it if it turns out to be wrong.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** A branch invented mid-task splits the work across two places and leaves whichever one nobody merges behind. See *Git — how work reaches main* in `CLAUDE.md`.

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

**T0.2**, tax determination, is the one foundation nothing here needs. None of these six documents carries GST.

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

## Stage T10 — fixed assets and depreciation (DEP)

Last because nothing else waits on it, and because it still carries an open question in `CLAUDE.md`.

- [ ] **T10.1 — `acc.FixedAssetCategories`, `acc.FixedAssets`, `acc.DepreciationSchedules`** — not designed in SPEC. **The category owns the GL mapping** (Fixed Asset / Accumulated Depreciation / Depreciation Expense); per-asset mapping does not scale.
- [ ] **T10.2 — Depreciation run** — a period posting `Dr Depreciation Expense / Cr Accumulated Depreciation`, the latter contra so the balance sheet subtracts it.
- [ ] **T10.3 — Disposal** — proceeds against written-down value, gain or loss to the P&L.
  *Blocked on an owner decision already recorded in `CLAUDE.md`*: straight-line only, or books **and** tax depreciation? Two schedules per asset is a different table shape, so it is cheaper to answer than to retrofit.

---

## Standing requirements

The same list as [`TRANSACTIONS.md`](./TRANSACTIONS.md#standing-requirements), unchanged and deliberately not copied here — documentation in the same commit, `OrgId` and RLS on every per-customer table, `[Authorize]` and `RequireModulePermission` on every endpoint, idempotent postings, `ExchangeRate` snapshot at document date, both check suites green before a box is ticked, and every page working at ~360px.

One requirement copied to two files is one requirement that drifts.

## Open decisions owned here

| # | Question | Needed by | Recommendation |
|---|---|---|---|
| T0.7 | Does every document write an `acc.Journals` row, or only manual journals? | T1 | Manual journals only |
| — | Fixed assets: straight-line only, or books **and** tax depreciation? | T10 | *(open in `CLAUDE.md`)* |

The other four are in [`TRANSACTIONS.md`](./TRANSACTIONS.md#open-decisions-gathered).

## Sequencing across the two files

**T1 first, before anything in either file.** A posting nobody can read is a posting nobody checks, and the manual journal is the cheapest document to prove the ledger with — no stock, no tax, no contact. Everything Sales and Purchase post afterwards is verified on the screens T0.6 and T1 put in place.

Then the other file runs T2 → T5, and the rest of this one follows what it produces: **T6** needs invoices and bills to settle, **T8** wants every subledger finished, and **T10** waits on nothing and is last for that reason.
