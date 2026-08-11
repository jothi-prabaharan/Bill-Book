# FLOW-SALES.md — the sale, end to end

How a sale travels from quote to cash: which document follows which, what each one posts, what it does to stock, and where the flow is allowed to skip a step.

This is a **flow document, not a plan**. It has no checkboxes. [`TRANSACTIONS.md`](./TRANSACTIONS.md) says what to build and in what order; this says how the result behaves. [`SPEC.md`](./SPEC.md) holds the columns.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** See *Git — how work reaches main* in `CLAUDE.md`.

**Status: designed, not coded.** `sal.*` has project folders and no entities. The stock half beneath it — reservation, the guarded issue, layer consumption, returns to the originating layer, the COGS posting — is **built and has never been called by a document**. So this describes an intended flow running on a real foundation, and says which is which at each step.

---

## The chain

```
QTE ──▶ SOR ──▶ INV ──▶ RCM
quote   order   invoice  receipt

        POS  = INV + RCM in one action
        CRN  = the way back out
```

**Every arrow is optional.** The chain is what the documents mean, not a sequence anyone is forced through:

| Start here | When |
|---|---|
| `QTE` | The customer wants a price before committing |
| `SOR` | Committed, but not yet delivered — the only reason stock gets reserved |
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

Taken **at post, not at draft**. A draft that is never posted must not consume a number in a series that has to be gapless — consecutive numbering is statutory on an Indian invoice, not a preference. Drafts show as unnumbered.

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

None of it has been called by a document. The sales flow is what finally calls it.

## What is missing beneath the flow

- **The ledger door posts one leg type per call** and refuses a request whose legs do not balance among themselves. An invoice's per-line revenue, per-rate tax and single header receivable balance in no subset. This is [T0.1](./TRANSACTIONS.md#stage-t0--foundations-before-the-first-document) and it blocks the invoice outright.
- **No tax determination exists anywhere.** [T0.2](./TRANSACTIONS.md#stage-t0--foundations-before-the-first-document).
- **No document numbering series exist.** [T0.3](./TRANSACTIONS.md#stage-t0--foundations-before-the-first-document).
- **`acc.TransactionRatio` is unbuilt**, so nothing can allocate a credit note or a receipt yet.
