import { describe, expect, it } from 'vitest';
import {
  changeRows,
  changeSummary,
  editLogQuery,
  nextDay,
  pageCount,
  editHistoryPath,
  entrySummary,
  formatValue,
  humanize,
  isLineEntry,
  operationLabel,
  type EditLogEntry,
} from './edit-history.model';

const entry = (over: Partial<EditLogEntry> = {}): EditLogEntry => ({
  editLogId: 1,
  entityName: 'Invoices',
  entityKey: '7',
  rootEntityName: 'Invoices',
  rootEntityKey: '7',
  operation: 'Update',
  changes: { DueDate: { old: '2026-09-01', new: '2026-09-15' } },
  userId: null,
  userName: 'Asha',
  changedAt: '2026-09-30T10:00:00Z',
  requestId: null,
  source: 'Tracked',
  ...over,
});

describe('edit history model', () => {
  it('builds the history path with each part encoded', () => {
    expect(editHistoryPath('sales', 'Invoices', 7)).toBe('/api/edit-log/sales/history/Invoices/7');
    expect(editHistoryPath('accounting', 'Journals', 'a/b')).toBe('/api/edit-log/accounting/history/Journals/a%2Fb');
  });

  it('labels the three operations in plain words', () => {
    expect(operationLabel('Insert')).toBe('Created');
    expect(operationLabel('Update')).toBe('Changed');
    expect(operationLabel('Delete')).toBe('Removed');
  });

  it('humanizes column and table names', () => {
    expect(humanize('DueDate')).toBe('Due date');
    expect(humanize('InvoiceLines')).toBe('Invoice lines');
    expect(humanize('GSTIN')).toBe('GSTIN');
    expect(humanize('PlaceOfSupplyStateCode')).toBe('Place of supply state code');
  });

  it('formats absent, null, boolean and object values', () => {
    expect(formatValue(undefined)).toBe('—');
    expect(formatValue(null)).toBe('—');
    expect(formatValue(true)).toBe('Yes');
    expect(formatValue(12.5)).toBe('12.5');
    expect(formatValue({ a: 1 })).toBe('{"a":1}');
  });

  it('turns changes into old and new rows', () => {
    expect(changeRows(entry())).toEqual([
      { field: 'DueDate', label: 'Due date', before: '2026-09-01', after: '2026-09-15' },
    ]);
  });

  it('shows an insert with no old side and a delete with no new side', () => {
    expect(changeRows(entry({ operation: 'Insert', changes: { Quantity: { new: 2 } } }))[0]).toMatchObject({ before: '—', after: '2' });
    expect(changeRows(entry({ operation: 'Delete', changes: { Quantity: { old: 2 } } }))[0]).toMatchObject({ before: '2', after: '—' });
  });

  it('tells a line entry from the document itself', () => {
    expect(isLineEntry(entry())).toBe(false);
    const line = entry({ entityName: 'InvoiceLines', entityKey: '42', operation: 'Insert' });
    expect(isLineEntry(line)).toBe(true);
    expect(entrySummary(line)).toBe('Invoice lines 42 · Created');
    expect(entrySummary(entry())).toBe('Changed');
  });

  it('asks for the day after the last picked day, because the server end is exclusive', () => {
    expect(nextDay('2026-09-30')).toBe('2026-10-01');
    expect(nextDay('2026-12-31')).toBe('2027-01-01');
    expect(editLogQuery({ from: '2026-09-01', to: '2026-09-30', userId: null, entity: 'Invoices', page: 2, pageSize: 50 })).toEqual({
      page: '2',
      pageSize: '50',
      from: '2026-09-01T00:00:00',
      to: '2026-10-01T00:00:00',
      entity: 'Invoices',
    });
  });

  it('leaves out every filter that is not set', () => {
    expect(editLogQuery({ from: null, to: null, userId: null, entity: null, page: 0, pageSize: 25 })).toEqual({ page: '1', pageSize: '25' });
  });

  it('summarises a change on one line, capped', () => {
    expect(changeSummary(entry())).toBe('Due date: 2026-09-01 → 2026-09-15');
    const many = entry({ operation: 'Insert', changes: { A: { new: 1 }, B: { new: 2 }, C: { new: 3 } } });
    expect(changeSummary(many, 2)).toBe('A: 1; B: 2; +1 more');
  });

  it('counts pages, never fewer than one', () => {
    expect(pageCount(0, 50)).toBe(1);
    expect(pageCount(101, 50)).toBe(3);
  });
});
