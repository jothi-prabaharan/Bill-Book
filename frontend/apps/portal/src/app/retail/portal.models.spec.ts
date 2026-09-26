import { describe, expect, it } from 'vitest';
import { invoiceLink, quoteState, statusLabel, statusTone, PortalQuoteItem, PortalStatementLine } from './portal.models';

const line = (transactionTypeCode: string, transactionId = 7): PortalStatementLine => ({
  ledgerDate: '2026-09-01',
  documentNo: 'X',
  transactionTypeCode,
  transactionId,
  description: null,
  debit: 0,
  credit: 0,
  balance: 0,
});

describe('portal invoice status (TK-95)', () => {
  it('reads part-paid as a customer would say it', () => {
    expect(statusLabel('PartPaid')).toBe('Part-paid');
    expect(statusLabel('Overdue')).toBe('Overdue');
  });

  it('marks money late as a warning, paid as good and void as muted', () => {
    expect(statusTone('Overdue')).toBe('danger');
    expect(statusTone('Paid')).toBe('success');
    expect(statusTone('Void')).toBe('muted');
    expect(statusTone('Open')).toBe('neutral');
  });

  it('links a statement line to its invoice only for invoices and till sales', () => {
    expect(invoiceLink(line('INV', 12))).toBe(12);
    expect(invoiceLink(line('POS', 13))).toBe(13);
    expect(invoiceLink(line('RCM'))).toBeNull();
    expect(invoiceLink(line('BIL'))).toBeNull();
  });
});

describe('portal quote state (TK-96)', () => {
  const quote = (over: Partial<PortalQuoteItem>): PortalQuoteItem => ({
    quoteId: 1,
    documentNo: 'QT/1',
    documentDate: '2026-09-01',
    validUntil: '2026-09-30',
    currencyCode: 'INR',
    totalAmount: 100,
    customerResponse: 'None',
    respondedAt: null,
    respondedByName: null,
    canRespond: true,
    ...over,
  });

  it('says what the customer answered, or what is still open to them', () => {
    expect(quoteState(quote({ customerResponse: 'Accepted', canRespond: false }), '2026-09-26')).toBe('You accepted this quote');
    expect(quoteState(quote({ customerResponse: 'Rejected', canRespond: false }), '2026-09-26')).toBe('You declined this quote');
    expect(quoteState(quote({}), '2026-09-26')).toBe('Waiting for your answer');
    expect(quoteState(quote({ validUntil: '2026-09-25', canRespond: false }), '2026-09-26')).toBe('No longer valid');
    expect(quoteState(quote({ canRespond: false }), '2026-09-26')).toBe('Already turned into an order');
  });
});
