# Inventory masters

Units, categories, purities, warehouses and the item master itself.

## Units — one conversion mechanism

Every unit belongs to a **type** (Quantity, Weight, Volume, Length, Area, Time), and every type has exactly one **base unit**. Each unit stores its factor to that base, and that single number is where all conversion comes from:

```
qty in unit B  =  qty × factor(A) ÷ factor(B)
```

Kilograms base at 1, grams at 0.001. Nothing else is stored, so nothing can disagree.

**Pack sizes are units of their type**, not per-item mappings. A 50 kg bag is a Weight unit with a factor of 50; a 25 kg bag is another. That keeps one mechanism instead of two, at the cost of a slightly longer unit list.

**`UQC` is a second code**, because carat and tola are not notified GST units. Your staff type `CRT`; GSTR-1 receives `OTH`.

Moving a type's base unit rescales every factor in it so the relationships hold. It is refused once any item uses a unit of that type — the rescale would restate quantities already recorded.

## The item's five unit fields

| Field | What it decides |
|---|---|
| Unit type | Which units the other four may use |
| **Inventory unit** | What stock and cost are held in, and quantity precision |
| Sales unit | Default on sales documents |
| Purchase unit | Default on purchase documents |
| Report unit | What stock reports display in |

All four units must belong to the unit type — that is what makes them convertible.

**Choose the inventory unit as the one you actually trade in.** Sugar on kilos resolves to the gram, which is plenty. Gold on kilos at three decimals would round a half-gram sale away entirely, so gold bases on grams. Prices are always stored per the inventory unit, so switching the sales unit from kilos to grams cannot silently multiply a price by a thousand.

A shop selling sugar by the kilo *and* by 250 g needs no special setup: both are Weight units, the counter picks one, and stock stays a single number.

## Costing

Chosen per item, and **fixed once stock has moved** — the layers were never recorded under another method, so switching would restate every posting since.

| Method | Needs |
|---|---|
| Not stocked | Services and non-inventory items |
| Weighted average | Nothing. One cost per item, company-wide |
| FIFO / LIFO | Nothing beyond receipt order |
| FEFO | Batch **and** expiry tracking |
| Specific identification | Serial numbers |

FEFO consumes by earliest **expiry**, not earliest receipt — which is the whole point when a later delivery is shorter-dated than one already on the shelf.

LIFO is not permitted for Indian statutory reporting; it is available for internal analysis only.

## Categories

Three levels deep at most. Each can default an item's **profile**, **costing method** and **unit type** — copied onto the item when it is created, and then the item's own. Changing a category's default never rewrites items already saved.

## Item profiles

The profile decides which extra tab appears and which extension table the item carries.

- **Pharma** — salt, strength, dosage form, pack size, manufacturer, drug schedule, storage, and **minimum expiry on receipt**, which refuses inward stock that is too short-dated to sell. Selecting it presets FEFO with batch and expiry tracking, and marks prices tax-inclusive.
- **Jewellery** — metal, purity, nominal weights, wastage and making charges. Selecting it presets specific identification with serial tracking. The weights here are the **design** values; each physical piece records its own weights and HUID against its serial number.
- **Standard** — everything else.

## Warehouses

Locations for movements and reporting. **Stock is one shared pool** across every warehouse and branch, and weighted average cost is company-wide — warehouses never hold separate stock or separate costs. Per-warehouse quantities come from aggregating movements.

A warehouse in another state carries its own GSTIN, because it changes the place of supply for goods despatched from it. Cold-chain storage is a field rather than a note: breaking it makes the stock unsaleable.

## Metal purities

Gold, silver and platinum purities with their **factor** — the fraction of pure metal, so 22K is 0.9160. It multiplies the pure-metal rate to price a piece, which is why it is frozen once an item uses it.

## Barcodes

An item can carry several: the manufacturer's EAN, a shop-printed label, a pack-level code. Each is unique across the organization, because one scan has to resolve to one item. A pharma pack's **GS1 DataMatrix** carries GTIN, batch and expiry in one symbol, so the scanner parses it rather than matching it whole.
