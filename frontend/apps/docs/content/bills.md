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
