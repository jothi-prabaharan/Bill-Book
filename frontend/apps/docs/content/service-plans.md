# Service plans

Keep track of each customer's recurring service, such as an AC serviced every six months or a water purifier's filter changed every year, and remind them before it falls due. Service plans are under **Contacts › Service plans**, and the dashboard's **Services due** card shows what is coming up in the next two weeks.

## Setting up a plan

Choose **New plan** and give it:

- a **name** ("AC service") and the **customer**;
- how often it falls due: **every** 1 to 24 days, weeks, months or years;
- the **start date**, and optionally the **next service** date when it is not the start date;
- optionally an **end date**, after which the plan ends by itself;
- how many days **ahead** to remind the customer (7 by default).

## Recording a service

Open the plan, set **Service done on** and choose **Record service**. The next service is scheduled one interval on from that day. A plan whose next service would fall after its end date ends.

## Raising a visit

**Raise a visit** opens a support ticket for the customer, due within the branch's SLA for a medium-priority ticket. The plan shows the ticket and its status, and it raises no second visit while the first is still open. Tickets are listed under **Support › Tickets** for branches with CRM.

## Reminders

The background worker checks every hour.

- **Who:** customers whose next service falls within the plan's reminder days are emailed once for that service.
- **Late services:** a service up to a week late is still reminded; after that it is left to staff.
- **Who is skipped:** customers who unsubscribed, and customers with no email address.
- **Wording:** the email comes from the branch's **Service reminder** template, editable under email templates. It can use `{{name}}`, `{{plan}}` and `{{due_date}}`.

Reminders are email only.

## Pausing and ending

- **Pause** stops reminders and the Services due card.
- **Resume** carries on.
- **End** stops the plan for good.
