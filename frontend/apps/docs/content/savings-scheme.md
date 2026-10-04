# Savings schemes

A jewellery savings scheme lets a customer pay a fixed amount every month for a set number of months, then spend the total, plus your bonus, on jewellery. **Savings schemes are a paid add-on for Jewellery branches**, under **Sales › Savings schemes**.

## Setting up a scheme

On the **Schemes** tab choose **New scheme** and give it:

- a **code** and a **name** (`GOLD11`, "Gold 11 + 1");
- the **monthly instalment** and the number of **months** (1 to 60);
- the **bonus**, one of:
  - **Free instalments**: "pay 11, the 12th is on us" is 1 free instalment.
  - **Fixed amount.**
  - **Percentage** of everything paid in.
  - **No bonus.**
- **Grace days**: how late an instalment may be before the enrolment lapses (30 by default).

The form shows the bonus a customer would earn. A change to a scheme applies only to customers who join afterwards; everyone already enrolled keeps the terms they joined on.

## Enrolling a customer

On the **Customers** tab choose **Enrol a customer**, pick the scheme and the customer, and set the date of the first instalment. Every instalment is laid out a month apart from that date. The customer must already be saved as a customer in this branch.

## Taking an instalment

Open the enrolment and choose **Take instalment**, with the account the money went into and how it was paid.

- Each instalment is a **receipt to the customer's advance**: it appears under **Receive money** and in the customer's ledger like any other advance.
- Instalments are taken in order, one at a time.
- Paying the last one **matures** the enrolment.

## Redeeming on an invoice

Raise and post the customer's invoice as usual, then open the enrolment and choose **Redeem on an invoice**.

- **Bonus first:** a matured enrolment puts the bonus on the invoice first, then what was paid, never more than the invoice still owes.
- **Paid money left over** stays on the customer's advance, to spend on another invoice or to refund.
- **Bonus left over** beyond the invoice is given up.
- **Not yet matured:** an enrolment can only be redeemed by ticking **Close early and give up the bonus**.

## Lapses

The background worker checks twice a day.

- **Lapsing:** an enrolment whose next instalment is more than its grace days late **lapses**. It takes no more instalments and earns no bonus. What was paid is still the customer's, to redeem on an invoice or to refund.
- **Reminders:** customers with an email address get a reminder three days before an instalment is due, and one when it goes late. Each is sent once per instalment.

## Refunds

**Refund** returns what was paid and not yet spent, from the account you choose. The bonus is never refunded, and nothing is deducted. If spend money needs approval in your branch, refund it through **Spend money** instead.

## How it is accounted for

| Event | Posting |
|---|---|
| Instalment | Dr bank or cash, Cr the customer's advance (prepayment) |
| Redemption | The instalment receipts are allocated to the invoice, as the Allocate dialog does |
| Bonus | Dr **Savings Scheme Bonus** (expense), Cr the customer's receivable, allocated to the invoice |
| Refund | Dr the customer's advance, Cr bank or cash |

The invoice is at its full value, so GST is charged on everything the customer takes home.
