# Contacts

Customers, vendors, job workers and prescribers — **one master, not four**.

**Contacts**

## Why one list

In Indian SMB books the same party is routinely both a customer and a vendor. A separate customer list and vendor list would mean the same GSTIN typed twice, two sets of addresses to keep in step, and two records to update when they move.

So a contact carries **roles** instead: tick every one that applies.

| Role | What it enables |
|---|---|
| Customer | Selectable on sales documents; gets a receivable sub-account |
| Vendor | Selectable on purchase documents; gets a payable sub-account |
| Job worker | Karigars and third-party processors who hold your stock |
| Prescriber | Doctors, for the Schedule H1 register |

Receivable and payable stay apart in the ledger through their sub-accounts, not through separate master records. At least one role is required — a contact with none would not appear on any screen.

## Contact code

Left empty, the code comes from the numbering series: `CUST-00001` for a customer, `VEND-00001` for a vendor. Both roles ticked gets a customer code, since that is the one customers quote back at you.

You can type your own if the series allows a manual override. The code cannot be changed after saving — it is on documents already.

## GST

**Registration type decides whether a GSTIN may exist at all.**

| Registration | GSTIN |
|---|---|
| Regular · Composition · SEZ | Required |
| Unregistered · Overseas · Consumer | Not allowed |

**The GSTIN's first two digits must match the place of supply.** This is checked on save and refused if it does not hold. Left wrong, every document for that contact would split its tax the wrong way — CGST + SGST where IGST belongs — quietly, and the error would only surface at filing.

Place of supply resolves in this order: the document's own override, then the default **shipping** address's state, then the contact's place of supply, then the default billing address. Shipping comes first because for goods the delivery location is what decides the tax split.

One GSTIN, one contact. A second contact with the same GSTIN is refused — if they both buy and sell, tick both roles on the one record.

## Addresses

Billing and shipping, with one default of each. **Copy billing to shipping** is on the toolbar because that is the common case.

An address in another state is not a formality: it changes the place of supply for goods delivered there, and a branch with its own registration carries its own GSTIN on the address.

## People

Every contact needs **at least one person**, and exactly one of them is the **default**.

The default is where the contact's email and phone come from — there is no separate contact-level email, deliberately, so there is never a question of which of two addresses an invoice goes to. For that reason the default person must have an email or a mobile number; anyone else can be a name alone.

Roles come from a small master maintained in a popup, reachable from the contact list and from the form itself, so a missing role never forces you to abandon a half-filled contact. Renaming a built-in role is fine; deleting one that people hold is not — make it inactive, and existing contacts keep it so their history still reads correctly.

## Trading limits

Three limits, all optional:

- **Credit limit** — the most this contact may owe.
- **Max outstanding days** — how far past **the due date** an invoice may go before new documents are blocked. Counted from the due date, not the invoice date, so a Net 60 customer does not trip a 45-day limit while still perfectly within terms.
- **Max discount %** — the ceiling on a line or document discount.

Breaching one **blocks** the document. A user holding the override permission can push it through, and the override is recorded against the document.

These are enforced when Sales and Purchase are built. Until then they are stored and not applied.

## What happens when you save

A contact gets **two sub-accounts** in the ledger — one receivable, one payable — created by Accounting. They are what let the ledger report per contact without the chart of accounts growing a line per customer.

## Deactivating

Contacts are never deleted. Documents point at them and their sub-accounts hold ledger history. Deactivating takes them out of the pickers and leaves everything else intact.
