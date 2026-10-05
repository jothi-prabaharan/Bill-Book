# Kitchen tickets

A **kitchen ticket** (KOT) tells a station what to make. Kitchen tickets are there only while **Kitchen tickets** is on in [Restaurant settings](restaurant-settings); while it is off, the kitchen display is hidden and the kitchen API refuses with a message.

## Sending an order to the kitchen

On the order screen, **Send to the kitchen** sends every dish the kitchen has not yet seen.

- Dishes are split by the station that makes them (set per dish on the [menu](restaurant-menu)): one ticket for the kitchen, one for the bar, one for the counter. Each prints at once.
- Each ticket takes the next number from the branch's **Kitchen Ticket Number** series (`KOT/2627/00001`).
- A dish is sent once. Adding more dishes later and sending again makes new tickets with only the new dishes.
- A sent dish can no longer be changed or taken off the order; cancel it instead (below).

## Printing

A ticket prints through the browser on the paper set in **Settings › Print templates › Kitchen ticket**, which starts as an 80 mm thermal roll: station and table large at the top, then each dish with its quantity, portion, choices and note. There are no prices on it.

- The page is sized to the paper exactly, so choose the kitchen printer in the browser's print dialog and leave scaling at 100%.
- **Reprint** prints a ticket again. Every copy after the first says **REPRINT** and its count, so the kitchen never makes a dish twice from a second copy.

## The kitchen display

**Restaurant › Kitchen display** shows every ticket still being worked, oldest first, at one station or all. It checks for new tickets every ten seconds.

- Each dish shows its quantity, portion, choices and the waiter's note.
- Cooks move a dish on: **Start** (preparing), **Ready**, **Served**. A drink can go straight to Ready. Nothing moves back.
- A ticket waiting 20 minutes or more is outlined in red.
- A ticket leaves the display once every dish on it is served or cancelled, or its order is billed or cancelled.

## Cancelling a dish

**Cancel** on a dish, from the kitchen display or the order screen, always asks why.

- A dish the kitchen has not started can be cancelled by anyone with **Restaurant › Void**.
- Once the kitchen has started it (preparing or ready), it needs **Restaurant › Approve** as well, because the food is already spent.
- A served dish cannot be cancelled here.
- A cancelled dish stays on the ticket and the order, struck through, with its reason, and is not billed.
- Cancelling the whole order cancels its dishes in the kitchen with the same reason.

## Billing and kitchen tickets

With **Send to the kitchen before billing** on in the settings, an order cannot be billed while any dish on it has not been sent.
