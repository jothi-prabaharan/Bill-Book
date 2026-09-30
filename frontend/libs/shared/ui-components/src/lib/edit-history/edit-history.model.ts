/**
 * The edit log as the History tab and the audit-log report read it (TK-118).
 * Every tenant service serves the same routes under `/api/edit-log/{service}`,
 * so one model and one set of helpers covers all of them.
 */

export type EditOperation = 'Insert' | 'Update' | 'Delete';

/** One logged change: `changes` is `{ Column: { old?, new? } }`. */
export interface EditLogEntry {
  editLogId: number;
  entityName: string;
  entityKey: string;
  rootEntityName: string;
  rootEntityKey: string;
  operation: EditOperation;
  changes: Record<string, { old?: unknown; new?: unknown }>;
  userId: string | null;
  userName: string | null;
  changedAt: string;
  requestId: string | null;
  source: string | null;
}

export interface EditLogPage {
  rows: EditLogEntry[];
  total: number;
  page: number;
  pageSize: number;
}

export interface EditLogUser {
  userId: string;
  userName: string | null;
}

/** One column's change, ready to show. */
export interface EditChangeRow {
  field: string;
  label: string;
  before: string;
  after: string;
}

/** The API path of a service's edit log, e.g. `/api/edit-log/sales`. */
export function editLogBase(service: string): string {
  return `/api/edit-log/${encodeURIComponent(service)}`;
}

/** The history route of one document or master. */
export function editHistoryPath(service: string, entity: string, key: string | number): string {
  return `${editLogBase(service)}/history/${encodeURIComponent(entity)}/${encodeURIComponent(String(key))}`;
}

const OPERATION_LABELS: Record<EditOperation, string> = {
  Insert: 'Created',
  Update: 'Changed',
  Delete: 'Removed',
};

export function operationLabel(operation: EditOperation | string): string {
  return OPERATION_LABELS[operation as EditOperation] ?? operation;
}

/**
 * A column or table name as a person reads it: `DueDate` → "Due date",
 * `InvoiceLines` → "Invoice lines", `TaxAmount` → "Tax amount". A trailing
 * `Id` stays, because "Contact id" says what the number is.
 */
export function humanize(name: string): string {
  const words = name
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/([A-Z]+)([A-Z][a-z])/g, '$1 $2')
    .split(' ')
    .filter(Boolean);
  if (words.length === 0) return name;
  return words
    .map((w, i) => (i === 0 ? w[0].toUpperCase() + w.slice(1) : /^[A-Z0-9]+$/.test(w) && w.length > 1 ? w : w.toLowerCase()))
    .join(' ');
}

/** A logged value as text. Absent and null both show as an em dash. */
export function formatValue(value: unknown): string {
  if (value === undefined || value === null || value === '') return '—';
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}

/** An entry's changed columns, in the order they were logged. */
export function changeRows(entry: Pick<EditLogEntry, 'changes'>): EditChangeRow[] {
  return Object.entries(entry.changes ?? {}).map(([field, change]) => ({
    field,
    label: humanize(field),
    before: formatValue(change?.old),
    after: formatValue(change?.new),
  }));
}

/**
 * Whether an entry is about the document itself or about one of its lines —
 * the History tab says "Invoice lines #42 changed" for the second.
 */
export function isLineEntry(entry: Pick<EditLogEntry, 'entityName' | 'rootEntityName'>): boolean {
  return entry.entityName !== entry.rootEntityName;
}

/** The entry's one-line summary: "Changed", or "Invoice lines 42 · Created". */
export function entrySummary(entry: EditLogEntry): string {
  const op = operationLabel(entry.operation);
  return isLineEntry(entry) ? `${humanize(entry.entityName)} ${entry.entityKey} · ${op}` : op;
}

/** One service's log, as the audit-log report offers it. */
export interface EditLogArea {
  service: string;
  label: string;
}

export interface EditLogFilters {
  /** `yyyy-MM-dd`, inclusive. */
  from: string | null;
  /** `yyyy-MM-dd`, inclusive: the query asks for everything before the next day. */
  to: string | null;
  userId: string | null;
  entity: string | null;
  page: number;
  pageSize: number;
}

/**
 * The report's query string. The server's `to` is exclusive, so the last day
 * the person picked is kept by asking for everything before the day after it.
 */
export function editLogQuery(filters: EditLogFilters): Record<string, string> {
  const query: Record<string, string> = {
    page: String(Math.max(1, filters.page)),
    pageSize: String(filters.pageSize),
  };
  if (filters.from) query['from'] = `${filters.from}T00:00:00`;
  if (filters.to) query['to'] = `${nextDay(filters.to)}T00:00:00`;
  if (filters.userId) query['userId'] = filters.userId;
  if (filters.entity) query['entity'] = filters.entity;
  return query;
}

/** `yyyy-MM-dd` plus one day, in the calendar rather than in hours. */
export function nextDay(isoDate: string): string {
  const [y, m, d] = isoDate.split('-').map(Number);
  const next = new Date(Date.UTC(y, m - 1, d + 1));
  return next.toISOString().slice(0, 10);
}

/** Every changed column on one line, for the report's grid. */
export function changeSummary(entry: EditLogEntry, max = 4): string {
  const rows = changeRows(entry);
  const shown = rows.slice(0, max).map((r) =>
    entry.operation === 'Update' ? `${r.label}: ${r.before} → ${r.after}` : `${r.label}: ${entry.operation === 'Insert' ? r.after : r.before}`,
  );
  return rows.length > max ? `${shown.join('; ')}; +${rows.length - max} more` : shown.join('; ');
}

/** How many pages a report of `total` rows has. */
export function pageCount(total: number, pageSize: number): number {
  return Math.max(1, Math.ceil(total / Math.max(1, pageSize)));
}
