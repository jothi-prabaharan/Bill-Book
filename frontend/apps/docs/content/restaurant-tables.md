# Tables

**Restaurant › Tables** is the floor plan: every table on every floor, and where each is in its turn. It is there only while **Tables and floors** is on in [Restaurant settings](restaurant-settings). While it is off, the screen is hidden and its API refuses with a message.

## Setting up

Choose **Set up** (needs Restaurant › Create).

- **Add a floor** for each room or area: Ground floor, Terrace, AC hall. A new Restaurant branch starts with one, *Main floor*.
- **Add a table** with its number or code (stored in capitals, so `t1` becomes `T1`) and how many it seats.
- Tap a table while setting up to change it. **In use** off leaves it off the floor plan; a table can be switched off or deleted only while it is free and not joined to others.
- A floor with tables on it cannot be deleted. Switch it off instead, once no one is seated there.

Choose **Done** to go back to the floor plan.

## A table's turn

Each tile shows the table's status:

| Status | Means | Next |
|---|---|---|
| **Free** | Ready for guests | Seat guests, or clear it |
| **Occupied** | Guests are seated | Mark billed, or *Guests left* if they go without ordering |
| **Billed** | The bill is out | Clear table, *Reopen* for one more round, or free it |
| **Being cleaned** | Being cleared after guests left | Ready |

Tap a table to see what can happen next. A step that does not follow is refused: a free table cannot be billed.

## Bookings and the waiting list

The floor plan shows **Today's bookings** still to arrive across the top, and *Booked 20:00* on a table held later today. Tapping a free table offers to seat the party waiting longest that fits it. Booking tables and keeping the waiting list are on [Bookings](restaurant-bookings).

## Moving guests

**Move to another table** moves a seated party to a free table. The new table takes over the status, and any tables joined to the old one, and the old table becomes free. Their open order moves with them once orders are taken on the order screen.

## Joining and separating tables

**Join tables** pushes tables together for a large party. The table you start from is the **head** of the group; the others show *With T1*.

- A group is ordered and billed as its head table. A joined table's status follows the head, so change it on the head.
- A table already in a group cannot join another one. Separate it first.
- A group is occupied if anyone is seated at any of its tables.
- **Leave the group** takes one table out; **Separate the group** on the head breaks it up. Tables that leave are free.
- When the head table becomes free again, the group comes apart by itself.
