# Billing

**Restaurant › Billing** turns an order into a bill. A restaurant bill is an ordinary **sales invoice**: it is numbered, taxed, posted to the accounts and printed exactly like any other invoice, through the branch's invoice print template.

Open an order and choose **Bill**, or open Billing and add an open order.

## What goes on the bill

- **Every dish** that has not been billed or cancelled is ticked. Untick dishes to **split the bill**: only the ticked dishes are billed, and the rest stay on the order for the next bill. Each dish is billed once.
- **Bill another order with these** adds another open order to the same bill, to **merge bills**: one invoice for several tables or orders.
- **Customer**: the order's customer, or the branch's **walk-in customer** when there is none. Choose a customer to bill to someone else.

## Service charge

With a service charge set in [Restaurant settings](restaurant-settings), the bill carries a **Service charge** line at that percentage of the dishes, before tax. Untick it to waive it on this bill.

GST treats a restaurant's service charge as part of what it is charged on, so it is taxed at the dishes' own rate. When dishes carry different rates (5% food, 18% drinks), the service charge is split into one line per rate.

## Tips

With **Accept tips** on, enter a **tip** and the account it was paid into. A tip is the staff's money, not the branch's income, so it is **not on the invoice**: it is posted on its own, to the cash or bank account it came into and to **Tips Payable** in the chart of accounts. The total to pay shows the bill and the tip together.

## Payment

Add how the bill was paid: **Cash**, **Card** or **UPI**, each with its amount and the account it went into. A bill can be paid several ways.

- Card and UPI can pay at most the bill. Only cash gives **change**, shown before you bill.
- With no payment, the bill goes on the customer's account to be paid later. A bill to the walk-in customer must be paid.

## Billing

The totals are worked out with the invoice's own tax calculator before you bill. **Bill and print** makes and posts the invoice, records the payments, posts the tip, and prints the bill.

- An order whose every dish is billed or cancelled becomes **Billed**, and its table shows **Billed** on the floor plan until it is cleared.
- What the branch requires is checked again: a table, a customer and a waiter where the settings ask for them, and, with **Send to the kitchen before billing** on, every dish sent to the kitchen. What is missing is shown above the button.

## Loyalty points

**A bill to a named customer earns loyalty points** (TK-166), once **Loyalty points per ₹100** is set in [Restaurant settings](restaurant-settings). Points are whole: a ₹250 bill at 1 point per ₹100 earns 2. A bill to the walk-in customer earns none, because it names nobody.

- Choosing the customer shows their **balance**, and what this bill will earn.
- After billing, the bill says what was earned and the customer's new balance. The points are recorded once: billing is never counted twice, whatever is retried.
- **Voiding the bill's invoice takes its points back**, once, and the customer's history shows both the earning and the reversal.
- If the points could not be recorded at the moment of billing, the bill still stands and says so. Choose **Record the points** to record them; it is safe to press again.

**Spending points against a bill is not built yet.** Whether a redeemed point is a discount on the bill or a payment towards it changes how the bill posts, and that is for the owner to decide (D-52).

## Not supported yet

- **Alcohol.** Liquor is outside GST and is billed under state excise, often on a separate bill. Until the owner decides how (D-41), keep alcohol off the restaurant menu.
- A bill cannot be split by guest within one dish (two people sharing one plate): split by dish instead.
