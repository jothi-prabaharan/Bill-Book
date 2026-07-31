# Numbering series

Every generated code in the product comes from a numbering series: customer codes, vendor codes, item codes, warehouse codes, and — as Sales and Purchase land — invoice, credit note and receipt numbers.

**Settings › Numbering series**

## What a series is made of

A generated code is assembled from up to four parts, joined by the separator, with the suffix added at the end:

| Part | Example | Notes |
|---|---|---|
| Prefix | `INV` | Free text, up to 15 characters |
| Financial year | `2526` | Optional. Four formats to choose from |
| Branch code | `CHN` | Optional. Only for a series tied to a branch |
| Number | `00042` | Zero-padded to the chosen number of digits |

`INV` + `2526` + `00042` with `/` as the separator gives **INV/2526/00042**.

The screen shows a live preview as you type, so a prefix or a padding width that reads badly is obvious before any record uses it.

## Financial year and resetting

The year segment follows your organization's **financial year**, not the calendar year — a series set to reset yearly restarts on 1 April, not 1 January.

| Reset | Behaviour |
|---|---|
| Never | The counter runs on forever. Right for master codes |
| Every financial year | Restarts on the first day of your financial year |
| Every month | Restarts on the 1st |
| Every day | Restarts at midnight |

A reset happens on the first record saved in the new period, not at the stroke of midnight, so a series with no activity does not skip a period.

## Masters and documents

A series numbers either **master records** or **documents**, and the difference is not cosmetic.

**Document series must run consecutively** within a financial year — that is a GST requirement, not a preference. Two consequences follow:

- **Typing a number by hand is not allowed** on a document series. The option is disabled and the database refuses it.
- **The number is taken when the record is saved**, never when the form is opened. Abandoning a half-filled invoice therefore leaves no gap.

Master series are relaxed about both: a jeweller who already runs an item-code scheme can key their own codes, and nobody audits a gap in customer numbering.

## Several series for one thing

You can have more than one series for the same code — a separate invoice series per branch, for example. One of them is the **default**, which is what a form preselects; a branch series wins over the org-wide series when the record belongs to that branch.

Every series for the same code and branch shares one rule: exactly one may be the default, and the database enforces it.

## Moving the counter

The next number lives behind its own **Counter** action rather than sitting on the edit form, because it is the one change that can produce a duplicate.

Moving it **forwards** is safe and skips numbers. Moving it **backwards** walks over numbers that may already be on records; the screen warns, and if the next generated code does collide, the record's own uniqueness check refuses the save. Nothing is silently overwritten.

## Reordering

Drag the handle to reorder the list. The order is stored, so it is the same for everyone in the organization. Reordering is available while inactive series are hidden — with a filter applied, "between these two rows" has no stable meaning.

## What ships with a new organization

Five master series are created when an organization is set up:

| Code | Name | Example |
|---|---|---|
| `CUSTOMER` | Customer Code | CUST-00001 |
| `VENDOR` | Vendor Code | VEND-00001 |
| `ITEM` | Item Code | ITM-00001 |
| `WAREHOUSE` | Warehouse Code | WH-001 |
| `BANK` | Bank Code | BNK-001 |

All five allow a manual override, and all five can be renamed and reformatted. Their **codes** cannot be changed — that is what the rest of the system looks them up by.

Document series arrive with Sales and Purchase.

## Deactivating

A series is never deleted, because codes it issued are sitting on records and the series is how those codes are explained later. Deactivating is refused if it would leave a code with no active series — otherwise the next contact or item saved would fail with an error about numbering, far from anything the user just did.
