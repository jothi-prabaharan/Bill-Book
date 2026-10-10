# Document lists

Every list of documents reads the same way (TK-187). That covers quotes, sales orders, invoices, delivery challans, credit notes, purchase orders, goods receipts, bills, debit notes, journal entries, spend money, receive money and transfers, and the chart of accounts. Learn one and you have learnt them all.

## The parts, top to bottom

- **The heading** names the list, then the document's code and how many records are in the chosen period: *Purchase orders · POR · 3 records*. The button on the right starts a new document.
- **The status tabs** are *All*, then every status the document can be in, each with its count: *Open ( 1 )*, *PartlyReceived ( 1 )*, *Cancelled ( 0 )*. A status nothing is in still has its tab and reads *( 0 )*. The counts are for the chosen period.
- **The period** comes first in the card: *This month*, *Last month*, *This financial year* or *Custom*. A list opens on the financial year. Under *Custom*, change **From** and **To** and press **Apply**. The list does not change until you do, and Apply is offered only when the dates make a range. The dates use your branch's date format.
- **The total** sits at the right of the period, in your branch's base currency: *Total in INR ₹5,66,000*. It covers what the list is showing: every matching row, not just the page you are on. On a narrow screen it moves to a line of its own.
- **The grid** fills the rest of the screen, however tall or wide the window is, and follows it when you resize. The grid scrolls inside the card, so the heading, tabs and period stay put.

## Reading a row

- **Number**: the document's code in a small box, then its number. *POR POR-000615*.
- **Date**: written with the month by name, *11 Aug 2026*.
- **Status**: a small tag, the same colour on every list. Still to do is orange, done is grey, and not yet real or no longer real is outlined.
- **Taxable** and **Total**: amounts in your branch's money format, with paise only when there are some: *₹4,00,000*, but *₹3,38,983.05*. The Total shows the document's currency after it. A document in another currency is written without the ₹.

A purchase order or sales order shows its **fulfilment** once it is confirmed: *Open*, *PartlyReceived* (or *PartlyDelivered*), *Closed* or *Cancelled*. Before that it shows *Draft*.

Click a row to open the document. The buttons in a row, such as **Allocate**, **Delete**, **Reverse** and **Void**, act without opening it.

Every row is one line. A list with more columns than fit scrolls sideways inside the card. On a phone each row becomes a card instead.

## Selecting

The box in front of each row selects it. **All**, above the grid, selects every row the list holds across every page. The box in the grid's heading selects the rows on this page only. The strip shows how many you have selected.

## Pages

At the foot, **Page size** chooses 10, 25, 50 or 100 rows a page, and the list remembers your choice. On the right are the rows on screen, *1 to 10 of 23*, and first, previous, next and last.

## Column menus

The ☰ in each column heading filters that column. The arrows beside the heading sort by it. **Clear**, in the strip above the grid, drops every filter and sort at once.

## Lists with filters of their own

**Invoices** keep their number search and their *Overdue only* and *E-invoice needs attention* boxes. **Sales orders** keep their number search. These sit in the card beside the period. Both lists page on the server, so their tab counts and total come from the server too, and cover every matching document.

**Chart of accounts** has tabs for *Active*, *Inactive* and *Locked* accounts. It shows each account's **Debit** or **Credit** balance for the chosen period: one side per account, the side its balance falls on, with *—* on the other. Click an account to edit it. **Deactivate** is in the editor.

## Not built yet

The design shows a 📎 count of attachments on a document. Sales, purchase and accounting documents cannot carry attachments yet, so no list shows one.
