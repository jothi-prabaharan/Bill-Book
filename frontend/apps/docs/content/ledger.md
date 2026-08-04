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

**Stock and manual journals.** Every stock movement that changes what the branch owns posts its cost — see [Reaching the accounts](#/stock) — and a person can post an entry by hand. Sales, Purchase and Banking follow as those services are built.
