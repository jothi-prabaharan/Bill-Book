# Menu

**Restaurant › Menu** is what a Restaurant branch sells: dishes in categories, with portions, choices, prices that differ by order type, and timed menus such as breakfast. Waiters order from it and the bill is priced from it.

A dish is an item from **Inventory › Items**: its name, tax rate, HSN code and unit come from there, and so does its price unless the menu says otherwise. Add the item first, then put it on the menu.

## Categories

The headings the menu is listed under. A new Restaurant branch starts with Starters, Soups, Main course, Breads, Rice, Desserts and Beverages; rename, add or switch them off under the **Categories** tab.

- An **inactive** category and its dishes are left off the order screen.
- A category with dishes in it cannot be deleted. Move its dishes first, or make it inactive.

## Dishes

Choose **Add a dish**, pick the item, and set:

- **Category**: where it is listed.
- **Timed menu**: leave as *All day*, or choose one to serve the dish only then.
- **Made at**: Kitchen, Bar or Counter. Kitchen tickets are split by this (see Kitchen display).
- **Available to order**: off while the dish is sold out.

### Sold out

The **On / Sold out** button in the dish list marks a dish sold out in one tap, from a phone too. A sold-out dish stays on the order screen, marked, so the waiter can tell the guest, but it cannot be added to an order. A choice can be sold out on its own (no more paneer) from the dish's choices.

### Portions

Leave **Portions** empty to sell the dish as one thing at the item's price. Add portions to sell it by size: **Half and full** adds the usual pair, the half at half the full price.

- Each portion has its own **price** and is ordered on its own. A dish with portions cannot be ordered without choosing one.
- The **portion factor** is how much of the full recipe the portion uses: 0.5 for a half. Recipes use it to work out the ingredients used.

### Prices by order type

Only where takeaway or delivery costs something different from dine-in. Add a price for the order type, and for a dish with portions, for the portion. Anything without one sells at its own price.

### Choices

Groups of options on a dish: **Spice level** (choose exactly 1), **Extras** (up to 3).

- **Choose at least** above 0 makes the group required: the order cannot be saved without it.
- **Choose at most** caps how many can be picked.
- Each choice can **add** to the price (extra cheese +₹30), or take off with a negative amount. The total cannot go below zero.

## Timed menus

Under the **Timed menus** tab, a timed menu names days and hours, such as *Breakfast, weekdays, 07:00 until 11:00*. Its dishes are on the order screen only then.

- The start is included and the end is not: a dish on 07:00–11:00 can be ordered at 07:00 but not at 11:00.
- An end before the start runs past midnight: *22:00 until 02:00* is a late menu, and the hours after midnight count as the evening it began. A Friday late menu is still on at 01:00 on Saturday.
- A timed menu switched **off** serves nothing until it is on again. Deleting one puts its dishes on all day.

## What the menu does not do yet

- Dishes are listed in the order they were added; there is no drag-to-reorder.
- A choice group belongs to one dish. A group used on many dishes is set up on each.
