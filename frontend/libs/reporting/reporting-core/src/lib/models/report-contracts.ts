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
  | 'FixedAssets'
  | 'People'
  | 'Time'
  | 'Leave'
  | 'Pay'
  | 'Statutory'
  | 'Recruitment'
  | 'Claims'
  | 'Projects'
  | 'Custom';

/**
 * `Xlsx` and `Csv` are the supported export formats. `Pdf` is declared so the
 * route's shape is stable and is refused server-side — PDF is explicitly outside
 * the reporting requirement and must not be offered in the export UI.
 */
export type ExportFormat = 'Xlsx' | 'Pdf' | 'Csv';

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
  /**
   * What a running balance stood at before this page's first row, for the reports
   * that carry one. Null for every report that does not.
   */
  openingBalance: number | null;
  truncated: boolean;
  truncationReason: string | null;
}

export interface ReportCatalogItem {
  reportKey: string;
  title: string;
  module: ReportModule;
  description: string | null;
  sortOrder: number;
  app?: 'RetailErp' | 'School' | 'Hrms' | 'Payroll';
  /** Set on a saved custom report (TK-113); `reportKey` is then its dataset. */
  customReportId?: number | null;
  /** True on a dataset, the starting point of the report builder. */
  isDataset?: boolean;
}

/** A report built from a dataset and saved (TK-113). */
export interface CustomReport {
  customReportId: number;
  datasetKey: string;
  title: string;
  description: string | null;
  isShared: boolean;
  /** Whether this user may change or delete it. Set by the server. */
  mayEdit: boolean;
  layout: ReportQuery;
  /** Columns the saved layout named that the dataset no longer has, by heading. */
  droppedColumns: string[];
}

export type ReportScheduleFrequency = 'Daily' | 'Weekly' | 'Monthly';

export type ReportScheduleRunStatus = 'Sent' | 'Skipped' | 'Failed';

export type WeekDay = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';

/** A report emailed as a workbook on a schedule (TK-114). The owner is whoever saves it. */
export interface ReportSchedule {
  reportScheduleId: number;
  reportKey: string;
  customReportId: number | null;
  title: string;
  frequency: ReportScheduleFrequency;
  dayOfWeek: WeekDay | null;
  dayOfMonth: number | null;
  /** The hour it sends at, 0 to 23, in the branch's time zone. */
  sendAtHour: number;
  timeZoneId: string | null;
  recipientUserIds: string[];
  isActive: boolean;
  layout: ReportQuery;
  nextRunAt: string | null;
  lastRunAt: string | null;
  lastStatus: ReportScheduleRunStatus | null;
  /** What the last run did: sent to whom, or why nothing was sent. */
  lastMessage: string | null;
}

export interface ReportRecipient {
  userId: string;
  displayName: string;
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
  app?: 'RetailErp' | 'School' | 'Hrms' | 'Payroll';
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
