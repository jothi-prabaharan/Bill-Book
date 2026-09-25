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

**Download PDF** on a posted or voided invoice saves the PDF that was filed when it was posted.
That copy is kept for the record and is not redrawn later, so it has its own fixed layout rather than
the print template, and a voided invoice's copy is not stamped VOID. It is a plain PDF, not the
PDF/A archive format yet. A draft has no filed copy. The download needs the same permission as
printing.

Two things do not print yet: the amount in words, and the place of supply's state name — it prints
as the two-digit state code for now.

## E-invoicing

A branch whose turnover puts it under e-invoicing registers its **B2B, export and SEZ** invoices
and credit notes at the government's Invoice Registration Portal (IRP), which answers with an
**IRN** and a signed QR code for the printed invoice. Sales to consumers are never registered.

It is switched on per branch, on **Settings › Organization › Statutory**:

- **E-invoicing from** is the date the branch starts. Documents dated before it are not
  registered. Leave it empty if the branch does not e-invoice. The threshold is on the turnover of
  the whole business across all its GSTINs, which this product may not see all of, so the branch
  says whether it is in rather than the product guessing.
- **E-way bills** says whether the branch generates e-way bills from the product.

Both need the branch's GSTIN, and saving is refused without one. A change reaches invoicing within
six hours.

Before anything is sent, the invoice is checked for everything the IRP would refuse:

- both GSTINs are valid and match their states
- every line has a 6- or 8-digit HSN or SAC code and, for goods, a GST unit (UQC)
- both PIN codes are six digits and the place of supply is set
- the total matches the lines to the rupee

A problem is named in plain words, such as "Line 3 has no GST unit (UQC)", so it can be fixed.

Each line's GST unit is copied from its unit of measure when the invoice or credit note posts. This
is also the unit the sales register and GSTR-1's HSN summary report.

### Registering, and what happens when the IRP says no

**Posting** a B2B, export or SEZ invoice on an e-invoicing branch registers it at the IRP straight
after the posting is saved. Posting never waits on the IRP and never fails because of it: the
invoice is posted either way, and its **E-invoice** panel says where it stands.

- **IRN issued**: the panel shows the IRN and the acknowledgement. The printed invoice carries
  them and the signed QR code, even if the print template does not place them itself.
- **IRN pending**: the IRP could not be reached. It is tried again automatically: after a minute,
  then after 5 and 15 minutes, then every few hours. Until then the invoice prints stamped
  **IRN PENDING**, because without its IRN it is not a valid tax invoice.
- **IRN refused**: something about the invoice or the branch needs fixing, and the panel says what,
  such as "The PIN code of the customer must be six digits". Fix it and press **Retry now**. Retrying
  needs the **e-invoice** permission, which Owner, Administrator and Sales have. A refused invoice
  also prints stamped IRN PENDING and is listed in the error log for follow-up.

If a registration went through but its answer was lost, the retry fetches the IRN that was already
issued instead of failing on the duplicate.

The invoice list has an **E-invoice** column and an **E-invoice needs attention** filter, which
lists every invoice whose IRN is pending or refused.

### Voiding an e-invoice

**Voiding** a registered invoice cancels its IRN first. This is allowed for **24 hours** from the
acknowledgement. After that the void is refused, and the correction is a credit note. If the IRP
refuses the cancellation, the void is refused too and nothing changes. An invoice whose IRN was
never issued is simply voided, and its pending registration is dropped.

Credit notes against B2B, export and SEZ supplies are registered and cancelled the same way.

**The portal provider is still to be chosen.** Until it is, an installation cannot reach the real
IRP, and a branch that switches e-invoicing on sees its invoices refused with "No e-invoicing
provider is configured".

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

### Paying and posting a till sale

`POST api/sales/pos/sales` makes, pays and posts a till sale in one call. It needs the permission that posts an invoice, which the Sales role has. The request holds the lines and one or more **tenders**: cash, card or UPI. Each tender names the bank or cash account the money went into. `GET api/bank-accounts/tender-options` lists those accounts for the till, by name and kind only. It leaves out loan, overdraft and credit card accounts.

- **The tenders must pay the total.** Card and UPI together may pay at most the total. Cash pays the rest, and anything over it is handed back as **change**. A sale whose tenders fall short is refused, and nothing is saved.
- **A paid till sale owes nothing.** Posting debits each tender's bank or cash account, less the change from cash, rather than the customer's receivable. A till sale saved without tenders is posted as owed, like any invoice.
- **Stock is taken at once.** If another till sold the last unit a moment earlier, the sale is refused with the item named (409), and the whole sale is undone.
- The sale takes its number from the branch's `POS` series.

Until this change a till sale could not be posted at all. It debited an account called "Cash", which no chart of accounts has.

### The till screen

The desktop app's till is driven by the keyboard:

| Key | Does |
|---|---|
| **F2** | Add an item |
| **F3** | Change the customer |
| **F4** | Change the selected line's quantity |
| **F6** | Void the selected line |
| **F8** / **F7** | Hold the cart / recall the last held cart |
| **F9** | Tender |
| **F10** | Reprint the last sale's receipt |
| **↑ ↓** | Move the selection |
| **Esc** | Close the tender or the search |

- **A barcode scanner adds the item it reads.** A scanner types the code as a fast burst of keys ending in Enter, which the till tells apart from a person typing. The item whose code is exactly the scan is added, or the only item matching it. If several items match, the search opens on the scan rather than adding the wrong one.
- **Tender** opens with the whole amount ready in cash. Choose cash, card or UPI and the account the money goes into, add as many tenders as the payment needs, and **Complete sale**. The total is rounded to the rupee, as the posted invoice is, and change is shown before the sale is sent.
- **Held sales** stay on the till only until the app closes.
- **The till number** is set at the top of the screen and remembered on this computer.
- **Offline, the till refuses to sell.** The status at the top says so, and nothing is queued to send later. A sale is either posted, with its stock taken, or not made at all.

### The receipt

After each sale the till prints a receipt on a thermal receipt printer. It prints the branch's name, address, phone and GSTIN, the bill number and date, and each item with its quantity, rate and amount. It then prints the taxable value, GST split by component and rate, any round-off, the total, how the sale was paid, and the change. Every figure on it comes from the posted invoice.

- **Set up the printer once per till** under **Printer** at the top of the screen. Choose the paper width (80 mm or 58 mm) and how the printer is connected. A USB or serial printer is given by its device path, such as `/dev/usb/lp0` on Linux or `\\.\COM3` on Windows. A network printer is given by its address and port, usually 9100. **Test print** checks the connection before you save. You can also set the line printed at the bottom.
- **The sale stands if the printer fails.** If the printer is out of paper or unplugged, the sale is still posted and the till says the receipt did not print. Fix the printer and press **F10** to reprint. A reprint is marked *DUPLICATE*.
- **Receipts print only from the desktop app.** A browser cannot reach a receipt printer.
- **Names in Tamil or other scripts print as question marks.** Receipt printers print text only in their built-in character set.

## Picking the customer and items

**The customer is picked by name.** Choose the **Customer** field, search by code, name or GSTIN, and pick from the matches. Their GSTIN fills the GSTIN field if it is still empty; one you have already typed is kept, because the GSTIN decides CGST and SGST against IGST. **Items are picked the same way** from the item column of the line grid: search by code or name, or scan a barcode, and the line shows the item's code and name. A document opened for editing shows its customer by name. Once it is posted or voided, neither picker opens.

## Finding one

The list pages on the server and can be filtered by status, searched by invoice number, and narrowed to overdue. At narrow widths the grid becomes one card per invoice and the filters stack.
