# Restaurant reports

**Restaurant › Reports** lists the restaurant reports. Each opens on the ordinary report screen, so it can be filtered, grouped, re-sorted and exported to Excel or CSV like any other report. They are also listed under **Restaurant** on **Reports**.

Every report takes a **From** and **To** date. Sales figures come from **posted sales invoices**, because a restaurant bill is a sales invoice ([Billing](restaurant-billing)). A draft or a voided invoice is not a sale. Money is in the branch's base currency.

## Day-end

One row per trading day:

- **Bills**: posted invoices that day, including any counter sale invoiced directly.
- **Covers**: guests on that day's billed orders.
- **Item sales**: the taxable value of the dishes and items sold.
- **Service charge**: the service charge on that day's bills.
- **Other lines**: anything else on the invoices, such as an account line.
- **Tax** and **Round off**.
- **Total**: the day's posted invoices. This is the figure that goes to the ledger.
- **Cash**, **Card** and **UPI**: what was taken at the counter. **On account** is the rest, left on customers' accounts.
- **Tips**: kept apart from sales, in Tips Payable.
- **Average bill** and **Per cover**.

The day-end's total for a day always equals the day's posted invoices. Credit notes and returns are not taken off; they are on the sales register.

## Item sales and category sales

**Item sales** lists each dish and item sold in the period, with its quantity, net sales, tax and average selling price. **Category sales** adds the same figures up by menu category. Both add up to the day-end's **Item sales**. Lines with no item, such as the service charge, are on the day-end only.

## Waiter-wise sales

One row per waiter: orders, covers and dishes they served that were billed in the period, the sales those dishes made at menu price, and the dishes cancelled on their orders. Orders taken with no waiter are on a row of their own. Sales here are at menu price, before the service charge and the invoice's tax split, so they do not match the day-end to the paisa.

## Table turnover

One row per table: how many times it was **turned** (billed orders opened at it), the covers it sat, covers per turn, **seat use** (covers as a share of seats across its turns) and what it billed. An active table that was never used is listed with no turns. This report is there only while **Tables** is on in [Restaurant settings](restaurant-settings).

## Food cost

One row per day: the day's food sales (item sales) against the **ingredients** its bills used ([Recipes](restaurant-recipes)) and the **wastage** written off, with the gross margin and **food cost %**. Costs are what the costing engine settled. Stock that is not costed yet counts as nothing, and **Not yet costed** says how many movements are still waiting. This report and the wastage report are there only while **Recipes and wastage** is on.

## Wastage

Every wastage entry in the period: the item, quantity, unit, reason, note and what it cost. An entry still waiting for approval, or for the costing engine, shows a cost of nothing and **Costed** is No.

## KOT cancellations

Every dish taken back after its kitchen ticket was sent: the date and time, KOT and order number, table, station, dish, quantity, its value at menu price, the reason, the waiter, and who cancelled it. A dish removed before it was sent never reached the kitchen and is not listed. This report is there only while **Kitchen (KOT)** is on.

## When a report is missing

A report whose switch is off in Restaurant settings is not listed, and Restaurant › Reports says which switch to turn on. Restaurant reports appear only on a Restaurant branch.

## Who can see them

The reports need **Reports › View** and **Restaurant › View**. Exporting needs **Reports › Export**.
