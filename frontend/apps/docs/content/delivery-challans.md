# Delivery challans

**Sales › Delivery challans**

The document the goods actually leave on. A challan takes stock off the shelf, and, when it delivers against a sales order, records how much of that order has now gone out.

## Where it sits

```
QTE ──▶ SOR ──▶ DLC ──▶ INV ──▶ RCM
quote   order   challan  invoice  receipt
```

**Every arrow is optional.** A challan is for when the goods go out before the bill does: a delivery ahead of invoicing, goods sent on approval, material sent out for job work, a transfer to another branch, a sample. A shop that bills over the counter raises an invoice, which issues its own stock, and never touches this screen.

## What a delivery challan does and does not do

| | |
|---|---|
| Stock | **Issues it** when the challan is posted |
| Sales order | **Moves its quantities.** What was delivered goes up, what was held back goes down, and the order becomes *Partly delivered* or *Closed* |
| GST return | **Nothing.** A challan is not a supply. The invoice raised from it is what is filed |
| Numbering | A number is taken **when the challan is created** |

### The type of challan

| Type | What it is for |
|---|---|
| **Sale** | Goods going out to be invoiced |
| **Job work** | Material sent to someone to work on and send back |
| **On approval** | Goods the customer may keep or return |
| **Branch transfer** | Stock moving to another of your branches |
| **Sample** | Goods given out as a sample |

Only a sale is a supply. The others move stock and sell nothing.

## Against a sales order

Type the order's number into **Sales order** and choose **Load order**. The challan takes the order's customer, addresses and currency, and one line for every order line that still has something left to deliver, at the quantity left.

- **Lower a quantity to deliver part of a line**, and the rest stays on the order for a later challan
- **Remove a line to leave it for later** entirely
- **Every line must be one of the order's lines.** A line added by hand is refused on a challan against an order — raise it on a challan of its own
- **Only a confirmed order can be delivered against**, and only for the order's own customer

The checks run again when the challan is posted, not only when it is saved: another challan may have delivered against the same order in between. A challan that would deliver more than is still outstanding is refused with how much is left, and nothing is dispatched.

## Picking the customer and items

**The customer is picked by name.** Choose the **Customer** field, search by code, name or GSTIN, and pick from the matches. Their GSTIN fills the GSTIN field if it is still empty; one you have already typed is kept, because the GSTIN decides CGST and SGST against IGST. **Items are picked the same way** from the item column of the line grid: search by code or name, or scan a barcode, and the line shows the item's code and name. A document opened for editing shows its customer by name. Once it is posted or voided, neither picker opens.

## The statuses

| Status | What it means |
|---|---|
| **Draft** | Being keyed. It already has its number. Nothing has left the shelf |
| **Posted** | Dispatched. The stock is issued and the order updated. No longer editable |
| **Void** | Withdrawn before dispatch, with a reason. Keeps its number |

## Posting one

**Post** dispatches the challan. If Inventory cannot issue the goods — usually because there is not enough on hand — the challan stays a draft and nothing on the order moves.

A posted challan cannot be withdrawn. The goods have physically left, and taking the paperwork back would leave the stock gone with nothing to explain it. **Raise a return instead.**

## Voiding one

Only a draft can be voided, and **the reason is required**. A draft moved nothing, so voiding one moves nothing back — the order is exactly as it was.

## What it does not do yet

- **A sale challan does not yet post to *Goods Delivered Not Invoiced*.** The goods' cost reaches the accounts through stock costing, as it does for any other issue, and lands in cost of sales at dispatch. Holding it in a clearing account until the invoice is raised is designed and not yet built
- **The order is chosen by its number**, not from a lookup. The customer and the items are picked by name
- **The e-way bill is recorded, not generated.** Its number and date are stored on the challan
