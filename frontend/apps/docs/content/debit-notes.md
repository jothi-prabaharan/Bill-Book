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
