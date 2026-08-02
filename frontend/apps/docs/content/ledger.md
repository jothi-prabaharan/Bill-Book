# Journal & ledger

**Status: partial.** The ledger table, its rules and the posting API are built. Nothing writes to it yet, and there is no screen — see [Build status](#/status).

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

### Postings replace, they do not accumulate

A posting is identified by four things: the document type, the document, the line, and which leg of the document it is. Posting that key again **deletes the previous rows and writes the new ones**.

Two things fall out of that, both of them the point:

- **A caller can retry.** A dropped response after a successful post is the ordinary case between two services, and a retry that doubles the ledger is how a general ledger stops being trustworthy.
- **A restated cost corrects itself.** When a backdated receipt changes what an earlier sale cost, the same key is posted again carrying the new figure.

Because the key includes which leg it is, a service replacing its own rows cannot disturb another service's rows on the same document. A sale's revenue and receivable legs and its cost-of-goods legs are written by different services at different moments, and each replaces only its own.

An empty leg list withdraws a posting that should no longer exist.

## Isolation

The ledger is a per-branch table with an `OrgId`, an EF Core query filter, and a **Postgres row-level security policy**.

The query filter is the first line of defence, not the last: it is a property of the code, and one query written to ignore it would read another branch's general ledger. The policy is a property of the database and holds however the connection is used.

## What is not built

- **`acc.Journals` and `acc.JournalDetails`** — the manual journal-entry document. The ledger does not need them to work; they are what a person posting an entry by hand types into.
- **The ledger screen and the trial balance.** Nothing displays any of this yet.
- **Callers.** No service posts today. Stock is the first, and it is being wired now.
