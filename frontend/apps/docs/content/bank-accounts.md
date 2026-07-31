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
