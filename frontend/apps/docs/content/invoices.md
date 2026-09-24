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

**From an order** lists every confirmed sales order with something left to bill, and turns the one you pick into an invoice for what is left.

- **The lines are read from the order on the server, not sent by the screen.** An invoice that claimed to come from an order it did not match would leave the two disagreeing for the rest of their lives — and this is the document the department eventually reads
- **The tax is recomputed at the invoice's own date**, not copied from the order. An order taken in March and invoiced in June is charged at June's rates
- **Only a confirmed order can be invoiced.** An unconfirmed one is holding no stock, so invoicing it would issue goods nobody reserved
- **A due date is required, and the order has none to give.** An order's delivery date is when goods are expected, not when money is — so either set the due date or choose a payment term

An order can be billed on several invoices — the first for part of it, the next for the rest. Each invoice bills only what the order still has to bill, and when part of a line has already gone out on a delivery challan, the invoice bills that part without taking it out of stock a second time. See [Sales orders](sales-orders) for how the order keeps count.

## Due dates and overdue

An invoice needs a due date; a POS sale does not, because it is paid at the till.

The list has an **overdue** filter and shows how many days late each invoice is. Only a **posted** invoice can be overdue — a draft owes nothing yet, and a voided one never will — and that rule lives on the server, so the screen and a report cannot disagree about it.

The figure under the list is **the page's own total**, and says so. The list pages on the server, so a running total across every match is a different query; a number labelled "outstanding" that only covered one page is the kind somebody reconciles against and finds short.

## Printing

**Print** on an invoice lays it out with the branch's print template for invoices — the one marked
default in Settings › Print templates, or the one the invoice names — so changing the template
changes how every invoice prints. The page shows the document as it will print, and **Print**
sends that document alone to the browser's print dialog, where it can also be saved as a PDF.

**Only a posted invoice is a tax invoice.** A draft still prints, so it can be checked before it is
posted, but every page is stamped **PROFORMA**; a voided invoice is stamped **VOID**. The stamp is
added whatever the template holds, so a template cannot leave it off.

Printing needs the permission to print sales documents, which Owner, Administrator and Sales
have and Viewer does not.

Two things do not print yet: the amount in words, and the place of supply's state name — it prints
as the two-digit state code for now.

## POS sales

A till sale is an invoice with `POS` on it rather than `INV` — same table, same tax determination, same posting. It carries a till, a payment mode and the cash tendered, and it needs no due date.

**The till screen lives in the desktop app**, because a receipt is printed with ESC/POS commands straight to a USB or serial printer and a browser cannot reach one. Nothing else waits on it: the counter sale it replaces is an invoice raised directly.

### The cart

The till opens on an empty cart for the branch's **walk-in customer** — the contact whose code is `WALKIN`. If the branch has no such contact the till says so, and a customer has to be chosen for each sale.

- **Add item** searches items by name or code. Adding the same item again raises its quantity rather than starting a second line.
- Each line's **quantity** and **price** can be changed where they stand; a quantity of zero removes the line. The price starts at the item's sales price, or at its MRP when it has none — and an MRP always includes GST.
- **Change** picks another customer. A customer registered in a different state turns CGST and SGST into IGST on every line at once.
- The GST under each line and the totals beneath the cart are a **preview**, worked out exactly as the invoice form works them out. The server recalculates them when the sale is saved.

A taxable item with no current sales rate in the tax master shows a warning on its line and carries no GST until the rate is fixed.

**Tender, change and the till itself are not built yet**, so checkout does not finish a POS sale. Barcode scanning is not built either.

## Picking the customer and items

**The customer is picked by name.** Choose the **Customer** field, search by code, name or GSTIN, and pick from the matches. Their GSTIN fills the GSTIN field if it is still empty; one you have already typed is kept, because the GSTIN decides CGST and SGST against IGST. **Items are picked the same way** from the item column of the line grid: search by code or name, or scan a barcode, and the line shows the item's code and name. A document opened for editing shows its customer by name. Once it is posted or voided, neither picker opens.

## Finding one

The list pages on the server and can be filtered by status, searched by invoice number, and narrowed to overdue. At narrow widths the grid becomes one card per invoice and the filters stack.
