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



# Goods receipts

A goods receipt records what actually turned up. It is the document that puts
stock on the shelf on the purchase side.

```
POR ──▶ GRN ──▶ BIL ──▶ SPM
order   receipt  bill    payment
```

It usually arrives **before** the bill. Goods on the shelf and the invoice still
in the post is the ordinary case, not an edge one, and it is the reason this
document exists separately from the bill at all.

## What posting one does

Three things, in this order:

1. **The accepted quantity goes into stock**, at what it cost, opening a cost
   layer that later sales draw against.
2. **The ledger takes `Dr Inventory / Cr Goods Received Not Invoiced`.** GRNI is
   a clearing account: this opens the obligation, and the bill closes it against
   Accounts Payable. What sits in it is goods you are holding that nobody has
   invoiced you for yet.
3. **The purchase order moves toward closed**, by the accepted quantity.

**No GST is posted here.** Input tax credit is claimed against the *vendor's*
invoice number, and a delivery note is not an invoice. The tax figures are on the
lines so the document reads correctly and the bill can be matched against it, but
they reach no account until the bill.

## Recording a delivery

Purchase → **Goods receipts → New goods receipt**.

Pick the vendor, then optionally the **order** it came against — that copies
across whatever is still outstanding on it, which is what the person holding the
cartons is looking at. Receiving without an order is fine; nothing is advanced.

Record the vendor's own delivery note number. It is theirs, not yours, and it is
what a storekeeper reconciles against the docket in their hand.

### Accepted and rejected

**Only the accepted quantity becomes stock.** You key what was *refused*, and the
rest is accepted automatically — the two have to add up to what was delivered,
and a form with two boxes that must sum to a third spends its life inconsistent
while somebody types.

A rejection needs a reason. A rejection with no reason is the row somebody has to
reconstruct from memory a year later, when the vendor disputes the credit.

Rejecting does **not** close the order for those units. Ten ordered, ten
delivered, two refused leaves the order at eight received and still partly open —
because two are still owed.

### Batch and expiry

Enter these for items that track them. Inventory refuses the receipt and names
the line if a batch-tracked item arrives without one, so nothing is silently
recorded against the wrong lot.

## Getting it wrong

**A posted receipt cannot be voided.** The stock is on the shelf and the ledger
has it, and voiding cannot un-receive goods. To send them back, raise a **debit
note** — that reverses both the stock and the posting, and leaves both documents
on record.

A receipt still in draft voids normally, with a reason.

If posting fails partway — the ledger is unreachable, say — the receipt stays a
draft and you simply post it again. The stock will not be recorded twice: a
movement is keyed on the document and line it came from, so the second attempt
recognises what it already did.

## Only goods

Freight, insurance and duty do not belong on a receipt, and neither do fixed
assets. A goods receipt records goods arriving; costs that attach to them are
landed cost on the **bill**, and an asset is capitalised from the bill that buys
it. The screen refuses expense and capital lines for that reason.

## Not built yet

The bill is not coded, so nothing clears Goods Received Not Invoiced yet — its
balance will grow with every receipt until bills land. That is expected at this
stage rather than a fault, but it does mean the account is currently one-way.



# Bills

A bill records what a vendor is owed, and it is the document input tax credit is
claimed on.

```
POR ──▶ GRN ──▶ BIL ──▶ SPM
order   receipt  bill    payment
```

Most bills are entered straight in — a service, a utility, a trader who never
sends a delivery note. Attaching a goods receipt is the other case, and it
changes what posting does.

## The vendor's number, not yours

Every bill carries two numbers:

- **Our number** — allocated automatically, for internal reference.
- **The vendor's bill number and date** — theirs, and required.

Input tax credit is claimed against *their* number, and GSTR-2B reconciles on it.
So **one vendor cannot bill the same number twice in a financial year** — the
second one is refused when you enter it. That refusal is the point: claiming the
same number twice is a duplicate credit, and finding out at entry is much cheaper
than finding out from a notice.

The same number in the *next* financial year is fine — vendors reset their
numbering each April.

## With a receipt, or without

**Attached to a goods receipt**, the goods are already on the shelf and already
sitting in Goods Received Not Invoiced. The bill therefore **moves no stock**: it
clears that account and owes the vendor. The lines are copied from the receipt
and locked, because a bill has to agree with its receipt to the paise.

> If the vendor billed a different price from the one the goods were received at,
> the bill is refused. Handling that difference — purchase price variance — is
> not implemented yet. For now, either correct the price, or detach the receipt
> and bill without it.

**With no receipt**, nothing has arrived in the books yet, so the bill does both
jobs: it **moves the stock** and debits Inventory directly.

## Three kinds of line

- **Stock** — goes to inventory, or clears the receipt that already put it there.
- **Expense** — goes to the account you name. Freight, a subscription, a service.
- **Capital** — goes to **Fixed Asset** and moves no stock.

> Capital lines currently all land on a single Fixed Asset account. When the
> fixed asset register is built, each asset category will carry its own accounts
> and the bill will create the register row as well.

## Due dates and aging

A bill needs a due date. Choose a **payment term** and it is worked out for you,
or set the date directly.

The list shows how many days past due each posted bill is, with an **overdue
only** filter and a running total — which is the number a payment run starts
from. Aging is grouped by vendor because the payable is recorded against the
vendor, not against one lump figure.

## Getting it wrong

A posted bill is not edited. Correct it with a **debit note**, which is also how
goods go back.

Voiding a posted bill withdraws its ledger entries rather than leaving them
behind under a cancelled document — but a bill that **brought the goods in
itself** cannot be voided, because voiding cannot un-receive stock. Send it back
with a debit note instead.

## Not built yet

Debit notes are not coded, so there is currently no way to return goods or
reverse a bill's tax. Landed cost — freight and duty spread across the lines —
is held on the document but not yet apportioned.



# Debit notes

A debit note is the way back out of a purchase. It reduces what a vendor is
owed, reverses the input tax you claimed — and sends the goods back, when that is
what happened.

## The reason decides whether stock moves

This is the thing to get right. Five reasons, and **only one of them moves
stock**:

| Reason | Goods go back? |
|---|---|
| **Purchase return** | **Yes** — this is the only one |
| Price correction | No. The vendor billed the wrong price; the goods stayed. |
| Post-purchase discount | No. A discount agreed after the bill. |
| Short delivery or damage | No. Nothing physical goes back. |
| Cancellation | No. The bill is reversed in full. |

Moving stock for an overcharge would take away inventory you still have on the
shelf, so the reason is not a label — it changes what posting does. The form says
which kind you have chosen before you post.

## Against a bill, always

A debit note has to name the bill it corrects, and each line has to name the bill
line. Two reasons:

- **GST requires it** — a debit note against a filed bill has to say what it is
  correcting.
- **Returned stock has to find the cost layer it arrived on.** Valuing a return
  at today's weighted average rather than at what those particular units cost
  would move value into or out of the business that never existed.

Only **posted** bills can be corrected. A draft bill owes nothing yet — edit the
bill itself instead.

## Raising one

Purchase → **Debit notes → New debit note**.

Pick the vendor, then the bill. The bill's lines are copied in, each showing how
much is still returnable, and you key how much of each is going back. **Take
everything outstanding** fills them all in for a full return.

Prices come from the bill and cannot be changed — a credit has to match what was
charged. Tax is worked out by the server and reversed against the same rates the
bill claimed.

A line already returned in full is not offered at all: **the same goods cannot be
credited twice**, and returning more than was bought would claim the input tax
credit back twice over.

## What posting does

- **Accounts Payable is debited** — the vendor is owed less, against that
  vendor's own balance so aging stays correct.
- **Purchase Returns is credited.** This is a *contra* expense account: it
  reduces what you bought, and reports subtract it rather than adding a negative
  number.
- **Input GST is credited** — the claim is reversed. Credit cannot be kept on
  goods that went back.
- **Stock leaves**, if the reason is a purchase return, at what the layers it
  came from were carrying.

## Getting it wrong

**A posted purchase return cannot be voided.** The stock has already left, and
voiding cannot bring it back — if the vendor returns the goods, receive them
again on a new goods receipt.

A money-only note **can** be voided: withdrawing its entries puts the payable
back exactly as it was.



