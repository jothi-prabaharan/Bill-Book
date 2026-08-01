# Branches

The places an organization trades from — a shop, a godown, a second showroom.

**Settings › Branches**

## What a branch is, and what it is not

A branch is a **reporting and numbering dimension**. It tells you which shop wrote an invoice and which counter a journal line belongs to, and it can give a document series its own prefix.

**A branch is not a stock boundary.** Stock is one shared pool across every branch, and weighted average cost is company-wide. Two shops selling the same SKU draw on one quantity and one cost. Splitting stock per branch is what makes the same item carry two different costs and the totals stop reconciling — so nothing on this screen partitions inventory.

A warehouse names a branch for reporting. Its quantity still comes out of the shared pool.

## Head office

**Exactly one branch is the head office**, always. It is where a document lands when it names no branch.

- The first branch created is the head office whether or not you tick the box.
- Making another branch the head office moves the flag; it is never held by two.
- The head office cannot be deactivated or demoted directly. Promote a different branch first — the screen says so rather than quietly leaving the organization with none.

Every new organization is created with a **Head Office** branch already in place, carrying the organization's own address, GSTIN and contact details, so a branch picker is never empty on day one.

## Branch code

Short — up to ten characters — because it is read aloud and typed. `HO`, `CHN`, `BLR2`.

The code matters beyond display: a numbering series can **include the branch code** in the numbers it generates, so `INV/2526/CHN/00042` says where it was written without a lookup.

The code is **copied onto the series**, not read from the branch each time a number is composed. Two reasons: composing a document number must never reach across into the master database, and renaming a branch later must not silently restyle numbers already issued.

## GSTIN per branch

Set a GSTIN on a branch **only when that branch holds its own registration** — typically because it is in another state. Left empty, the organization's GSTIN applies.

Where one is set, **its first two digits must match the branch's state**, and saving is refused otherwise. This is the same check the contact master runs, for the same reason: the first two digits are the state code, and a mismatch splits every document's tax the wrong way — CGST + SGST where IGST belongs — without anything complaining until filing.

## Where branches are used

| Where | How |
|---|---|
| Numbering series | A branch series is preferred over the org-wide one for that branch |
| Warehouses | Names the branch a location reports under |
| Journal lines | Carried on every debit and credit for branch-wise reporting |

All three store the branch id without a database-level foreign key. Branches live in the **master database** next to the organization they belong to, and the rest of that list lives in the customer's own database — Postgres cannot enforce a key across two databases, so the reference is validated in code instead. This is the same arrangement as a contact's country and state.

## Ordering and deactivation

Drag rows to reorder; the order is what every branch picker uses.

Branches are never deleted. Documents, journal lines and numbering series all point at the id, and a deleted branch would leave them naming nothing. Deactivating takes a branch out of the pickers and leaves its history intact.
