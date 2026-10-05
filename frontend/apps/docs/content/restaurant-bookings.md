# Bookings

**Restaurant › Bookings** holds tables for later and keeps a list of parties waiting now (TK-165). Bookings hold tables, so the screen is there only while **Tables and floors** is on in [Restaurant settings](restaurant-settings); while it is off, the screen is hidden and its API refuses with a message.

## Booking a table

Choose **New booking** (needs Restaurant › Create) and enter:

| Field | Notes |
|---|---|
| Guest's name | Required. |
| Phone | Required, to reach the guest. A local number is kept without its country code; a foreign one keeps its leading `+`. Spaces and dashes are taken out. |
| Guests | 1 to 500. A table that seats fewer is refused; choose a bigger one or join two first. |
| Coming at | The day and time, at the branch. |
| Hold the table for | Minutes from the time they are coming; 90 unless you change it, between 15 minutes and 12 hours. |
| Table | Optional. A booking can be taken now and given a table later, or when they arrive. |
| Note | Birthday, window seat. |

**A table cannot be booked twice for overlapping times.** A table booked for 8:00 pm for 90 minutes is held until 9:30 pm, so a booking for it at 8:30 pm is refused and says when the table is booked; one at 9:30 pm is fine, because a booking ending as another starts does not clash. Two hosts booking the same table at the same moment are checked one after the other, so only one of them gets it.

The day's bookings are listed earliest first, with each one's time, party, table and phone. Pick another **Day** to see that day's.

## When they arrive, or do not

- **Seat** opens an order on the booked table, or on the table you choose instead, with the party size as the number of guests and the guest's name on the order, and takes you to it. The booking then shows as seated, linked to its order.
- **Change** edits a booking still booked: another time, table or party size, checked for clashes the same way.
- **No-show** and **Cancel** free the booking's time on its table for someone else.

A booking that has been seated, cancelled or marked a no-show cannot be changed again.

## The waiting list

**Waiting now** lists parties who walked in when no table was free, longest waiting first, with how many minutes each has waited.

- **Add a party** with a name, how many, and a phone to call them on (optional).
- **Seat** offers the free tables big enough for the party, opens an order on the one you choose and takes you to it.
- **Left** takes a party off the list when they give up.

**A table that comes free takes the party waiting longest that fits it.** Tap a free table on the [floor plan](restaurant-tables) and it offers *Seat Meena (2) from the waiting list* when someone fits. A party too large for that table keeps its place and waits for a bigger one, rather than holding up the parties behind it.

## On the floor plan

While there are bookings still to come today, **Today's bookings** runs across the top of the floor plan: time, table, name and party size, earliest first, with a link to this screen. A table with a booking later today says *Booked 20:00* on its tile.
