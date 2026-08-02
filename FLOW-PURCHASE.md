# FLOW-PURCHASE.md — the purchase, end to end

How a purchase travels from order to payment: which document follows which, what each one posts, what it does to stock, and where the flow is allowed to skip a step.

This is a **flow document, not a plan**. It has no checkboxes. [`TRANSACTIONS.md`](./TRANSACTIONS.md) says what to build and in what order; this says how the result behaves. [`SPEC.md`](./SPEC.md) holds the columns.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** See *Git — how work reaches main* in `CLAUDE.md`.

**Status: designed, not coded.** `pur.*` has project folders and no entities. The stock half beneath it — receipts, cost layers, batch and serial capture, backdated recosting — is **built and has never been called by a document**, so every receipt today lands as an opening balance.

---

## The chain

```
POR ──▶ GRN ──▶ BIL ──▶ SPM
order   receipt  bill    payment

        DBN = the way back out
```

**Every arrow is optional, and the shortcuts are the common cases:**

| Start here | When |
|---|---|
| `POR` | Ordering ahead. Committed to the vendor, nothing has arrived |
| `GRN` | Goods turned up without a formal order |
| `BIL` | A service, or a trader who never raises a receipt. **The most common entry point** |

A purchase order does **not** reserve anything. There is nothing to reserve — the stock is not there yet. That asymmetry with the sales flow is deliberate and is the single most common thing to get wrong when mirroring one onto the other.

---

## POR — purchase order

**Posts nothing. Moves nothing. Reserves nothing.**

A commitment to a vendor at agreed prices. It stays open until fully received, then closes; a partial receipt leaves it partly open, and it can be closed short when the balance is never coming.

Its prices are what the goods receipt costs stock at, until a bill says otherwise — see *Price variance* below.

---

## GRN — goods receipt

**The moment stock exists.** Quantity moves inside the request through the same guarded conditional update everything else uses, and a cost layer opens at the received cost.

Three things belong here and nowhere else, because all three are user input and must be in the answer to the caller rather than in a background failure:

- **Batch and expiry** — required when the item is batch- or expiry-tracked. The lot carries its own MRP, which may differ from the item's.
- **Serial numbers** — created on the way in, one per unit, with per-piece HUIDs for jewellery.
- **Rejections** — what arrived versus what is accepted. Only the accepted quantity becomes stock.

### What it posts, and the open question

Goods are on the shelf; the bill that values them may be days away. Posting nothing at receipt leaves the inventory asset understated for exactly that long — the stock exists and the books do not know.

The recommendation, [open for decision at T4.1](./TRANSACTIONS.md#stage-t4--purchase-order-receipt-bill-por-grn-bil):

| Document | Debit | Credit |
|---|---|---|
| `GRN` | Inventory | **Goods Received Not Invoiced** (a Liability) |
| `BIL` against it | Goods Received Not Invoiced, + Input GST | Accounts Payable |
| `BIL` with no receipt | Inventory or Expense, + Input GST | Accounts Payable |

GRNI is a clearing account: the receipt opens the obligation, the bill closes it. A balance sitting in it is goods held and not yet billed, which is a number a controller actually wants.

Note what this does **not** change: `StockLedgerMapping` already refuses to post a receipt that carries a source document, on the grounds that Purchase will post it. That stays true. What moves is *when* Purchase posts — at the receipt rather than only at the bill.

---

## BIL — bill

The vendor's invoice. What the branch owes, and what it may claim back.

### Three kinds of line

| Line | Posts to | Moves stock |
|---|---|---|
| **Stock** | Inventory, or clears GRNI | yes, if no receipt preceded it |
| **Expense** | the expense account | no |
| **Capital** | the category's **Fixed Asset** account, and creates the register row | no |

The capital line is how **every purchased fixed asset gets onto the books**. Nothing else does it — a fixed asset register filled in by hand would disagree with its control account from the first entry. The register row names the bill that capitalised it, and the tie is checked. See [T10.2](./TRANSACTIONS-ACCOUNTING-BANKING.md#stage-t10--fixed-assets-acquisition-depreciation-disposal-dep).

### Input GST

Claimable, including on capital goods. Determined by the branch's state against the vendor's place of supply — same state → CGST + SGST, different → IGST — at the rate **in force on the bill date, never today's**. Same shared component the sales side uses; there is exactly one, by rule.

A vendor who is composition-scheme or unregistered charges no GST and the bill must not claim any. That is a property of the contact, read at the bill.

### Due date

From the payment terms on the contact, defaulting to the branch's own. This is what puts the bill into payables aging.

### Price variance — the question the flow raises

A receipt costs stock at the order's price and opens a layer at it. The bill may then say something different — a price change, freight, a rebate.

The layer is already open and may already have been consumed by a sale, whose COGS is therefore stated at the receipt price. Two ways out:

- **Revalue the layer** and let the recosting machinery restate every issue that drew on it. It already does exactly this for backdated receipts, and would state the true cost of sale.
- **Post the difference to a purchase price variance account** and leave inventory at the receipt cost. Simpler, and standard in manufacturing, but the margin on those sales stays slightly untrue.

**Not decided, and not currently in any plan stage.** It only bites when receipt and bill disagree, which is why it is easy to miss until it is live. The recosting engine existing already tilts this towards revaluation — the expensive half is built.

---

## DBN — debit note

The way back out: a purchase return, a short delivery found later, or a rebate.

| | |
|---|---|
| Debit | Accounts Payable |
| Credit | **Purchase Returns** — a *contra* Expense account, so the report subtracts it |
| GST | Input GST reversed on the same rates the bill used |
| Stock | Returned **to the layers it came from**, at their original cost |

It allocates against the bill through `acc.TransactionRatio` rather than floating unapplied, and can never exceed what is outstanding.

---

## SPM — money out

A payment posts under its **own** identity and points back at what it settles:

| | Debit | Credit |
|---|---|---|
| Account | Accounts Payable | Bank or cash |
| `TransactionTypeCode` | `SPM` | `SPM` |
| `LedgerSourceId` | 2 `BILLPAYMENT`, or 8 `VENDORPREPAYMENT` | same |
| `MappingTransactionId` | **the bill's id** | same |
| `MappingTransactionTypeCode` | **`BIL`** | same |

That mapping pair is the whole mechanism for tracing a payment to its bill.

**An advance paid before any bill exists** is a vendor prepayment, landing in Advance to Vendor and drawn down by allocation when the bill arrives. **A payment across several bills** writes one allocation row per bill.

**Foreign currency**: a bill raised at one rate and settled at another posts an extra pair to Realized FX Gain/Loss, computed from the difference between the two documents' stored rates — never from a live rate.

---

## How this differs from the sales flow

Mirroring the sales flow onto purchase gets four things wrong. They are worth having in one place:

| | Sales | Purchase |
|---|---|---|
| Does the order touch stock? | **Reserves** it | **Nothing** — it is not there yet |
| When does stock move? | On the invoice | On the **receipt**, which usually precedes the bill |
| Is there a clearing account? | No | **GRNI**, because goods and the bill arrive apart |
| Which side is the tax? | Output GST, a liability | Input GST, an **asset** — money reclaimable |

The fifth difference has no equivalent at all: a bill can carry a **capital** line, and a sales document never can.

---

## What is missing beneath the flow

- **The ledger door posts one leg type per call** and refuses a request whose legs do not balance among themselves. A bill's per-line, per-rate and header legs balance in no subset. [T0.1](./TRANSACTIONS.md#stage-t0--foundations-before-the-first-document), and it blocks the bill outright.
- **No tax determination exists anywhere.** [T0.2](./TRANSACTIONS.md#stage-t0--foundations-before-the-first-document).
- **No document numbering series exist.** [T0.3](./TRANSACTIONS.md#stage-t0--foundations-before-the-first-document).
- **No Goods Received Not Invoiced account** is in the chart-of-accounts seed. Adding one is safe on existing branches — the seed has been idempotent per account since master 1.4.
- **`acc.TransactionRatio` is unbuilt**, so nothing can allocate a debit note or a payment yet.
- **Price variance is undecided**, per above.
