import { describe, expect, it } from 'vitest';
import { EInvoiceState, eInvoiceNeedsAttention, irnCancellable } from './invoice.service';

const registered = (ackDate: string): EInvoiceState => ({
  eInvoiceId: 1,
  status: 'Registered',
  irn: 'a'.repeat(64),
  ackNo: '112600000000001',
  ackDate,
  attempts: 1,
});

describe('e-invoice state (TK-92)', () => {
  it('needs attention while refused or still pending, and not once issued or cancelled', () => {
    expect(eInvoiceNeedsAttention('Failed')).toBe(true);
    expect(eInvoiceNeedsAttention('Pending')).toBe(true);
    expect(eInvoiceNeedsAttention('Registered')).toBe(false);
    expect(eInvoiceNeedsAttention('Cancelled')).toBe(false);
    expect(eInvoiceNeedsAttention(null)).toBe(false);
  });

  it('can be cancelled for 24 hours from its acknowledgement and not after', () => {
    const acked = '2026-09-20T10:00:00Z';

    expect(irnCancellable(registered(acked), new Date('2026-09-21T09:00:00Z'))).toBe(true);
    expect(irnCancellable(registered(acked), new Date('2026-09-21T11:00:00Z'))).toBe(false);
  });

  it('only a registered IRN can be cancelled', () => {
    expect(irnCancellable({ ...registered('2026-09-20T10:00:00Z'), status: 'Pending' }, new Date('2026-09-20T11:00:00Z'))).toBe(false);
    expect(irnCancellable(null)).toBe(false);
  });
});
