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
