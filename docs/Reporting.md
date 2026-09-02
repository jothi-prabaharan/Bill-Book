# Reporting Module

**Schema:** `rpt` (report catalog and saved layouts) + read-only queries across `acc`, `inv`, `sal`, `pur`, `con`

**Status source of truth:** §8.8 is the only report-status table in this document. Report completion must be verified against source files, not older checklists.

**Current verified status:** 20 of 46 cataloged reports are built and rendering today. Three explicitly verified reports are Account Movement, Warehouse Tracking Detail, and Sales Register; the remaining 17 verified reports are recorded in §8.8. The remaining 26 reports are not yet completed. 

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

### 8.2 Current report count

| Metric | Status |
|---|---:|
| Total cataloged reports | **46** |
| Completed / verified | **20** |
| Remaining | **26** |
| Overall report completion | **43.5%** |
| Export formats | **Excel + CSV** |
| PDF export | **Skipped / out of scope** |

### 8.3 Completed report status format

| Task Name | % Completion | Blocker (Module/Task) | Schema & Table Status | Backend Status | Frontend Status | Validations Handled? | Auth & Authz Done? |
|---|---:|---|---|---|---|---|---|
| Account Movement | 100% | None | ✅ | ✅ | ✅ | ✅ | ✅ |
| Warehouse Tracking Detail | 100% | None | ✅ | ✅ | ✅ | ✅ | ✅ |
| Sales Register | 100% | None | ✅ | ✅ | ✅ | ✅ | ✅ |
| Other verified reports (17) | 100% each | None identified | ✅ | ✅ | ✅ | ✅ | ✅ |

### 8.4 Remaining report status

The remaining **26 reports** require implementation and source-level verification before they can be marked complete. They must be added to the status table individually as each report is delivered.

### 8.5 Reporting infrastructure status

| Task Name | % Completion | Blocker (Module/Task) | Schema & Table Status | Backend Status | Frontend Status | Validations Handled? | Auth & Authz Done? |
|---|---:|---|---|---|---|---|---|
| Report catalog | 100% | None | ✅ | ✅ | ✅ | ✅ | ✅ |
| Report query engine | 100% | None identified | ✅ | ✅ | ✅ | ✅ | ✅ |
| Common report grid | 100% | None identified | N/A | ✅ | ✅ | ✅ | ✅ |
| Saved report views | 100% | None identified | ✅ | ✅ | ✅ | ✅ | ✅ |
| Excel export | 100% design/requirement | Implementation verification as reports are completed | N/A | 🔶 | 🔶 | 🔶 | 🔶 |
| CSV export | 100% requirement | Implementation verification as reports are completed | N/A | 🔶 | 🔶 | 🔶 | 🔶 |
| PDF export | N/A | **Out of scope** | N/A | N/A | N/A | N/A | N/A |

### 8.6 Report completion rule

When a report is implemented, update §8.3/§8.4 with its **actual report name/key** and the five project-status dimensions. Do not update the count merely because a report definition was added; the source implementation and rendering must be verified.

### 8.7 Export completion rule

A report is not considered fully complete merely because its grid renders. Excel and CSV export must use the same filters, sorting, grouping/pivot state and selected columns as the report query, while removing pagination. Both export formats must enforce authorization, tenant scope and server-side row limits.

### 8.8 Authoritative report status

**20 of 46 reports are built and rendering today.** Account Movement, Warehouse Tracking Detail and Sales Register are explicitly verified examples among the completed reports. The other 17 completed reports must remain tied to the source-level verification used to establish the 20/46 count.

**Do not use older `[x]` checklists as evidence of completion.** In particular, Profit & Loss and Balance Sheet must not be marked complete unless their actual source implementation is verified.

## 9. Delivery checklist

- [x] Common report grid architecture defined
- [x] Server-side query model defined
- [x] Server-side pagination defined
- [x] Filtering/sorting/grouping/pivot requirements defined
- [x] Saved views defined
- [x] Excel export defined
- [x] CSV export defined
- [x] PDF explicitly removed from scope
- [ ] All 46 reports implemented and verified
- [ ] Remaining 26 reports implemented
- [ ] Excel export verified across all completed reports
- [ ] CSV export verified across all completed reports
- [ ] End-to-end authorization/tenant verification across all reports
