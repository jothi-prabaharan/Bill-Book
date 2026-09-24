# Credit notes

**Sales › Credit notes**

The correction of a posted invoice. A credit note reduces what the customer owes, reverses the sale and its GST, and — when goods come back — puts them back into stock at what they cost.

## Where it sits

```
INV ──▶ CRN
invoice  credit note
```

A credit note always corrects **one** posted invoice, for the same customer. GST requires it: a credit note is filed against the invoice it reduces.

## Why it is raised

| Reason | What happens |
|---|---|
| **Sales return** | Goods come back. They go back into stock at their original cost, onto the same cost layers they left from |
| **Price correction** | The invoice was priced wrong. Nothing physical happens |
| **Discount after sale** | A discount agreed after the invoice was raised |
| **Deficiency or damage** | Short delivery, damage, a gesture. No stock comes back |
| **Cancellation of the invoice** | The invoice should not have existed and is reversed |

Only a sales return moves stock.

## Raising one

**Pick the customer by name** from the **Customer** field: search by code, name or GSTIN. Their open invoices are then listed below the form. Pick the invoice from that list, or type its number into **Invoice** and choose **Load invoice**. The note takes the invoice's customer, addresses and currency, and one line per invoice line:

- **for a sales return**, at the quantity still left to come back — what was sold, less what earlier credit notes already returned
- **for any other reason**, at the quantity invoiced, so the price can be corrected

Lower a quantity or a price to credit part of a line; remove a line to leave it out. **Every line must be one of the invoice's lines, for the same item** — a line added by hand is refused.

## What posting does

| | |
|---|---|
| Accounts | **Sales Returns** debited with the value returned, **Output GST** debited with the tax reversed at the invoice's rates, and **Accounts Receivable** credited with the total. Rounding goes to **Round Off** |
| The invoice | The note is **claimed against it**, so the invoice shows less outstanding |
| Stock | For a sales return, the goods **come back at their original cost**. The cost of sales is reversed by stock costing, at that same cost |
| GST return | The note is recorded for GSTR-1 against its original invoice |
| PDF | A PDF of the note, naming the invoice it is against, is filed for the record. `GET api/sales/credit-notes/{id}/pdf` downloads it |

Posting is refused, with the reason, when:

- the invoice is no longer posted, or is for another customer
- a sales return would bring back more than the invoice line has left
- the note would claim more than the invoice still has outstanding

A refusal leaves the note as a draft with nothing moved, so it can be fixed and posted again.

## Voiding one

The reason is required, always.

- **A draft** is simply withdrawn
- **A posted price correction, discount, deficiency or cancellation** is withdrawn from the accounts and the GST register, and its claim on the invoice is released
- **A posted sales return cannot be voided.** Its goods are back on the shelf; withdrawing the paperwork would leave them there with nothing to explain them. Sell them again on an invoice instead

## Permissions

Posting a credit note needs the **approve** permission for sales, and voiding one needs **void** — the same as every other sales document.

## What it does not do yet

- **The invoice is keyed by its number** when it is not one of the customer's open invoices listed on the form. The customer is picked by name
- **Refunding the customer** is a money-out document in Accounts, not part of the credit note
