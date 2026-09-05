# Reporting Module

**Schema:** `rpt` (report catalog and saved layouts) + read-only queries across `acc`, `inv`, `sal`, `pur`, `con`

**Status source of truth:** §8 is the only report-status section in this document, and it is a reconciliation against source rather than a checklist. Report completion must be verified against source files.

**Current verified status (4 September 2026):** **41 report sources are wired end to end** — source, DI registration, catalog seed and test coverage, each of the four asserted against the assembly by `ReportLayerCertificationTests`. Of those, **34 are among the 46 entries in `reports.json`** and 7 are extra reports the engine made cheap to add. **12 of the 46 are not implemented**, and §8.2 says which and why.

**The "20 of 46" this document used to carry was wrong in both directions and is corrected below.** It undercounted what was built by fourteen and mis-stated what remained. Recounted by diffing `reports.json` against the report keys the sources declare, which is a thirty-second answer and worth redoing rather than carrying forward.

## 1. Decisions already taken

Six questions were settled before this was written. They are recorded here because each one closes off a large branch of design, and re-opening one means re-reading everything below it.

| Question | Decision | What it buys |
|---|---|---|
| What renders the grid | **In-house `bb-report-grid`**, in `libs/shared/ui-components` | No licence, no vendor bundle, and consistent reporting UI |
| Where filter / sort / group / pivot / page execute | **Server-side, always** | One code path, one place `OrgId` is enforced, one place totals are computed |
| Which libraries may be used | **No new packages.** Only packages already pinned by the project | Avoids unnecessary vendor dependencies |
| How Excel is produced | **Server-side, over the full result set**, via `DocumentFormat.OpenXml` | Export matches the current query and is not limited to the visible page |
| How CSV is produced | **Server-side, over the full result set**, using the report's declared columns and current query state | Lightweight interchange format suitable for accounting/data workflows |
| What PDF export does | **Not supported / intentionally skipped** | No PDF implementation is required for Reporting |
| Whether layouts persist | **Yes — `rpt.ReportDetails`** | Users can save and reuse report layouts |

The grid is therefore **dumb by design**: it holds no data, fetches nothing, and computes no total. It receives a column definition, a query state and a page of rows, and it emits a new query state.

## 2. The one decision still open

**Reporting has to read `acc`, `inv`, `sal`, `pur` and `con`. Hard rule 8 says a service never crosses a boundary by referencing another service's `DbContext`.**

A report engine cannot ask for its data over HTTP. Account Transaction joins ledger rows to accounts, sub-accounts, contacts and tax masters and then pages the result; done across multiple API calls it would prevent efficient server-side paging. The recommended approach remains a read-only `ReportingDbContext` mapping over the required tables with migrations excluded and tenant filtering re-applied.

## 3. The common grid — `bb-report-grid`

### 3.1 What it must do

**Filter.** Per-column, typed filters. Filters combine with AND and are displayed as removable chips. Report-level parameters remain separate from column filters.

**Order.** Multi-column sorting is supported.

**Column select.** Users can select and reorder report columns.

**Group.** Groups can nest and return server-calculated subtotals.

**Pivot.** Rows, columns and values can be configured with aggregates. The server returns declared pivot columns.

**Export — Excel and CSV.** Export re-runs the current report query with the current filters, sort, grouping, pivot and selected columns, **without paging**. Excel is produced server-side using the existing OpenXML dependency. CSV is a plain tabular export of the same result. **PDF is intentionally not part of the Reporting requirement.**

**Pagination.** Server-side page sizes 25 / 50 / 100 / 200.

**Fixed header rows.** The header is sticky.

**Fixed columns.** Reports may configure leading columns as frozen.

### 3.2 What it deliberately does not do

- **No HTTP.** The host page or `reporting-core` fetches; the grid receives data.
- **No formatting policy of its own.** Formatting comes from the report column definition and currency context.
- **No aggregate arithmetic.** Totals arrive from the server.
- **No inline editing.** Reports are read-only.
- **No PDF generation or PDF export.** PDF is explicitly out of scope.

### 3.3 Component API

```text
bb-report-grid
  inputs
    definition   ReportDefinition
    state        ReportQueryState
    result       ReportResult | null
    busy         boolean
    currency     CurrencyContext
    freezeHeader boolean = true
    freezeColumns number = 0
  outputs
    stateChange  ReportQueryState
    export       ExportFormat           xlsx | csv
    rowActivate  ReportRow
```

One input carries the whole query state and one output replaces it. The host owns the state, so the URL, a saved view and the browser back button can all work through the same object.

### 3.4 Files

```text
libs/shared/ui-components/src/lib/report-grid/
  report-grid.component.ts|html|scss
  report-column.model.ts
  report-query.model.ts
  report-result.model.ts
  column-chooser.dialog.ts|html|scss
  filter-bar.component.ts|html|scss
  group-panel.component.ts|html|scss
  pivot-panel.component.ts|html|scss
  report-pager.component.ts|html|scss

libs/reporting/reporting-core/src/lib/
  report-catalog.service.ts
  report-query.service.ts
  saved-view.service.ts
  report-state.ts
  models/

libs/reporting/reporting-ui/src/lib/
  report-list/report-list.page.ts
  report-host/report-host.page.ts
  saved-views/saved-view.dialog.ts
```

**One host page serves every report.** A report is data — a key, a column list and a parameter set — so forty-six reports do not require forty-six pages. A report that needs something the generic host cannot express gets its own page and still hosts the same grid.

### 3.5 Responsive reporting rule

**Reports MUST remain table/grid based on desktop, tablet and mobile. Reports must NOT be converted into transaction/master-style cards.**

On narrow screens the report grid may use horizontal scrolling, compact columns, sticky important columns and other table-specific responsive techniques. The reporting data model, query and business logic remain the same across breakpoints.

## 4. The query contract

### 4.1 Endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/reports` | The catalog: key, title, module, description, whether the caller may run it |
| `GET` | `/api/reports/{key}` | One report's parameters and full column metadata |
| `POST` | `/api/reports/{key}/query` | Run the report and return one page |
| `POST` | `/api/reports/{key}/export?format=xlsx` | Export the current query as Excel without paging |
| `POST` | `/api/reports/{key}/export?format=csv` | Export the current query as CSV without paging |
| `GET` | `/api/reports/{key}/views` | Saved layouts visible to the caller |
| `POST` / `PUT` / `DELETE` | `/api/reports/{key}/views[/{id}]` | Manage saved layouts |

`format` supports **`xlsx` and `csv` only**. `pdf` is not supported and must not be added to the report export UI or API contract unless the project requirement is explicitly changed later.

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

`grandTotal` spans the whole result, not only the current page.

## 5. Export requirements

### 5.1 Excel (XLSX)

- Server-side generation.
- Uses the current report filters, sorting, grouping, pivot and selected columns.
- Ignores pagination so the export contains the full result set subject to the configured export row cap.
- Frozen header row.
- Report-defined column widths.
- Number/date/currency formats matching the report column metadata.
- Group subtotals and grand totals where applicable.
- Uses the existing `DocumentFormat.OpenXml` dependency; do not introduce another spreadsheet package.

### 5.2 CSV

- Server-side generation.
- Uses the same query state as the screen.
- Ignores pagination and exports the full result set subject to the configured export row cap.
- Uses the selected report columns in their display order.
- Proper CSV escaping for commas, quotes and line breaks.
- UTF-8 output so accounting data and multilingual customer/item names are preserved.
- Where grouping/pivoting is active, CSV follows the same declared result columns returned by the report engine.

### 5.3 PDF

**Not required. Do not implement PDF export.**

The Reporting UI must expose only:

- **Export Excel**
- **Export CSV**

No PDF button, menu item, endpoint, placeholder or future PDF dependency is required.

## 6. Security and tenancy

All report queries and exports must enforce the authenticated user's organization/tenant scope. Report permissions must be checked before catalog access, query execution, saved-view access and exports. Export endpoints must enforce the same authorization as the corresponding report query.

## 7. Performance

Filtering, sorting, grouping, pivoting and pagination remain server-side. Export queries must use the same query semantics as the visible report while removing paging. Export row limits must be enforced server-side to protect the API from unbounded downloads.

## 8. Report Catalog and Status

### 8.1 Status rules

A report is **Completed (100%)** only when its schema/data source, backend query, frontend rendering, validations and authorization are implemented and verified against the source code. A report that exists only in design documentation or an old checklist is not complete.

### 8.2 The reconciliation

`reports.json` holds **46 distinct `(ReportGroup, ReportName)` pairs**, and that file is where the number 46 comes from. Four of the forty-six declare **no columns at all** — Balance Sheet, Cash Flow Statement – Direct, Profit & Loss and Business Performance. The first three are statement reports with pages of their own and are built; the fourth is a bare name with no column list, which is not a report definition to implement.

| | Count |
|---|---|
| Entries in `reports.json` | **46** |
| Implemented | **34** |
| Not implemented | **12** |
| Implemented beyond `reports.json` | **7** |
| **Report sources wired end to end** | **41** |

The seven beyond the specification are Account Movement, Customer Statement, Vendor Statement, GSTR-1 Summary, Sales Register, Purchase Register and Warehouse Tracking Detail. They are real reports in the product; they simply are not in the file the count is taken from, which is why 34 + 7 = 41 rather than 41 of 46.

#### The twelve not implemented

**Four fixed-asset reports — blocked, not pending.** Depreciation Schedule, Disposal Schedule, Fixed Asset Reconciliation and Fixed Assets Schedule all read a fixed-asset register that does not exist: the register is Phase 2 and is itself blocked on two open schema decisions (whether acquisition and disposal get transaction codes of their own, and straight-line only versus books **and** tax depreciation). Nothing can be built here until those are answered — see the roadmap note in `CLAUDE.md`.

**One is not a report.** *Business Performance*, under a group called *Financial performance*, appears in `reports.json` as a group and a name with no columns, no sub-group and nothing else. There is no specification to implement. It needs a business decision about what it is before it can be engineering work.

**Seven are genuinely pending and unblocked.** Each is a substantial report of 17–43 columns, and each needs read models that do not exist yet:

| Report | Columns | What it needs first |
|---|---|---|
| Receivable Invoice Detail | 43 | Invoice ↔ allocation read models |
| Receivable Invoice Summary | 27 | the same, plus realised/unrealised FX per invoice |
| Invoice/DN Payment Collection | 33 | `acc.ReceiveMoney` and allocation read models |
| Payable Invoice Detail | 40 | Bill ↔ allocation read models |
| Payable Invoice Summary | 26 | the same |
| Bill/DN Payment | 34 | `acc.SpendMoney` and allocation read models |
| Purchase Receive Order Details | 17 | a goods-receipt **line** read model |

The common shape is that all seven report a document against what has been settled on it. `ReportingDbContext` maps invoices, bills, receipts and orders, but maps **no allocation, settlement or money-document tables at all** — so every one of the seven starts by adding read models over `acc`, not by writing a query. That is the work, and it has not been done.

### 8.3 What "wired" is asserted to mean

`ReportLayerCertificationTests` asks the assembly, not a list, and fails the build if a report exists in fewer than four places:

```
source class  →  DI registration  →  catalog seed row per column  →  covered by the source theories
```

Plus, per report, from the suites beside it: unique column keys, aggregates only on money and quantity columns, groupable columns typed as text, a declared permission beyond `reports.view`, and seed rows matching the source's declared keys exactly in both directions.

**A report is not complete because its source compiles.** Fifteen tracker and finance reports were once written, registered nowhere and listed in no test, and 239 tests passed over the gap — every test was a theory over a list, and a report absent from the list is a report no theory runs on. That is the failure the certification suite exists to make impossible.

### 8.4 Export status

Both formats are built and asserted, and neither has query semantics of its own — the writers take the `ReportResultView` the engine produced, so an export cannot disagree with the screen it was taken from.

| | Status |
|---|---|
| Excel (`format=xlsx`) | **Built.** `ExcelReportWriter`, on the already-pinned OpenXML dependency |
| CSV (`format=csv`) | **Built.** `CsvReportWriter`, RFC 4180, UTF-8 with a BOM, invariant numbers |
| PDF (`format=pdf`) | **Refused by design**, and outside the reporting requirement |

The CSV half was a requirement this document recorded as decided while `ExportFormat` carried only `Xlsx` and `Pdf`; `?format=csv` was an unreachable branch of an enum. It is implemented now, with 12 tests over quoting, encoding, multilingual text, null handling and column order.

## 9. Delivery checklist

- [x] Common report grid architecture defined
- [x] Server-side query model defined
- [x] Server-side pagination defined
- [x] Filtering/sorting/grouping/pivot requirements defined
- [x] Saved views defined
- [x] Excel export defined
- [x] CSV export defined
- [x] PDF explicitly removed from scope
- [x] Excel export built and verified
- [x] CSV export built and verified
- [x] Every wired report certified across source, DI, catalog and tests
- [x] Authorization and tenant scope asserted over the whole assembly — see `EndpointGuardTests` and `ReportingQueryFilterTests`
- [ ] The seven pending sales/purchase settlement reports (§8.2)
- [ ] The four fixed-asset reports — blocked on the Phase 2 register and two open schema decisions
- [ ] *Business Performance* — undefined in `reports.json`; needs a business decision, not engineering
