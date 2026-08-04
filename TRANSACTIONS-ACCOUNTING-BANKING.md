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

- [x] **T0.5 — `acc.Journals` and `acc.JournalDetails`**
  Designed in SPEC, built nowhere. The manual journal document, and the only place a reversal is paired **line by line** — without which a partially reversed journal cannot be told from a fully reversed one, because both balance.
  Entities, `DbSet`s, Fluent config, the two check constraints, the deferred balance trigger on `Posted` only, and RLS. See T0.7 for whether other documents write here.
  *Done when*: a two-line journal saves as a draft while unbalanced, refuses to post unbalanced, and a reversal links both headers and every line.
  **Done**, and verified against a real PostgreSQL rather than against the model. **Two triggers, not one** — the line trigger never fires when a draft is posted, because posting changes the header and leaves the lines alone, so a second deferred trigger on `Journals` covers the one path that matters most. Four extra check constraints beyond SPEC's two: a draft holds no number and a posted entry must have one (both halves of T0.3's rule), `PostedAt` agrees with the status, the rate is positive, and nothing reverses itself.
  **Two deliberate deviations from SPEC, both recorded here rather than silently.** `JournalNo` is **nullable** with a filtered unique index — SPEC has it required, but T0.3 says the number is taken at post, and a required column would force a draft to consume one. And `JournalDetails` **carries `OrgId`** where SPEC says it is scoped through its parent: every other detail table in the product carries its own, and CLAUDE.md requires `OrgId` plus a query filter on every per-customer table without exception. Scoping through the parent means no EF query filter at all and an RLS policy that has to subquery the header — strictly weaker than the two lines every other table gets for nothing.

- [x] **T0.7 — Decide whether a document also writes a `Journals` row** *(owner decision)*
  SPEC's `Journals.TransactionTypeCode` reads "`JRN` when hand-written, else the source document's type", which implies every invoice gets a journal header shadowing it. 5.12 established the opposite mechanism: services post to `JournalLedger` directly, keyed by their own document, with no header in Accounting.
  *Recommendation: manual journals only.* A second header per invoice is a second thing to keep in step with the first, and the ledger rows already carry the document type and id. The cost of being wrong is one nullable column later; the cost of building it is a shadow table under every document in the product.
  **This decision reaches across the split** — a yes to the wider reading puts a `Journals` write into every Sales and Purchase document in the other file, which is precisely the reason to answer it before T1 rather than after T3.
  **Answered: manual journals only**, as recommended. `acc.Journals` holds `JRN` and nothing else; every other document posts straight to `JournalLedger` under its own type and id. `TransactionTypeCode` and `SourceId` stay on the header so a future document that does want one has somewhere to say so — that is the one nullable column the recommendation priced in. **Nothing in `TRANSACTIONS.md` now writes a `Journals` row**, which is what that file was waiting to hear.

---

## Stage T1 — the manual journal (JRN)

The simplest document that posts: no stock, no tax, no contact required. Doing it first proves the extended ledger door on something with nothing else going on. **This is the first stage of the whole plan**, in either file.

- [x] **T1.1 — Journal entry API** — create, edit while draft, post, reverse. Debit xor credit per line, balance guarded on post, `LedgerSourceId = 12`, `JournalId` set on the ledger rows.
  *Done when*: posting a balanced journal writes ledger rows that appear on T0.6's screens, an unbalanced post is refused, and a reversal offsets it exactly.
  **Done.** `JournalService` + `api/journals`, every leg at `LedgerTypeId = 3` (CONTROL) — a hand-written line has no item, tax, cost or rounding dimension behind it, and the whole entry then replaces as one key set on a retried post. The number allocation, the status flip and the ledger rows are **one transaction**: `LedgerPostingService` now joins an open transaction instead of always starting its own, so a refused posting rolls the number back rather than leaving a gap in a series that has to be gapless.
  **This needed T0.3's JRN series**, which did not exist — Accounting now seeds its own document series at branch creation (`JV/2526/00001`, financial-year reset, no manual override). That discharges T0.3 for Accounting only; Sales and Purchase still seed theirs.
  Verified against PostgreSQL: 13 tests covering the draft/post/reverse lifecycle, the line-level reversal pairing, consecutive numbering, and a refused post leaving the counter where it was.
- [x] **T1.2 — Journal entry page** — grid of lines with account and sub-account pickers, running debit/credit totals with the difference shown, draft save, post, reverse.
  *Done when*: a journal can be keyed, saved, posted and reversed without leaving the page, and the totals show the imbalance while it exists.
  **Done.** List and editor on one page. The account picker offers only what a hand entry may target — a seeded control account is driven by its own subledger, and a picker that offered what the server refuses is a screen arguing with itself. Sub-accounts narrow to the line's own account. Posted entries open read-only with a Reverse action, because a posted entry is never edited.

- [x] **Period locks — closing the books, one date per branch per role** *(owner request, not an original box)*
  `acc.PeriodLocks`: one `LockedUpto` per role per branch, inclusive, owned by Accounting because closing a period is an accounting act and every other service reads it before posting.
  **Per role rather than per branch, which is the whole point.** A close is rarely all-or-nothing — the month shuts to everyone who keys documents while staying open to whoever has to make the adjusting entries the close itself produced. A single date for the branch would close the books on the person doing the closing.
  **It refuses what reaches or leaves the ledger** — post, reverse, void — and nothing else. A draft touches nothing, so it may be written and edited with any date at all; it simply cannot be posted into a closed period. Someone catching up on last month's paperwork can still type it.
  **A user holds exactly one role per branch** (`idn.UserOrganizationRole` is read singly), so a per-role lock resolves to one date per user with nothing to reconcile. That required a **`role_id` claim on the access token**, which did not exist — the token carried `permission[]` only, and permissions cannot say which role granted them. `ICurrentUser.RoleId` follows it through every service.
  **A caller with no role gets the branch's strictest lock, not none.** That covers a token minted before the claim existed. Falling open would let a stale token post into a closed period for as long as it lives; falling strictest is safe and still lets ordinary work through.
  Wired into the manual journal today — post and reverse — and read by `GET api/period-locks/mine`, so a date picker can bind its minimum and prevent the refusal rather than report it. **Banking's money documents will call the same guard when T6.2 builds their API.** Nine tests against a real PostgreSQL.

---

## Stage T6 — money: spend, receive, transfer (SPM, RCM, TRM)

Where the receivable and payable balances raised by Sales and Purchase are actually cleared. `mst.LedgerSources` already distinguishes payment from refund and prepayment from allocation, which is what makes these three types carry nine different meanings.

Needs T3.3 and T4.5 from the other file — there has to be something outstanding before there is anything to pay.

- [x] **T6.1 — `bnk.*` transaction schema** — payments, receipts and transfers, with lines that allocate to documents.
  **Done — as three tables, not one.** Built first as a single discriminated pair following T2.1's recommendation for `sal`, then **split at the owner's direction**: `bnk.SpendMoney` + `bnk.SpendMoneyDetails`, `bnk.ReceiveMoney` + `bnk.ReceiveMoneyDetails`, and `bnk.TransferMoney`.
  What the split buys: each document carries exactly the columns it needs. A payment has a payee and no destination account, so `ContactId` is `NOT NULL` and there is no destination column at all — where the shared table needed both nullable plus a check constraint policing which combination was legal per type code. A transfer has no contact column at all. And no table carries a transaction type column, because each table *is* its type; the code is supplied at the ledger.
  **`bnk.TransferMoney` has no detail table.** A transfer allocates to nothing — it settles no invoice, clears no bill, holds no advance — so a line table would carry one row per document repeating the header. If a transfer ever needs to split (a bank charge taken out of the amount moved is the realistic case) that is a detail table added then, which is cheaper than carrying an empty one until it happens.
  **The cost, recorded because it is real**: bank reconciliation matches a statement line against *any* movement on an account, which is now a three-way union rather than one query. Same for a combined "money in and out" list.
  **The mapping is on the header as well as the line.** The header names the document a payment is *about* when it is about exactly one — `BIL` or `DBN` on the spend side, `INV` or `CRN` on the receive side — so listing payments needs no join. **The lines stay authoritative**: a payment split across three bills leaves the header mapping null, because no single document is what it is about. Anything reconciling a bill reads the lines.
  **`LedgerSourceId` is on the detail line, not the header** — which is the whole shape of T6.2 made structural. A payment that runs past what was owed is a bill payment *and* a deposit at once, so what a document *means* cannot live on the header. The same mechanism covers a payment split across several bills: one line per bill, one bank movement.
  Enforcement is where most of this task actually lives, and it is all in the database: a document must add up before it posts (a deferred trigger, and a **second** on the header because posting never touches the lines — the same pair the journal carries), a transfer has a destination and no contact, a payment has a contact and no destination, nothing transfers to itself, a draft holds no number and a posted document must have one, half a mapping is refused, and only `SPM`/`RCM`/`TRM` are accepted. Twelve tests in a new `Banking.Api.Tests` against a real PostgreSQL, skipping with a reason when none answers.
  **Banking now seeds its own document series** — `PAY`, `REC`, `TRF`, financial-year reset, no manual override — which is T0.3's per-service rule applied here. Banking had no seeder at all, so this adds one and wires it into Platform's `TenantSeeder` **after** Accounting, because Accounting owns the numbering table.
  Not in this box, and still open: the API, the screens and allocation. Those are T6.2–T6.6.
- [x] **T6.2 — Spend money (SPM)** — one document type carrying six meanings, chosen by `LedgerSourceId` rather than by transaction type. `MappingTransactionId` and `MappingTransactionTypeCode` point at what is being settled — that pair is the entire mechanism for tracing a payment to what it clears.

  | # | Type | Posting | `LedgerSourceId` |
  |---|---|---|---|
  | 1 | **Bill payment** | `Dr Accounts Payable / Cr Bank` | 2 `BILLPAYMENT` |
  | 2 | **Prepayment** — advance to a vendor before their bill | `Dr Accounts Receivable` › *Prepayment Advance Receivable* `/ Cr Bank` | 8 `VENDORPREPAYMENT` |
  | 3 | **Overpayment** — paid past what was owed | `Dr Accounts Payable` › *trade* (settled) **+** `Dr Accounts Receivable` › *Overpayment Advance Receivable* (excess) `/ Cr Bank` | 2 **and** 16 `VENDOROVERPAYMENT`, per leg |
  | 4 | **Credit-note refund** — a *sales* return paid back to a customer | `Dr Accounts Receivable` › *trade* `/ Cr Bank` | 6 `CREDITNOTEREFUND` |
  | 5 | **Overpayment refund** — a customer's excess given back | `Dr Accounts Payable` › *Overpayment Advance Payable* `/ Cr Bank` | 18 `CUSTOMEROVERPAYMENTREFUND` |
  | 6 | **Prepayment refund** — a customer's advance given back | `Dr Accounts Payable` › *Prepayment Advance Payable* `/ Cr Bank` | 19 `CUSTOMERPREPAYMENTREFUND` |

  `›` is the sub-account beneath the control account. **There are only two control accounts** — advances are not separate accounts, they are sub-accounts under receivables and payables, keyed by `SubAccountPurpose`.

  **Overpayment is not a type of its own — it is two types on one document**, and that is why the ledger source sits on the leg. Stamp the whole document "overpayment" and a payables report filtering on bill payments silently misses the part that *was* a bill payment.

  **Rows 5 and 6 clear different balances**, not the same one with different labels: a contact's prepayment advance and its overpayment advance are separate sub-accounts under Accounts Payable. The ledger source is then provenance rather than the only discriminator. The cost, accepted deliberately: a customer holding both carries two credit balances, so a statement has to add them up.

  **A *purchase* credit note is a debit note here, and its refund is money coming in.** The vendor's credit note against goods you returned is recorded as `DBN` — you debit the vendor and reduce payables — and settling it means the vendor pays *you*. So it is a **Receive money** type under `DEBITNOTEREFUND` (7, direction `In`), not a spend-money one. The credit-note refund at row 4 is the sales side: `CRN`, the customer returned goods, you pay them back.

  *Done when*: paying a bill clears it from payables aging and the ledger row names the bill it paid; an overpayment settles its bill and leaves the excess readable as an advance against that vendor; and refunding a customer's advance clears exactly that contact's balance.

  **Already built, ahead of this task** — none of it needed a bill to exist:
  - **`acc.SubAccounts.Purpose`** (`Primary` / `PrepaymentAdvance` / `OverpaymentAdvance`), in the unique key beside `TaxComponent`. Without it a contact's three sub-accounts under one parent key identically and only the first is ever written.
  - **The posting door now resolves a sub-account on its whole key.** Found while starting this task, and it was a live defect rather than a gap: the door matched on `(AccountId, ReferenceType, ReferenceId)`, which stopped being unique the moment a contact gained three sub-accounts under Accounts Receivable — so a supplier deposit would have landed on whichever row the database returned first, silently, with the trade balance and the advance never reconciling again. **The same ambiguity already existed for tax**, where CGST, SGST and IGST share a parent and a rate; nothing had hit it only because no document posts a tax leg yet. `SubAccountPurpose` and `TaxComponent` are now both on the leg, defaulting to `Primary` and `None`, and the lookup uses the table's full unique key. Two regression tests.
  - **A contact now gets six sub-accounts**: the trade balance, a prepayment advance and an overpayment advance beneath **each** of Accounts Receivable and Accounts Payable. Grouped by the direction the balance runs, not by counterparty role, so every sub-account's type matches the parent it hangs from — `AccountTypeId` is copied from the parent, and grouping the other way would have a report by type contradict the same report by account.
  - **No separate advance control accounts.** `Advance to Vendor` and `Advance from Customer` were seeded and then removed: the whole of a party's position now sits under two control accounts rather than four.
  - `mst.LedgerSources` 16–19. `LedgerSource.Code` widened to 30, because the naming scheme runs past 20 characters and abbreviating a key to fit a column is how a lookup table fills with codes nobody can read.
  - **`LedgerSourceId` moved onto the ledger leg**, the same move T0.1 made for `LedgerTypeId` and for a related reason — see T0.1 in [`TRANSACTIONS.md`](./TRANSACTIONS.md).
  **A consequence to carry into the balance sheet.** Because the advances live inside the AR and AP control accounts, **neither control total is a Schedule III line on its own** — the Act requires advances to suppliers and from customers to be reported apart from trade receivables and payables. `SubAccountPurpose` is what splits them back out, and any balance sheet that ignores it overstates both trade lines by the advances held. Nothing owns that split yet; it belongs to whoever builds the balance sheet.

  **Done — the API.** `SpendMoneyService` + `api/spend-money`: draft, edit, post, void, delete. `MoneyPostingMap` turns each line's ledger source into the account it debits, so the six meanings are a lookup rather than a branch. **One control leg and one bank leg per line**, so each pair balances on its own and every ledger row carries the source that produced it — an overpayment's two halves stay distinguishable, which is the whole point.
  **The ledger call goes before the number and the status flip**, deliberately. A posting written for a document that then fails to flip is replaced on the retry; a document marked posted with no ledger rows behind it is a silent hole in the accounts. The number allocation and the flip are one transaction, so a failed post gives the number back.
  **The period lock reaches Banking** through the same guard, over the forwarded user token. **Unreadable is not unlocked**: if Accounting cannot say whether the books are closed, the document is refused as transient rather than allowed through.
  **Done — the screens.** Banking › Spend money and Banking › Receive money are **one component told which direction it is**, routed twice. They are the same document read in opposite directions, and two copies would drift — with the copy nobody opened being the one that drifted.

  **There is no account picker, and that is the substance of the screen rather than a simplification of it.** Which account a line posts to follows from the contact and the line's ledger source, so the screen resolves the contact's sub-account from `GET /api/sub-accounts?referenceType=Contact` and *shows* it under the line. Shown, because someone keying a payment still has to see where it lands; not editable, because the server resolves the same sub-account from the same rule and a picker could only disagree with it. It also forces the order: the contact is chosen before any line, since without one there is nothing to point a line at, and a payment against the bare control account is the posting that makes a contact statement stop tying.

  The allocation panel shows amount, allocated and **unallocated** as the lines are keyed — unallocated money is normally an overpayment, so it wants a line rather than tidying away, and **Fill rest** puts the remainder on one. The document a line settles is derived from its source (a bill payment can only settle a bill, an advance settles nothing) rather than picked beside it, and changing the source drops an id keyed against the old one.

  Two things worth writing down, because both were silent failures rather than errors:
  - **`SubAccountListItem` now sends its enums by name.** Nothing in this product configures a string enum converter, so `ReferenceType`, `TaxComponent` and `Purpose` were going out as integers while both screens that read them compared against names. The sub-account list has been drawing a blank purpose chip since it was built. Projected to strings, the way the money documents already send `Status` and `PaymentMethod`, with a test on the contract.
  - **The header figures are getters, not `computed`.** A `computed` only recomputes when a signal it read changed; built over a plain ngModel field it caches the first answer forever, so raising the header amount would have left "unallocated" at zero with Post still enabled. The same trap the journal screen hit with its line array, in the opposite direction.

  **Still open here**: allocation (T5.1) — which is what would stop a payment exceeding what a bill actually owes. **T6.3 will want sources 20–21** — the vendor-side mirrors, for a vendor refunding your overpayment or returning your advance. They are deliberately not seeded yet: `LedgerSources` ids are contractual, and seeding a code nothing writes is a row that gets used for the wrong thing before its document arrives.
- [x] **T6.3 — Receive money (RCM)** — the mirror, against invoices, with customer prepayments landing in the contact's prepayment advance rather than against a document.
  **Done.** `ReceiveMoneyService` + `api/receive-money`, the mirror of T6.2 in every respect but the direction of each pair of legs: the bank is debited and the contact's control account credited. Five sources map on this side — invoice payment, customer advance, customer overpayment, debit-note refund, and a supplier returning what we overpaid.
  The two vendor-side refund mirrors (a supplier returning a deposit) still have no seeded ledger source. They are ids 20–21 when something writes them; seeding a code nothing writes is a row that gets used for the wrong thing.
  **The screen is the spend-money component with `direction: 'receive'` on its route** — see T6.2. Only the wording changes: *Received from* rather than *Paid to*, *Received into* rather than *Paid from*, and the five receive-side sources in the line picker.
- [x] **T6.4 — Transfer money (TRM)** — bank to bank or bank to cash. No contact, no sub-account, no control leg; the one source with no counterparty.
  **Done.** `TransferMoneyService` + `api/transfer-money`. Two legs, both naming a bank account by the id Accounting issued when it was created, both under `MONEYTRANSFER`. No detail table and none needed — a transfer settles nothing.
  **The only money document that could be built end to end today**, because it waits on no invoice or bill. A transfer moves no value in or out, so the trial balance is unchanged by one; what changes is which account holds it.
  **Its own screen, not a third mode of the payment screen.** Everything that makes that one complicated — the contact, the allocation lines, the sub-account the account is locked to — is absent here, and a shared screen would carry the machinery anyway to switch it all off. What is left is two account pickers, an amount, and **⇅** to swap the ends, which is the correction people reach for most.
- [x] **T6.5 — Realized FX on settlement** — an extra pair to Realized FX Gain/Loss at `LedgerTypeId = 5`, computed from the difference between the document's `ExchangeRate` and the payment's. **Never from a live rate.**
  *Done when*: an invoice raised at one rate and settled at another leaves no residual balance on the contact, and the difference sits in Realized FX Gain/Loss.

  **Done.** The property that decides the whole shape: **the balance comes off at the rate it went on at.** Relieve a receivable at today's rate and the ledger still balances — while the contact keeps a residue against an invoice they have paid in full, in a currency they no longer owe. Nobody goes looking for that, which is what makes it the dangerous half of this task rather than the arithmetic.

  So a settlement's three legs do not share one rate, and the ledger door had to stop assuming they did. `LedgerLegRequest` gained an optional `CurrencyCode` and `ExchangeRate`: null on every leg of an ordinary document, set on the two that settlement produces. The control leg converts at the settled document's rate, the bank leg at the payment's, and the FX leg is **denominated in base at rate 1** — the difference never existed in the foreign currency, so recording it in one would invent a figure the counterparty never saw. The balance check is in base, so the request balances *because* the three converted differently; forcing them onto one rate leaves either a residue or an unbalanced posting, and there is no third answer.

  **The rate is read back off the settled document's own ledger rows** — `GET api/ledger/documents/{code}/{id}/rate`, the control leg's, falling back to the lowest-id leg for a document with none. Accounting is the one service holding every document's rate in one table, so Banking asks it rather than integrating with Purchase and Sales separately for a number that is already written down. Nothing consults a rate table anywhere in this path: a historical document that repriced itself would restate settled history.

  **The FX leg takes whichever side the pair is short on**, rather than a sign rule per direction. Spend and receive are mirrors, and a sign asserted twice is a sign that can be right once — this way the gain and the loss are the same call with two arguments swapped, and neither can be inverted while the other is correct.

  Two refusals, both deliberate:
  - **An unreadable rate refuses the document as transient**, exactly as an unreadable period lock does. "Could not ask" must not look like "no difference", or a lookup that blipped posts the whole payment at today's rate and leaves the residue this task exists to prevent.
  - **Settling a document raised in another currency is refused, not converted.** Two conversions in one settlement is a cross-rate, and inventing one puts a figure in the books that no recorded rate produces.

  A void now withdraws `FX` as well as `CONTROL` — a withdrawal that named only the types a document happened to write would strand an exchange difference on a voided payment, unbalanced and attached to nothing.

  Eight tests: the loss, the mirrored gain, the same-rate no-op, a settled document with nothing posted yet (not an error — there is no earlier rate to differ from), the unreadable rate, the currency mismatch, the per-leg rate at the door, and the rate read.
- [x] **T6.6 — Partial and over-payment** — a payment across several documents, and a receipt exceeding what is owed becoming a prepayment rather than a negative balance.

  **Half of this was already structural, and the other half was unguarded.** T6.2 put the ledger source and the settled document both on the *line*, so ₹50,000 across three bills was already three lines and three pairs of legs, each traceable to the bill it cleared; and an excess already landed in the overpayment advance rather than as a negative trade balance. What was missing is that nothing checked the lines were saying anything sensible, and both mistakes available here are silent ones.

  **A line naming the wrong kind of document** traces a payment to something it did not pay — worse than a missing link, because a statement reconciling on it reads as answered. The kind now follows from the source rather than being a second choice beside it: `MoneyPostingMap.Settles` says a bill payment settles a `BIL`, an invoice payment an `INV`, a debit-note refund a `DBN`, and an advance nothing at all. A line naming a document its source cannot settle is refused, and so is an advance claiming one.

  **Two lines claiming one document the same way** leave two ledger rows against one balance. Refused — but keyed on the **source** as well as the document, because paying a bill and overpaying against it are two claims on one bill and that is T6.2's whole overpayment story. Two *bill payment* lines against one bill is the keying slip; a bill payment and its excess are not.

  **The header's mapping is now derived from the lines, not taken from the caller.** It is a summary of them: a caller free to disagree with its own lines can save a document whose header names one bill while its body pays three, and a list reads the header first. One document when every line that names one names the same; nothing at all otherwise, because a payment across three bills is about three and picking one would be arbitrary.

  **On the screen**, the unallocated figure now offers to place itself: *Add ₹1,000 as an overpayment* adds the line under the right source and, when exactly one document is being settled, names it. That is the difference between an excess that is recorded and one that is quietly folded into the settled line — where it turns the contact's balance the other way up and every aging report reads it as trade.

  **Not here, and it is T5.1: how much a document actually owes.** That needs the bill and the invoice to exist. Until then a line may settle more than its document is for; this refuses the mistakes catchable without asking anyone what the balance is.

  Seven tests: three bills on one payment, the derived header both ways, the wrong document kind, an advance claiming a document, the repeated claim, the partial-plus-excess split across two control accounts, and an overpayment legally naming the invoice it ran past.

---

## Stage T8 — opening balances (OPB)

`CLAUDE.md` calls this the highest-risk screen in the system, and it is deliberately late: it is the one document that touches every subledger at once, so it wants all of them finished.

- [x] **T8.1 — Orchestration** — Accounting drives; Inventory takes opening quantity and unit cost and seeds the weighted average; Contacts takes opening AR and AP **per contact, never a lump sum**, or aging is broken from day one.

  **Done.** `acc.OpeningBalances` + `acc.OpeningBalanceLines`, **one per branch enforced by a unique index on `OrgId`**. A branch has one moment it started and every balance since is measured from it; a second row is a second starting position with nothing to say which one anything was measured from.

  **Per contact was only half of it.** The plan gives aging as the reason for not taking AR and AP as a lump, and the same reason keeps going: a contact owing ₹300,000 across invoices of thirty, ninety and a hundred and eighty days ages nothing like one ₹300,000 balance. So the line is **one per document**, carrying the original system's reference and date — the reference is what the contact quotes when they query it, the date is what the aging runs from. A contact has as many lines as they have unpaid documents.

  **Four line kinds, and only one of them picks an account.** `GlAccount` names one; `ContactReceivable` and `ContactPayable` resolve theirs from the kind, because a receivable free to choose its own account is a receivable that can be filed under Sales Revenue; `Item` names neither. Enforced by a check constraint as well as in C#, the constraint for the shape and the C# for the reason.

  **Stock is Inventory's to record, through a new `internal/stock/opening` door.** An item line is a quantity and a unit cost and has nowhere to type a value — the cost seeds the weighted average, so every cost of sale until the next receipt comes from it, and only Inventory can seed it. Inventory then posts `Dr Inventory / Cr Opening Balance Equity` itself, on the path a receipt already takes. A value Accounting posted would be a second figure against the Inventory account, free to disagree with the stock the items actually carry. The door takes the internal key with the tenant in the body rather than forwarding the user's token — otherwise finalizing a migration needs inventory permissions for an accounting act — and it is idempotent on the document and line, so a retried finalize is told the stock is already there rather than doubling it.
- [x] **T8.2 — The validation** — Opening Balance Equity nets to zero, and finalize is blocked until AR, AP and Inventory subledgers tie to their control accounts.

  **Done, and the shape of the posting is what makes the validation possible.** Every line posts as its own balanced pair against Opening Balance Equity — `Dr Bank / Cr OBE`, `Dr OBE / Cr Accounts Payable` — so the ledger balances after every line and **OBE accumulates precisely what has not been accounted for yet**. Zero is the whole validation. A migration finalized with OBE non-zero invents equity out of a figure somebody forgot, and it balances perfectly while doing it.

  A pair per line rather than one OBE leg for the document, deliberately: a ledger row on the equity side traces back to the line that produced it, which is the only way anyone finds the wrong figure afterwards.

  Three more blockers, each catching something that balances anyway:
  - **A contact with no sub-account.** The line would reach the control account with nothing beneath it — perfectly balanced, and the contact owes nothing, so the statement, the aging and the first receipt against it are all wrong while the trial balance says everything is fine. The contacts are named, not counted.
  - **A missing control account**, including Opening Balance Equity itself, which even a document of nothing but bank balances needs.
  - **Inventory's recorded value disagreeing with the document's.** The one subledger Accounting cannot compute for itself, so it is asked and the answers compared. A migration that ties everywhere except the one place nobody looks is what this is for.

  Readiness is a **read of its own**, not a by-product of pressing Finalize — a migration is keyed over weeks, and finding at the end that it is out by ₹4,300 means auditing weeks of work instead of the line just typed.
- [x] **T8.3 — Read-only after go-live**, and migrated fixed assets skip historical depreciation.
  *Done when*: a trial balance drawn immediately after finalize balances, every subledger ties, and the screen refuses to reopen.

  **Done, except the fixed-asset half — see below.** `OpeningBalanceStatus` is `Draft → Finalized` and stops there: **no void and no reversal, unlike every other document in the product.** A journal or a payment can be reversed because it is one event among thousands; reversing an opening balance does not restate a transaction, it deletes the starting position every balance since has been measured from. An error found afterwards is corrected the way any error in a closed period is. Save, delete and finalize all refuse once finalized, and the check constraints hold the same line from underneath.

  **The screen is the readiness panel, not the grid.** Accounting › Opening balances shows debits, credits, stock and the equity plug continuously, with every blocker listed rather than summarised. Stacks to cards at ~360px, because this is a screen people key hundreds of rows into.

  **Deferred with the register, not blocked: migrated fixed assets.** There is no asset register to migrate into, so an asset comes across as a plain account balance and carries no cost, life or schedule of its own. "Skips historical depreciation" is a property of a register row, so it moves to Phase 2 with T10 — see the note on that stage. Recorded in the docs page in the user's own words rather than left to be discovered. **T8 is otherwise complete for Phase 1.**

  **The *Done when* line, closed properly.** It asks for a trial balance drawn immediately after finalize, and for every subledger to tie — and the first was only being inferred from the ledger rows while the second was a proxy: I was checking that each contact had a sub-account *provisioned*, which is not the same claim as the subledger *tying*.

  So there is now a real tie: **`GET api/ledger/sub-ledger-tie`**, each control account against the sum of the balances beneath it, with the difference named as what belongs to nobody. **It is the check double-entry cannot make.** A receivable posted to Accounts Receivable with no sub-account is a perfectly balanced entry — the trial balance foots and nobody owes the money — so the statement, the aging and the first receipt against it are all wrong while nothing in double-entry is broken. Accounts with no sub-accounts are left out rather than listed at a difference equal to their whole balance, which would bury the two rows that matter.

  It runs **both sides of go-live**: as a blocker, so a branch already adrift cannot open its books on top of it (afterwards nobody could tell which of the two put the difference there), and as a read on the screen after finalize, so the migration is confirmed to have landed rather than merely permitted.

  Twelve tests: the balanced migration and its six ledger rows, the plug left holding something, stock tying against capital without Accounting posting its value, the unprovisioned contact, Inventory refusing, Inventory's value disagreeing, read-only after go-live, save replacing rather than duplicating, a line naming the wrong thing for its kind, a trial balance actually drawn after finalize with every subledger tying, a control balance with nobody beneath it that foots and still does not tie, and an existing untied subledger blocking go-live.

---

## Stage T10 — fixed assets: acquisition, depreciation, disposal (DEP)

> **Deferred to Phase 2** *(owner decision, 4 August 2026)*. Nothing in this stage is built and nothing else in the plan waits on it. `CLAUDE.md`'s roadmap moves with it — Fixed Assets is out of the Phase 1 line and into Phase 2.
>
> **It was already blocked twice over**, which is what makes deferring it cheap rather than a loss: T10.2 capitalises an asset from the bill that bought it, and the bill is **T4.5** in [`TRANSACTIONS.md`](./TRANSACTIONS.md) and does not exist; and two owner decisions in the table below are still open, both of which change the table shape. Building it now would mean guessing at both and reworking the schema when the bill lands.
>
> **The one thing that moves with it**: T8.3's "migrated fixed assets skip historical depreciation". That is a property of a register row, so it defers to wherever the register goes. **T8 is otherwise complete for Phase 1** — the docs page says so in the user's own words rather than leaving it to be discovered.
>
> The boxes below are kept, not deleted. The analysis in them is the reason acquisition comes before depreciation, and it is the thing most likely to be got wrong by whoever picks this up in Phase 2.

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

| # | Question | Needed by | Recommendation | Answer |
|---|---|---|---|---|
| T0.7 | Does every document write an `acc.Journals` row, or only manual journals? | T1 | Manual journals only | **Manual journals only.** Settled — see T0.7 above |
| T10.2 | Do asset acquisition and disposal get transaction type codes of their own? | T10 — **Phase 2** | No — capitalise from `BIL`, dispose under `INV` or `JRN` | *open, deferred with the stage* |
| — | Fixed assets: straight-line only, or books **and** tax depreciation? | T10 — **Phase 2** | *(open in `CLAUDE.md`)* | *open, deferred with the stage* |

**On T10.2**, since it is the one raised by this stage rather than inherited. Adding an `FXA`/`FXD` pair to `mst.TransactionTypes` would give the asset register clean provenance under its own codes — but a purchase of a laptop **is** a purchase: same vendor, same input GST, same payment terms, same aging. A new code buys a numbering series, a screen, a lifecycle and a posting path that all duplicate Purchase, to record something Purchase already records. Capitalising from a bill line costs one flag on the line and one register row.

The case against the recommendation, stated honestly: a disposal posted under `INV` puts a non-inventory sale into the sales ledger, where a revenue report has to know to exclude it. That is a real cost, and it is the reason to answer this rather than let it be decided by whoever writes T10.4 first.

The other four are in [`TRANSACTIONS.md`](./TRANSACTIONS.md#open-decisions-gathered).

## Sequencing across the two files

**T1 first, before anything in either file.** A posting nobody can read is a posting nobody checks, and the manual journal is the cheapest document to prove the ledger with — no stock, no tax, no contact. Everything Sales and Purchase post afterwards is verified on the screens T0.6 and T1 put in place.

Then the other file runs T2 → T5, and the rest of this one follows what it produces: **T6** needs invoices and bills to settle, **T8** wants every subledger finished, and **T10** needs the bill from T4.5 — an asset is capitalised from the document that bought it, so the fixed asset register cannot come before the document.

That last dependency is new. T10 was described as waiting on nothing and being last for that reason, which was only true while the stage had no acquisition in it.
