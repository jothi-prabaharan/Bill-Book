# HSN & SAC codes

**Status: partial.** The table, search API and CSV importer are built. The seed covers the chapter and group headings; the detailed codes are loaded from the authoritative file.

## What they are

- **HSN** — Harmonised System of Nomenclature, for **goods**. 2, 4, 6 or 8 digits.
- **SAC** — Services Accounting Code, for **services**. Six digits, always beginning `99`.

Both are national, identical for every customer, so they live in the **master database** (`mst.HsnSacCodes`). Per-customer items reference them by unenforced id — a cross-database foreign key is impossible in Postgres.

One table with a `CodeType` discriminator rather than two, because they are structurally identical and appear in the same picker on an item.

## Columns

| Column | Notes |
|---|---|
| `Code` | 2–8 digits, unique |
| `CodeType` | HSN or SAC |
| `Description` | The official description |
| `ChapterCode` | First two digits, for grouping |
| `DefaultGstRate` | **A suggestion, not a constraint** |
| `DigitLength` | 2 = chapter heading, 4/6/8 = a real code |

### Why `DefaultGstRate` and not a tax-rate link

The rate lives on the item, not the code. Two reasons: the same HSN can attract a different rate by condition, and `TaxMasters` is per-customer while HSN is global, so no foreign key could exist anyway. The code's rate merely **pre-selects** the org's matching tax rate when an item is created; the item's own rate stays authoritative.

### Chapter rows are headings, not codes

A 2-digit row is a chapter — "62 · Articles of apparel, not knitted" — and must never go on an invoice line. The search endpoint excludes them by default; pass `includeChapters=true` only for building a grouped picker.

## What is seeded

**129 rows**: the 97 Harmonised System chapters (77 is reserved and unused) and 32 SAC groups.

**The detailed codes are deliberately not hard-coded.** There are roughly thirteen thousand, they change with each CBIC notification, and an incorrect HSN on a GST return is a filing error — so they are imported from the authoritative source rather than transcribed into a migration.

## Loading the full list

Export the current master list from the GST portal (Services → User Services → Search HSN Code) or the CBIC tariff, then:

```
POST /internal/hsn-sac/import      multipart file upload
```

Expected CSV, header row required:

```
Code,CodeType,Description,DefaultGstRate
84713010,HSN,"Portable digital automatic data processing machines",18
998313,SAC,"Information technology consulting services",18
```

The import is **idempotent** — an existing code is updated, a new one inserted, and nothing is ever deleted, because items already reference these rows. Malformed lines are reported rather than silently skipped.

## Searching

```
GET /api/master/hsn-sac?search=8471&codeType=HSN&take=50
GET /api/master/hsn-sac/chapters
```

Matches on code prefix or description text, capped at 200 results.

## Two things still open

**Digit length by turnover.** GST requires 4 digits for B2B below ₹5 crore turnover and 6 above. That is a property of the *organization*, not the code, so it belongs in configuration — a `hsn.digits` key enforced when an item is saved. Not implemented.

**The code must be copied onto the invoice line.** An item's HSN can change, but a filed invoice must keep the code it was filed under, and GSTR-1 needs an HSN summary read from the lines. So the line stores its own copy at posting time, exactly like the exchange rate. That belongs with the invoice tables, which do not exist yet.
