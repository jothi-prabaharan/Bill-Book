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

That is deliberate. A branch handed over half-created cannot save an item — saving one requires a unit type — so it stays visibly unfinished rather than looking ready and failing at the first thing you try. If a service could not be reached, the branch waits with a **Finish setup** action.

**Finish setup adds only what is missing**, so it is safe to press at any time — on a half-provisioned branch, on a branch set up last year, on one that is already complete, where it does nothing. It is also how a branch created before a new default existed gets it: when a GST rate or a unit is added to what we ship, running setup again on an older branch brings it in without touching anything already there.

What is yours stays yours. Rows are matched on their internal name rather than their label, so a payment term you renamed is recognised as already present and never duplicated back under its original wording, and anything you added yourself is left alone. Two cases are skipped rather than forced: a default we ship whose name you have already used for something of your own, and a unit type whose base unit you have changed — the conversion factors we ship are relative to the original base, and adding them against a different one would silently misstate stock.

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

## Adding a branch beyond your licence

Your **licence** covers a number of branches — a trial covers one. Adding one beyond that is **not refused**. It is created, seeded and usable, on its own **30-day trial**, and marked *Trial* in the list.

That is deliberate. A branch is a complete set of books, and nobody can judge one from an empty screen: it has to be set up, its masters adjusted and a month traded through it. Thirty days rather than the account's fourteen, because a fortnight does not cover a monthly cycle.

The trial is a **cap, not an extension**. Login enforces whichever ends first, so a trial branch under a licence expiring next week stops next week. When it ends the branch stops and everything in it is kept; your other branches are unaffected, and there is nothing to renew on the account itself. Adding a licence for the branch clears the trial.

Nothing takes payment yet, so nothing clears the flag automatically.

## Suspending and deleting

**The first branch cannot be suspended.** The account would have nowhere to sign in to.

Branches are never deleted. Their documents, ledger rows and stock all live under the branch's id, and removing it would leave that history belonging to nothing. Suspending takes a branch out of use and leaves everything intact.

## Editing the branch you are in

**Settings › Organization** edits the branch you are signed in to, in three tabs:

- **Profile** — code, name, address, contact details, website, logo.
- **Statutory** — GSTIN, PAN, TAN, TIN, CIN and Udyam number. The GSTIN's first two digits must match the state on Profile; the form says so before the save is refused.
- **Financial** — financial year start month. The base currency is shown but fixed: every posting in the branch converts to it, so changing it after anything has been posted would restate the books.

Which branch is taken from your sign-in rather than the address, so it is always the one you are working in. To edit a different one, switch to it from Branches.

TAN, TIN, CIN, Udyam number, website and logo had no screen at all before this — they could be set at signup and never corrected, which is not how a CIN or an MSME registration arrives.

## When a branch's access ends

Every branch carries its **own end date**, set when the branch is created and taken from the account's licence at that moment. It is checked at every sign-in, alongside the licence.

It is a **cap, not a replacement**. Whichever of the two ends first is the one that applies, so a branch can never outlive the licence paying for it — and a branch can be wound down early without touching the account everyone else works in. A seasonal counter, a franchise leaving, a location closing: the branch stops and nothing else does.

Signing in still works. You land on a page saying **this branch has closed**, with the date, and you sign out and pick another branch — the switcher lives on a settings page, and settings pages are behind the same check that stopped you. The wording is deliberately different from an expired licence: your account is fine, so there is nothing to renew and nobody should be sent to a billing page.

Branches created before this existed have no end date of their own, and follow the account's licence exactly as they always did.

> **Renewing the licence does not move the branch dates.** Each branch holds its own copy, taken when it was created. Extending the licence without extending the branches leaves them closed under an account that is perfectly valid. There is no renewal screen yet; when there is, it has to move both.
