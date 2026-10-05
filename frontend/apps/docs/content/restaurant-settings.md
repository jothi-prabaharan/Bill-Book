# Restaurant settings

**Settings › Restaurant** decides how a Restaurant branch runs: which restaurant screens it has, and what an order must carry before it can be saved or billed. Only branches whose business type is **Restaurant** have it.

A new Restaurant branch starts as **billing only**: every switch off and nothing required.

## How the restaurant runs

The four choices at the top set the two main switches for you:

| Choice | Tables | Kitchen tickets |
|---|---|---|
| **Billing only** | Off | Off |
| **Counter and kitchen** | Off | On |
| **Dine-in, no kitchen tickets** | On | Off |
| **Full service** | On | On |

A choice changes only those two switches and the requirements that depend on them. Recipes, the service charge and tips stay as they were. Turning tables on also requires a table for dine-in orders, unless you had already asked for one on every order.

## Screens

- **Tables and floors:** orders can be taken against a table. While it is off, the **Tables** screen is hidden and its API refuses with a message.
- **Kitchen tickets:** orders go to the kitchen on a ticket (KOT) and show on the **Kitchen display**. While it is off, the kitchen screen is hidden.
- **Recipes and wastage:** selling a dish uses up its ingredients. While it is off, **Recipes** and **Wastage** are hidden.

## What an order needs

- **A table:** never, for dine-in orders, or for every order. It can be set only while tables are on. With tables off nothing is demanded, whatever was saved earlier.
- **A customer:** never, for dine-in orders, or for every order.
- **Send to the kitchen before billing:** an order cannot be billed with items the kitchen has not been sent. It can be set only while kitchen tickets are on.
- **A waiter:** every order must name the waiter who took it.

## Billing

- **Service charge:** a percentage added to every bill before tax, from 0 (none) to 25%.
- **Accept tips:** a tip can be recorded on a bill. It is not taxed.

## Who can change it

Anyone with **Restaurant › View** can read the settings, because every restaurant screen follows them. Saving needs both **Restaurant › Edit** and **Settings › Edit**.
