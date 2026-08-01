# Stock

How much of each item you hold, what it cost, and everything that moved it.

**Inventory › Stock**

## One pool per branch

**There is one quantity per item within a branch, shared across every warehouse in it.** Not one per warehouse.

A warehouse records *where* a movement happened. It does not hold a balance of its own. Two counters in the same shop draw down one number.

This is a decision, not a simplification. Split the quantity per warehouse and the same item ends up carrying two different weighted average costs, valuation stops agreeing with the ledger, and every report has to say which location it means before it can say anything else.

Per-warehouse quantities, when they are needed, come from adding up the movements — not from a second balance that can drift from the first.

**Branches do not share stock.** A branch is a separate set of books with its own items and its own quantities, so there is nothing to reconcile between them — the organization boundary already keeps them apart, and no code has to remember to.

## Weighted average cost

One cost per item in the branch, moved only by what you buy:

```
newAverage = (oldQty × oldAverage + receivedQty × receivedCost) ÷ (oldQty + receivedQty)
```

Everything else leaves it alone:

| Movement | Quantity | Average cost |
|---|---|---|
| Opening balance | + | sets it |
| Receipt | + | recalculated |
| Issue (a sale) | − | unchanged |
| Sales return | + | unchanged — it comes back at what it left at |
| Purchase return | − | unchanged |
| Adjustment | ± | unchanged — a count is not a purchase |
| Transfer in / out | no change | unchanged |

An issue moves quantity only. That is what makes gross profit meaningful: the cost of what was sold is settled when it was bought, not when it was sold.

Anything that brings stock in and sets the cost — an opening balance or a receipt — **requires a unit cost**. Receiving without one would drag the average toward zero and quietly understate the cost of every sale after it.

## Entering quantities in any unit

Every movement stores the quantity **twice**: as you entered it, in the unit you entered it in, and again converted into the item's inventory unit.

Receive two 50 kg bags, issue 300 grams, and stock reads as one figure — 99.7 kg — because both were converted through the item's unit type on the way in. The **conversion factor is stored on the movement**, not looked up later. If a unit's factor is ever corrected, movements already recorded keep the factor they were written under; re-deriving it would silently restate history.

The unit you enter in has to belong to the item's unit type. Anything else has no factor to convert through, and is refused rather than guessed at.

## Selling the last unit

Stock comes down through a single conditional statement:

```sql
UPDATE ItemStock SET QuantityOnHand = QuantityOnHand - @qty
WHERE ItemId = @id AND QuantityOnHand >= @qty
```

If it changes no rows, there was not enough and **nothing** changed. There is no read followed by a write, because that is the gap where two tills both see the last unit and both sell it.

The decrement is also **synchronous**. Costing, accounting and notifications all happen afterwards and can be retried; the quantity cannot wait, because by the time a queued message is processed the second customer has already been served.

## The movement history

Append-only. Rows are never edited or deleted — a mistake is corrected by a movement in the opposite direction, the same way a posted journal is reversed rather than changed.

Each row keeps the average cost as it stood immediately afterwards, so a disagreement about valuation can be walked back through the receipts that caused it.

Movements that come from a document carry its type and id. That pair is **unique**, which is what stops a redelivered message moving stock twice — the message bus guarantees at-least-once delivery, so a duplicate has to be refused by the database rather than trusted not to arrive.

## Transfers

Warehouse to warehouse writes two movements — one out, one in — and changes the pool by nothing at all, because the pool was never split.

It is still refused when the source warehouse holds less than the quantity: a location cannot ship what it does not have, even though the company total is unaffected.

## What freezes once stock moves

The moment an item has a single movement, five things on it are fixed:

- Unit type
- Inventory unit
- Costing method
- Item profile
- Batch, expiry and serial tracking

Every quantity and cost recorded so far was written under them. Changing the inventory unit from kilos to grams would not reinterpret the history — it would corrupt it, silently, by a factor of a thousand.

Everything else on the item — name, prices, category, reorder levels — stays editable.

## Batches

A **lot** of one item received together: same run, same expiry, usually the same printed MRP.

Receiving against a batch-tracked item asks for the batch number. An existing lot is reused; a new one is created. Expiry-tracked items must give an expiry on a new lot — without it nothing can decide which lot goes out first.

A batch carries its **own MRP**, which wins over the item's. A price rise reprints the pack, and the older lot has to keep selling at what is printed on it.

## Serial numbers

For items tracked piece by piece. Enter one serial per unit — the count has to match the quantity exactly, or pieces go untracked and the count stops agreeing with the stock.

Jewellery pieces carry a **HUID** alongside the serial: the six-character BIS hallmark id. It sits on the piece, never on the item, because two rings of the same design carry two different HUIDs. Each is unique across the branch.

On the way out, the serials you name are the pieces that left — and on a specific-identification item, they are also what decides the cost.

## Cost layers

Every receipt writes a **layer**: how much came in, what it cost, and how much of it is left. What differs by costing method is what draws them down.

| Method | Takes from |
|---|---|
| Weighted average | Nothing — one running average, layers kept as history |
| FIFO | The oldest receipt first |
| LIFO | The newest receipt first |
| FEFO | Whatever expires soonest; no expiry sorts last |
| Specific identification | The layer the named piece arrived on |

An issue records **which layers it consumed, and how much from each**. An issue of 30 against three layers writes three rows, and their costs sum to its cost of goods sold. The **Layers** button on the movement history shows exactly that.

This is what makes a disputed margin answerable: the cost of a sale can be walked back to the purchases it came from, receipt by receipt.

Layers come down the same way stock does — a guarded statement, never a read followed by a write, so two sales cannot take the same last unit of a layer.

Costing runs **inside the same transaction** as the movement. Layers and stock have to agree exactly, and committing them together is the only way to guarantee it.

## What is not here yet

- **Backdated receipts do not restate anything.** A receipt dated before issues that already consumed layers should unwind those allocations, replay them and post a COGS adjustment. Today it creates a layer at the back of the queue and leaves the earlier issues costed as they were.
- **Returns do not go back to their originating layer.** A sales return adds quantity at the running average rather than at the cost the piece left on, so buy-sell-return does not yet land stock value exactly where it started.
- **Nothing posts to the ledger.** An issue computes its cost of goods sold and stops there — `Dr COGS / Cr Inventory` is Accounting's to write, and the two are not yet connected.
