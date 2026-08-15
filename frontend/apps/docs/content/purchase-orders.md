# Purchase orders

A purchase order records what you have asked a vendor for. It is the first
document in the purchase chain:

```
POR ──▶ GRN ──▶ BIL ──▶ SPM
order   receipt  bill    payment
```

Every arrow is optional. A bill keyed straight in, with no order and no receipt
behind it, is the most common entry point — a service, a utility, a trader who
never raises paperwork.

## What an order does, and what it deliberately does not

**It reaches no account and holds no stock.** Nothing is posted to the ledger,
and no quantity is set aside. This is the single biggest difference from a sales
order, which does reserve stock when you confirm it: a sales order holds goods
that are on your shelf and could otherwise be promised twice, while a purchase
order is a request for goods that are not here yet. There is nothing to hold
back.

What the order buys you is a numbered record of what was agreed, and something
for a goods receipt to be raised against when the delivery arrives.

## Raising one

Settings → **Purchase → Purchase orders → New purchase order**.

| Field | Notes |
|---|---|
| Vendor | Chosen from a search — only contacts marked as vendors appear. Picking one fills the GSTIN if it is blank. |
| Vendor GSTIN | Optional. When set, it decides the place of supply. |
| Order date | The document's own date, and the date every rate on it is taken at. |
| Expected delivery | When the vendor is expected to deliver. Cannot fall before the order date. |
| Place of supply | The two-digit state code the supply is made in. |

Lines use the same grid as every other sales and purchase document. On each line
you choose an item — or type a description, if your branch allows lines without
one — then the quantity, price, discount, tax treatment and the **GST rate**.

Totals and tax are computed by the server from the lines, at the rates in force
on the order date. The figures on screen update as you type so you can see what
you are agreeing to, but they are never what gets saved — the server recomputes
all of them, so a stale or tampered screen cannot produce a document whose foot
disagrees with its body.

If no rate can be chosen, no purchase GST rates are set up yet: add them under
**Settings → Tax master**. A rate marked sales-only will not be offered here.

### Place of supply matters more here than on an invoice

Left blank, the place of supply follows the vendor's GSTIN, which is right for
an ordinary registered vendor.

**A vendor who is unregistered or on the composition scheme has no GSTIN**, so on
those orders there is nothing to fall back to and the order is refused until you
set the state code yourself. That is deliberate rather than fussy: without one of
the two there is no way to tell an intra-state supply (CGST + SGST) from an
inter-state one (IGST), and a wrong answer there still prints, still totals and
surfaces months later as a mismatch on a return.

### Three kinds of line

Purchase is where all three line kinds are actually used:

- **Stock** — goes to inventory. Needs an item.
- **Expense** — goes to the account you name on the line. Freight, a service, a
  subscription.
- **Capital** — goes to a fixed asset category's account. Needs the category,
  because the category owns the accounts, not the individual asset.

## Approving and issuing

An order moves through three states, and a branch can use as much of that as it
wants:

| Action | What it does |
|---|---|
| **Save draft** | Keeps working on it. Editable. |
| **Approve** | Marks it reviewed and waiting to be issued. Still editable — the reviewer is often the person who spots the typo. |
| **Issue to vendor** | Commits it. The order is frozen, and a goods receipt can be raised against it. |

Approving and issuing both need the **purchase.approve** permission, not
`purchase.edit`. Committing the company to a spend is a different authority from
keying the paperwork, and a clerk who raises orders is precisely the person who
should not also be able to issue them. A branch that does not want the review
step can issue straight from draft.

## Withdrawing one

**Orders are never deleted.** The number was spent when the order was created, so
deleting the row would leave an unexplained gap in the sequence. Void it instead:
the order and its number stay on record, marked void, with the reason you gave.

The reason is required. A void with no reason is the row somebody has to
reconstruct from memory a year later.

An order that a goods receipt or a bill already points at **cannot be voided** —
withdrawing it would leave the other document referring to something that was
taken back. Undo the receipt first.

## Two statuses, side by side

The list shows both, and they mean different things:

- **Status** — where the document is in its life: Draft, ReadyToPost, Posted,
  Void.
- **Received** — how much has arrived: Open, PartlyReceived, Closed, Cancelled.

An order can be Posted and still Open: issued to the vendor, nothing delivered
yet. Received status starts moving when goods receipts are built.

## Not built yet

The goods receipt, the bill and the debit note are designed but not coded, so
today an order can be raised, issued and voided but nothing can be received
against it. Until the goods receipt lands, stock still arrives through an opening
balance.

A few things on the form are still simpler than they will be: the warehouse, the
unit of measure and the batch are not yet selectable on a line, and an expense or
capital line takes an account or category by its id rather than from a picker.
Nothing there blocks raising an order — the server fills in sensible defaults —
but they arrive with the goods receipt, which is the first document that needs
them for real.
