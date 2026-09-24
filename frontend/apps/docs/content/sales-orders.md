# Sales orders

**Sales › Sales orders**

An order the customer has committed to but has not yet been delivered or invoiced. It is the only document in the sales chain that **reserves stock**, and the only one that reserves stock without selling it.

## Where it sits

```
QTE ──▶ SOR ──▶ DLC ──▶ INV ──▶ RCM
quote   order   challan  invoice  receipt
```

**Every arrow is optional.** An order is for when the customer has committed and the goods are not going out today — a made-to-order piece, stock coming in next week, a delivery scheduled for the end of the month. A shop selling over the counter raises an invoice and never touches this screen.

## What a sales order does and does not do

| | |
|---|---|
| Accounts | **Nothing.** No entry reaches the ledger, ever |
| Stock | **Reserves it.** The quantity stays in stock and in the valuation but stops being available to anyone else |
| Numbering | A number is taken **when the order is created**, not when it is confirmed |
| Tax | Determined and shown, at the rates in force on the order's date |

A promise is not a supply, so the double entry belongs to the invoice raised from the order. Recognising revenue here would book a sale on goods that have not left the building.

The reservation is the part that matters. Without it an order that is confirmed and not yet shipped leaves its stock fully available, and the last unit gets promised to two customers by two salespeople who were both looking at a correct screen.

## The statuses

| Status | What it means |
|---|---|
| **Draft** | Being keyed. It already has its number. Nothing is reserved |
| **Ready to confirm** | Checked and waiting for whoever confirms it. **Still editable** — the person reviewing it is usually the one who spots the error |
| **Confirmed** | The customer has committed and the stock is reserved. No longer editable |
| **Void** | Withdrawn, with a reason. Keeps its number, and gives back any reservation |

Beside the status, an order carries a **fulfilment status** of its own — Open, Partly delivered, Closed or Cancelled. It is a separate fact because an order closed short by agreement and an order delivered in full produce exactly the same arithmetic over the challans beneath them.

## Delivered and billed in parts

An order can go out in several deliveries and be billed on several invoices, and the two need not line up. Each line keeps three figures:

| | |
|---|---|
| **Delivered** | What has left — on a [delivery challan](delivery-challans), or on an invoice raised with nothing delivered before it |
| **Invoiced** | What has been billed on posted invoices |
| **Held** | What stock is still reserved for the line. Every delivery takes what it ships off it |

The fulfilment status follows **delivery**: *Partly delivered* after the first goods go out, *Closed* when every line is out in full. Beside it, a confirmed order shows **Fully invoiced** or **Invoice owed** — an order can be closed and still owe an invoice, when its goods went out on challans that have not been billed yet.

**An invoice against the order bills delivered goods first.** When part of a line has already gone out on a challan and not been billed, the invoice bills that part without taking it out of stock again, and issues only the rest. So it does not matter whether the invoice names the challan: the same goods never leave twice.

**Nothing is billed twice either.** An invoice that would bill more of a line than it has left is refused, with how much is left. Voiding a posted invoice gives its billing back to the order, but not its delivery — the goods did leave — so the next invoice bills them without issuing them again.

### Fulfilling an order

**Fulfil** on a confirmed order raises and posts an invoice for everything still unbilled — or for the lines and quantities you choose. It follows the rules above, so goods already delivered on a challan are billed and not issued again.

### Closing one short

**Close short** stops an order that will not be delivered in full, with a reason. Whatever stock the order is still holding is released. What was delivered stays delivered, and can still be billed — only that, since the rest was agreed never to go.

## Confirming one

**Confirm & reserve** asks Inventory to hold back every stock line on the order.

If there is not enough of something, the order is **not** confirmed — it stays a draft, and the message names the items that were short rather than saying "insufficient stock". A twenty-line order refused with a single sentence is a phone call to the customer with nothing to say.

Because the reservation is taken before the status moves, there is no state in which the screen says an order is committed and the shelf says its stock is free.

## From a quote

**From a quote** on a new order lists every quote that has been approved and not already converted, and turns the one you pick into an order.

Two things about the conversion are worth knowing:

- **The lines are read from the quote on the server, not sent by the screen.** An order that claimed to come from a quote it did not match would leave the two documents disagreeing for the rest of their lives.
- **The tax is recomputed at the order's own date**, not copied from the quote. A quote priced in March and converted in June is charged at June's rates, which is what actually has to be filed.

A quote becomes at most one order. Converting the same quote twice is refused, so a double-click cannot raise two.

## Voiding one

An order is never deleted. Its number was spent when it was created, so abandoning it is a **void with a reason**, and the row stays with its number — a gap in a document series is what an auditor asks about, and a withdrawn order with a reason on it is an answer.

- **The reason is required.** Always
- **An order with an invoice or a delivery challan against it cannot be voided.** Undo that first; voiding underneath it would leave the other document pointing at something that was withdrawn
- **Voiding a confirmed order gives its reservation back.** If Inventory cannot release it, the void is refused rather than recorded — a voided order still holding stock is stock nobody can sell and no document explains

## Picking the customer and items

**The customer is picked by name.** Choose the **Customer** field, search by code, name or GSTIN, and pick from the matches. Their GSTIN fills the GSTIN field if it is still empty; one you have already typed is kept, because the GSTIN decides CGST and SGST against IGST. **Items are picked the same way** from the item column of the line grid: search by code or name, or scan a barcode, and the line shows the item's code and name. A document opened for editing shows its customer by name. Once it is posted or voided, neither picker opens.

## Finding one

The list pages on the server, so a branch with ten years of orders opens as fast as a branch with ten. It can be filtered by status and searched by order number.

At narrow widths — a phone held upright — the grid becomes one card per order, the filters stack, and every action is a full-width target.
