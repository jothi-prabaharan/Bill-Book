# Recipes and wastage

Recipes say what each dish is made of, so billing a dish takes its ingredients out of stock and the menu shows what each dish costs to make. Wastage records food thrown away. Both are there only while **Recipes and wastage** is on in [Restaurant settings](restaurant-settings).

## Recipes

**Restaurant › Recipes** lists every dish on the menu with what a portion costs to make and its **food cost**: that cost as a share of the dish's price. Around 25–35% is usual; above 40% is shown in red.

Tap a dish to give it a recipe:

- **Makes (portions)**: how many full portions the ingredients below make. A curry cooked by the pot of ten is entered as the pot, with **10** here.
- **Ingredients**: each a stocked item from the item master, with a quantity in any unit it is measured in: grams of paneer bought by the kilogram is fine.
- **In use**: off, billing the dish uses no ingredients.

A portion's cost is worked out from each ingredient's current average cost, so it moves as purchase prices move.

## What billing uses

When a bill is made, its dishes' ingredients leave stock:

- each dish uses its recipe for its quantity: two full portions of a recipe that makes two use the whole recipe;
- a **half** portion uses half a portion's ingredients (its portion factor on the [menu](restaurant-menu));
- a choice that uses stock (extra cheese) uses its quantity for each dish it is on;
- a dish that is itself stocked, such as a bottled drink, leaves stock as an item on the invoice, and only its choices are counted here.

Each ingredient leaves stock once per bill and is costed like any sale, to cost of goods sold. If an ingredient is **short**, the bill is refused and says which. Receive the stock, or correct the count with a stock adjustment, then bill again.

A dish with no recipe uses no ingredients.

## Wastage

**Restaurant › Wastage** records food thrown away: the item, how much, in which unit, and why (spoiled, expired, burnt, dropped, made too much, returned by a guest, or other), with a note.

Each entry is a **stock adjustment** out of stock, so it is costed to cost of goods sold like any write-off and appears among the stock adjustments. Where the branch approves stock adjustments, the entry waits for approval before it leaves stock; its status says so.

The list shows each entry's cost at the item's current average cost.

## Who can do what

Recipes and wastage need **Restaurant › View** to see and **Restaurant › Create** or **Edit** to change. Picking ingredients and units reads the item master, so **Inventory › View** is needed too.
