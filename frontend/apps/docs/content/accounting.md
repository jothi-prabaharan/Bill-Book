# Chart of accounts

**Status: built.** The first per-customer feature — everything before it lived in the shared master database.

## Two levels, not three

`mst.AccountTypes` (5 fixed) → `acc.Accounts`. Sub-types were removed; `ParentAccountId` supplies any display grouping they used to give, and a parent must share its child's account type so the tree cannot cross report sections.

Because sub-types held the contra flag, **`IsContra` now lives on the account**. Set it where the normal balance runs opposite the type — accumulated depreciation, sales returns, discount given, purchase returns. Reports subtract those; miss it and the report overstates silently.

`AccountTypeId` points at the master database, so no foreign key is possible. It is validated in C#.

## Usage flags

Which pickers an account appears in:

| Flag | Meaning |
|---|---|
| `IsSales` | Selectable on sales documents |
| `IsPurchase` | Selectable on purchase documents |
| `IsPayment` | Selectable as the settlement account on a payment |
| `IsBank` | Is a bank or cash account — bank pickers, reconciliation, transfers |
| `IsContra` | Reports subtract it |
| `IsJE` | Selectable on a manual journal line — **backend-only, never shown in the UI** |
| `IsLock` | Posting freeze. Operational and reversible, unlike the config lock |

`IsJE` is off for the seeded control accounts (AR, AP, Inventory, GST) on purpose: the system posts to those from documents, and a hand-written journal straight to Accounts Receivable would break its tie to the per-contact sub-ledger.

## The configuration lock

An account is **config-locked** when it has been used (`IsUsed`) or is a seeded system account. Locked means these are frozen:

`AccountTypeId` · `AccountCode` · `IsContra` · `IsSales` · `IsPurchase` · `IsPayment` · `IsBank`

Still editable: **display name, active state, posting lock, parent**.

The reason is not tidiness. Re-pointing a used Expense account to Asset would move a year of postings from the profit and loss to the balance sheet without touching a single ledger row — the numbers would change and nothing would say why. The API refuses it; the screen renders those fields read-only and explains itself.

`IsUsed` is set on first reference and never cleared. An account cannot become unused.

Today the only thing that sets it is **sub-account provisioning** — giving an account a sub-account marks it used in the same transaction. Journals, documents and opening balances will each set it as they are built; until then, a user-created account with nothing beneath it stays editable.

The account's **currency** is frozen with the rest of the configuration. Changing it on an account that already holds postings would restate every one of them at a different rate.

## Seeded accounts

Thirteen written when an organization is created, all `IsSystemDefault`, so locked from birth:

Accounts Receivable · Inventory · Input GST · Fixed Asset · Accounts Payable · Goods Received Not Invoiced · Output GST · Opening Balance Equity · Sales Revenue · Cost of Goods Sold · Purchase Returns · Realized FX Gain/Loss · Unrealized FX Gain/Loss

**Purchase Returns** is a **contra** expense: goods sent back reduce what you
bought, so a report subtracts it rather than adding a negative number. Sales
Returns and Discount Given work the same way on the income side.

**Fixed Asset** is where a capital line on a bill lands. It is a holding account
for now: the design is that each fixed asset category carries its own asset,
depreciation and expense accounts, but the asset register is not built yet — so
capitalised purchases collect here until it is, and are split out then.

**Goods Received Not Invoiced** is a clearing account, not a resting place. When goods arrive before the vendor's bill does, the receipt debits Inventory and credits this; the bill then clears it and credits Accounts Payable. What is left sitting in it is stock on the shelf that nobody has invoiced yet — which is a figure worth looking at, and the reason the alternative was rejected: posting nothing until the bill arrives understates the inventory asset for however long the paperwork takes. It is off the manual-journal picker for the same reason Accounts Receivable and Accounts Payable are — a hand posting to it leaves a residue that no document can ever clear.

A branch created before this account existed picks it up the next time its chart of accounts is seeded; the seed adds only what is missing and leaves everything else alone.

There is deliberately **no separate account for advances**. Money that moved before a document existed — a deposit paid to a supplier, a customer's advance, the excess when a payment is rounded up — sits as a *sub-account* beneath Accounts Receivable or Accounts Payable, so a party's whole position lives under two control accounts rather than four. See below.

They can be **renamed for display** — the hidden `AccountSystemName` is what code matches on — but never deleted, and their code and flags never change.

## Sub-accounts

Per-contact, per-item and per-tax detail beneath a control account, so the chart stays small while the ledger keeps a sub-dimension.

**Never created by hand.** They are provisioned by the master that owns them:

| Owner | Creates |
|---|---|
| Contact | 6 — trade, prepayment advance and overpayment advance, beneath **each** of Accounts Receivable and Accounts Payable |
| Item | 3 — Inventory, Cost of Goods Sold, Sales Revenue |
| Tax rate | up to 6 — CGST, SGST and IGST beneath **each** of Input GST and Output GST |

For a tax sub-account, three things identify it: the **parent account** gives the direction (input or output), `ReferenceId` gives the rate, and `TaxComponent` gives the component. That is why the unique key includes the component — otherwise CGST, SGST and IGST would collide under one parent.

`AccountTypeId` is copied from the parent account on write and never accepted from a caller; if the two disagreed, a report grouped by type would contradict the same report grouped by account.

A contact's six are grouped by **the direction the balance runs**, not by whether the party is a customer or a supplier. Everything under Accounts Receivable is an asset; everything under Accounts Payable is a liability. That matters because a sub-account's type is copied from its parent — group them by counterparty instead and a customer's deposit would be a liability filed under an asset, so a report grouped by account type would contradict the same report grouped by account.

Both directions are created for every contact regardless of role, because a contact is one record that can buy and sell. A contact who becomes a supplier next quarter would otherwise have a payable with no sub-account and drop silently out of the aging.

All six are per contact for the same reason: every one of those balances is answered about a named contact. You refund a particular customer's deposit, not a pooled one, and a control account whose balance cannot be split by contact cannot be reconciled at all.

**What tells them apart** is the sub-account's *purpose* — trade, prepayment advance or overpayment advance. Without it, a contact's three sub-accounts under one parent would be indistinguishable to the database and only the first would ever be created. It does the same job for a contact that the tax component does for CGST, SGST and IGST under one tax parent.

### The balance-sheet consequence

Because advances sit **inside** the receivables and payables control accounts, neither control total is a balance-sheet line on its own. Schedule III of the Companies Act requires advances to suppliers and advances from customers to be reported separately from trade receivables and trade payables — so a balance sheet has to split each control account by sub-account purpose. That split is mechanical, but it is not automatic: a report that sums the control account and stops overstates both trade lines by the advances held.

Provisioning is **idempotent** — per target, not just per call — because the events that trigger it are at-least-once. A contact created before the advances existed gains exactly the four it is missing when provisioning is re-run, and keeps the two it already had. Retiring a master deactivates its sub-accounts rather than deleting them, so history survives.

If a control account cannot be resolved — the chart was never seeded for the organization, or a system account was renamed at the database level — provisioning creates what it can and reports the rest as **missing**. A partial provision is a 409, not a 200: a contact with no Accounts Receivable sub-account would silently drop out of the aging report.

The **Sub-accounts** screen (Accounting → Sub-accounts) lists them grouped under their control account, filterable by owner type. It is read-only in both directions — nothing on it can be created, renamed or retired, because the owning master decides all three.

```
GET    /api/accounts?includeInactive=false
POST   /api/accounts
PUT    /api/accounts/{id}
DELETE /api/accounts/{id}          deactivates; system accounts refused
GET    /api/sub-accounts?referenceType=&referenceId=
POST   /internal/sub-accounts/provision     409 when control accounts are missing
POST   /internal/sub-accounts/deactivate
```



# Banks & bank accounts

**Banking › Banks** and **Banking › Bank accounts**

## Two masters, not one

A **bank** is the institution — HDFC Bank, State Bank of India. A **bank account** is one of your own accounts at it. Keeping them apart means the bank's name is typed once rather than differently on every account, and balances can be reported by institution.

Cash in hand and wallets are bank accounts with no bank behind them. Everything else must name one.

## Every account gets a ledger account

Creating a bank account creates a matching account in your **chart of accounts**, automatically. Without one there is nothing to post a receipt or a payment to; without the bank account there is no account number to reconcile against.

Which account it creates depends on the type:

| Account type | Becomes | Under |
|---|---|---|
| Savings · Current | Asset | Bank Accounts (1500) |
| Cash in hand · Wallet | Asset | Cash in Hand (1400) |
| Overdraft · Cash credit · Credit card | **Liability** | Bank OD & Credit Cards (2300) |

Overdrafts and credit cards are liabilities because an overdrawn account is borrowing. Reporting it as a negative asset is the kind of thing an auditor asks about.

The three parent groups are created with the organization and are **locked**, so a posting can never land on the group instead of the account underneath it.

**You cannot create a bank account from the Chart of Accounts screen.** It would have no account number and no IFSC, so it would appear in bank pickers and reconciliation with nothing behind it.

## Names stay in step

The bank account owns its name and pushes it to the ledger account. Renaming "HDFC Current" to "HDFC Current — Main" changes both. The chart of accounts shows the name read-only for these accounts rather than letting the two drift apart.

Deactivating a bank account deactivates its ledger account with it.

## When the ledger call fails

The account and its ledger account are written by two different services, so they cannot share one transaction. The account is saved first and linked immediately after.

If Accounting is unreachable at that moment, **the account still saves** — marked **Not linked** — and a **Link ledger** action retries it. Losing everything typed because another service was briefly down would be worse. An unlinked account cannot be transacted on until it is linked, and retrying is safe: Accounting keys the ledger account on the bank account's id, so a retry finds the account it already made rather than creating a second one.

## Balances

**Nothing here stores a balance.** It comes from the ledger, which is the entire point of the link — one number, derived from postings, rather than a second figure that can disagree with the books.

## The default account

One account is the default, preselected on receipts and payments. It cannot be deactivated while it holds that role — make another one the default first.



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

The panel at the foot shows the amount that moved, how much of it the lines account for, and what is left over. **Fill rest** puts whatever remains onto a line in one click.

## One payment, several documents

A payment is spread across documents by adding a line for each — ₹50,000 across three bills is three lines, and each one carries the bill it settles. In the ledger that becomes three separate pairs of entries, so any of the three bills can be traced back to the money that cleared it.

Paying **less** than a document is for is simply a line for less than the document: the rest of that balance stays outstanding.

Three rules the screen and the server both hold to:

- **A line settles the kind of document its purpose implies.** A bill payment settles a bill; an invoice payment settles an invoice; an advance settles nothing at all. You do not choose the kind separately, and a payment cannot be pointed at a document it did not pay.
- **Each document is named once per purpose.** Splitting a payment across two lines of the same bill is refused — put the whole amount for that bill on one line. Paying a bill and recording an overpayment against the same bill is different, and allowed.
- **The document shown on the payment as a whole is worked out from the lines.** One document when every line settles the same one; nothing when the payment is spread across several, because it is about all of them.

### Money left over

When the lines stop short of the amount that moved, the panel offers to place the remainder: **Add ₹1,000 as an overpayment**.

That is worth doing rather than folding the extra into the settled line. An overpayment recorded as one sits in that contact's **overpayment balance**: visible as money held, refundable on its own, and reported apart from what is genuinely trade. Folded into the settled line it turns their balance the other way up instead — an aging report then shows a supplier owing you money as though it were a trade receivable, on a line that quietly nets against their next bill.

The excess names the document it ran past when there is exactly one, so it stays traceable to the payment that caused it.

> **What is still owed on a bill is not yet checked.** Nothing stops a line settling more than its document is for — that arrives with allocation.

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



# Bank statements & reconciliation

**Banking › Statements**

Import what your bank says happened, and check it against what you recorded.

## Nothing here posts to your accounts

This is the thing to understand before anything else. The money already moved, and the document that recorded it — a payment, a receipt, a transfer — already posted it to your books.

A statement is the **bank's** account of those same movements. Reconciling compares the two; it never produces a third. Matching a line changes nothing in your ledger, your trial balance or any report. It records that you and the bank agree about one movement.

That is why a reconciliation can never double your transactions, and why you can match, undo and re-match as much as you like without consequence.

## Set up the column mapping once

Every bank lays its export out differently — two columns for withdrawal and deposit, or one signed column; `dd/MM/yyyy` or `dd-MMM-yyyy`; three lines of branch address above the header or none. So the first time you import for an account, you say which column is which. It is remembered.

| Setting | What it is for |
|---|---|
| **Rows to skip** | The branch address and account summary printed above the table |
| **Date format** | Exactly how the date is written. Not guessed — `03/04/2026` is a real date under three different formats |
| **Date, Description, Reference** | The columns to read them from, by header name or by position |
| **Withdrawal + Deposit**, *or* **one signed amount** | The two layouts banks use. Choose one; the screen clears the other |
| **Balance** | Optional, and worth filling in — see below |

Column names are matched loosely, so `Withdrawal Amt.` and `withdrawal amt` are the same column.

If your bank changes its export format, edit the mapping. Nothing already imported is affected.

## Importing

Drop in a **CSV** or **Excel (.xlsx)** file and import.

**Importing the same period again is fine, and expected.** Nobody downloads tidy non-overlapping statements — people download the last thirty days every week. Every line is recognised by what the bank said about it, so a re-import adds only what is new. "312 rows read, 40 new, 272 already imported" is the correct result of your fourth download this month, not a warning.

Two identical movements on the same day — two ₹500 cash withdrawals — are correctly kept as two. They are two movements, not a duplicate.

**If the file carries a running balance**, the import also checks the bank's own arithmetic: do the lines between the opening and closing figures account for the difference? That catches the one thing per-line matching cannot — a row that never arrived, because the file was truncated or the mapping quietly skipped it.

**An import is all or nothing.** If any row cannot be read, nothing is stored and the message names the row and what it could not read. A half-imported statement is worse than none: the lines that landed look reconcilable and the ones that did not are invisible.

## Matching

Lines that clearly correspond to one of your documents are matched for you as they import. Everything else is listed for a decision.

**A line is only matched automatically when there is exactly one plausible answer.** Amount and direction have to agree exactly, the dates within ten days, *and* the reference has to agree — and no other document may be equally good. A business paying one supplier the same amount every week has several identical documents within days of each other, and picking between them would be a coin toss recorded as a decision.

Where the software is not sure, it offers the candidates with a reason beside each — *"Amount and reference both agree, 2 days apart"* — and you choose.

For each line you can:

- **Pick a document** it corresponds to
- **Set it aside**, with a reason, for a line that is genuinely not yours to record
- **Undo** either, at any time

One document can be matched to only one line. If two statement lines look like the same payment, one of them is a different movement and needs its own document.

> **A line with no candidates is usually a bank charge, interest, or a fee nobody keyed.** Record it as a Spend money or Receive money document and it will match on the next look.

## When is it reconciled?

When nothing is left undecided — every line either matched or deliberately set aside. The list shows each statement's progress, and the working view hides what you have already dealt with so what remains is what you see.

## Exporting

**Export CSV** or **Export Excel** downloads the statement with your reconciliation beside the bank's own figures: status, the document each line was matched to, and any note.

It is meant to be opened, sent to an accountant, or attached to a query — not re-imported. Re-importing it would be importing your own opinion back as the bank's.

Amount columns come out as numbers, so a spreadsheet can total them.

## What is not here yet

**Automatic bank feeds.** Statements are imported by hand from a file you download. A live connection to the bank is a separate thing and is not built.



# Journal & ledger

**Status: built.** The ledger, the manual journal, the account ledger screen and the trial balance are all in place. Stock posts to the ledger, and a person can now post to it by hand and read back what either of them wrote.

## One posting target

Every financial document in the product — invoice, bill, payment, refund, journal, opening balance, depreciation, stock movement — writes its double-entry legs to **one table** and nowhere else. Reports read that table and no other.

That is the whole design. A second place where postings can live is a second answer to "what is this account's balance", and the two only ever disagree in front of an auditor.

## One row is one leg

A row carries a **debit or a credit, never both, and never a negative amount.**

Both rules are enforced by the database, not by whoever writes the row. A signed amount would let a reversal be recorded as a negative debit, and after that no column in the ledger can be summed without knowing which convention its writer used.

Reversing an entry means writing the offsetting entry. Nothing edits a posting that already exists.

## Balance is checked on the set, not the row

Rows are grouped by the document that produced them — its type code and its id — and the sum of debits must equal the sum of credits **in base currency** across that group.

This cannot be a check constraint: it spans rows. It is a **deferred** constraint trigger, which matters more than it sounds. A posting is several rows, and it is only balanced once the last one is in. An immediate check would reject every multi-leg posting on its first row. Deferred, it fires once at commit and judges the finished set.

The practical consequence: an unbalanced posting is accepted by each `INSERT` and then refused at `COMMIT`, naming the document and both totals.

## Base currency is what balances

Every row stores the amount twice — in the currency of the transaction, and again in the branch's base currency — along with the exchange rate that converted it.

**The rate is a snapshot at the posting date and is never looked up live.** A historical document that repriced itself because a rate moved is not a historical document.

The base-currency columns are what the balance trigger checks and what every report sums. Converting a rate leg by leg can leave a rounding difference, and the caller has to resolve it rather than the database quietly absorbing it.

## How a posting is made

Other services do not insert ledger rows. They describe a posting and ask Accounting to write it:

- accounts are named by their **system name** — "Inventory", "Cost of Goods Sold" — never by id, because an account id is a per-branch number in a database the caller does not read
- a sub-dimension is named by **what it refers to** — this item, this contact — and Accounting finds the sub-account beneath the control account itself
- the whole set arrives together and is written in one transaction

Deciding which accounts a sale or a stock issue touches belongs to the service that owns the document. Deciding whether the result is a legal posting belongs here.

### A document has several kinds of leg, and they only balance together

An invoice credits sales revenue **per line**, credits output GST **per rate**, and debits the customer **once for the whole document**. No subset of those balances on its own.

So a posting carries the leg type and the document line on **each leg**, and one call carries the lot. Balance is judged across the whole request rather than key by key.

### Postings replace, they do not accumulate

A leg is identified by four things: the document type, the document, the line, and which leg of the document it is. Posting that key again **deletes the previous rows and writes the new ones**.

Two things fall out of that, both of them the point:

- **A caller can retry.** A dropped response after a successful post is the ordinary case between two services, and a retry that doubles the ledger is how a general ledger stops being trustworthy.
- **A restated cost corrects itself.** When a backdated receipt changes what an earlier sale cost, the same key is posted again carrying the new figure.

Because the key includes both the leg type and the line, a service replacing its own rows cannot disturb another service's rows on the same document. A sale's revenue and receivable legs and its cost-of-goods legs are written by different services at different moments, and each replaces only its own. It also means a service that posts a document one line at a time — which is how costing works — cannot have line two erase line one.

**Withdrawing a posting is the one asymmetric case.** A void has no legs to say what it is removing, so it names the leg types explicitly and clears them across the whole document, leaving any other writer's legs alone.

### A document can be several things at once

What a leg came from — a bill payment, an advance, a refund — is recorded on the **leg**, not on the document.

Paying ₹11,000 against a ₹10,000 bill is the case that decides it. That single payment is two things: ₹10,000 settles the bill, and ₹1,000 becomes an advance held against the supplier. Record the document as one thing — "an overpayment" — and a payables report asking for bill payments quietly misses ₹10,000 of a real one. Nothing fails; the total is just wrong.

So the ledger row carries its own source, and both halves read correctly: the settled part is a bill payment against that bill, and the excess is an advance against that supplier.

## The manual journal

The document a person writes when no other document fits: an accrual, a correction, a transfer between two accounts, an asset built out of purchased material.

It is the only screen in the product that names two accounts directly. Everything else — an invoice, a bill, a payment — decides its own accounts from what it is.

### Draft, posted, reversed

**A draft is free.** It may be unbalanced, it holds no number, and it posts nothing. Someone keying a twelve-line accrual is out of balance for eleven of them, and a screen that refused to save until it balanced would force the whole entry to be typed in one sitting.

**Posting is the irreversible act.** It takes the next number from the JRN series, writes the ledger rows and freezes the entry. All three happen in one transaction: a post that fails anywhere gives the number back rather than leaving a gap in a series that has to run consecutively.

**The number is taken at post, not when the form opens.** A draft that is never posted must not consume a number, and a person who starts an entry and changes their mind has done exactly that. Drafts read as *Unnumbered* until they post.

**A posted entry is never edited.** It is corrected by a reversing entry that stands beside it — both stay in the ledger, and both are visible.

### Reversal is paired line by line

The reversing entry has the same accounts and the same amounts with debits and credits swapped. The two headers point at each other, **and so does every line**.

The line-level pairing is not decoration. A reversal that missed a line would still balance and would still look complete at the header, so nothing else in the system would catch it.

### What a journal may post to

Any account in the chart, except a seeded control account that has not been opened to hand entries.

Receivables, payables and inventory are driven by their own subledgers — invoices, bills, stock movements. A hand entry against one puts the control account and its subledger out of step with nothing to reconcile them against. Those accounts are left out of the picker rather than offered and then refused.

## Reading the ledger back

Two screens, and between them every posting in the product can be checked by a person.

**Account ledger** — one account over a date range: what it stood at when the range opened, every posting inside it, and a running balance down the page. Every row names the document behind it and links to it, so a figure can always be traced to what produced it.

Balances are held internally in debit terms and turned the right way up for display, because "Accounts Payable: −40,000" is a number every accountant has to stop and translate.

**Trial balance** — every account with a balance, grouped by account type, each in the single column its net actually falls on. The two column totals agreeing is the one number that says the whole system is sound; when they disagree the page says so at the top rather than showing a tidy table with the discrepancy buried in it.

Neither screen is a database view. A view that omitted `security_invoker` would run as its owner and read straight past row-level security, handing one branch another branch's general ledger — and the join it would have saved is a few lines of LINQ.

## Isolation

The ledger, the journals and their lines are per-branch tables with an `OrgId`, an EF Core query filter, and a **Postgres row-level security policy**.

The query filter is the first line of defence, not the last: it is a property of the code, and one query written to ignore it would read another branch's general ledger. The policy is a property of the database and holds however the connection is used.

## Who posts today

**Stock, money documents and manual journals.** Every stock movement that changes what the branch owns posts its cost — see [Reaching the accounts](#/stock) — the money documents post their own legs in the transaction that writes them, and a person can post an entry by hand. Sales and Purchase follow as those services are built.



# Opening balances

**Accounting › Opening balances**

Where your books begin here: everything the branch owned, owed and was owed on the day it started using this product.

This is the one screen worth slowing down for. Every balance the product will ever report is measured from these figures, and an opening balance that is *wrong* balances just as neatly as one that is right — nothing downstream catches it, because everything downstream is measured from it.

## One per branch, and one way

A branch has one moment it started. There is no second opening balance, no reopening the first, and no reversing it: reversing an opening balance does not restate a transaction, it deletes the ground everything since stands on.

While it is a draft it is completely free — key it over weeks, save as often as you like, delete it and start again. Nothing posts and nothing is numbered until you press **Open the books**. After that the screen is a record of what was brought across, and an error is corrected the way any error in a closed period is: with a journal entry that leaves a trail.

## The figure to watch

Every line posts against an account called **Opening Balance Equity**. A bank balance is `Dr Bank / Cr Opening Balance Equity`; something you owe is `Dr Opening Balance Equity / Cr Accounts Payable`.

That means the books balance after every single line — and whatever Opening Balance Equity is left holding is **exactly the part of your position you have not keyed yet**. It is usually capital or retained earnings. When it reaches nothing, the migration is complete.

The panel at the foot shows it as you type, not when you press the button. Finding out at the end that the whole thing is out by ₹4,300 means auditing weeks of work; finding out as you key the line means fixing the line.

## The four kinds of line

| What | Names | Posts to |
|---|---|---|
| **Account balance** | An account | That account |
| **Owed to us** | A contact | That contact's balance under Accounts Receivable |
| **We owe** | A contact | That contact's balance under Accounts Payable |
| **Stock on hand** | An item, a quantity and a unit cost | Inventory, posted by Inventory itself |

Only the first kind picks an account. A receivable that could choose its own account is a receivable that can be filed under Sales Revenue.

### Receivables and payables go in one line per document

Not one line per contact, and certainly not one figure for the lot.

A customer owing ₹300,000 across invoices of thirty, ninety and a hundred and eighty days ages nothing like a single ₹300,000 balance — and an aging report is the first thing anyone opens after a migration. So each unpaid invoice or bill is its own line, carrying the **original document's number and date**. The number is what the contact will quote when they query it; the date is what the aging runs from.

> A contact with no ledger accounts cannot be brought across — the balance would land on the control account with nothing underneath it, which balances perfectly and leaves the contact owing nothing. Open the contact, use **Provision accounts**, then come back. The screen names the contacts this applies to.

### Stock carries a cost, not a value

An item line is a quantity and a **unit cost**, and there is nowhere to type a value — the value is the two multiplied.

That unit cost is the most consequential figure on this screen. It seeds the item's weighted average, so every cost of sale until the next purchase is computed from it. It is also why Inventory records these lines rather than Accounting posting a figure of its own: a value keyed here would be a second number against the Inventory account, free to disagree with the stock the items are actually carrying.

## Opening the books

**Open the books** is refused until:

- **Opening Balance Equity nets to zero.** Anything left is a piece of the position not yet keyed.
- **Every control account exists** — receivables, payables, and Opening Balance Equity itself.
- **Every contact named has ledger accounts**, so its balance lands on the contact rather than on the bare control account.
- **Inventory records the stock and reports back the same value this document says.** If the two disagree, nothing posts. A migration that ties everywhere except the one place nobody looks is the failure this check exists for.

Every reason it is refused is listed on the screen, not summarised.

If Inventory cannot be reached, or refuses a line, **nothing posts at all** and the whole thing can be retried. A branch that opened its books with two thirds of its stock is worse than one that did not open at all, because only the second is obvious.

## Afterwards

Draw a trial balance immediately. Debits and credits agree, Opening Balance Equity is nil, and every contact you brought across shows on its statement with the right age.

The screen also shows a **Subledgers** panel once the books are open: each control account against the sum of the balances beneath it.

That is a different claim from the trial balance, and it is the one worth reading. A receivable posted to Accounts Receivable with **no contact underneath it** is a perfectly balanced entry — the trial balance foots, and nobody owes the money. The statement is wrong, the aging report is wrong, and the first receipt has nothing to settle against, and double-entry never says a word because nothing in double-entry is broken. The **Belongs to nobody** column is that figure. It should be zero on every row.

Accounts with no subledger — bank, cash, capital — are not listed. They have nothing to tie to, and showing them at a difference equal to their whole balance would bury the two rows that matter.

> The same check runs *before* go-live. A branch whose books are already adrift cannot open on top of it — afterwards nobody could tell which of the two put the difference there.

## What is not here yet

**Fixed assets**, which are a Phase 2 feature. There is no asset register yet, so an asset you are migrating comes across as an ordinary account balance — bring it in at its written-down value against your Fixed Asset account, the way you would any other balance.

What you do not get until the register lands is the asset's own record: its cost, its life, and a depreciation schedule that runs on its own. When it does, migrated assets skip historical depreciation — they arrive at written-down value and depreciate from go-live, so nothing you bring across now has to be re-entered.



# GST & tax

**Status: built.** Rates, effective dating and the per-rate GST sub-ledger.

## The rate table

Each rate carries the full split: `TotalRate`, `CgstRate`, `SgstRate`, `IgstRate` and `CessRate`.

**CGST, SGST and IGST are derived, never entered.** Enter a total and the API computes CGST and SGST as half each, IGST as the whole. The request model has no fields for them, so a caller cannot set them independently, and a database check constraint enforces the invariant as well:

```
CgstRate = SgstRate  AND  CgstRate + SgstRate = TotalRate  AND  IgstRate = TotalRate
```

Two layers, because a split that drifts is not visible until a return is filed and the numbers disagree.

Which components a transaction actually uses is decided at posting time: **intra-state → CGST + SGST, inter-state → IGST**. Every rate carries all three ready.

## Seeded rates

Six, written when an organization is created: GST 0%, 5%, 12%, 18%, 28% and **Bullion 3%**. The bullion rate is an ordinary row — nothing in the schema privileges the standard slabs.

Seeded rates can be **renamed** for display; their hidden `TaxSystemName` is what code matches on, so renaming "GST 18%" changes only the label.

## Effective dating — rates supersede, never overwrite

Rates change by law, and a document dated before the change must still resolve the rate that applied *then*. So there is no in-place edit of a rate:

**Revise** closes the current version's `EffectiveTo` the day before the new one starts, and inserts a successor. The old row stays, and the list shows it as *Superseded* under "Show superseded".

```
GET /api/tax-masters/resolve/{taxGroupId}?onDate=2026-03-15
```

That is what a document uses — not "today's rate".

Only the version currently in force can be revised, and the new date must be after the one it replaces; both are refused with a specific error rather than silently accepted.

## `TaxGroupId` — why revisions keep one sub-ledger

Every version of "GST 18%" shares a **`TaxGroupId`**, set to the first version's own id.

This exists because sub-accounts reference a tax rate. If they referenced the row id, revising GST 18% would create a *second* set of six GST sub-accounts and split the GST sub-ledger at the revision date — input tax credit before and after the change would sit in different buckets for no reason. Keying on the group keeps it continuous, and a revision reuses the sub-accounts it already has.

## Sub-accounts per rate

Creating a rate provisions up to **six** sub-accounts:

| Applies to | Created under | Components |
|---|---|---|
| Purchases | Input GST *(Asset)* | Input CGST · Input SGST · Input IGST |
| Sales | Output GST *(Liability)* | Output CGST · Output SGST · Output IGST |

At least one direction is required — a rate usable on neither document is dead data, and a check constraint refuses it.

This is what makes GSTR-1/3B and input tax credit readable straight from the sub-ledger, broken down **by rate and by component**, instead of one lumped Input GST balance you would have to decompose afterwards.

Deactivating a rate deactivates its sub-accounts. Nothing is deleted — postings reference them.

## Known limitation

`CessRate` is a percentage. Cess on some goods, notably tobacco, is levied as a **fixed amount per unit**, which this column cannot express. It only matters if you trade those categories; supporting it would need an amount-per-unit column and a compounding rule.



