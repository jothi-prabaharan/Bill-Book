# Recurring billing

Raise the same invoice or bill on a schedule: monthly rent, a yearly maintenance fee, a subscription. **Recurring billing is a free add-on**: any branch whose business type allows it can switch it on under Settings › Organization, with nothing to buy.

- **Sales › Recurring invoices** repeats a sales invoice.
- **Purchase › Recurring bills** repeats a purchase bill.

## Setting up a schedule

1. Raise the invoice or bill once, the ordinary way. It becomes the template.
2. Open **Recurring invoices** (or **Recurring bills**) and choose **New schedule**.
3. Choose the document to repeat, how often (weekly, monthly, quarterly, every six months or yearly, every 1 to 12 of those), and the start date.
4. Optionally set an end date, a number of occurrences to stop after, and **Post each new invoice** to post them as they are raised.

## What each occurrence copies

Each occurrence is a new document dated on the occurrence, with the template's customer or vendor, addresses, lines, notes, terms and custom fields.

- **Tax:** it is taxed at the rates in force on its own date, not the template's.
- **Due date:** it keeps the template's payment term. A template due 15 days after its date gives copies due 15 days after theirs.
- **Batches and order links** are not copied, because they belonged to the template.
- **Bills:** a new bill's vendor bill number is the schedule's prefix and the date (`REC-20261101`) until the vendor's real number is typed in. For that reason bills are left as drafts unless you choose to post them.
- **Month ends:** dates are counted from the start date, so a schedule starting on 31 January falls on 28 February and then on 31 March again.

## When occurrences are raised

The background worker checks every hour and raises whatever has fallen due. An occurrence is raised **once**: running again, or two workers running at the same moment, raises nothing twice.

- **Catching up:** if the worker was down, it catches up each missed occurrence on its own date, up to 12 per schedule per run.
- **Refused occurrences:** if an occurrence is refused (a credit limit, a missing rate), nothing is raised. The reason shows on the schedule, and the next run tries again.
- **The plan's monthly limit:** a Trial or Professional branch may create 1,000 Sales and 1,000 Purchase documents a month, and an occurrence counts as one. When the month is full the occurrence is **skipped**, not failed: nothing is raised, the schedule keeps its due date with the plan's sentence on it, and it is raised on the first run after a place is free, which is the first of next month. Elite has no limit.
- **Refused posting:** if posting is refused, the document stays a draft and the reason shows beside it under **Raised**.

## Pausing and ending

- **Pause** stops a schedule. **Resume** carries on from the next date after today. What fell due while it was paused is skipped, not raised all at once.
- **End** stops it for good. A schedule also ends by itself after its end date or its last occurrence.
- **Changing a schedule:** the template, start date and frequency cannot change once something has been raised, because they decide which dates the earlier documents were raised for. End the schedule and start a new one instead.
- **Turning the add-on off** stops every schedule without deleting it.

## Not supported yet

- **Foreign currency:** only documents in the branch's own currency can be repeated. An exchange rate is fixed on the document's date and never looked up later, so a copy cannot reuse the template's.
- **Retainer invoices:** these do not exist in the product yet, so they cannot be repeated (D-47).
