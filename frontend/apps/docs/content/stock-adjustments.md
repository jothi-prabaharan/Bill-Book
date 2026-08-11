# Stock adjustments

Correcting what you hold, as a document rather than one movement at a time.

**Inventory › Stock adjustments**

## Why a sheet and not a movement

Stock could already be corrected one movement at a time from the [stock screen](#/stock), and for a single breakage that is still the quickest thing to do.

A physical count is not that. Counting twenty items is **one event**: it happened on one date, for one reason, and one person authorised the whole of it. Recorded as twenty loose movements it keeps the quantities and loses every one of those facts — and the accounts show twenty unexplained adjustments where there was one count.

So a sheet is a document: it has a number, a date, a reason, and lines.

## Write-off or count

The two differ in what you type, not in what is posted.

| | You key | The system works out |
|---|---|---|
| **Write-off** | how much to remove or add back | nothing |
| **Physical count** | what was actually on the shelf | the difference against the books |

On a count, **lines that agree are dropped rather than posted as zero**. On a real count sheet most lines agree; that is the good news, not twenty rows of nothing.

A count also stores what the system believed **at the moment of counting**, beside what you counted. That is what lets the arithmetic be re-checked six months later — against the figure that was actually being disputed, rather than one that has moved since.

## Draft, then post

A draft holds **no number** and has moved **no stock**. Edit it, add to it, throw it away; nothing has happened.

Posting does both at once: the stock moves and the number is taken. The number comes from the `ADJ` series and is taken **at post, never at draft**, because a number taken when a form opens is a number lost every time somebody changes their mind — and a document series with a hole in it is what an auditor asks about.

**The whole sheet or none of it.** If any line cannot post — writing off more than is on hand, most often — nothing on the sheet posts, and it stays a draft with no number spent. A half-posted count is worse than one that did not post, because only the second is obvious.

## Who authorised it

There is no separate approver field, and that is deliberate: **whoever posted it is the approver**. The sheet already records who created it and who posted it, so a count keyed by one person and posted by another shows exactly that — which is the segregation of duties an adjustment needs. A third column repeating one of the first two would only be a second place to disagree.

## Reversing, because there is no void

A posted money document can be voided: its ledger rows are withdrawn and nothing else ever happened. **An adjustment cannot**, because the stock physically moved. Undoing it means moving the stock back, and the movement history is append-only — a mistake is corrected by a movement the other way, never by deleting the first.

So reversing writes a **mirror sheet**: every line the other way round, posted as its own document, dated today rather than back on the original's date. Both documents keep their numbers and each points at the other. The pair is a better record than a void would have left.

A sheet can only be reversed once. Stock coming back in returns at the running average — the reversal names no cost of its own, because it is putting back what the original took.

## What it does to the accounts

Each line records an ordinary [stock movement](#/stock) carrying the sheet's id, and the movement-to-ledger mapping files a movement under its document. So a twenty-line count produces twenty movements and **one adjustment in the general ledger**.

Stock written off debits Cost of Goods Sold and credits Inventory; stock found does the reverse. The value is settled by the costing engine a moment behind, so a sheet reads *Costing…* until it has.

## What is not here yet

- **No approval step before posting.** Anyone who may post, may post. Workflow approvals are a later phase.
- **Serial-tracked items** cannot be adjusted from a sheet, because serials are keyed per unit and the sheet has no place to key them. Use the stock screen for those.
