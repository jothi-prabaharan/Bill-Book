# Inventory Module

**Schema:** `inv`

## Overview
Handles stock levels, reservations, adjustments, and the physical count of inventory. Integrates with Accounting for inventory valuation (weighted average) and Sales/Purchase for stock movements.

## Task Checklist
- [x] **0.1 — Schema design:** `inv.*` tables for stock layers and movements.
- [x] **1.1 — Stock Reservations:** API to reserve stock for sales orders.
  `POST internal/stock/reserve` and `internal/stock/release`, guarded by the shared key with the tenant in the body. Reserve is all-or-nothing: every line's availability is checked before any line is taken, and a line that loses the race afterwards gives back what the call already took — reserving four lines and failing on the fifth would leave stock held by an order that never confirmed. Sales calls it on confirm; `SalesOrderServiceTests` proves the reservation is taken, recorded per line, and handed back on void.
- [~] **1.2 — Stock decrement:** Guarded release-then-issue in one transaction upon invoicing.
  **The order is right and the transaction is missing.** `internal/stock/issue` releases the line's reservation and then issues it, which is the correct sequence — issuing first would count the order's own reservation against it. But the two calls are not wrapped in a transaction, so a release that succeeds followed by an issue that fails leaves the stock released and not issued: available to somebody else while the invoice that wanted it failed.
  The fix is small and the machinery is already there — `StockService` joins an ambient transaction rather than opening its own, so a `BeginTransactionAsync` around the loop is enough. Tick this when the whole request is all-or-nothing, and `Posting_invoice_with_sales_order_releases_reservation` gains a sibling that fails the issue and asserts the reservation survives.
- [x] **9.1 — Stock Adjustments:** Header and lines with reasons and approval routing.
- [x] **9.2 — Physical Count:** Adjustments based on counted quantities.
- [ ] **TBD — Expiry and Batch Tracking:** Manage batch dates and serials during stock movements.
# Inventory.md — how stock actually moves

What happens to quantity, cost and the ledger when stock moves, step by step.

This is a **flow document, not a plan**. It has no checkboxes. [`TRANSACTIONS.md`](./TRANSACTIONS.md) and [`TRANSACTIONS-ACCOUNTING-BANKING.md`](./TRANSACTIONS-ACCOUNTING-BANKING.md) say what to build; this says how the thing behaves once built. [`SPEC.md`](./SPEC.md) holds the columns.

> **Note. Work on the designated branch and merge it into `main`. Never create a new branch.** See *Git — how work reaches main* in `CLAUDE.md`.

**Status: built.** Unlike the sales and purchase flows, almost everything below is running code — `StockService`, `CostingService`, `StockLedgerPoster`, `CostingEngine.Worker`. Where something is designed but not coded it says so explicitly.

---

## The pool

**One quantity and one running cost per item, per branch.** `inv.ItemStock` has `ItemId` as both key and foreign key, so a second row for the same item is structurally impossible.

`WarehouseId` is a **location dimension only**. It never partitions the pool, the cost layers or the weighted average. A transfer between warehouses writes two movements and changes the pool by nothing. Branches do not share stock at all — that is `OrgId`, and the query filter handles it without any code thinking about it.

Three quantities, and only two of them are stored:

| | |
|---|---|
| `QuantityOnHand` | stored. What is physically on the shelf |
| `QuantityReserved` | stored. Promised to a confirmed order, still on the shelf |
| **available** | **derived** — `QuantityOnHand - QuantityReserved`, computed in the projection |

Availability is derived rather than stored so it cannot disagree with the two numbers behind it. **Reserved is never subtracted from on hand**: the stock is there and worth what it cost, so valuation, stock counts and the Inventory account are all untouched by a reservation. Only availability moves.

`chk_stock_reserved` (`>= 0 AND <= QuantityOnHand`) is what stops the pair going incoherent when application code is wrong — a release that ran twice would otherwise drive the reserve negative and silently free stock nobody released.

---

## The movement

Every change is one row in `inv.StockMovements`. Eight types:

| Type | Direction | Opens a layer | Notes |
|---|---|---|---|
| `Opening` | in | yes | Migration and go-live |
| `Receipt` | in | yes | Goods in. **The only movement that changes weighted average cost** |
| `Issue` | out | no | Goods out against a sale |
| `Adjustment` | either | in only | Count correction, breakage, write-off |
| `TransferOut` / `TransferIn` | out / in | no | Paired. The pool is unchanged |
| `SalesReturn` | in | no¹ | Customer return coming back |
| `PurchaseReturn` | out | no | Stock going back to a vendor |

¹ A linked sales return goes back onto the layers it came from rather than opening a new one — see *Returns* below.

Direction is **stored alongside the type, not inferred from it**, because an adjustment runs either way and a reader summing a column should not have to know the rule.

Each row stores the quantity **as entered** and the **base quantity**, plus the conversion factor used. The factor is stored, not re-derived, so correcting a unit factor later cannot restate recorded history; a check constraint asserts the two quantities agree. That is what lets a receipt in bags and an issue in grams land on one stock figure.

**Idempotency key**: `(OrgId, SourceType, SourceId, SourceLineId)`, uniquely indexed. Every document writes through it, so a retried request cannot double-move stock.

---

## Step 1 — the quantity moves, inside the request

**Synchronous, and deliberately so.** `CLAUDE.md` requires the point-of-sale decrement to be synchronous or two tills oversell the last unit.

Going out, it is one statement:

```
UPDATE ItemStock
SET QuantityOnHand = QuantityOnHand - @qty
WHERE ItemId = @id AND QuantityOnHand - QuantityReserved >= @qty
```

expressed as `ExecuteUpdateAsync`, and **the row count is the answer**. Zero rows means no row, or not enough in it — either way nothing changed and the caller is refused. There is never a read followed by a write, so there is nothing to race against.

The guard is against **available**, not on hand. A sale is refused when the stock is there but promised to someone else.

Coming in, the same statement recomputes the weighted average in place, reading the pre-statement values:

```
newWac = (oldQty × oldWac + recvQty × recvCost) / (oldQty + recvQty)
```

Only a receipt moves the average. An issue takes `qtySold × currentWac` as its cost and leaves the average alone.

**Reserve and release** run the identical guarded update. Reserving more than is available and releasing more than was reserved both change no rows and are refused rather than overdrawing.

**Issuing reserved stock is release-then-issue in one transaction.** Issue first and the order's own reservation is counted against it, and the sale is refused for stock it is holding itself.

At the end of step 1 the movement row exists with `CostingStatus = Pending` and `LedgerStatus = Pending`, and the caller has its answer. Everything after this is asynchronous.

---

## Step 2 — the cost settles, on the worker

`CostingEngine.Worker` walks each organization, claims work and settles it.

**There is no message broker. `inv.StockMovements` is the queue.**

- **Ordering** comes from the read — `ORDER BY ItemId, MovementDate, StockMovementId`. That is a property of the query, not a promise from a broker, which is why replaying movements out of order still costs identically.
- **Exactly-once** comes from a guarded `Pending → InProgress` status claim whose row count is the answer. Two workers racing means one of them changes no rows. There is no redelivery to dedupe because there is no delivery.
- A crashed worker's claims are reclaimed after a timeout. A movement that keeps failing is parked as `Failed` with the reason on the row rather than retrying forever, because stock and the ledger disagree until someone looks at it.

Until a movement reaches `Costed`, **the screen says so rather than showing zero**. That is the whole reason the status column exists.

### Weighted average

No layers are consumed. The receipt already moved the average in step 1; the issue takes `qtySold × currentWac`. Layers are still opened on receipt and stand as receipt history.

### Everything else — cost layers

A receipt opens an `inv.CostLayer` at what it cost. An issue records `inv.CostLayerConsumption` rows naming which layers it drew from and how much from each, so the cost of a sale walks back to the purchases behind it. Selection is a single `ORDER BY` once the layers exist:

| Method | Order |
|---|---|
| FIFO | receipt date |
| LIFO | receipt date, reversed |
| FEFO | expiry, nulls last |
| Specific identification | straight off the serial's own layer |

Each layer is consumed by the same guarded conditional update, capped by its own remaining quantity, so nothing can draw more than a layer holds.

**The costing method is per item**, chosen on the item master and **frozen the moment stock first moves** — earlier postings were made under it, and changing it later would restate history silently.

---

## Step 3 — the ledger posting, on a second queue

Posting cannot run until the cost is settled, and **must not be able to roll costing back**: Accounting being briefly unreachable is not a reason to un-cost a sale. So `LedgerStatus` sits beside `CostingStatus` with its own guarded claim, its own bounded attempts and its own filtered index. The worker drains it right after costing in the same tick.

`StockLedgerMapping` decides what a movement means. It is pure and tested — 12 tests — because it is the piece that fails *silently*: a wrong guard refuses a sale and somebody rings up, a wrong account produces a balance sheet that still balances and a margin that is untrue.

| Movement | Debit | Credit |
|---|---|---|
| `Issue` | Cost of Goods Sold | Inventory |
| `SalesReturn` | Inventory | Cost of Goods Sold |
| `Opening`, unsourced `Receipt` | Inventory | Opening Balance Equity |
| `Adjustment` out | Cost of Goods Sold | Inventory |
| `Adjustment` in | Inventory | Cost of Goods Sold |

**Two deliberate absences, both of them the point:**

- **A transfer posts nothing.** The pool was never split by location, so there is nothing to move between accounts.
- **A receipt against a purchase document posts nothing here.** That document's other leg is Accounts Payable, and only Purchase knows the vendor and the tax; posting the stock half here as well would double the inventory asset. A receipt with *no* document is the business asserting stock it holds — that is an opening balance, and lands as one.

Both are marked `NotApplicable` rather than retried.

Adjustments go to Cost of Goods Sold rather than a shrinkage account of their own: stock that has gone without being sold still cost what it cost, and a separate account would put it outside the margin every report reads.

The posting goes through `POST internal/ledger/postings`, the one door into `acc.JournalLedger`. A posting is keyed by (transaction type, transaction, line, leg type) and **posting it again replaces those rows** — which is what makes a retry safe and what lets a restated cost correct itself.

---

## Backdated receipts, and why quantities never move

A receipt dated before issues that already consumed layers invalidates every allocation after it. Under FIFO that stock should have gone out first.

1. Every issue on or after the receipt's date is unwound — the quantity is returned to the layers it came from.
2. Those issues go **back in the queue** as ordinary pending work, so the replay is not a second code path.
3. They are replayed in date order against the layers as they now stand.

**Allocations are superseded, never deleted.** `CostLayerConsumption.SupersededAt` plus a batch id, with the unique index filtered to current rows so the replacement sits beside what it replaced.

**Quantities are untouched throughout. Only cost moves.** `inv.RecostingAdjustments` records each restatement — sale, previous cost, new cost, signed delta, and the receipt that triggered it — and the old figures are kept rather than overwritten.

A restated issue re-enters the **posting** queue as well, so its ledger rows are *replaced* at the new cost rather than corrected by a second entry. Nobody has to compose an adjusting journal.

---

## Returns

A sales return puts quantity back on the layers it came from **at their original cost, not today's**. `StockMovement.ReturnsStockMovementId` names the issue being reversed; the return reads that issue's allocations and gives them back oldest first, each guarded by its own ceiling so no layer can hold more than it received.

Partial returns accumulate and cannot exceed what went out. A return left unlinked falls back to the running average — refusing it outright would block a return whose original sale predates this feature.

Buy, sell, return leaves stock value exactly where it started. `StockPosition.LayeredStockValue` sums the layers rather than trusting the running average, which is the figure that has to come back.

---

## Batches, serials and expiry

Handled **inside the request**, not on the worker, because both are user input and belong in the answer to the caller rather than in a background failure.

- A batch-tracked item requires a batch number on the way in; an expiry-tracked one requires an expiry date when the lot is new. The lot carries its own MRP, which may differ from the item's.
- A serial-tracked item creates its pieces on the way in and names exactly which pieces left on the way out. The count must equal the quantity.
- FEFO orders by expiry with nulls last; specific identification reads the serial's own layer.

---

## What is not built

- **No document owns a stock adjustment.** An unsourced movement files itself under `STA` with its own movement id as the document. A sheet of lines with a reason and an approval is [T9](./TRANSACTIONS.md#stage-t9--stock-adjustment-as-a-document-sta).
- **Nothing calls reserve or release.** Both are built and guarded; the sales order that would call them is T2.4.
- **No document sources a movement yet.** `SourceType`/`SourceId` are honoured throughout and every caller today leaves them null, so every receipt currently lands as an opening balance.



