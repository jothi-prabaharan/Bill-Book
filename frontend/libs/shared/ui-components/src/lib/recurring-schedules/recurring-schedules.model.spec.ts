import { describe, expect, it } from 'vitest';
import {
  cadence,
  draftOf,
  draftProblem,
  emptyDraft,
  requestOf,
  scheduleId,
  templateOptions,
  type RecurringSchedule,
} from './recurring-schedules.model';

describe('recurring schedules (TK-139)', () => {
  it('says the cadence in words', () => {
    expect(cadence('Monthly', 1)).toBe('Every month');
    expect(cadence('Weekly', 2)).toBe('Every 2 weeks');
    expect(cadence('HalfYearly', 1)).toBe('Every half-year');
  });

  it('refuses a draft with no name, no template or an end before the start', () => {
    const d = emptyDraft('2026-10-01');
    expect(draftProblem(d, 'invoice')).toBe('Name the schedule.');

    d.name = 'Rent';
    expect(draftProblem(d, 'bill')).toBe('Choose the bill to repeat.');

    d.templateId = 7;
    d.endDate = '2026-09-01';
    expect(draftProblem(d, 'invoice')).toBe('The end date cannot be before the start date.');

    d.endDate = null;
    d.placeOfSupplyStateCode = '3';
    expect(draftProblem(d, 'invoice')).toBe('Place of supply must be a 2-digit state code.');

    d.placeOfSupplyStateCode = '33';
    expect(draftProblem(d, 'invoice')).toBeNull();
  });

  it('names the template for the document the service expects', () => {
    const d = { ...emptyDraft('2026-10-01'), name: ' Rent ', templateId: 7 };

    expect(requestOf(d, 'invoice')).toMatchObject({ name: 'Rent', templateInvoiceId: 7 });
    expect(requestOf(d, 'invoice')).not.toHaveProperty('vendorBillNoPrefix');
    expect(requestOf(d, 'bill')).toMatchObject({ templateBillId: 7, vendorBillNoPrefix: 'REC-' });
  });

  it('reads template choices from a paged invoice list and from a bill array, leaving out voided ones', () => {
    const invoices = templateOptions(
      { rows: [{ invoiceId: 1, documentNo: 'INV-1', totalAmount: 118, status: 'Posted', documentDate: '2026-09-01' }, { invoiceId: 2, documentNo: 'INV-2', status: 'Void' }] },
      'invoice',
    );
    expect(invoices.map((o) => o.documentNo)).toEqual(['INV-1']);

    const bills = templateOptions([{ billId: 9, documentNo: 'BIL-9', contactName: 'Landlord', totalAmount: 50000 }], 'bill');
    expect(bills).toEqual([{ id: 9, documentNo: 'BIL-9', contactName: 'Landlord', totalAmount: 50000, documentDate: '' }]);
  });

  it('round-trips a schedule into the form', () => {
    const s: RecurringSchedule = {
      recurringBillScheduleId: 4,
      name: 'Rent',
      templateBillId: 9,
      templateDocumentNo: 'BIL-9',
      contactId: 3,
      templateTotal: 50000,
      frequency: 'Quarterly',
      every: 1,
      startDate: '2026-01-01',
      endDate: null,
      maxOccurrences: 4,
      nextRunDate: '2026-04-01',
      occurrencesCreated: 1,
      status: 'Active',
      autoPost: false,
      vendorBillNoPrefix: 'RENT-',
      placeOfSupplyStateCode: null,
      lastRunAt: null,
      lastError: null,
    };

    expect(scheduleId(s)).toBe(4);
    expect(draftOf(s)).toMatchObject({ templateId: 9, frequency: 'Quarterly', maxOccurrences: 4, vendorBillNoPrefix: 'RENT-', placeOfSupplyStateCode: '' });
  });
});
