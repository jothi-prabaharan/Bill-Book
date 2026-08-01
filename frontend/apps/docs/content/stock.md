# Stock

How much of each item you hold, what it cost, and everything that moved it.

**Inventory › Stock**

## One pool

**There is one quantity per item, across every branch and every warehouse.** Not one per location.

A warehouse records *where* a movement happened. It does not hold a balance of its own, and neither does a branch. Two shops selling the same SKU draw down one number.

This is a decision, not a simplification. Split the quantity per location and the same item ends up carrying two different weighted average costs, valuation stops agreeing with the ledger, and every report has to say which location it means before it can say anything else.

Per-location quantities, when they are needed, come from adding up the movements — not from a second balance that can drift from the first.

## Weighted average cost

One cost per item, company-wide, moved only by what you buy:

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

## What is not here yet

Costing methods other than weighted average are **selectable but not yet honoured**. An item set to FIFO, LIFO, FEFO or specific identification currently costs at weighted average, because nothing consumes cost layers yet. Batches and serial numbers are in the same position: the flags are stored and locked, and the tables behind them come with the costing engine.
