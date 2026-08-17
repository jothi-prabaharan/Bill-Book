/**
 * The report contracts, mirroring `Reporting.Entity/Models` on the server.
 *
 * Kept as one file rather than one per type: they are a single contract that
 * changes together, and splitting them means a rename that compiles on one side
 * and not the other. When the server's models change, this file is the diff.
 */

/** Matches `ColumnDataType`. Decides the filter operators offered and the formatting. */
export type ColumnDataType =
  | 'Text'
  | 'Number'
  | 'Money'
  | 'Quantity'
  | 'Percent'
  | 'Rate'
  | 'Date'
  | 'DateTime'
  | 'Boolean'
  | 'Enum'
  | 'Link';

/** Matches `FilterOperator`. */
export type FilterOperator =
  | 'Equals'
  | 'NotEquals'
  | 'Contains'
  | 'NotContains'
  | 'StartsWith'
  | 'EndsWith'
  | 'GreaterThan'
  | 'GreaterOrEqual'
  | 'LessThan'
  | 'LessOrEqual'
  | 'Between'
  | 'In'
  | 'NotIn'
  | 'IsNull'
  | 'IsNotNull';

export type SortDirection = 'Asc' | 'Desc';

export type AggregateFunction =
  | 'None'
  | 'Sum'
  | 'Count'
  | 'CountDistinct'
  | 'Min'
  | 'Max'
  | 'Avg';

export type ColumnAlignment = 'Left' | 'Right' | 'Center';

export type ReportModule =
  | 'Accounting'
  | 'Banking'
  | 'Inventory'
  | 'Sales'
  | 'Purchase'
  | 'FixedAssets';

/** `Xlsx` works; `Pdf` is declared and refused server-side. */
export type ExportFormat = 'Xlsx' | 'Pdf';

export interface ReportFilter {
  column: string;
  operator: FilterOperator;
  value?: string | null;
  /** The upper bound, for `Between` only. Inclusive. */
  value2?: string | null;
  /** The candidates, for `In` and `NotIn`. */
  values?: string[];
}

export interface ReportSort {
  column: string;
  direction: SortDirection;
}

export interface ReportPivotValue {
  column: string;
  aggregate: AggregateFunction;
}

export interface ReportPivot {
  rows: string[];
  columns: string[];
  values: ReportPivotValue[];
}

export interface ReportPage {
  number: number;
  size: number;
  /**
   * Counting the whole result is a second scan, so it is asked for on the first
   * page of a query and carried in state until a filter changes it.
   */
  includeCount: boolean;
}

export interface ReportFreeze {
  header: boolean;
  /** How many leading columns stay put. A count, not a flag — some reports want two. */
  columns: number;
}

/**
 * Everything the grid can ask for.
 *
 * **This is also the shape a saved view stores**, minus the page number — so
 * saving a layout and running a report use one type, and a saved view cannot
 * drift out of step with what the grid can express.
 */
export interface ReportQuery {
  parameters: Record<string, string | null>;
  columns: string[];
  filters: ReportFilter[];
  sorts: ReportSort[];
  groupBy: string[];
  pivot: ReportPivot | null;
  page: ReportPage;
  freeze: ReportFreeze;
}

export interface ReportColumn {
  key: string;
  /** Already substituted: the client never sees `%CurCode%`. */
  header: string;
  dataType: ColumnDataType;
  alignment: ColumnAlignment;
  width: number | null;
  isFilterable: boolean;
  isSortable: boolean;
  isGroupable: boolean;
  isPivotable: boolean;
  aggregate: AggregateFunction;
  /** Leads the card at ~360px, where a row cannot be a row. */
  isPrimary: boolean;
}

export interface ReportCurrency {
  code: string;
  decimals: number;
}

/** A group's subtotals. `path` runs outermost level first. */
export interface ReportGroupFooter {
  path: (string | null)[];
  rowCount: number;
  aggregates: Record<string, number | null>;
}

export interface ReportPageInfo {
  number: number;
  size: number;
  /** Null when the request declined the count. */
  totalRows: number | null;
  totalPages: number | null;
}

/**
 * One page of a report.
 *
 * **Render `columns` as given rather than what was asked for** — a pivot's
 * columns are only known once the query has run, so the response declares them.
 */
export interface ReportResult {
  reportKey: string;
  title: string;
  generatedAt: string;
  currency: ReportCurrency;
  columns: ReportColumn[];
  /** Column key to value. Loosely typed because a pivot has no fixed shape. */
  rows: Record<string, unknown>[];
  groupFooters: ReportGroupFooter[];
  /** Spans the whole result, not the page. */
  grandTotal: ReportGroupFooter | null;
  page: ReportPageInfo;
  truncated: boolean;
  truncationReason: string | null;
}

export interface ReportCatalogItem {
  reportKey: string;
  title: string;
  module: ReportModule;
  description: string | null;
  sortOrder: number;
}

export interface ReportParameter {
  name: string;
  label: string;
  dataType: ColumnDataType;
  isRequired: boolean;
  defaultValue: string | null;
}

export interface ReportMetadata {
  reportKey: string;
  title: string;
  module: ReportModule;
  description: string | null;
  parameters: ReportParameter[];
  columns: ReportColumn[];
  defaultColumns: string[];
  /**
   * The report's own order carries meaning — a running balance. The grid then
   * disables sorting on everything else, because the figures would be true of an
   * order the reader cannot see.
   */
  forcesSortOrder: boolean;
}

export interface SavedView {
  reportViewId: number;
  viewName: string;
  isShared: boolean;
  isDefault: boolean;
  layout: ReportQuery;
}

/** The starting query for a report, before anybody touches anything. */
export function emptyQuery(metadata?: ReportMetadata): ReportQuery {
  return {
    parameters: {},
    columns: metadata?.defaultColumns ?? [],
    filters: [],
    sorts: [],
    groupBy: [],
    pivot: null,
    page: { number: 1, size: 50, includeCount: true },
    freeze: { header: true, columns: 0 },
  };
}
