# Configuration

**Status: built.**

Business tunables an organization owns and edits in the app. Not deployment settings — connection strings, signing keys and service addresses come from `appsettings` and environment variables instead, covered in [Environments](#/environments).

## How it works

A key-value store for the long tail of tunables — decimal places, default due days — without a database column for each.

Two layers:

- A **system default** row (`OrgId` null) ships with the product
- An optional **per-organization override**

The **effective value** is the org's row when present, otherwise the default. So an organization that never touches a setting silently inherits improvements to the shipped default.

## Keys are seed data

The screen edits **values only**. It cannot add or delete keys, deliberately: a key nothing reads is dead data, and deleting a key that code reads breaks it at runtime. New keys arrive by EF migration, like the other masters.

Writing an unknown key returns `404` rather than creating it.

## Shipped keys

| Code | Category | Default | Drives |
|---|---|---|---|
| `unitPrice.decimals` | Formatting | 2 | Decimal places on unit-price inputs |
| `quantity.decimals` | Formatting | 2 | Decimal places on quantity inputs |
| `sales.dueDays` | Documents | 30 | Default payment terms on invoices |
| `purchase.dueDays` | Documents | 30 | Default payment terms on bills |
| `documents.allowFreeTextLines` ᵒ | Documents | `true` | Let a line carry a description, quantity and price with no item. Such a line moves no stock and never appears in a sales-by-item report |
| `documents.discountLevel` ᵒ | Documents | `Line` | Where a discount is entered — `Line`, `Header` or `Both`. A header discount is apportioned across the lines by taxable value **before** tax, because GST is charged per line |
| `documents.discountBeforeTax` ᵒ | Documents | `true` | On: the discount reduces the taxable value and so reduces GST. Off: tax is charged on the full value and the discount only reduces what is collected |

`DataType` (Number / Text / Boolean / Date / Json) tells the screen which input to render and the reader which cast to apply, so callers never parse strings by hand.

> Unit price and quantity have **separate** decimal settings on purpose. A unit price often needs more precision than the money total — selling at ₹12.4567 per unit while the line total rounds to 2 decimal places. One shared setting would force them equal. Money precision is different again: it comes from the **currency**, not from here, because it is a property of the currency (JPY 0, INR 2, KWD 3).

## The screen

`Settings → Configuration`. Keys are grouped by category, each showing its effective value, an "overridden" badge when it differs from the default, and a reset button labelled with the default value. Editing saves on blur.

```
GET    /api/organizations/{orgId}/configurations
PUT    /api/organizations/{orgId}/configurations/{code}   { value }
DELETE /api/organizations/{orgId}/configurations/{code}   clears the override
```

## One-time settings

Keys marked **ᵒ** are chosen once and then frozen. They stay fully editable until the branch posts its first sales or purchase document; after that the screen disables them and the API answers `409`.

The freeze is on **first use, not first save** — the same rule an item's costing method and an account's type already follow. The reason is the same too: these three decide how a document's tax and discount are computed, so changing one after the branch has traded leaves earlier documents computed one way sitting beside later ones computed another, with nothing on either saying which.

Set them during setup, before the first invoice.
