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
