# Master data

**Status: partial.** The global reference masters are built, as are the first three per-organization ones — chart of accounts, sub-accounts and the tax master. The trading masters (contacts, items, warehouses) are not.

## Built — global reference masters (`mst`)

All are seeded, have **no maintenance screen**, and expose read-only endpoints. New rows arrive by EF migration, because a new code with no logic behind it would be unusable anyway.

| Master | Rows | Endpoint |
|---|---|---|
| Countries | 5 | `GET /api/master/countries` |
| States | 37 Indian states/UTs by GST code | `GET /api/master/countries/{id}/states` |
| Currencies | 5 | `GET /api/master/currencies` |
| TransactionTypes | 16 | `GET /api/master/transaction-types` |
| LedgerTypes | 6 | `GET /api/master/ledger-types` |
| LedgerSources | 15 | `GET /api/master/ledger-sources` |
| AccountTypes | 5 | `GET /api/master/account-types` |
| HSN/SAC codes | 129 seeded headings, rest imported | `GET /api/master/hsn-sac` |
| Permissions | 120 (12 modules × 10 actions) | via roles |

HSN/SAC is the exception to "no maintenance screen" — it has a CSV importer, because the detailed codes change with each CBIC notification. See [HSN & SAC codes](#/hsn-sac).

### Transaction types

Keyed by a **three-letter code**, stored directly on every ledger row so a row reads without a join. `IsLedgerPosting` distinguishes documents that post from those that do not.

`QTE` Quote · `BIL` Bill · `POR` Purchase Order · `GRN` Goods Receipt · `SOR` Sales Order · `INV` Invoice · `CRN` Credit Note · `DBN` Debit Note · `JRN` Journal · `SPM` Spend Money · `RCM` Receive Money · `TRM` Transfer Money · `OPB` Opening Balance · `DEP` Depreciation · `STA` Stock Adjustment · `POS` POS Sale

Quotes and orders are commercial documents only — nothing reaches the ledger until they become an invoice or bill.

> A code can **never** change once data exists. Every ledger, journal and allocation row stores it as a plain string with no foreign key, so there is nothing to cascade a rename through.

### Ledger sources

A payment and a refund share the same transaction type — both are Spend or Receive Money — so the **source** is what tells them apart. Refund reports, GST returns and bank reconciliation all filter on it, not on the transaction type.

Payment and refund are paired in opposite directions (`BILLPAYMENT` out / `BILLREFUND` in) so each pair reconciles against the same document.

## The rename rule for system masters

Seeded masters carry two names:

| Column | Editable | Purpose |
|---|---|---|
| `SystemName` | **No** — hidden | The canonical identity. Code, reports and seeds key on this |
| `DisplayName` | Yes | What the UI shows |

So a customer can relabel "Accountant" as "Finance Lead", or "Cost of Goods Sold" as "Direct Cost", **without changing what the row is or what posts to it**. Applies to account types, roles and tax rates.

## Built — per-organization masters (`acc`)

These live in the shared tenant database, scoped by `CustomerId` and `OrgId`. Each is seeded when an organization is created.

| Master | Rows at creation | Screen |
|---|---|---|
| Chart of accounts (`acc.Accounts`) | 10 control accounts | Accounting → Chart of accounts |
| Sub-accounts (`acc.SubAccounts`) | none — provisioned by their owner | Accounting → Sub-accounts (read-only) |
| Tax master (`acc.TaxMasters`) | 6 GST rates, effective-dated | Settings → Tax |

See [Chart of accounts](#/chart-of-accounts) and [GST & tax](#/gst).

## Designed, not built

- **Contacts**, **Items**, **UOM**, **Warehouse**, **Item Category**

These belong to the Contacts and Inventory services, neither of which exists yet. Until they do, the only sub-accounts in the system are the tax ones — the contact and item provisioning paths are built and tested by nothing.



# Currencies

**Status: built.**

## The master

`mst.Currencies` is the single source for code, symbol and display formatting.

| Column | Purpose |
|---|---|
| `Code` | ISO 4217 |
| `Symbol` | ₹ $ £ |
| `Format` | Display grouping mask |
| `DecimalPlaces` | **Drives rounding, not just display** |
| `SymbolPosition` | Prefix or suffix |

### Why `Format` is a column and not a constant

Western grouping is in threes — `###,###,##0.00` renders `1,234,567.89`. **Indian grouping is lakh/crore** — `##,##,##0.00` renders `12,34,567.89`. A single hard-coded mask would render every rupee amount wrong, so each currency carries its own.

`DecimalPlaces` is separate from the mask because money rounding must never be parsed out of a display string — and it is not always 2. JPY and KRW are 0; KWD and BHD are 3.

## Per-organization activation

An organization transacts in a subset of the ~180 world currencies, held in `mst.OrgCurrencies`.

- The **base currency is enabled and active from organization creation** and **cannot be deactivated** — every posting converts to it, so switching it off would break base-currency amounts on every future transaction.
- The list page shows **active currencies only** by default; a "Show inactive" toggle reveals the rest.
- **Add** offers a dropdown of currencies not yet enabled, and adds the chosen one active.
- Each row has an **active toggle**, disabled on the base currency.

```
GET  /api/organizations/{orgId}/currencies?includeInactive=false
GET  /api/organizations/{orgId}/currencies/available
POST /api/organizations/{orgId}/currencies          { currencyId }
PUT  /api/organizations/{orgId}/currencies/{id}/active   { isActive }
```

Deactivating rather than deleting keeps history intact: a currency that was used last year stops appearing in pickers but its old transactions still resolve their symbol and format.

## Exchange rates

Every transaction stores `CurrencyCode`, `ExchangeRate` and a computed base-currency amount. **The rate is a snapshot at the transaction date and is never looked up live** — otherwise historical documents would silently reprice every time rates moved.

Rate history lives in `rat.CurrencyRates`, dated, not just today's value. *(Not yet built.)*



# HSN & SAC codes

**Status: partial.** The table, the search API, the CSV importer and the **Settings › HSN & SAC codes** screen are built. The seed covers only the chapter and group headings — until the detailed codes are imported, a search for a specific code finds nothing, and the screen says so rather than looking broken.

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

## The screen

**Settings › HSN & SAC codes.** Search by code or description, filter to goods or services, and optionally show chapter headings or retired codes. Paged fifty at a time, with the matched total beside the pager.

**Read-only, deliberately.** This table is in the master database — one set of rows shared by every customer on the platform — so a customer editing it would be editing it for everyone. The list is maintained centrally from the CBIC file; what a business needs from this screen is to find a code and see the rate it usually attracts.

A strip at the top reports how much of the nomenclature is actually loaded. That is there because the alternative is unreadable: with only the headings seeded, every search for a real code returns nothing, and an empty table looks like a broken search rather than data nobody has imported.

## Searching

```
GET /api/master/hsn-sac?search=8471&codeType=HSN&skip=0&take=50
GET /api/master/hsn-sac/chapters
GET /api/master/hsn-sac/summary
```

Matches on code prefix or description text, fifty at a time and capped at 200. The list response carries `total` — counted before paging — alongside the rows, so the screen can say how many matched rather than how many fitted.

`summary` returns how many HSN chapters, HSN codes, SAC groups, SAC codes and retired rows exist. It is what the coverage strip reads.

## Assigning a code to an item

**Items › General** carries the picker. Type two characters or more of a code or its description and pick from the matches; the item's own type decides which half of the nomenclature is searched, so a service is offered SAC codes and goods HSN. The chosen code shows with its description and its usual rate, and **Change** replaces it.

Picking a code with a usual rate **pre-selects** the matching GST rate beside it — but only when no rate has been chosen. The same code attracts a different rate by condition, so a rate someone has already set is a decision, not a default waiting to be corrected. The item's own rate is what reaches an invoice either way.

If a search finds nothing, the field says so and points at Settings › HSN & SAC codes, because the likely reason is that the detailed list has not been imported rather than that the code does not exist.

## Two things still open

**Digit length by turnover.** GST requires 4 digits for B2B below ₹5 crore turnover and 6 above. That is a property of the *organization*, not the code, so it belongs in configuration — a `hsn.digits` key enforced when an item is saved. Not implemented.

**The code must be copied onto the invoice line.** An item's HSN can change, but a filed invoice must keep the code it was filed under, and GSTR-1 needs an HSN summary read from the lines. So the line stores its own copy at posting time, exactly like the exchange rate. That belongs with the invoice tables, which do not exist yet.



# Roles & permissions

**Status: built** (API and screen).

## The model

Three tables: **Roles** → **RolePermissions** → **Permissions**, with **UserOrganizationRoles** assigning a role to a user *per organization*. One login can hold a different role in each organization it can reach.

Permissions are `{module}.{action}` — **12 modules × 10 actions = 120**.

Modules: dashboard, contacts, crm, inventory, sales, purchase, accounting, banking, reports, settings, support, platform
Actions: view, create, edit, approve, void, delete, print, export, import, AllUserData

`AllUserData` is not really an action — it is a **data scope**. Without it a user sees only records they created; with it they see the whole organization's. It is enforced as a query filter, not as a gate on an endpoint, so a permission check alone would not implement it.

`platform.*` is operator-only. It never appears in a customer's permission matrix and cannot be granted to a customer-defined role.

## The five system roles

| Role | Granted |
|---|---|
| **Owner** | Everything except `platform.*` |
| **Administrator** | Everything except `platform.*` |
| **Accountant** | All 10 actions in accounting, banking, reports, purchase — plus read-only contacts and inventory |
| **Sales** | All 10 actions in sales, contacts, crm — plus read-only inventory |
| **Viewer** | Every `.view` permission, nothing else |

Grants are **module-level**: a role that owns a module gets every action in it, including `approve`, `void` and `AllUserData`.

The two read-only additions are there because a role has to look at things it does not own in order to do its own job. Sales cannot sell what it cannot look up, and an accountant values stock and chases receivables that are held per contact. Neither grant allows a write: a salesperson still cannot edit an item, and an accountant still cannot edit a contact. Take them away on a copy of the role if that is not how you work.

## Which permission an endpoint asks for

The module is the one that **owns the data**, not the menu the screen sits under. GST rates and numbering series appear under Settings and belong to Accounting, so they ask for `accounting.*` — an accountant who could not edit a tax rate because of where it is filed would be a menu deciding an access rule.

| Screen | Asks for |
|---|---|
| Chart of accounts, sub-accounts, tax rates, payment terms, numbering series | `accounting.*` |
| Banks, bank accounts | `banking.*` |
| Contacts, contact roles, contact documents | `contacts.*` |
| Items, categories, stock, warehouses, units, purities | `inventory.*` |
| Users, roles, branches, currencies, configuration, email | `settings.*` |

Reading asks for `.view`, changing asks for `.edit`, and deleting asks for `.delete`. Country and state lists, currencies and the HSN/SAC master are reference data read by every module and ask only that you are signed in.

## What you see

The menu shows only what you can open. A role without `inventory.view` has no Inventory entry, and a bookmark or a typed address for one of its screens lands on Home rather than on a page that fails as it loads.

That is presentation, not protection. The permissions are read out of your sign-in token, which lives in your browser, so it is not something to rely on — every request is checked again on the server against a signed copy of the same claims, and that check is the one that decides. Hiding the menu entry is about not offering what you cannot have.

> Worth knowing when you assign these: Accountant and Sales can approve and void documents in their own modules, and can see every user's records there. If you need someone who can enter but not approve, create a customer role rather than using these.

## What a system role allows

| | System role | Customer role |
|---|---|---|
| Rename for display | ✅ | ✅ |
| Edit description | ✅ | ✅ |
| Change permissions | ❌ fixed | ✅ |
| Delete | ❌ never | ✅ soft delete |

Renaming changes the label only. The hidden `SystemName` is the identity that code and reports key on, so calling Accountant "Finance Lead" changes what users see and nothing about what it grants.

Deleting a customer role is a **soft delete** and is refused with `409` while any active user still holds it — a hard delete would orphan those assignments.

## The screen

`Settings → Roles`. The list shows every role with its active user count and permission count, and a System badge where applicable.

The editor renders the 120 permissions as a **module accordion** with select-all per module, since a flat grid of 120 checkboxes is unusable. On a phone it collapses to two columns per module. For a system role the whole matrix renders read-only.

```
GET    /api/roles                 list, system + own
GET    /api/roles/permissions     the matrix, grouped by module
GET    /api/roles/{id}            one role with its permission ids
POST   /api/roles                 create a customer role
PUT    /api/roles/{id}            update
DELETE /api/roles/{id}            soft delete, 409 when in use
```



# Numbering series

Every generated code in the product comes from a numbering series: customer codes, vendor codes, item codes, warehouse codes, and — as Sales and Purchase land — invoice, credit note and receipt numbers.

**Settings › Numbering series**

## What a series is made of

A generated code is assembled from up to four parts, joined by the separator, with the suffix added at the end:

| Part | Example | Notes |
|---|---|---|
| Prefix | `INV` | Free text, up to 15 characters |
| Financial year | `2526` | Optional. Four formats to choose from |
| Branch code | `CHN` | Optional. The branch's own code, so a number names where it was written |
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

You can have more than one series for the same code. One of them is the **default**, which is what a form preselects.

Series belong to a branch, because every branch keeps its own books — so two branches numbering invoices never collide, and neither needs to know the other exists. Turning on **include branch code** puts that branch's own code into the number, so `INV/2526/CHN/00042` says where it was written.

The code is copied onto the series rather than read back from the branch each time, so renaming a branch later cannot restyle numbers already issued.

Every series for the same code shares one rule: exactly one may be the default, and the database enforces it.

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

## Bank details

Where a **vendor is paid**: account holder, account number, IFSC, branch, and a UPI id — often the only detail a small vendor gives you.

These are somebody else's accounts. They are deliberately separate from the organization's own accounts under **Banking**, which are accounts you hold, reconcile against a statement, and which each carry a ledger account. Nothing here posts to the ledger; it is payout information and nothing more.

A contact can hold several accounts with **exactly one default** — the one a payment screen picks without asking. Setting a new default clears the old one, and removing the default hands it to the first account left, so a contact with accounts always has one.

The account holder name is worth typing carefully: banks reject a transfer whose name does not match the account, and the rejection comes back days later.

## Licences

Registrations the contact holds, each with an expiry.

| Type | Who holds it |
|---|---|
| Drug licence | Chemists and distributors — Form 20/21 retail, 20B/21B wholesale |
| FSSAI | Anyone trading food lines and nutraceuticals |
| BIS | Hallmarked jewellery |
| Medical registration | Prescribers, from their state medical council |
| Other | Anything else worth a renewal date |

**The expiry is the point.** Supplying Schedule H stock to a chemist whose drug licence has lapsed is an offence, not an oversight, and nobody checks a date they cannot see. Each licence shows as **valid**, **expiring within 30 days**, or **expired** as soon as you open the tab.

`GET /api/contacts/licences/expiring?withinDays=30` returns everything lapsing across all contacts, ordered soonest first, with negative days for ones already gone. A licence with no expiry date is simply never flagged.

Licences are held here rather than as two columns on the contact because a distributor holds several, with different numbers and different dates.

## Documents

Files against the contact: GST certificate, PAN card, agreement, purchase order, cancelled cheque, MSME certificate, licence scan.

**PDF or image, up to 10 MB.** Both the size cap and the accepted types are configuration, so a deployment that needs 25 MB scans can have them without a code change. The file uploads the moment you choose it — it does not wait for the contact to be saved — which also means the contact has to exist first; the tab says so on a contact you have not saved yet.

Downloads go **through the API**, which checks your organization before handing over a byte, and always as a download rather than something the browser renders in place. Files are stored under a generated key beginning with the organization id, so the tenant boundary is in the storage path as well as in the row that points at it — the name you upload never decides where the file lands.

Removing a document takes it off the list and leaves the stored file alone. A mistaken delete that also destroyed the bytes would be unrecoverable.

An optional expiry date can be set on any document, for agreements and certificates that lapse.

## Trading limits

Three limits, all optional:

- **Credit limit** — the most this contact may owe.
- **Max outstanding days** — how far past **the due date** an invoice may go before new documents are blocked. Counted from the due date, not the invoice date, so a Net 60 customer does not trip a 45-day limit while still perfectly within terms.
- **Max discount %** — the ceiling on a line or document discount.

Breaching one **blocks** the document. A user holding the override permission can push it through, and the override is recorded against the document.

These are enforced when Sales and Purchase are built. Until then they are stored and not applied.

## What happens when you save

A contact gets **two sub-accounts** in the ledger — one receivable, one payable — created by Accounting. They are what let the ledger report per contact without the chart of accounts growing a line per customer.

Accounting is a separate service, so that step can fail on its own. When it does the contact is still saved — undoing it after Accounting may already have written its rows would be the worse outcome — but it is marked **No sub-ledger** on the list and cannot be invoiced or billed until it has one.

**Link sub-ledger** on the row creates them. It is safe to press twice.

## Deactivating

Contacts are never deleted. Documents point at them and their sub-accounts hold ledger history. Deactivating takes them out of the pickers and leaves everything else intact.



# Contact person roles

What a person at a contact does — Accounts, Purchase, Dispatch.

**Settings › Contact person roles**, and the same list as a popup on the contact form.

## Why it is a master and not a fixed list

Every trade names these differently. A pharmacy has a purchase contact and a drug licence holder; a jeweller has a karigar and a showroom manager. A fixed list would be wrong for most customers on the day they signed up, so the list is theirs to edit.

Seeded roles arrive with each branch. They can be **renamed but never deleted**, because contacts already point at them and a role that vanishes takes the meaning of those rows with it.

## Order is the dropdown's order

Drag to reorder. The order here is the order the role dropdown offers on a contact person, so the two or three used constantly can sit at the top instead of being hunted for alphabetically.

One role can be the **default**, preselected on a new contact-person row. Zero is also fine.

## Deleting, and the alternative

**A role people already hold cannot be deleted.** Make it inactive instead: existing contacts keep it and their history still reads correctly, while nobody can choose it again. The count of who holds it is shown beside each row so the refusal is never a surprise.

## Two places, one list

The same master appears twice on purpose:

- **Settings › Contact person roles** — where anybody setting a branch up will look for it, beside payment terms and the other masters.
- **A popup on the contact form** — so somebody halfway through typing a contact can add a missing role without abandoning what they have keyed.

Both render the same component. There is one list and one place to change how it behaves; two copies would drift, and the popup is the one that would be forgotten.



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

Locations for movements and reporting. **Stock is one shared pool across every warehouse in the branch**, with one weighted average cost — warehouses never hold separate stock or separate costs. Per-warehouse quantities come from aggregating movements. Another branch's stock is a different pool entirely, because a branch is a separate set of books.

A warehouse in another state carries its own GSTIN, because it changes the place of supply for goods despatched from it. Cold-chain storage is a field rather than a note: breaking it makes the stock unsaleable.

## Metal purities

Gold, silver and platinum purities with their **factor** — the fraction of pure metal, so 22K is 0.9160. It multiplies the pure-metal rate to price a piece, which is why it is frozen once an item uses it.

## Barcodes

An item can carry several: the manufacturer's EAN, a shop-printed label, a pack-level code. Each is unique across the organization, because one scan has to resolve to one item. A pharma pack's **GS1 DataMatrix** carries GTIN, batch and expiry in one symbol, so the scanner parses it rather than matching it whole.



# Stock

How much of each item you hold, what it cost, and everything that moved it.

**Inventory › Stock**

## One pool per branch

**There is one quantity per item within a branch, shared across every warehouse in it.** Not one per warehouse.

A warehouse records *where* a movement happened. It does not hold a balance of its own. Two counters in the same shop draw down one number.

This is a decision, not a simplification. Split the quantity per warehouse and the same item ends up carrying two different weighted average costs, valuation stops agreeing with the ledger, and every report has to say which location it means before it can say anything else.

Per-warehouse quantities, when they are needed, come from adding up the movements — not from a second balance that can drift from the first.

**Branches do not share stock.** A branch is a separate set of books with its own items and its own quantities, so there is nothing to reconcile between them — the organization boundary already keeps them apart, and no code has to remember to.

## Weighted average cost

One cost per item in the branch, moved only by what you buy:

```
newAverage = (oldQty × oldAverage + receivedQty × receivedCost) ÷ (oldQty + receivedQty)
```

Everything else leaves it alone:

| Movement | Quantity | Average cost |
|---|---|---|
| Opening balance | + | sets it |
| Receipt | + | recalculated |
| Issue (a sale) | − | unchanged |
| Sales return | + | unchanged — it comes back at what it left at |
| Purchase return | − | unchanged |
| Adjustment | ± | unchanged — a count is not a purchase |
| Transfer in / out | no change | unchanged |

An issue moves quantity only. That is what makes gross profit meaningful: the cost of what was sold is settled when it was bought, not when it was sold.

Anything that brings stock in and sets the cost — an opening balance or a receipt — **requires a unit cost**. Receiving without one would drag the average toward zero and quietly understate the cost of every sale after it.

## Entering quantities in any unit

Every movement stores the quantity **twice**: as you entered it, in the unit you entered it in, and again converted into the item's inventory unit.

Receive two 50 kg bags, issue 300 grams, and stock reads as one figure — 99.7 kg — because both were converted through the item's unit type on the way in. The **conversion factor is stored on the movement**, not looked up later. If a unit's factor is ever corrected, movements already recorded keep the factor they were written under; re-deriving it would silently restate history.

The unit you enter in has to belong to the item's unit type. Anything else has no factor to convert through, and is refused rather than guessed at.

## Selling the last unit

Stock comes down through a single conditional statement:

```sql
UPDATE ItemStock SET QuantityOnHand = QuantityOnHand - @qty
WHERE ItemId = @id AND QuantityOnHand - QuantityReserved >= @qty
```

If it changes no rows, there was not enough and **nothing** changed. There is no read followed by a write, because that is the gap where two tills both see the last unit and both sell it.

The decrement is also **synchronous**. Costing, accounting and notifications all happen afterwards and can be retried; the quantity cannot wait, because by the time a queued message is processed the second customer has already been served.

## Reserved and available

Stock promised to a confirmed order that has not shipped yet is **reserved**.

| | What it means |
|---|---|
| **On hand** | What is physically in the branch |
| **Reserved** | Of that, what is already promised |
| **Available** | On hand less reserved — what a new order may draw on |

**Reserving never moves on-hand.** The stock is still on the shelf and still worth what it cost, so a stock count, a valuation and the inventory account all keep reading on hand and none of them change when a reservation is made. Only "can I sell this" changes, and that is what reads available.

Available is not stored. It is on hand minus reserved, worked out wherever it is needed — a third column could disagree with the two it comes from, and there would be no way to tell which was right.

Reserving and releasing go through the same conditional statement selling does, so a reserve is refused when there is not enough available rather than overdrawing it, and a release is refused when there is nothing reserved to release. Two things depend on that: a release that ran twice would drive the reserve negative and quietly free stock nobody released, and the database refuses that outright — reserved can never be below zero, nor above on hand.

**Issuing reserved stock is release-then-issue, in that order and inside one transaction.** Issue first and the item's own reservation is still counted against it, so an order can be refused the stock it is holding.

Nothing reserves anything yet — Sales is what will. Until then every item reads a reserved of nothing and an available equal to its on hand.

## Reaching the accounts

A movement changes what the branch owns, so it also has to reach the general ledger. A sale of stock that cost ₹300 posts:

```
Dr  Cost of Goods Sold   300
    Cr  Inventory              300
```

**This posting is the only reason gross profit exists.** Revenue is Income and cost of goods sold is Expense, and a report can subtract one from the other only because they are separate account types. Without it, the inventory asset would fall with nothing recording what the stock cost.

What each movement means:

| Movement | Debit | Credit |
|---|---|---|
| Sale, write-off, shrinkage | Cost of Goods Sold | Inventory |
| Sales return, count correction upward | Inventory | Cost of Goods Sold |
| Opening balance, receipt with no document | Inventory | Opening Balance Equity |
| Transfer between warehouses | *nothing* | |
| Receipt or return against a purchase | *nothing — the document posts it* | |

Two of those rows are deliberate absences. A **transfer** changes where stock is, not what the branch owns — the pool was never split, so there is nothing to move between accounts. And goods received **against a purchase document** are posted by that document, whose other leg is Accounts Payable: only Purchase knows the vendor and how much of the amount was tax, and posting the stock half here as well would double the inventory asset.

A **receipt with no document behind it** is the business asserting stock it holds, which is what an opening balance is, so it lands the same way. Purchase supersedes that the day it arrives.

**Posting is asynchronous, and one step behind costing.** It cannot happen until what the movement cost has been settled, and it does not happen inside the costing transaction — tying a stock movement's fate to the accounts service being reachable at that instant would either roll back a settled cost or lose the posting it owed. So the movement carries a posting status of its own, shown beside each row in the history, and the queue is again the movements table.

A movement that **cannot** be posted says so rather than going quiet. While anything is in that state, stock and the general ledger disagree, and the stock screen says exactly that. The usual cause is a control account missing from the chart of accounts.

**A restated cost reposts itself.** When a backdated receipt changes what an earlier sale cost, that sale goes back into both queues and posts again — [replacing its ledger rows](#/ledger) rather than adding a second entry.

## The movement history

Append-only. Rows are never edited or deleted — a mistake is corrected by a movement in the opposite direction, the same way a posted journal is reversed rather than changed.

Each row keeps the average cost as it stood immediately afterwards, so a disagreement about valuation can be walked back through the receipts that caused it.

Movements that come from a document carry its type and id. That pair is **unique**, which is what stops a redelivered message moving stock twice — the message bus guarantees at-least-once delivery, so a duplicate has to be refused by the database rather than trusted not to arrive.

## Transfers

Warehouse to warehouse writes two movements — one out, one in — and changes the pool by nothing at all, because the pool was never split.

It is still refused when the source warehouse holds less than the quantity: a location cannot ship what it does not have, even though the company total is unaffected.

## What freezes once stock moves

The moment an item has a single movement, five things on it are fixed:

- Unit type
- Inventory unit
- Costing method
- Item profile
- Batch, expiry and serial tracking

Every quantity and cost recorded so far was written under them. Changing the inventory unit from kilos to grams would not reinterpret the history — it would corrupt it, silently, by a factor of a thousand.

Everything else on the item — name, prices, category, reorder levels — stays editable.

## Batches

A **lot** of one item received together: same run, same expiry, usually the same printed MRP.

Receiving against a batch-tracked item asks for the batch number. An existing lot is reused; a new one is created. Expiry-tracked items must give an expiry on a new lot — without it nothing can decide which lot goes out first.

A batch carries its **own MRP**, which wins over the item's. A price rise reprints the pack, and the older lot has to keep selling at what is printed on it.

## Serial numbers

For items tracked piece by piece. Enter one serial per unit — the count has to match the quantity exactly, or pieces go untracked and the count stops agreeing with the stock.

Jewellery pieces carry a **HUID** alongside the serial: the six-character BIS hallmark id. It sits on the piece, never on the item, because two rings of the same design carry two different HUIDs. Each is unique across the branch.

On the way out, the serials you name are the pieces that left — and on a specific-identification item, they are also what decides the cost.

## Cost layers

Every receipt writes a **layer**: how much came in, what it cost, and how much of it is left. What differs by costing method is what draws them down.

| Method | Takes from |
|---|---|
| Weighted average | Nothing — one running average, layers kept as history |
| FIFO | The oldest receipt first |
| LIFO | The newest receipt first |
| FEFO | Whatever expires soonest; no expiry sorts last |
| Specific identification | The layer the named piece arrived on |

An issue records **which layers it consumed, and how much from each**. An issue of 30 against three layers writes three rows, and their costs sum to its cost of goods sold. The **Layers** button on the movement history shows exactly that.

This is what makes a disputed margin answerable: the cost of a sale can be walked back to the purchases it came from, receipt by receipt.

Layers come down the same way stock does — a guarded statement, never a read followed by a write, so two sales cannot take the same last unit of a layer.

## When the cost is settled

**The quantity moves inside the request. What it cost is settled a moment later**, by the costing engine.

That split is deliberate. A till cannot wait for layer arithmetic to finish, and it must never sell the last unit twice — so the decrement is synchronous and everything downstream is not. A movement carries its costing state, so an unsettled cost reads as *"costing…"* rather than as zero.

Batch numbers and serial numbers are still checked **in the request**. Both are things a person typed: a batch that does not exist, or a serial that is not on the shelf, is the caller's mistake and is answered as one rather than surfacing later as a background failure nobody is watching.

Asynchronous costing brings two hard problems, and the engine solves both by **using the movements table as the queue** rather than a message broker:

- **Order.** FIFO gives the wrong answer if movements are costed out of sequence. Work is read in `(item, date, id)` order and processed one at a time per item, so the order is a property of the read rather than something a broker has to promise.
- **Exactly once.** A movement is claimed by a guarded status change from *Pending* to *In progress*. Two engines racing means one of them changes no rows and moves on. There is no redelivery to guard against, because there is no delivery.

A restart loses nothing: a movement's state is a row, not a message in flight. Claims left behind by a crashed engine are reclaimed after a timeout.

A movement that keeps failing stops after a set number of attempts and is marked **Failed**, with the reason on the row. It does not retry forever, because a cost that cannot settle needs a person, and an infinite retry hides that. The stock screen says so at the top.

## Returns

A sales return should name **the sale it reverses**. Naming it puts the stock back on the layers it left from, at the cost it left at — so buy, sell, return leaves stock value exactly where it started.

Left unlinked, the return re-enters at the current running average instead. On a layered item that quietly changes what the stock is worth even though nothing was bought or sold on net, so the field is worth filling in.

Partial returns are fine, and are given back oldest allocation first. Two partial returns of the same sale cannot together give back more than went out, and no layer can ever hold more than it originally received — the database refuses both.

## Backdated receipts

A receipt entered late, dated before sales that have already happened, is a problem: under FIFO that stock **should** have gone out first, and it did not. The sales after it were costed against the wrong layers.

Recording such a receipt **restates them automatically**. The affected sales are unwound, their quantity is given back to the layers it came from, and they go back into the costing queue — where the engine replays them in date order like any other pending work, rather than through a second, different code path.

Nothing is deleted. The old allocations keep their rows, marked as superseded — a restated cost is only defensible if what it replaced is still readable. And **quantities never change**: what moved, moved. Only what it cost changes.

Each restatement is recorded as its own row: which sale, what it cost before, what it costs now, the difference, and which receipt caused it. They appear under **Costs restated** on the item's movement history.

Under specific identification there is nothing to re-select — the same pieces went out, so the same layers are consumed, and only their costs can have moved.

## What is not here yet

- **The restatement does not post to the ledger.** The adjustment is recorded and visible, but the matching `Dr/Cr COGS` journal is Accounting's to write from it, and Inventory and Accounting are not yet connected. Until they are, a restated cost shows here and not in the accounts.
- **Nothing posts to the ledger on an ordinary sale either.** An issue computes its cost of goods sold and stops — `Dr COGS / Cr Inventory` is the same missing connection.



# Stock adjustments

Correcting what you hold, as a document rather than one movement at a time.

**Inventory › Stock adjustments**

## Why a sheet and not a movement

Stock could already be corrected one movement at a time from the [stock screen](#/stock), and for a single breakage that is still the quickest thing to do.

A physical count is not that. Counting twenty items is **one event**: it happened on one date, for one reason, and one person authorised the whole of it. Recorded as twenty loose movements it keeps the quantities and loses every one of those facts — and the accounts show twenty unexplained adjustments where there was one count.

So a sheet is a document: it has a number, a date, a reason, and lines.

## Write-off or count

The two differ in what you type, not in what is posted.

| | You key | The system works out |
|---|---|---|
| **Write-off** | how much to remove or add back | nothing |
| **Physical count** | what was actually on the shelf | the difference against the books |

On a count, **lines that agree are dropped rather than posted as zero**. On a real count sheet most lines agree; that is the good news, not twenty rows of nothing.

A count also stores what the system believed **at the moment of counting**, beside what you counted. That is what lets the arithmetic be re-checked six months later — against the figure that was actually being disputed, rather than one that has moved since.

## Draft, then post

A draft holds **no number** and has moved **no stock**. Edit it, add to it, throw it away; nothing has happened.

Posting does both at once: the stock moves and the number is taken. The number comes from the `ADJ` series and is taken **at post, never at draft**, because a number taken when a form opens is a number lost every time somebody changes their mind — and a document series with a hole in it is what an auditor asks about.

**The whole sheet or none of it.** If any line cannot post — writing off more than is on hand, most often — nothing on the sheet posts, and it stays a draft with no number spent. A half-posted count is worse than one that did not post, because only the second is obvious.

## Who authorised it

There is no separate approver field, and that is deliberate: **whoever posted it is the approver**. The sheet already records who created it and who posted it, so a count keyed by one person and posted by another shows exactly that — which is the segregation of duties an adjustment needs. A third column repeating one of the first two would only be a second place to disagree.

## Reversing, because there is no void

A posted money document can be voided: its ledger rows are withdrawn and nothing else ever happened. **An adjustment cannot**, because the stock physically moved. Undoing it means moving the stock back, and the movement history is append-only — a mistake is corrected by a movement the other way, never by deleting the first.

So reversing writes a **mirror sheet**: every line the other way round, posted as its own document, dated today rather than back on the original's date. Both documents keep their numbers and each points at the other. The pair is a better record than a void would have left.

A sheet can only be reversed once. Stock coming back in returns at the running average — the reversal names no cost of its own, because it is putting back what the original took.

## What it does to the accounts

Each line records an ordinary [stock movement](#/stock) carrying the sheet's id, and the movement-to-ledger mapping files a movement under its document. So a twenty-line count produces twenty movements and **one adjustment in the general ledger**.

Stock written off debits Cost of Goods Sold and credits Inventory; stock found does the reverse. The value is settled by the costing engine a moment behind, so a sheet reads *Costing…* until it has.

## What is not here yet

- **No approval step before posting.** Anyone who may post, may post. Workflow approvals are a later phase.
- **Serial-tracked items** cannot be adjusted from a sheet, because serials are keyed per unit and the sheet has no place to key them. Use the stock screen for those.



# Payment terms

Credit terms — Net 30, Due on Receipt, End of Month. Set once, then picked on a contact and carried onto every document for them.

**Settings › Payment terms**

## The four rules

A term turns a document date into a due date. Which of the fields below matters depends on the rule you pick.

| Rule | Due date | Example, for a bill dated 18 August |
|---|---|---|
| Due on receipt | The document date | 18 August |
| A number of days after the invoice | Document date + days | Net 30 → 17 September |
| End of the invoice month | Last day of that month, plus any extra days | 31 August |
| A day of the following month | That day next month | Day 10 → 10 September |

**A day past the end of a short month falls on its last day.** Day 31 in February becomes the 28th, or the 29th in a leap year — it never rolls into March.

The list shows the due date each term would produce for a bill dated today. "Net 30" needs no explanation, but "end of month plus 15" does, and a worked example settles it faster than any label.

## Early-payment discounts

The classic "2/10 net 30" — 2% off if paid within 10 days, otherwise the full amount at 30 days — is two fields: **discount %** and **paid within (days)**.

The discount window cannot run past the due date. A discount earned after payment was already due could never be taken, so it is refused rather than saved as something that looks configured but never fires.

## Where a term can be used

Each term is marked as available on sales documents, purchase documents, or both. A term available on neither is refused — it would be data nothing could ever reference.

One term is the **default**, preselected on new contacts. Exactly one, enforced by the database.

## Built-in terms

Six are created with every organization: Due on Receipt (the default), Net 15, Net 30, Net 45, Net 60 and End of Month.

You can **rename** them and change where they are used. You cannot change their **rule** — contacts and unpaid documents already point at them, and moving Net 30 to Net 90 would silently restate due dates on invoices already issued. Add a new term instead.

## Deactivating

A term is never deleted. Contacts point at it, unpaid documents were dated by it, and the row is how a historical due date is explained. Deactivating takes it out of the pickers and leaves everything already using it alone.

## Where the calculation lives

Sales and Purchase do not each work out due dates themselves. They ask Accounting, which owns this master, over `GET /api/payment-terms/{id}/due-date`. Two implementations would eventually disagree about what End of Month means, and the disagreement would only surface as a customer arguing about a due date.



# Closing dates

How far back the books are closed.

**Settings › Closing dates**

## Per role, not per branch

There is one closing date **per role**, and that is the whole point of the screen.

A close is rarely all-or-nothing. The month shuts to everyone who keys documents, and stays open to whoever has to make the adjusting entries the close itself produces — the accruals, the depreciation, the stock revaluation. A single date for the branch would close the books on the person doing the closing.

So: shut Sales and Viewer to 31 March, leave Accountant open, and the work can finish.

## What the date means

**Inclusive.** Closed up to 31 March means nothing posts *on or before* 31 March.

**A role with no date is not closed at all.** Absence of configuration is not a restriction — a branch that has never closed a period has no rows here and everyone posts freely. That is the right default, because the alternative locks a product nobody has set up yet.

## What is actually refused

Only what **reaches or leaves the ledger**: posting, reversing, voiding. Never the keying.

A draft touches nothing, so a draft can be written and edited with any date at all — it simply cannot be *posted* into a closed period. Somebody catching up on last month's paperwork can still type it and finds out when they post, rather than being stopped at the first field with no explanation.

## Reopening

Moving a date **earlier** reopens a period that was closed, and the screen says so before you save it. That is the one change here worth stopping to think about: figures somebody has already reported on become editable again.

**Reopen all** removes the role's date entirely, which opens every period for it.

## Two things worth knowing

**You can close your own role.** If your role's date covers today, you will not be able to post either — including the adjusting entries you may have opened the screen to make. The banner at the top shows the date that currently applies to you.

**A caller whose role cannot be identified gets the branch's strictest date, not none.** That happens with a token minted before roles were recorded on it. Falling open would let a stale token post into a closed period for as long as it lived; falling to the strictest date is safe and still lets ordinary work through, because most branches close every role to the same day anyway.

## Why the note matters

The reason is read far more often than it is written. "Year end signed off by the auditor" answers the question somebody will ask in nine months, when they find they cannot post to a date and nobody remembers deciding it.



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

`DataType` (Number / Text / Boolean / Date / Json) tells the screen which input to render and the reader which cast to apply, so callers never parse strings by hand.

> Unit price and quantity have **separate** decimal settings on purpose. A unit price often needs more precision than the money total — selling at ₹12.4567 per unit while the line total rounds to 2 decimal places. One shared setting would force them equal. Money precision is different again: it comes from the **currency**, not from here, because it is a property of the currency (JPY 0, INR 2, KWD 3).

## The screen

`Settings → Configuration`. Keys are grouped by category, each showing its effective value, an "overridden" badge when it differs from the default, and a reset button labelled with the default value. Editing saves on blur.

```
GET    /api/organizations/{orgId}/configurations
PUT    /api/organizations/{orgId}/configurations/{code}   { value }
DELETE /api/organizations/{orgId}/configurations/{code}   clears the override
```

## Not here: the three document settings

Whether a line may stand without an item, where a discount is keyed, and whether a discount reduces tax are **not** configuration keys. They live on **Settings › Organization**, beside the base currency and the financial-year start, because they are structural decisions about how this branch's documents are built rather than values that can be tuned.

They are frozen the same way the base currency is: editable until the branch posts its first sales or purchase document, fixed after that.



