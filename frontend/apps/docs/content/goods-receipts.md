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
