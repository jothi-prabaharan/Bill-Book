# Orders

**Restaurant › Orders** is where waiters take orders, on a phone, a tablet or the counter screen. An order runs from the first dish to the bill: dishes are added round by round, and the bill turns it into an invoice (see Billing).

## The open orders

The list shows every open order with its table (or *Takeaway*, *Delivery*), waiter, number of dishes, how many have not gone to the kitchen, and the total before tax. Tap one to open it, or choose **New order**.

From **Tables**, tapping a table offers **Take an order** for a table with none, or **Open order** for a table that has one.

## Taking an order

1. Choose **Dine-in**, **Takeaway** or **Delivery**. The menu shows what is served now, priced for that order type.
2. Fill in what the order needs. A field the branch requires (see [Restaurant settings](restaurant-settings)) is marked with a star, and the order is not saved without it:
   - **Table**: only while tables are switched on. A takeaway or delivery needs one only when the branch asks for a table on every order.
   - **Waiter**: who is taking the order. It starts as you when you are on the branch's staff.
   - **Customer**: picked from the branch's contacts.
   - **Guests** and a **note** for the whole order, if wanted.
3. Tap dishes to add them. A dish with portions or choices asks for them first, with a quantity and a note for the kitchen (*no onion*). A sold-out dish is shown greyed out and cannot be added.
4. Choose **Save order**. Every dish is priced by the menu as it is saved, so the price is the menu's, not the screen's.

The order takes the next number from the branch's **Restaurant Order Number** series (`ORD/2627/00001`). Seating an order at a table marks the table occupied.

## Adding to an order

Open the order, tap more dishes and choose **Add to order**. A new round after the bill went out reopens the table.

- A dish not yet sent to the kitchen can be changed with **−** and **+**, or taken off with **−** at one.
- A dish the kitchen has can no longer be changed here. Cancel it on the kitchen ticket instead.

## Changing an order

- **Order type**: switching between dine-in, takeaway and delivery prices every dish again for the new type.
- **Waiter, customer, guests, note**: change them and choose **Save details**.
- **Move**: moves the order to a free table, and the old table is free. Moving a table on the floor plan moves its order too.
- **Joining tables** on the floor plan puts every joined table's order onto the head table's: the group has one order and one bill.

## Cancelling

**Cancel order** (needs Restaurant › Void) asks why. Dishes already in the kitchen are cancelled with the same reason, and the table is free once it has no other open order.

## Orders from Zomato and Swiggy

An order placed on Zomato or Swiggy can be posted to the branch's API and arrives as an ordinary **Delivery** order, so it is priced, sent to the kitchen and billed like any other.

`POST api/restaurant/aggregator-orders` (Restaurant › Create, usable by a person or by an API client) takes:

- `aggregator`: `Zomato` or `Swiggy`;
- `externalOrderId`: the aggregator's own order id;
- `lines`: the dishes, by **this branch's own item ids**. Give each aggregator your item id as its partner item id when you list the menu there, and the ids line up with no mapping to maintain;
- optionally the customer's `customerName`, `customerPhone` and `deliveryAddress`, the aggregator's `aggregatorTotal` and a `notes`.

What happens:

- **The order's note says where it came from**: *Zomato 4821 · Ravi · 98xxxxxx · 12 Lake Road*.
- **Sending the same order id again changes nothing.** It answers with the order made the first time and says it was already received, so a retry from the aggregator's side is safe. The same id from the *other* aggregator is a different order.
- **The branch's required-field rules do not apply.** Whether the branch asks for a customer or a waiter on a delivery, an aggregator order has neither.
- **With the kitchen switched on, the kitchen ticket is made at once**, one per station; print it from the kitchen screen as usual.
- **The dishes are priced by your menu, not by the aggregator.** The aggregator's total is kept only to compare later. A sold-out dish, or a menu that cannot be reached, refuses the whole order and saves nothing, so the aggregator can send it again.

`GET api/restaurant/aggregator-orders` lists what has come in, newest first, and can be filtered by `aggregator` and `date`.

This is order intake only. Sending your menu to the aggregators, passing status changes back and matching their payouts are not built.

## When something cannot be reached

Orders are priced by the menu and checked against the restaurant settings. If either cannot be reached, nothing is saved and the screen says to try again, rather than saving an order at a wrong price or against rules it could not check.
