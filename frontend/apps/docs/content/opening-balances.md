# Opening balances

**Accounting › Opening balances**

Where your books begin here: everything the branch owned, owed and was owed on the day it started using this product.

This is the one screen worth slowing down for. Every balance the product will ever report is measured from these figures, and an opening balance that is *wrong* balances just as neatly as one that is right — nothing downstream catches it, because everything downstream is measured from it.

## One per branch, and one way

A branch has one moment it started. There is no second opening balance, no reopening the first, and no reversing it: reversing an opening balance does not restate a transaction, it deletes the ground everything since stands on.

While it is a draft it is completely free — key it over weeks, save as often as you like, delete it and start again. Nothing posts and nothing is numbered until you press **Open the books**. After that the screen is a record of what was brought across, and an error is corrected the way any error in a closed period is: with a journal entry that leaves a trail.

## The figure to watch

Every line posts against an account called **Opening Balance Equity**. A bank balance is `Dr Bank / Cr Opening Balance Equity`; something you owe is `Dr Opening Balance Equity / Cr Accounts Payable`.

That means the books balance after every single line — and whatever Opening Balance Equity is left holding is **exactly the part of your position you have not keyed yet**. It is usually capital or retained earnings. When it reaches nothing, the migration is complete.

The panel at the foot shows it as you type, not when you press the button. Finding out at the end that the whole thing is out by ₹4,300 means auditing weeks of work; finding out as you key the line means fixing the line.

## The four kinds of line

| What | Names | Posts to |
|---|---|---|
| **Account balance** | An account | That account |
| **Owed to us** | A contact | That contact's balance under Accounts Receivable |
| **We owe** | A contact | That contact's balance under Accounts Payable |
| **Stock on hand** | An item, a quantity and a unit cost | Inventory, posted by Inventory itself |

Only the first kind picks an account. A receivable that could choose its own account is a receivable that can be filed under Sales Revenue.

### Receivables and payables go in one line per document

Not one line per contact, and certainly not one figure for the lot.

A customer owing ₹300,000 across invoices of thirty, ninety and a hundred and eighty days ages nothing like a single ₹300,000 balance — and an aging report is the first thing anyone opens after a migration. So each unpaid invoice or bill is its own line, carrying the **original document's number and date**. The number is what the contact will quote when they query it; the date is what the aging runs from.

> A contact with no ledger accounts cannot be brought across — the balance would land on the control account with nothing underneath it, which balances perfectly and leaves the contact owing nothing. Open the contact, use **Provision accounts**, then come back. The screen names the contacts this applies to.

### Stock carries a cost, not a value

An item line is a quantity and a **unit cost**, and there is nowhere to type a value — the value is the two multiplied.

That unit cost is the most consequential figure on this screen. It seeds the item's weighted average, so every cost of sale until the next purchase is computed from it. It is also why Inventory records these lines rather than Accounting posting a figure of its own: a value keyed here would be a second number against the Inventory account, free to disagree with the stock the items are actually carrying.

## Opening the books

**Open the books** is refused until:

- **Opening Balance Equity nets to zero.** Anything left is a piece of the position not yet keyed.
- **Every control account exists** — receivables, payables, and Opening Balance Equity itself.
- **Every contact named has ledger accounts**, so its balance lands on the contact rather than on the bare control account.
- **Inventory records the stock and reports back the same value this document says.** If the two disagree, nothing posts. A migration that ties everywhere except the one place nobody looks is the failure this check exists for.

Every reason it is refused is listed on the screen, not summarised.

If Inventory cannot be reached, or refuses a line, **nothing posts at all** and the whole thing can be retried. A branch that opened its books with two thirds of its stock is worse than one that did not open at all, because only the second is obvious.

## Afterwards

Draw a trial balance immediately. Debits and credits agree, Opening Balance Equity is nil, and every contact you brought across shows on its statement with the right age.

## What is not here yet

**Fixed assets.** There is no asset register to migrate into, so a migrated asset currently comes across as an account balance and carries no cost, life or depreciation schedule of its own. When the register lands, migrated assets skip historical depreciation — they arrive at written-down value and depreciate from go-live.
