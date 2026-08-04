# Spend, receive & transfer money

**Banking › Spend money**, **Banking › Receive money** and **Banking › Transfer money**

Three screens for the three ways money moves without an invoice or a bill being raised: out, in, and between your own accounts.

## What each one is for

| Screen | What it records | Counterparty |
|---|---|---|
| **Spend money** | Money leaving — a bill paid, a deposit placed with a supplier, a customer refunded | A contact |
| **Receive money** | Money arriving — an invoice settled, an advance taken, a supplier refunding you | A contact |
| **Transfer money** | Money moving between your own accounts — a till banked, savings topped up | None |

Spend and receive are the same document read in opposite directions, so the two screens work identically. A transfer has no counterparty at all, which is why it is a simpler screen rather than a third option on the other one.

## A payment is not one thing

Money rarely means only one thing. Paying ₹11,000 against a ₹10,000 bill is a bill payment **and** a deposit with that supplier. So a payment is keyed as **lines**, each saying what that part of the money is for:

**On Spend money**

| What this is for | Where it lands |
|---|---|
| Bill payment | Settles what you owe on a supplier's bill |
| Advance to supplier | A deposit placed before the bill exists |
| Overpayment to supplier | The excess when the payment ran past the bill |
| Credit note refund to customer | Paying a customer back for goods they returned |
| Refund of customer's overpayment | Giving back what a customer paid over what they owed |
| Refund of customer's advance | Giving back an advance a customer placed |

**On Receive money**

| What this is for | Where it lands |
|---|---|
| Invoice payment | Settles what a customer owes |
| Advance from customer | Taken before the invoice exists |
| Overpayment from customer | The excess when the receipt ran past the invoice |
| Debit note refund from supplier | A supplier paying you back for goods you returned |
| Refund of our overpayment | A supplier returning what you overpaid |

The panel at the foot shows the amount that moved, how much of it the lines account for, and what is left over. **Unallocated money is not an error to be tidied away** — it is usually an overpayment, and it belongs on the document as a line of its own rather than nowhere. **Fill rest** puts whatever remains onto a line in one click.

## The account is chosen for you

There is no account picker on these screens, and that is deliberate.

Which account a line posts to follows from two things you have already said: **who** the contact is, and **what the line is for**. A bill payment lands on that contact's balance under **Accounts Payable**; a deposit placed with them lands on their prepayment balance under **Accounts Receivable**. The screen shows the account it will use, beneath the line, so nothing is hidden — but it cannot be overridden, because the server resolves the same account from the same rule and a picker that disagreed with it would post to one account while the document said another.

This is also why the contact comes first. Until one is chosen there is no balance to point a line at, and a payment against the bare control account is exactly the posting that makes a contact statement stop tying to the books.

> If a contact shows **no ledger accounts**, its balances were never set up — open the contact and use **Provision accounts**, then come back.

**Advances read the opposite way round to how they sound.** A deposit *paid* to a supplier sits under **Accounts Receivable**, because they now owe you goods; an advance *taken* from a customer sits under **Accounts Payable**, because you owe them. Balances are grouped by the direction they run, not by who the counterparty is.

## What it settles

A line that settles a document names it — a bill number, an invoice number. The document *kind* is not something you choose beside the line: a bill payment can only settle a bill, and an advance settles nothing, so changing what a line is for changes what it can point at.

When the whole payment is about one document, the document is recorded on the payment as a whole as well as on the line. Split across several, only the lines can say, so only the lines do.

## Draft, posted, void

The same three states as a journal entry, and the same one-way street.

- **Draft** — free. Its lines need not add up, it holds no number, and it touches no ledger. Delete it and nothing is left behind.
- **Posted** — in the ledger, with a number. Immutable from here.
- **Void** — posted and then withdrawn. The ledger rows go; **the document and its number stay**, because a gap in a document series is what an auditor asks about.

A posted document is never edited. If it is wrong, void it and key it again.

A number is taken at **post**, not at save, so drafts you abandon leave the sequence unbroken.

## Closed periods

If the books are closed to you for that date, posting is refused and the screen says so. Voiding is refused too — a void changes what the ledger says about that date just as much as a post does. Dating the document after the lock, or having the lock moved, is what unblocks it.

## Transfers

Both ends are your own accounts, so there is no contact, nothing to allocate, and no counterparty balance involved: the transfer debits one account and credits the other and stops there.

The two ends must be different accounts. **⇅** swaps them, which is the correction most often needed.

## Foreign currency

Settle a foreign-currency bill or invoice and the rate has usually moved since it was raised. The balance is cleared **at the rate it was raised at**, the bank moves at the rate you are paying at, and the difference between the two is recorded as a realized gain or loss in **Realized FX Gain/Loss**.

That is not a refinement — it is what makes the document actually clear. Relieve the balance at today's rate instead and the books still balance while the contact keeps a leftover balance against a bill they have paid in full, which nobody would think to look for.

A USD 1,000 bill raised when the rate was ₹80, paid when it is ₹100:

| | Debit | Credit |
|---|---|---|
| Accounts Payable — the supplier | ₹80,000 | |
| Realized FX Gain/Loss | ₹20,000 | |
| Bank | | ₹100,000 |

A receipt works the same way with the sides reversed, so a rate that moved in your favour lands as a gain.

Two things are refused rather than guessed:

- **Paying a document raised in a different currency from the payment.** Converting twice in one settlement means inventing a cross-rate, and no rate on record produces the answer.
- **A rate that cannot be read.** If the rate the document was raised at cannot be established, the payment is not posted — "could not check" is not the same as "no difference", and treating it as such is how the leftover balance above gets created silently.

Nothing here ever looks a rate up live. Every rate used was recorded at the time, on the document it belongs to.

## What is not here yet

- **Allocation limits.** Nothing yet checks that a payment against a bill does not exceed what that bill actually owes.
