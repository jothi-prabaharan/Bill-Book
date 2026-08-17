# Reports

**One grid, forty-six reports.** This file is the whole of the reporting requirement: the common grid every report renders in, the query contract behind it, the `rpt` schema, and the catalog of reports with their columns.

Nothing in here is built yet. `Reporting.Api` is a scaffold whose `Program.cs` returns `{"service":"Reporting","status":"not implemented"}`, `libs/reporting/*` hold a `.gitkeep` each, and there is no `rpt` migration. This is the design that gets agreed **before** any of that changes.

Read [`CLAUDE.md`](../../CLAUDE.md) first — the hard rules there apply here without exception, and one of them (rule 8, service boundaries) needs a decision this document raises rather than assumes.

---

## 1. Decisions already taken

Four questions were settled before this was written. They are recorded here because each one closes off a large branch of design, and re-opening one means re-reading everything below it.

| Question | Decision | What it buys |
|---|---|---|
| What renders the grid | **In-house `bb-report-grid`**, in `libs/shared/ui-components`, built on Angular CDK | No licence, no vendor bundle, and the ~360px card-list mode the house rule demands. Matches `bb-document-line-grid`, which is hand-rolled for the same reasons |
| Where filter / sort / group / pivot / page execute | **Server-side, always** | Account Transaction and Journal Report reach millions of rows in a live branch. One code path, one place `OrgId` is enforced, one place a total is computed |
| How PDF and Excel are produced | **Server-side, over the full result set** — XLSX via `DocumentFormat.OpenXml`, PDF via the Syncfusion .NET path | The export is the same query without paging, so it cannot disagree with the screen. A 200k-row export is possible at all |
| Whether layouts persist | **Yes — `rpt.ReportDetails`** | Reports here carry 8 to 40 columns. Without a saved layout every user re-picks them on every visit |

The grid is therefore **dumb by design**: it holds no data, fetches nothing, and computes no total. It receives a column definition, a query state and a page of rows, and it emits a new query state. Exactly the contract `bb-document-line-grid` already works to.

---

## 2. The one decision still open

**Reporting has to read `acc`, `inv`, `sal`, `pur` and `con`. Hard rule 8 says a service never crosses a boundary by referencing another service's `DbContext`.**

A report engine cannot ask for its data over HTTP. Account Transaction joins ledger rows to accounts, sub-accounts, contacts and tax masters and then pages the result; done across five API calls it is a hundred round trips and no server-side paging is possible. So the rule has to bend somewhere, and there are only three places it can:

1. **`ReportingDbContext` maps its own read-only entities over the other schemas' tables, with `ExcludeFromMigrations`.** Reporting owns no migration for them, never writes to them, and re-declares each one's `OrgId` query filter. This is the same shape as `NumberingSeries`, which three services already map for a reason the project decided was worth it. **Recommended.**
2. **Postgres views, one per report, owned by each service.** Rejected: creating a view is raw SQL, and rule 1 permits raw SQL only for `CREATE DATABASE`, RLS, triggers and `set_config`.
3. **Each service exposes an `internal/` reporting endpoint and Reporting composes.** Rejected: no server-side paging, no cross-schema join, no realistic performance.

Option 1 costs a real thing and it should be said plainly: a column renamed in `sal` breaks Reporting at compile time rather than at run time, which is the good half — but it does couple the two, and every schema change now has a second place to look. **Confirm this before Stage R0 starts.**

---

## 3. The common grid — `bb-report-grid`

### 3.1 What it must do

Nine capabilities were asked for. Each is spelled out here as the acceptance condition, not as a feature name.

**Filter.** Per-column, typed to the column: text gets contains / starts with / equals, numbers and dates get the comparison operators plus *between*, enums and lookups get a multi-select *in*, booleans get a tri-state. Filters combine with AND. A filter chip row sits above the grid showing what is applied, and each chip clears on its own. Report-level parameters (date range, branch, as-at date) are **not** column filters — they are a separate parameter bar the host page renders, because they change what the query means rather than which rows survive it.

**Order.** Click a header to sort, click again to reverse, click a third time to clear. Shift-click adds a second and third sort key, and the header shows its position in the order. Multi-column sort is not optional: Aged Receivables sorts by contact then by invoice date, and one key cannot say that.

**Column select.** A chooser listing every column the report defines, with the selected ones in their display order and reorderable by drag. Columns the report marks `IsDefault` are the initial set. Search inside the chooser, because Fixed Assets Schedule offers 33 columns and nobody scrolls a list that long looking for *Residual Value*.

**Group.** Drag a column to a group panel, or pick it from the chooser. Groups nest to three levels, each with a collapsible header row carrying the group's key, its row count, and a subtotal for every numeric column. Collapse and expand state is client-side; the aggregates are not — the server returns group footers with the page, so a subtotal is right even when the group spans pages.

**Pivot.** Rows, columns and values, each a list of report columns; values carry an aggregate (sum, count, min, max, avg). The server computes the matrix and returns it with a **declared column set**, because pivot columns are data-dependent — a pivot of sales by month has as many columns as there are months with data. The grid renders whatever columns the response declares and never invents one.

**Export — PDF and Excel.** Both re-run the query with the current filters, sort, grouping, pivot and column selection, and **without paging**. XLSX carries a frozen header row, column widths, number formats matching the column type, and group subtotals as real rows. PDF carries the report title, the parameter summary, the branch name, the generated timestamp and page numbers, and repeats the header on every page. A row cap applies (§5.6) and the user is told before a capped export runs, not after.

**Pagination.** Server-side, page sizes 25 / 50 / 100 / 200, with first / previous / next / last and a "showing 1–50 of 12,480". The count is a second query and is the expensive half, so it is returned only on the first page of a query and cached in the client's state until a filter changes.

**Fixed header rows.** The header is always sticky. Where a report has a second header band — pivot column groups — both bands stick. This is `position: sticky` on `<th>`, not a cloned table.

**Fixed first column, optional.** Off by default, on per report or per saved view, and extensible to *N* leading columns rather than exactly one, because Batch Tracking Detail wants Item Code and Batch No both frozen. Frozen columns are `position: sticky; left: …` with a computed offset and a shadow marking the seam.

### 3.2 What it deliberately does not do

- **No HTTP.** The host page or `reporting-core` fetches; the grid receives.
- **No formatting policy of its own.** How money renders comes from the column's type and the branch's currency, both supplied.
- **No aggregate arithmetic.** Every subtotal and grand total arrives from the server. A total computed twice is a total that can differ.
- **No inline editing.** Reports are read-only. Row activation emits an event and the host decides whether that opens a document.

### 3.3 Component API

Sketch, not signature — this is what the component needs, and the exact names get fixed when it is written.

```
bb-report-grid
  inputs
    definition   ReportDefinition      column metadata: key, header, type, alignment, width,
                                        isDefault, isFilterable, isSortable, isGroupable,
                                        isPivotable, aggregate, format
    state        ReportQueryState      columns, filters, sorts, groupBy, pivot, page, freeze
    result       ReportResult | null    rows, groupFooters, grandTotal, declaredColumns, page
    busy         boolean
    currency     CurrencyContext        base code + decimals, for %CurCode% substitution
    freezeHeader boolean = true
    freezeColumns number = 0
  outputs
    stateChange  ReportQueryState       any interaction — the host re-queries
    export       ExportFormat           pdf | xlsx — the host calls the export endpoint
    rowActivate  ReportRow              double-click / Enter on a row
```

One input carries the whole query state and one output replaces it. The host owns the state, so the URL, a saved view and the browser back button all work by setting the same object.

### 3.4 Files

```
libs/shared/ui-components/src/lib/report-grid/
  report-grid.component.ts|html|scss     the grid itself
  report-column.model.ts                 ReportDefinition, ReportColumn, ColumnDataType
  report-query.model.ts                  ReportQueryState, filters, sorts, pivot spec
  report-result.model.ts                 ReportResult, ReportRow, group footers, totals
  column-chooser.dialog.ts|html|scss     select + reorder + search
  filter-bar.component.ts|html|scss      chips, and the per-type filter editors
  group-panel.component.ts|html|scss     drop target, nesting, clear
  pivot-panel.component.ts|html|scss     rows / columns / values with aggregates
  report-pager.component.ts|html|scss    page size, navigation, the count

libs/reporting/reporting-core/src/lib/
  report-catalog.service.ts              GET /api/reports, GET /api/reports/{key}
  report-query.service.ts                POST query, POST export
  saved-view.service.ts                  rpt.ReportDetails CRUD
  report-state.ts                        state ↔ URL serialization
  models/                                mirrors of the server contracts

libs/reporting/reporting-ui/src/lib/
  report-list/report-list.page.ts        the catalog screen, grouped by module
  report-host/report-host.page.ts        one generic page driven by :reportKey
  saved-views/saved-view.dialog.ts       save, rename, set default, share to branch
```

**One host page serves every report.** A report is data — a key, a column list, a parameter set — so forty-six reports do not mean forty-six pages. A report that needs something the generic host cannot express gets its own page and still hosts the same grid.

### 3.5 At 360px

The house rule holds: the grid becomes a card per row. The card shows the columns the definition marks `IsPrimary` as a title line and the rest as label/value pairs, group headers become section dividers, the filter bar becomes a full-screen sheet, and pagination becomes previous/next only. Pivot is **not** offered below the tablet breakpoint — a matrix has no card form — and the grid says so rather than rendering something unusable.

---

## 4. The query contract

### 4.1 Endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/reports` | The catalog: key, title, module, description, whether the caller may run it |
| `GET` | `/api/reports/{key}` | One report's parameters and full column metadata |
| `POST` | `/api/reports/{key}/query` | Run it. Body is the query state; response is one page |
| `POST` | `/api/reports/{key}/export?format=pdf\|xlsx` | Same body, no paging, returns a file |
| `GET` | `/api/reports/{key}/views` | Saved layouts visible to the caller |
| `POST` / `PUT` / `DELETE` | `/api/reports/{key}/views[/{id}]` | Manage them |

`POST` for a read is deliberate. A filter set with an `in` list of two hundred item ids does not fit a query string, and a URL that long is refused by proxies before it reaches YARP. The action is idempotent and permissioned as a read.

### 4.2 Request

```jsonc
{
  "parameters": { "from": "2026-04-01", "to": "2026-06-30", "asAt": null,
                  "warehouseId": null, "includeZeroBalances": false },
  "columns":  ["date", "accountCode", "account", "debit", "credit"],
  "filters":  [ { "column": "accountType", "operator": "In", "values": [1, 5] },
                { "column": "debit", "operator": "GreaterThan", "value": 0 } ],
  "sorts":    [ { "column": "date", "direction": "Asc" },
                { "column": "accountCode", "direction": "Asc" } ],
  "groupBy":  ["accountType"],
  "pivot":    null,
  "page":     { "number": 1, "size": 50, "includeCount": true },
  "freeze":   { "header": true, "columns": 1 }
}
```

`parameters` is report-specific and declared by the report's metadata; everything else is generic. When `pivot` is present, `groupBy` and `columns` are ignored — a pivot declares its own shape.

### 4.3 Response

```jsonc
{
  "reportKey": "account-movement",
  "generatedAt": "2026-08-17T09:14:22Z",
  "currency": { "code": "INR", "decimals": 2 },
  "columns": [ { "key": "date", "header": "Date", "type": "Date", "align": "left" } ],
  "rows":    [ { "date": "2026-04-02", "accountCode": "1100", "debit": 15000.00 } ],
  "groupFooters": [ { "path": ["Asset"], "rowCount": 214,
                      "aggregates": { "debit": 981200.00, "credit": 44100.00 } } ],
  "grandTotal":   { "rowCount": 12480, "aggregates": { "debit": 0, "credit": 0 } },
  "page": { "number": 1, "size": 50, "totalRows": 12480, "totalPages": 250 },
  "truncated": false
}
```

`columns` is echoed rather than assumed, because a pivot's columns are only known after the query runs. `grandTotal` spans the **whole result**, not the page — a page total is a number nobody wants.

### 4.4 Enums

All of these are C# enums, per hard rule 7, and are serialized by name.

- `FilterOperator` — `Equals`, `NotEquals`, `Contains`, `NotContains`, `StartsWith`, `EndsWith`, `GreaterThan`, `GreaterOrEqual`, `LessThan`, `LessOrEqual`, `Between`, `In`, `NotIn`, `IsNull`, `IsNotNull`
- `SortDirection` — `Asc`, `Desc`
- `AggregateFunction` — `None`, `Sum`, `Count`, `CountDistinct`, `Min`, `Max`, `Avg`
- `ColumnDataType` — `Text`, `Number`, `Money`, `Quantity`, `Percent`, `Rate`, `Date`, `DateTime`, `Boolean`, `Enum`, `Link`
- `ExportFormat` — `Pdf`, `Xlsx`
- `ReportModule` — `Accounting`, `Inventory`, `Sales`, `Purchase`, `FixedAssets`, `Banking`

### 4.5 Permissions and errors

Every route sits behind a token and `[RequireModulePermission("reporting")]`. A report additionally declares the module permission its data belongs to — Account Transaction needs `accounting.view`, Inventory Item Detail needs `inventory.view` — so a Sales user does not read the general ledger through the report engine. A report the caller may not run is **absent from the catalog** and returns `Forbid()` if requested by key.

`OrgId` comes from the claims and nowhere else. There is no `orgId` parameter on any report route; a consolidated multi-branch report, when it is designed, is a deliberate read above the query filter and is not any of the forty-six here.

---

## 5. The server engine

### 5.1 A report is a source plus metadata

```
IReportSource
  ReportKey, Module, Title, RequiredPermission
  IReadOnlyList<ReportColumn> Columns
  IReadOnlyList<ReportParameter> Parameters
  IQueryable<TRow> Build(ReportParameters p, ReportingDbContext db)
```

`Build` returns a LINQ projection and **executes nothing**. Filtering, sorting, grouping, pivoting and paging are applied to that `IQueryable` by the generic engine, so every report gets all nine grid features without writing any of them, and Postgres does the work.

### 5.2 The generic builder

Each `ReportColumn` carries a member expression naming its property on the row type. The builder composes `Where`, `OrderBy`/`ThenBy`, `GroupBy`, `Skip`/`Take` from the request against that map. **No raw SQL anywhere** — expression trees only, per hard rule 1.

A column absent from the map cannot be filtered or sorted on, which is what makes an arbitrary request from a client safe: there is no string that reaches the database.

### 5.3 Aggregates, group footers, grand total

Group footers come from a second query — the same filtered `IQueryable`, grouped by the requested keys, aggregating the numeric columns. The grand total is a third. Three queries per page is the accepted cost of subtotals that are right across page boundaries; `includeCount: false` on subsequent pages drops it back to two.

### 5.4 Pivot

`GroupBy` on the row axis and the column axis together, aggregate, then transpose in memory into a matrix. The transposition is the only step that is not translated to SQL, and it operates on the aggregated result — hundreds of rows, not millions. A pivot whose column axis produces more than **200 distinct values** is refused with a message naming the column, because a 5,000-column grid helps nobody.

### 5.5 Running balances

Account Transaction's *Running Balance* and General Ledger Summary's *Opening Balance* are window functions over the full ordered set — they cannot be computed on a page. Two consequences, and both are requirements rather than caveats:

- A report with a running balance **forces its sort order** to the ordering the balance is defined over, and the grid disables sorting on other columns while it is selected.
- The opening balance for page *n* is computed as the sum of everything before the page's first row and returned in the response header block, so page 3 starts from the right number.

### 5.6 Limits

- Query timeout **30s**; a report that exceeds it returns a message naming the report and suggesting a narrower date range, not a 500.
- Export cap **100,000 rows**. Above it the response is refused with the row count, and the user narrows the range. This cap is what defers the background-job export design; when a broker exists, the cap becomes the threshold at which an export is queued instead.
- Page size max **200**.

### 5.7 Indexes

Reports read what transactions write, and the write-side indexes are not the read-side ones. Each report's *Source* row in §8 names the tables it hits; before a report ships, an index review covers its date column, its `OrgId` prefix and its most-filtered dimension. `acc.JournalLedger` in particular needs `(OrgId, LedgerDate)`, `(OrgId, AccountId, LedgerDate)` and `(OrgId, SubAccountId, LedgerDate)` before Account Transaction is usable at volume.

### 5.8 Export writers

`ExcelReportWriter` uses `DocumentFormat.OpenXml`, already a dependency of `Accounting.Api` for statement import. `PdfReportWriter` uses the Syncfusion .NET path — noting that `SyncfusionPdfGenerator` is today a mock returning a UTF-8 string, so the first report export is also the work that makes that class real.

Both writers consume the **same** `ReportResult` shape the API returns, with paging off. One serializer, three outputs.

---

## 6. The `rpt` schema

Three tables, all `OrgId`-scoped with a query filter and an RLS policy, all inheriting `AuditableEntity`.

### `rpt.Reports` — the catalog

One row per report. `ReportKey` (unique per org), `Title`, `ReportModule`, `Description`, `RequiredPermission`, `IsActive`, `SortOrder`. Seeded reference data, so `CreatedBy` is null on every seeded row.

The catalog exists in the database rather than only in code because a branch turns reports off, reorders them, and renames them into its own language — none of which is a deploy.

### `rpt.ReportDetails` — the columns

One row per column per report, and the table that carries §8's catalog. `ReportId`, `ColumnKey`, `Header`, `ColumnDataType`, `IsDefault`, `IsFilterable`, `IsSortable`, `IsGroupable`, `IsPivotable`, `DefaultAggregate`, `Alignment`, `Width`, `SortOrder`, `IsPrimary` (shows in the 360px card title), `IsHidden`.

The `IReportSource` in code is the authority on what a column *means*; this table is the authority on how it is *presented*. A mismatch between the two is a seed error and is caught by a startup check that compares the source's column keys against the seeded rows.

### `rpt.ReportViews` — saved layouts

`ReportId`, `ViewName`, `OwnerUserId` (null = a branch-wide view), `IsDefault`, and the layout itself as **JSONB**: selected columns and their order, filters, sorts, grouping, pivot spec, freeze settings, page size. JSONB rather than four child tables because the layout is read and written whole, is never queried into, and evolves with the grid — and Postgres JSONB is a deliberate choice in this project rather than a shortcut.

One default per user per report; a branch-wide view needs `reporting.manage` to create.

---

## 7. Column conventions

The catalog in §8 is transcribed from the source list, and these conventions decode it.

**`%CurCode%` is the branch's base currency code, substituted into the header at render time.** *Debit(%CurCode%)* displays as *Debit(INR)* for an Indian branch. The value is the base-currency amount — `DebitAmountBase` on the ledger.

**`(Source)` is the document's own currency.** *Debit(Source)* is `DebitAmount`, the figure as the document was raised, and it is meaningless without the *Currency* and *ExchangeRate* columns beside it. Every report offering a `(Source)` column also offers both.

**Money is `decimal` end to end.** The backend stores `decimal` (`JournalLedger.DebitAmount`, `StockMovement.Quantity`); the report DTOs carry `decimal`; the grid formats by column type and the branch's currency decimals. The scaled-integer convention in `bb-document-line-grid` is a line-entry concern and does not reach here.

**Blank column entries in the source list are dynamic columns.** Every report with a blank row — the aged reports, the tracking reports, the schedules — has a set of columns whose number depends on the parameters: aging buckets (Current / 1–30 / 31–60 / 61–90 / 90+), or period columns. They are declared by the response, not by `rpt.ReportDetails`. **The bucket definition itself is a parameter** (bucket size and count), because 30-day buckets are a convention rather than a law.

**Three transcription problems in the source list, to confirm rather than to copy:**

1. **Aged Receivables Details and Summary both list a column called *Aged Payable*.** On a receivables report that is a copy-paste from the payables one; read as *Aged Receivable* below.
2. **Trial Balance lists *CAAccountID*.** An internal key. It is carried as a hidden column so a row can link to the account ledger, and is not offered in the chooser.
3. **Four reports are duplicated under two names** — *Sales Order Tracking* / *Sales Order Tracking NEW*, and *Quotation Tracking Report NEW* with no old counterpart. The "NEW" pair have different column sets (the NEW one has *Transferred Quantity* and *Outstanding Quantity*; the old one has *Item Code*, *UOM*, *SO Track Status*). They should be **one report with both column sets available**, defaulting to the NEW set, rather than two entries in the catalog.

---

## 8. The report catalog

Forty-six reports. **Status** says what stands between the report and being built:

- **Ready** — every table it reads exists and is migrated
- **Partial** — its core reads exist; named columns need a service that does not
- **Blocked: X** — service X is not built

### 8.1 Accounting

#### Account Movement — `account-movement` · Ready
*Source:* `acc.JournalLedger` → `acc.Account`

| Column | Type | Note |
|---|---|---|
| Date | Date | `LedgerDate` |
| Account | Text | |
| Account Code | Text | |
| Account Type | Enum | from `mst.AccountTypes`, resolved in C# — different database |
| Debit | Money | base currency |
| Credit | Money | base currency |
| Description | Text | |
| Reference | Text | |
| Source | Text | `TransactionTypeCode` → display name |

The plain movement listing. Groups naturally by Account Type then Account; that is its default grouping.

#### Account Transaction — `account-transaction` · Ready
*Source:* `acc.JournalLedger` → `acc.Account`, `acc.SubAccount`, `con.Contacts`, `acc.TaxMaster`

| Column | Type | Note |
|---|---|---|
| Date · Transaction No · Reference · Description · Source | Date/Text | |
| Account · Account Code · Related Account | Text | *Related Account* is the contra leg on the same document |
| Contact Code · Contact Name | Text | via `SubAccount` → `con.Contacts` |
| Currency · ExchangeRate · Revalued Cur | Text/Rate | |
| Debit(Source) · Credit(Source) · Gross(Source) · Net(Source) · Tax(Source) | Money | document currency |
| Debit(%CurCode%) · Credit(%CurCode%) · Gross(%CurCode%) · Net(%CurCode%) · Tax(%CurCode%) | Money | base currency |
| Tax Rate · Tax Rate Name | Percent/Text | |
| Foreign exchange (FX) | Money | realized/unrealized on the leg |
| Running Balance | Money | forces sort order — §5.5 |
| Permit No | Text | contact licence number, from `con.ContactLicences` |

The fullest accounting report and the one that sets the engine's performance bar.

#### General Ledger Summary — `general-ledger-summary` · Ready
*Source:* `acc.JournalLedger` → `acc.Account`

| Column | Type |
|---|---|
| Account · Account Code | Text |
| Opening Balance · Debit · Credit · Net Movement · Closing (YTD) | Money |

*Opening Balance* is everything before the period — §5.5 applies.

#### Trial Balance — `trial-balance` · Ready
*Source:* `acc.JournalLedger` → `acc.Account`

| Column | Type | Note |
|---|---|---|
| Account Code · Account Name · Account Type | Text/Enum | |
| Debit · Credit · Current Balance | Money | |
| CAAccountID | Number | hidden — §7 |

An HTML page already exists at `libs/accounting/accounting-ui/.../trial-balance`. **It moves onto the grid** rather than being duplicated; the existing endpoint stays for the balanced/unbalanced banner, which is a page concern and not a grid one.

#### Journal Report — `journal-report` · Ready
*Source:* `acc.Journal`, `acc.JournalDetail` → `acc.Account`, `con.Contacts`

| Column | Type |
|---|---|
| Date · Trans No · Reference · Narration · Description · Transactions | Date/Text |
| Account Code · Account Name · Contact Name | Text |
| Currency · Exchange Rate | Text/Rate |
| Debit(Source) · Credit(Source) · Debit(%CurCode%) · Credit(%CurCode%) | Money |
| Created By · Created Date · Approved/Posted By · Approved/Posted Date · Last Modified By · Last Modified Date | Text/DateTime |
| Reconciled Status | Enum |

The six audit columns come from `AuditableEntity`; the user names resolve from `mst.Users` in C#, **batched** — this is the N+1 the tenancy rules warn about, and on a 200-row page it is 200 lookups done wrong.

#### Bank Summary — `bank-summary` · Ready
*Source:* `acc.BankAccount` → `acc.Account`, `acc.JournalLedger`

| Column | Type |
|---|---|
| Account Name · Account Type · Currency Code | Text |
| Opening Balance · Received · Spent · Closing Balance · Bank Revaluation | Money |
| *(dynamic)* | — |

#### Reconciliation Report — `reconciliation` · Ready
*Source:* `acc.BankStatement`, `acc.BankStatementLine` → `acc.JournalLedger`

| Column | Type |
|---|---|
| Transaction Date · Transaction No · Reference · Description | Date/Text |
| Amount In · Revalued Amount In | Money |
| GroupBy | Text |
| *(dynamic)* | — |

*GroupBy* in the source list is not a column — it is the report's grouping parameter (by statement, by matched/unmatched). Recorded as a parameter.

#### Foreign Currency Gain or Loss — `fx-gain-loss` · Partial
#### Foreign Currency Gain or Loss Details — `fx-gain-loss-details` · Partial
*Source:* `acc.JournalLedger`, `acc.Account`, and the period-end revaluation

Both carry: AccountName, CurrencyCode, Due(Source), Due(%CurCode%), Revalue FxRate, Revalued Due(%CurCode%), and the nine gain/loss columns — Realized / Unrealized / Net, each as amount, Exposure and YTD. The Details report adds Contact Name, Transaction Date, Transaction No, Transaction Fx Rate, Reference and Source.

**Partial because unrealized figures come from a period-end revaluation job that is not written.** Realized gain/loss is posted today at settlement and is readable now; the unrealized columns return null until the job exists, and the report says so rather than showing zero.

### 8.2 Receivables and payables

All four aged reports and all four invoice detail/summary reports read documents that do not exist yet.

#### Aged Receivables Summary — `aged-receivables-summary` · Blocked: Sales
Contact Code, Contact Name, Contact Group, Primary Person, Email, Mobile, Phone, Organization, Outstanding Tax, Total, *Aged Receivable* (see §7), plus the **dynamic aging buckets**.

#### Aged Receivables Details — `aged-receivables-details` · Blocked: Sales
The Summary's columns plus Invoice Date, Due Date, Expected Date, Invoice Number, Invoice Reference, Invoice Sent, Original Currency, and the buckets.

#### Aged Payables Summary — `aged-payables-summary` · Blocked: Purchase
Contact Code, Contact Name, Contact Group, Primary Person, Email, Mobile, Organization, Document Type, Outstanding Tax, Permit No, Total, Aged Payable, plus buckets.

#### Aged Payables Details — `aged-payables-details` · Blocked: Purchase
The Summary's columns plus Invoice Date, Due Date, Transaction No, Reference, Original Currency, Phone, and buckets.

> **These four are the strongest argument for the engine.** Four reports, one aging calculation, one bucket parameter, one contact join — written once as two sources differing only in which control account they read.

#### Receivable Invoice Detail — `receivable-invoice-detail` · Blocked: Sales
Account Code, Account Name, Approved By, Contact Code/Name/Group, Created Date, Currency Rate, Description, Discount % / Discount(Source) / Discount(%CurCode%), Due Date, Invoice Date, Invoice Number, Invoice Seen, Invoice Sent, Item Code, Last Payment Date, Organization, Original Currency, Quantity, Reference, Source, Status, Tax Rate, Tax Rate Name, Theme, Unit Price(ex)(Source|%CurCode%), Unit Price(inc)(Source|%CurCode%), Gross / Net / Tax / Balance / Invoice Total each in Source and %CurCode%.

#### Receivable Invoice Summary — `receivable-invoice-summary` · Blocked: Sales
The Detail's header-level columns, without the line ones, plus Payments Credits, Realised Gains, Unrealised Gains.

#### Payable Invoice Detail — `payable-invoice-detail` · Blocked: Purchase
As Receivable Invoice Detail, with Transaction No in place of Invoice Number, Permit No added, and Invoice Seen / Invoice Sent / Theme absent.

#### Payable Invoice Summary — `payable-invoice-summary` · Blocked: Purchase
As Receivable Invoice Summary, plus Permit No and Payments Debits.

#### Invoice/DN Payment Collection Report — `invoice-payment-collection` · Blocked: Sales
INV/DN No, Date, DueDate, Contact Code/Name, Account Code, Currency, Currency Rate, Payment Currency, Payment Currency Rate, Revalued Currency Rate, Payment Date, Payment Mode, Payment Reference, Payment Status, Due Status, Paid To, Receipt No, Reference, Realized Gain/Loss, UnRealized Gain/Loss, and SubTotal / Tax / Total / Amount Paid / Balance Due each in Source and %CurCode%.

#### Bill/DN Payment Report — `bill-payment` · Blocked: Purchase
The mirror of the above: Bill/DN No, Date, DueDate, *Paid From* rather than *Paid To*, plus Contact Group and Permit No.

### 8.3 Inventory

#### Inventory Item List — `inventory-item-list` · Partial
*Source:* `inv.Item`, `inv.ItemStock`, `inv.ItemCategory`, `inv.UnitOfMeasure`

Item Code, Item Name, Item Group, Product Category, Inventory Type, Costing Method, Status, Date, Organization, Contact Name, Unit of Measurement, Purchase Description, Sales Description, Purchase Tax Rate, Sales Tax Rate, Inventory Account, Purchase Account, Sales Account, Quantity On Hand, Average Cost, Unit Cost Price, Unit Sale Price, Total Value.

**Partial:** *Quantity on Order*, *Quantity Received*, *Committed Quotes* and *Committed to DO* need `sal` and `pur` documents. They are declared and return null until those land.

#### Inventory Item Detail — `inventory-item-detail` · Partial
*Source:* `inv.StockMovement`, `inv.Item`, `acc.JournalLedger`

Date, Item Code, Item Name, Item Group, Product Category, Description, Contact Code, Contact Name, Organization, Costing Method, Unit of Measurement, Transaction No, Reference, Source, QoH Movement, Value Movement, Unit Cost Price, Unit Sale Price, Margin, Profit Per Item, Inventory Account, Purchase Account, Sales Account, Adjustment Account.

**Partial:** *Unit Sale Price*, *Margin* and *Profit Per Item* need sales documents.

#### Inventory Item Summary — `inventory-item-summary` · Partial
Item Code/Name/Group, Product Category, Inventory Type, Costing Method, Unit of Measurement, Organization, Opening Quantity, Opening Balance, Quantity Purchased, Purchases, Quantity Sold, Sales, Quantity Adjusted, Adjustments, COGS, Profit, Closing Quantity, Closing Balance, and the three account columns.

**Partial:** the purchased/sold split needs `sal` and `pur`; adjustments and opening/closing are readable today.

#### Inventory Aging Report — `inventory-aging` · Ready
*Source:* `inv.CostLayer`

Item Code/SKU, Item Name, Product Category, Unit of Measurement, Inventory Asset Account, Organization, *Aged Inventory*, plus dynamic buckets. Ages by **cost layer receipt date**, which is exactly what layers were built to answer.

#### Batch Tracking Status Report — `batch-tracking-status` · Ready
*Source:* `inv.ItemBatch`, `inv.ItemStock`, `inv.Warehouse`

Batch No, Item Code, Item Name, Description, Product Category, Manufactured Date, Expiry Date, Batch Quantity, Available Quantity, CostingMethod, Warehouse.

#### Batch Tracking Detail Report — `batch-tracking-detail` · Ready
*Source:* `inv.ItemBatch`, `inv.StockMovement`

The Status columns plus Transaction Date, Transaction No, Transaction Type, Contact Name, Quantity IN, Quantity OUT, Unit of Measurement, and dynamic period columns.

#### Serial Tracking Status Report — `serial-tracking-status` · Ready
Serial No, Item Code, Item Name, Product Category, Manufactured Date, Expiry Date, Available Quantity, CostingMethod, Warehouse.

#### Serial Tracking Detail Report — `serial-tracking-detail` · Ready
The Status columns plus Description-level movement: Transaction Date, Transaction No, Transaction Type, Contact Name, Quantity IN, Quantity OUT, Unit of Measurement, Warehouse.

#### Warehouse Tracking Status Report — `warehouse-tracking-status` · Ready
*Source:* `inv.Warehouse`, `inv.ItemStock`

Warehouse Name, Address, City, State, Country, Primary, Status, Quantity IN, Quantity OUT, Available Quantity.

#### Warehouse Tracking Detail Report — `warehouse-tracking-detail` · Ready
Warehouse Name, Item Code, Item Name, Batch/Serial No, Transaction Date, Quantity IN, Quantity OUT, Balance Quantity, Tracked Quantity, UnTracked Quantity, Total Quantity.

> *Tracked* / *UnTracked* split the stock that carries a batch or serial from the stock that does not. Worth stating in the UI, since a mismatch there is usually a data-entry problem rather than a stock problem.

### 8.4 Sales

All Blocked: Sales — the fifteen `sal` tables are written but have no migration, no service and no controller.

| Report | Key | Columns |
|---|---|---|
| Sales Analysis Report | `sales-analysis` | Contact Code, Contact Name, Organization, Product Code, Product Name, Product Category, Total Qty, Total Sales (%CurCode%) |
| Sales Analysis Detail Report | `sales-analysis-detail` | Date, Transaction No, Reference, Source, Contact Code/Name, Item Code/Name, Description, Product Category, Inventory Type, UOM, Quantity, Quantity Return, Currency, Currency Rate, Disc, Disc %, Price Adjustment, Unit Sales Price (Source/%CurCode%), Unit Cost Price (%CurCode%), Subtotal (Source/%CurCode%), Total Cost (%CurCode%), Profit (%CurCode%), Profit Per Unit (%CurCode%), Profit Margin, Inventory Asset Account, Purchase Account, Sales Account |
| Invoice Tracking Report | `invoice-tracking` | Invoice NO, Invoice Date, Due Date, Contact Name, Qty, Delivered Quantity, Outstanding Quantity, Total Amount, Status, + dynamic |
| Quotation Tracking Report | `quotation-tracking` | Quote NO, Quotation Date, Expiry Date, Contact Name, Qty, Transferred Quantity, Outstanding Quantity, Total Amount, Status |
| Sales Order Tracking Report | `sales-order-tracking` | **Merged, see §7.3** — SO No, SO Date, SO Reference, Shipment Date, Contact Name, Item Code, Item Name, UOM, SO Qty / Qty, Del Qty, Undel Qty, Transferred Quantity, Outstanding Quantity, Total Amount, Status, SO Track Status |
| Delivery Order Tracking Report | `delivery-order-tracking` | Delivery Order NO, Delivery Date, Contact Name, Qty, Invoiced Quantity, Outstanding Quantity, Status |
| Quote Invoice Tracking Report | `quote-invoice-tracking` | QuoteNo, Quote Date, Expiry Date, Contact Code/Name, Item Name, Quoted Quantity, InvoiceNo, Invoiced Date, Invoiced Quantity, Over Invoiced Quantity, DeliveryNo, Delivery Date, Delivered Quantity, Delivery Over Quantity, Pending Quantity |

### 8.5 Purchase

All Blocked: Purchase — the service has `.csproj` files and nothing else.

| Report | Key | Columns |
|---|---|---|
| Purchase Analysis Report | `purchase-analysis` | Contact Code, Contact Name, Organization, Product Code, Product Name, Product Category, Total Qty, Total Purchase (%CurCode%) |
| Purchase Order Tracking Report | `purchase-order-tracking` | PO NO, PO Date, Delivery Date, Contact Name, Qty, Received Quantity, Outstanding Quantity, Total Amount, Status |
| Receive Order Tracking Report | `receive-order-tracking` | Receive Order NO, Receive Date, Contact Name, Qty, Billed Quantity, Outstanding Quantity, Status |
| Receive Order Details Report | `receive-order-details` | Receive No, Receive Date, Delivery Date, Transaction Date, Transaction No, Transaction Type, Transaction Quantity, Contact Code/Name, Item Name, Description, Received Quantity, Pending Quantity |
| Bill Tracking Report | `bill-tracking` | Bill No, Bill Date, Due Date, Reference, Contact Name, Qty, Received Quantity, Outstanding Quantity, Total Amount, Status |

### 8.6 Fixed assets

All Blocked: Fixed Assets — Phase 2 by decision of 4 August 2026, and blocked twice over: an asset is capitalised from a bill, and Purchase does not exist.

| Report | Key | Columns |
|---|---|---|
| Fixed Assets Schedule | `fixed-assets-schedule` | Asset Number, Asset Name, Description, Asset Type, Serial Number, Warranty Expiry, Purchase Date, Purchase Price, Cost Limit, Residual Value, Dep Method, Averaging Method, Rate %, Effective Life, Dep Start Date, Asset Account, Accum Dep Account, Dep Expense Account, Opening Cost, Addition (Purchase) Cost, Disposal Cost, Closing Cost, Opening Accum Dep, Depreciation, Disposal Accum Dep, Closing Accum Dep, Opening NBV, Closing NBV, Disposal Date, NBV as at Disposal Date, Sales Proceeds, Gain on Disposal, Loss on Disposal |
| Depreciation Schedule | `depreciation-schedule` | The Fixed Assets Schedule columns less the cost-roll ones, plus Capital Gain and Cost |
| Fixed Asset Disposal Schedule | `fixed-asset-disposal-schedule` | AssetNumber, AssetName, AssetType, AssetTypeID, AssetValue, Cost, CostLimit, ResidualValue, Dep Method, Avg Method, Rate, Effective Life, Dep Start Date, Purchased, Disposed, Sale Price, Capital Gain, Loss |
| Fixed Asset Reconciliation | `fixed-asset-reconciliation` | Source, Opening Cost, Cost Debits, Cost Credits, Closing Cost, Opening Accum Dep, Accum Dep Debits, Accum Dep Credits, Closing Accum Dep, Opening Book Value, Closing Book Value |

These four are carried here so the register's schema is designed knowing what has to come out of it — *Averaging Method* and *Cost Limit* are columns nobody adds retrospectively without a migration.

### 8.7 Summary

| Module | Reports | Ready | Partial | Blocked |
|---|---|---|---|---|
| Accounting & banking | 9 | 7 | 2 | 0 |
| Receivables / payables | 10 | 0 | 0 | 10 |
| Inventory | 10 | 7 | 3 | 0 |
| Sales | 7 | 0 | 0 | 7 |
| Purchase | 5 | 0 | 0 | 5 |
| Fixed assets | 4 | 0 | 0 | 4 |
| **Total** | **45** | **14** | **5** | **26** |

Forty-five rather than forty-six after the Sales Order Tracking merge of §7.3.

---

## 9. Build phases

Each stage commits to `main` as it stands up — schema, then engine, then grid, then reports — per the git rule in `CLAUDE.md`. Every stage ships its documentation page and its release-note bullet in the same commit.

**R0 — the engine and the grid.** `rpt` schema and migration; `IReportSource`, the generic builder, the catalog and the query endpoint; `bb-report-grid` with all nine capabilities; the generic host page; both export writers. Proven against **Account Movement** and **Trial Balance** — the simplest report and the one that already has a page to compare against.
*Done when:* both render, filter, sort, group, page, freeze and export, and the exported XLSX row count matches the grid's stated total.

**R1 — the accounting reports.** Account Transaction (the running-balance case and the performance bar), General Ledger Summary, Journal Report, Bank Summary, Reconciliation. Includes the index review of §5.7 and the batched user-name resolution of §8.1.

**R2 — the inventory reports.** All ten, three of them shipping with their sales/purchase columns declared and null.

**R3 — saved views and pivot.** `rpt.ReportViews`, the saved-view dialog, branch-wide views behind `reporting.manage`, and the pivot panel. Split from R0 deliberately: pivot is the one capability no Ready report needs on day one, and the saved-view UI is easier to design once there are real layouts to save.

**R4 — receivables and payables.** With Sales and Purchase. Ten reports, two aging sources.

**R5 — sales and purchase reports.** Twelve reports, with their services.

**R6 — fixed assets.** Phase 2, with the register.

**Unlocked in order:** R0 → R1 → R2 → R3 are buildable today. R4 and R5 wait on services that do not exist. R6 waits on a Phase 2 decision that is still open.

---

## 10. To confirm before R0

1. **The cross-schema read of §2** — read-only mapped entities in `ReportingDbContext` with `ExcludeFromMigrations`, as a recorded exception to hard rule 8. This is the one that cannot be deferred.
2. **`rpt.ReportDetails` as the column catalog** and `rpt.ReportViews` as saved layouts, per §6 — confirming that "ReportDetails" means the per-report column metadata rather than the saved layout itself.
3. **The three transcription problems of §7** — *Aged Payable* on the receivables reports, *CAAccountID* on Trial Balance, and the Sales Order Tracking merge.
4. **Aging buckets** — is Current / 1–30 / 31–60 / 61–90 / 90+ the default, and is the bucket size a per-report parameter or a branch setting?
5. **The export cap of 100,000 rows** (§5.6), and whether a capped export should refuse or truncate with a warning row.
6. **Whether Trial Balance's existing page** is replaced by the grid host or kept beside it.
