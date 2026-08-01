# Branches

The places you trade from. Each one is a complete set of books.

**Settings › Branches**

## What a branch is here

Your **account is the head office**. Every **branch is an organization** under it, sharing the account's database and separated by its organization id.

That separation is real, not a label. A branch has **its own items, contacts, stock, chart of accounts, tax rates and numbering series**, and nothing crosses between them. Chennai cannot see Bangalore's rows — the query filter and the database's own row-level security both stop it.

The trade this makes: clean independent books per branch, and leakage between them made structurally impossible — at the cost of maintaining master data per branch, and of consolidated reporting being a deliberate read across branches rather than a default.

## Adding one

A branch is not a row you insert. It is a small provisioning.

Creating one writes the branch, then asks every service to set up its books: chart of accounts, GST rates, numbering series, payment terms, contact person roles, unit types, units and metal purities. Until that finishes the branch shows as **Setting up** and cannot be used.

That is deliberate. A branch handed over half-created cannot save an item — saving one requires a unit type — so it stays visibly unfinished rather than looking ready and failing at the first thing you try. If a service could not be reached, the branch waits with a **Finish setup** action; every seed is safe to run again.

No new database is created. Branches share the account's.

## Branch code

Short, up to ten characters: `HO`, `CHN`, `BLR2`. It is read aloud and typed.

It also goes into generated document numbers when a numbering series is set to include it, so `INV/2526/CHN/00042` says where it was written. The code is **copied onto the series** rather than read back each time, so renaming a branch later does not restyle numbers already issued.

## GSTIN and state

Set a GSTIN on a branch that holds its own registration — typically because it is in another state.

**Its first two digits must match the branch's state**, and saving is refused otherwise. Same rule as a contact's GSTIN, for the same reason: those digits are the state code, and a mismatch splits every document's tax the wrong way — CGST + SGST where IGST belongs — with nothing complaining until filing.

## Base currency and financial year

**The base currency is fixed once the branch exists.** Every amount posted in that branch is converted to it, so changing it later would restate the entire set of books. It is editable only while creating.

The financial year start month drives the year segment in generated numbers — April for India.

## Switching between branches

**Switch to** moves you into another branch without signing out. You get a new session carrying that branch and the permissions you hold *there* — permissions are per branch, so the same person can be an accountant in one and a viewer in another.

The page reloads on switching, deliberately: everything on screen belongs to the branch you just left.

Only branches you have been given access to appear.

## Limits, suspending and deleting

The number of branches allowed comes from your **licence** — a trial allows one. Adding beyond it is refused with an explanation rather than failing silently.

**The first branch cannot be suspended.** The account would have nowhere to sign in to.

Branches are never deleted. Their documents, ledger rows and stock all live under the branch's id, and removing it would leave that history belonging to nothing. Suspending takes a branch out of use and leaves everything intact.
