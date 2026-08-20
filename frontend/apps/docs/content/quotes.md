# Quotes

**Sales › Quotes**

A price offered to a customer, valid until a date. It is the first document in the sales chain and the only one that **posts nothing, reserves nothing and moves nothing**.

## Where it sits

```
QTE ──▶ SOR ──▶ DLC ──▶ INV ──▶ RCM
quote   order   challan  invoice  receipt
```

**Every arrow is optional.** A quote is for when the customer wants a price before committing to anything; a shop raising an invoice over the counter never touches this screen.

## What a quote does and does not do

| | |
|---|---|
| Accounts | **Nothing.** No entry reaches the ledger, ever |
| Stock | **Nothing.** Nothing is reserved and nothing moves |
| Numbering | A number is taken **when the quote is created**, not later |
| Tax | Determined and shown, at the rates in force on the quote's date |

Because it commits nothing, a quote is the cheapest way to put a price in front of somebody, and the safest — there is no state elsewhere in the system to unwind if it comes to nothing.

## It has its number from the start

A quote carries its document number from the moment it is created, while it is still a draft. That is deliberate: a quote exists to be sent to somebody, and they will read the number back over the phone.

The consequence is that **a quote is never deleted**. Abandoning one **withdraws** it, with a reason, and the row stays with its number. A gap in a document series is what an auditor asks about, and "we deleted it" is a worse answer than a withdrawn quote with a reason on it.

## Lapsing is not a status

Every quote has a **valid until** date, and it is required — a price with no end to it is a price offered forever.

When that date passes the quote is **lapsed**, and that is worked out from the date whenever the quote is read rather than written onto the row. Two reasons:

- A lapsed quote is **not** a withdrawn one. Nobody withdrew it; time passed.
- Nothing changes at midnight, so the answer is right at every moment rather than shortly after a nightly job runs.

A lapsed quote can still be read and reopened.

## The statuses

| Status | What it means |
|---|---|
| **Draft** | Being keyed. It already has its number |
| **Approved** | Checked and issued to the customer |
| **Withdrawn** | Taken back, with a reason. Keeps its number |

There is no "posted", because a quote never reaches the books.

## Keying one

The line grid is the same one every sales and purchase document uses, so it behaves identically here and on a bill.

- **Choose an item** and its price, unit and tax treatment come across onto the line as a **snapshot**. Repricing the item later does not restate a quote already sent.
- **Prices can include tax or exclude it**, per line. An MRP-inclusive price has the tax backed out of it rather than added on top.
- **A discount** can be keyed as a percentage or an amount. Whether it reduces the tax depends on your branch's *Discount reduces tax* setting.

### The totals are worked out twice, on purpose

The totals move as you type, because a grid that waited for the server before showing a line total would be unusable. **The figures that are saved are computed again on the server**, from the quantities, prices and discounts, against the GST rates in force on the quote's date.

That is not a redundancy to tidy away. A figure that ends up on a GST return cannot be one a browser worked out, and the two implementations are held to the same answers by a shared set of test cases — a disagreement between them is a failing test rather than a wrong return.

## Tax

Determined the same way it is everywhere else: your branch's state against the customer's place of supply, falling back to the state their GSTIN is registered in.

- Same state → **CGST + SGST**
- Different state → **IGST**

A GSTIN that contradicts the stated place of supply is **refused rather than guessed**. One of the two is wrong and there is no way to tell which; picking either files the tax under the wrong head, on a document that would otherwise print and total correctly.

## On a phone

The list becomes one card per quote and the form stacks to a single column at around 360px, which is the width this product is built to work at.

## What is not here yet

- **Converting a quote into a sales order from the quote screen.** The sales order can be raised directly in the meantime.
- **A PDF archived against the document** — printing is the browser's for now.
- **Emailing a quote to the customer.**
