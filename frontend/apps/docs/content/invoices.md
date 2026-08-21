# Invoices

**Sales › Invoices**

The document the sale actually happens on. It is the first one in the chain that **reaches the books**, and the one a GST return is filed from.

## Where it sits

```
QTE ──▶ SOR ──▶ DLC ──▶ INV ──▶ RCM
quote   order   challan  invoice  receipt
```

**Every arrow is optional.** A shop selling over the counter raises an invoice directly and never touches the three documents before it.

## What an invoice does

| | |
|---|---|
| Accounts | **Posts a double entry.** `Dr Accounts Receivable / Cr Sales Revenue`, plus `Dr Cost of Goods Sold / Cr Inventory` |
| Stock | **Issues it.** The goods leave, and any reservation the sales order was holding is released in the same step |
| Numbering | A number is taken **when the invoice is created**, not when it is posted |
| Tax | Determined at the rates in force on the invoice's date, and recorded for GSTR-1 |

Two entries rather than one, and they are separate on purpose: the first records what the customer owes and what was earned, the second records what it cost to earn it. Gross profit exists only because revenue and cost of goods sold are different accounts.

## Posting is the irreversible step

Everything before it is a draft. Posting writes the ledger entry, issues the stock and freezes the document.

**Preview entry** shows exactly what the posting will write — which accounts, which direction, how much — before you commit to it. That preview is produced by the same code that does the posting, so it cannot drift out of step with the entry it predicts. If the legs do not balance the screen says so, and posting is refused.

Once posted:

- **The invoice is never edited.** Correct it with a credit note, or void it and raise another. Both stay on record
- **It can still be voided**, with a reason, which posts a reversing entry rather than deleting anything
- **A credit note against it blocks the void.** Undo the credit note first; voiding underneath it would leave the note pointing at something that was withdrawn

## From a sales order

**From an order** lists every sales order that has been confirmed and not already invoiced, and turns the one you pick into an invoice.

- **The lines are read from the order on the server, not sent by the screen.** An invoice that claimed to come from an order it did not match would leave the two disagreeing for the rest of their lives — and this is the document the department eventually reads
- **The tax is recomputed at the invoice's own date**, not copied from the order. An order taken in March and invoiced in June is charged at June's rates
- **Only a confirmed order can be invoiced.** An unconfirmed one is holding no stock, so invoicing it would issue goods nobody reserved
- **A due date is required, and the order has none to give.** An order's delivery date is when goods are expected, not when money is — so either set the due date or choose a payment term

One order becomes at most one invoice. Converting the same order twice is refused.

## Due dates and overdue

An invoice needs a due date; a POS sale does not, because it is paid at the till.

The list has an **overdue** filter and shows how many days late each invoice is. Only a **posted** invoice can be overdue — a draft owes nothing yet, and a voided one never will — and that rule lives on the server, so the screen and a report cannot disagree about it.

The figure under the list is **the page's own total**, and says so. The list pages on the server, so a running total across every match is a different query; a number labelled "outstanding" that only covered one page is the kind somebody reconciles against and finds short.

## POS sales

A till sale is an invoice with `POS` on it rather than `INV` — same table, same tax determination, same posting. It carries a till, a payment mode and the cash tendered, and it needs no due date.

**The till screen itself is Phase 3** and lives in the desktop app, because a receipt is printed with ESC/POS commands straight to a USB or serial printer and a browser cannot reach one. Nothing else waits on it: the counter sale it replaces is an invoice raised directly.

## Finding one

The list pages on the server and can be filtered by status, searched by invoice number, and narrowed to overdue. At narrow widths the grid becomes one card per invoice and the filters stack.

## What it does not do yet

- **The customer and the items are keyed by id**, not chosen from a lookup. The picker arrives with the item lookup endpoint
- **Partial invoicing** — billing four of ten on an order and leaving the rest open — is designed and not yet built
