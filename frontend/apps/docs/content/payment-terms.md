# Payment terms

Credit terms — Net 30, Due on Receipt, End of Month. Set once, then picked on a contact and carried onto every document for them.

**Settings › Payment terms**

## The four rules

A term turns a document date into a due date. Which of the fields below matters depends on the rule you pick.

| Rule | Due date | Example, for a bill dated 18 August |
|---|---|---|
| Due on receipt | The document date | 18 August |
| A number of days after the invoice | Document date + days | Net 30 → 17 September |
| End of the invoice month | Last day of that month, plus any extra days | 31 August |
| A day of the following month | That day next month | Day 10 → 10 September |

**A day past the end of a short month falls on its last day.** Day 31 in February becomes the 28th, or the 29th in a leap year — it never rolls into March.

The list shows the due date each term would produce for a bill dated today. "Net 30" needs no explanation, but "end of month plus 15" does, and a worked example settles it faster than any label.

## Early-payment discounts

The classic "2/10 net 30" — 2% off if paid within 10 days, otherwise the full amount at 30 days — is two fields: **discount %** and **paid within (days)**.

The discount window cannot run past the due date. A discount earned after payment was already due could never be taken, so it is refused rather than saved as something that looks configured but never fires.

## Where a term can be used

Each term is marked as available on sales documents, purchase documents, or both. A term available on neither is refused — it would be data nothing could ever reference.

One term is the **default**, preselected on new contacts. Exactly one, enforced by the database.

## Built-in terms

Six are created with every organization: Due on Receipt (the default), Net 15, Net 30, Net 45, Net 60 and End of Month.

You can **rename** them and change where they are used. You cannot change their **rule** — contacts and unpaid documents already point at them, and moving Net 30 to Net 90 would silently restate due dates on invoices already issued. Add a new term instead.

## Deactivating

A term is never deleted. Contacts point at it, unpaid documents were dated by it, and the row is how a historical due date is explained. Deactivating takes it out of the pickers and leaves everything already using it alone.

## Where the calculation lives

Sales and Purchase do not each work out due dates themselves. They ask Accounting, which owns this master, over `GET /api/payment-terms/{id}/due-date`. Two implementations would eventually disagree about what End of Month means, and the disagreement would only surface as a customer arguing about a due date.
